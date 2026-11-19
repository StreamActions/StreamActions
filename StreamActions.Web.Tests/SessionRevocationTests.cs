/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Daemon;
using StreamActions.Daemon.Platforms;
using StreamActions.Web.Components.Layout;
using StreamActions.Web.Components.Pages;
using StreamActions.Web.Security;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using XunitTestContext = Xunit.TestContext;

namespace StreamActions.Web.Tests;

/// <summary>
/// HTTP integration and bUnit component tests for active session tracking (<see cref="PanelUserSession"/>) and force-logout revocation.
/// </summary>
public sealed partial class SessionRevocationTests
{
    #region HttpSessionLifecycleAndRevocation

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task SignIn_CreatesPanelUserSessionWithSidClaim_AndRevokeOthersOrForceLogoutInvalidatesCookiesViaOnValidatePrincipal()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using SessionWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            PanelUser user = await SeedUserWithOAuthAsync(factory.Services, "twitch", "sess-user-1", "session_streamer", isRoot: false, ct);
            IPanelSessionService sessionService = factory.Services.GetRequiredService<IPanelSessionService>();

            using HttpClient client1 = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            using HttpClient client2 = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            // 1. Client 1 signs in -> creates Session 1 with "sid" claim in auth cookie.
            HttpResponseMessage callback1 = await SignInViaMockTwitchAsync(client1, mockTwitch, "sess-user-1", "session_streamer", ct);
            _ = callback1.StatusCode.Should().Be(HttpStatusCode.Redirect);

            ClaimsPrincipal? principal1 = DecryptAuthCookiePrincipal(factory.Services, callback1);
            _ = principal1.Should().NotBeNull();
            Guid? sid1 = AuthClaims.GetSessionId(principal1);
            _ = sid1.Should().NotBeNull();
            _ = (await sessionService.IsSessionValidAsync(sid1!.Value, user.Id, ct)).Should().BeTrue();

            // 2. Client 2 signs in -> creates Session 2 with a distinct "sid" claim.
            HttpResponseMessage callback2 = await SignInViaMockTwitchAsync(client2, mockTwitch, "sess-user-1", "session_streamer", ct);
            _ = callback2.StatusCode.Should().Be(HttpStatusCode.Redirect);

            ClaimsPrincipal? principal2 = DecryptAuthCookiePrincipal(factory.Services, callback2);
            _ = principal2.Should().NotBeNull();
            Guid? sid2 = AuthClaims.GetSessionId(principal2);
            _ = sid2.Should().NotBeNull();
            _ = sid2.Should().NotBe((Guid)sid1);

            IReadOnlyList<PanelUserSession> activeBefore = await sessionService.GetActiveSessionsForUserAsync(user.Id, ct);
            _ = activeBefore.Select(static s => s.Id).Should().BeEquivalentTo([sid1.Value, sid2!.Value]);

            // Both clients can access /permissions.
            _ = (await client1.GetAsync(new Uri("/permissions", UriKind.Relative), ct)).StatusCode.Should().Be(HttpStatusCode.OK);
            _ = (await client2.GetAsync(new Uri("/permissions", UriKind.Relative), ct)).StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Client 1 invokes POST /account/sessions/revoke-others -> revokes Session 2 while preserving Session 1.
            using StringContent jsonBody = new("{}", System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage revokeOthersResponse = await client1.PostAsync(
                new Uri("/account/sessions/revoke-others", UriKind.Relative),
                jsonBody,
                ct);
            _ = revokeOthersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using JsonDocument revokeDoc = JsonDocument.Parse(await revokeOthersResponse.Content.ReadAsStringAsync(ct));
            _ = revokeDoc.RootElement.GetProperty("revokedCount").GetInt32().Should().Be(1);

            // Client 1 remains authenticated; Client 2's cookie is rejected by OnValidatePrincipal!
            _ = (await client1.GetAsync(new Uri("/permissions", UriKind.Relative), ct)).StatusCode.Should().Be(HttpStatusCode.OK);
            HttpResponseMessage client2AfterRevokeOthers = await client2.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = client2AfterRevokeOthers.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = client2AfterRevokeOthers.Headers.Location!.AbsolutePath.Should().Be("/login");

            // 4. Admin Force Logout All Sessions revokes Session 1 as well -> Client 1 is rejected on next request.
            int forceRevoked = await sessionService.RevokeAllSessionsForUserAsync(user.Id, ct);
            _ = forceRevoked.Should().Be(1);

            HttpResponseMessage client1AfterForceLogout = await client1.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = client1AfterForceLogout.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = client1AfterForceLogout.Headers.Location!.AbsolutePath.Should().Be("/login");
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task Logout_RevokesCurrentSessionInDatabase()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using SessionWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            PanelUser user = await SeedUserWithOAuthAsync(factory.Services, "twitch", "logout-user-1", "logout_streamer", isRoot: false, ct);
            IPanelSessionService sessionService = factory.Services.GetRequiredService<IPanelSessionService>();

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage callback = await SignInViaMockTwitchAsync(client, mockTwitch, "logout-user-1", "logout_streamer", ct);
            ClaimsPrincipal? principal = DecryptAuthCookiePrincipal(factory.Services, callback);
            Guid sid = AuthClaims.GetSessionId(principal)!.Value;
            _ = (await sessionService.IsSessionValidAsync(sid, user.Id, ct)).Should().BeTrue();

            HttpResponseMessage homeGet = await client.GetAsync(new Uri("/", UriKind.Relative), ct);
            string antiforgery = ExtractAntiforgeryToken(await homeGet.Content.ReadAsStringAsync(ct));

            using FormUrlEncodedContent logoutForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
            ]);
            HttpResponseMessage logoutResponse = await client.PostAsync(new Uri("/auth/logout", UriKind.Relative), logoutForm, ct);
            _ = logoutResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = logoutResponse.Headers.Location!.ToString().Should().Be("/login");

            _ = (await sessionService.IsSessionValidAsync(sid, user.Id, ct)).Should().BeFalse();
        }
    }

    #endregion

    #region BlazorUiAndLiveCircuitTermination

    [Fact]
    [Trait("Member", "AccountMfa")]
    public void AccountMfa_LogOutOfAllOtherSessions_RevokesOtherSessionsKeepsCurrentCircuitAndRedirectsOtherCircuits()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        Guid currentSessionId = Guid.NewGuid();
        Guid otherSessionId = Guid.NewGuid();

        using InMemoryEventBus sharedEventBus = new(NullLogger<InMemoryEventBus>.Instance);
        InMemoryPanelSessionService sessionService = new(sharedEventBus);
        sessionService.SeedActiveSession(user.Id, currentSessionId);
        sessionService.SeedActiveSession(user.Id, otherSessionId);

        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Optional);

        // Circuit 1: user's current session on AccountMfa.razor
        using BunitContext currentCircuit = CreateAccountMfaContext(
            AuthClaims.BuildClaimsPrincipal(
                user,
                mfaElevatedUntilUtc: timeProvider.GetUtcNow() + AuthClaims.StepUpWindow,
                sessionId: currentSessionId),
            user,
            policy,
            timeProvider,
            sessionService,
            sharedEventBus);

        // Circuit 2: another open browser tab/device for the same user on MainLayout.razor (or AccountMfa.razor)
        using BunitContext otherCircuit = CreateAccountMfaContext(
            AuthClaims.BuildClaimsPrincipal(
                user,
                mfaElevatedUntilUtc: timeProvider.GetUtcNow() + AuthClaims.StepUpWindow,
                sessionId: otherSessionId),
            user,
            policy,
            timeProvider,
            sessionService,
            sharedEventBus);

        IRenderedComponent<AccountMfa> currentComp = currentCircuit.Render<AccountMfa>();
        IRenderedComponent<AccountMfa> otherComp = otherCircuit.Render<AccountMfa>();

        NavigationManager currentNav = currentCircuit.Services.GetRequiredService<NavigationManager>();
        NavigationManager otherNav = otherCircuit.Services.GetRequiredService<NavigationManager>();

        _ = currentComp.Find("[data-account-sessions-section='true']").Should().NotBeNull();
        currentComp.Find("[data-revoke-other-sessions='true']").Click();

        _ = currentComp.Find("[data-account-mfa-status='true']").TextContent.Should().Be("Logged out of 1 other session(s).");
        _ = currentNav.Uri.Should().NotContain("/login?error=session_revoked");

        otherComp.WaitForAssertion(() =>
            _ = otherNav.Uri.Should().EndWith("/login?error=session_revoked"));
    }

    [Fact]
    [Trait("Member", "AdminAuth")]
    public void AdminAuth_ForceLogoutAllSessionsInDataTableGrid_RevokesAllTargetUserSessionsAndRedirectsOpenBlazorCircuits()
    {
        PanelUser rootUser = new() { Id = Guid.NewGuid(), DisplayName = "RootOperator", IsRoot = true, IsGlobalAdmin = true };
        PanelUser targetUser = new() { Id = Guid.NewGuid(), DisplayName = "TargetModerator", IsRoot = false, IsGlobalAdmin = false };
        Guid targetSessionId = Guid.NewGuid();

        using InMemoryEventBus sharedEventBus = new(NullLogger<InMemoryEventBus>.Instance);
        InMemoryPanelSessionService sessionService = new(sharedEventBus);
        sessionService.SeedActiveSession(targetUser.Id, targetSessionId);

        StubPanelUserStore userStore = new([rootUser, targetUser]);
        StubAdminMfaCredentialStore mfaStore = new();

        // Admin Circuit rendering AdminAuth.razor
        using BunitContext adminCircuit = new();
        _ = adminCircuit.AddAuthorization().SetAuthorized("RootOperator").SetPolicies(AuthClaims.RootOrGlobalAdminPolicy);
        _ = adminCircuit.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthStateProvider(
            AuthClaims.BuildClaimsPrincipal(rootUser, mfaElevatedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(10), sessionId: Guid.NewGuid())));
        StubInstanceSettingsService settingsService = new(new InstanceSettings
        {
            StepUpSudoPolicy = StepUpSudoPolicy.Optional,
        });
        _ = adminCircuit.Services.AddSingleton<IInstanceSettingsService>(settingsService);
        _ = adminCircuit.Services.AddSingleton<IInstanceOAuthConfigService>(new StubInstanceOAuthConfigService());
        _ = adminCircuit.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = adminCircuit.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = adminCircuit.Services.AddSingleton<IMfaPolicyService>(new StubMfaPolicyService(new EffectiveMfaPolicy(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Optional)));
        _ = adminCircuit.Services.AddSingleton<IPanelUserStore>(userStore);
        _ = adminCircuit.Services.AddSingleton<IMfaCredentialStore>(mfaStore);
        _ = adminCircuit.Services.AddSingleton<IPanelSessionService>(sessionService);
        _ = adminCircuit.Services.AddSingleton<IEventBus>(sharedEventBus);
        _ = adminCircuit.Services.AddSingleton(TimeProvider.System);

        // Target User Circuit rendering MainLayout.razor
        using BunitContext targetCircuit = new();
        _ = targetCircuit.AddAuthorization().SetAuthorized("TargetModerator").SetPolicies(AuthClaims.PanelAuthenticatedPolicy);
        _ = targetCircuit.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthStateProvider(
            AuthClaims.BuildClaimsPrincipal(targetUser, sessionId: targetSessionId)));
        _ = targetCircuit.Services.AddSingleton<IPanelUserStore>(userStore);
        _ = targetCircuit.Services.AddSingleton<IInstanceSettingsService>(settingsService);
        _ = targetCircuit.Services.AddSingleton<IWorkspaceSessionContext>(new StubWorkspaceSessionContext());
        _ = targetCircuit.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());
        _ = targetCircuit.Services.AddSingleton<IEventBus>(sharedEventBus);

        IRenderedComponent<MainLayout> targetLayout = targetCircuit.Render<MainLayout>();
        NavigationManager targetNav = targetCircuit.Services.GetRequiredService<NavigationManager>();

        IRenderedComponent<AdminAuth> adminComp = adminCircuit.Render<AdminAuth>();
        _ = adminComp.Find("[data-datatable-grid='admin-panel-users-grid']").Should().NotBeNull();
        _ = adminComp.Find($"[data-admin-user-row='{targetUser.Id:D}']").Should().NotBeNull();

        adminComp.Find($"[data-admin-force-logout='{targetUser.Id:D}']").Click();

        _ = adminComp.Find("[data-admin-auth-status='true']").TextContent
            .Should().Be("Forced logout of all sessions for TargetModerator.");
        _ = sessionService.IsSessionActive(targetSessionId).Should().BeFalse();

        targetLayout.WaitForAssertion(() =>
            _ = targetNav.Uri.Should().EndWith("/login?error=session_revoked"));
    }

    [Fact]
    [Trait("Member", "Login")]
    public void Login_WhenErrorIsSessionRevoked_RendersSessionRevokedAlertBanner()
    {
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings()));
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        NavigationManager nav = context.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/login?error=session_revoked");

        IRenderedComponent<Login> component = context.Render<Login>();
        _ = component.Find("[data-login-session-revoked='true']").TextContent
            .Should().Contain("Your session was logged out or revoked.");
    }

    #endregion

    private static BunitContext CreateAccountMfaContext(
        ClaimsPrincipal principal,
        PanelUser user,
        EffectiveMfaPolicy policy,
        TimeProvider timeProvider,
        IPanelSessionService sessionService,
        IEventBus eventBus)
    {
        BunitContext context = new();
        _ = context.AddAuthorization()
            .SetAuthorized(principal.Identity?.Name ?? "user")
            .SetPolicies(AuthClaims.MfaPendingPolicy, AuthClaims.PanelAuthenticatedPolicy);

        _ = context.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthStateProvider(principal));
        _ = context.Services.AddSingleton<IMfaPolicyService>(new StubMfaPolicyService(policy));
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubAdminMfaCredentialStore());
        _ = context.Services.AddSingleton<IPanelUserStore>(new StubPanelUserStore([user]));
        _ = context.Services.AddSingleton<IPanelSessionService>(sessionService);
        _ = context.Services.AddSingleton(eventBus);
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings()));
        _ = context.Services.AddSingleton(timeProvider);
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());
        _ = context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);

        return context;
    }

    private static ClaimsPrincipal? DecryptAuthCookiePrincipal(IServiceProvider services, HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies))
        {
            return null;
        }

        string prefix = $"{AuthClaims.AuthCookieName}=";
        string? rawCookieHeader = setCookies.FirstOrDefault(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(rawCookieHeader))
        {
            return null;
        }

        string cookieValue = rawCookieHeader.Split(';', 2)[0][prefix.Length..];
        IOptionsMonitor<CookieAuthenticationOptions> optionsMonitor = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        CookieAuthenticationOptions options = optionsMonitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);
        return options.TicketDataFormat.Unprotect(Uri.UnescapeDataString(cookieValue))?.Principal;
    }

    private static async Task<PanelUser> SeedUserWithOAuthAsync(
        IServiceProvider services,
        string platformId,
        string platformUserId,
        string username,
        bool isRoot,
        CancellationToken ct)
    {
        IInstanceOAuthConfigService oauthService = services.GetRequiredService<IInstanceOAuthConfigService>();
        await oauthService.SetAsync(platformId, new PlatformOAuthAppCredentials("instance-cid", "instance-secret"), ct).ConfigureAwait(false);

        IPanelUserStore userStore = services.GetRequiredService<IPanelUserStore>();
        PanelUser user = await userStore.CreateUserAsync(
            new PanelUser
            {
                DisplayName = username,
                IsRoot = isRoot,
                IsGlobalAdmin = isRoot,
            },
            new UserLoginIdentity
            {
                ProviderId = platformId,
                ProviderUserId = platformUserId,
                ProviderUsername = username,
            },
            ct).ConfigureAwait(false);

        IWorkspaceStore wsStore = services.GetRequiredService<IWorkspaceStore>();
        _ = await wsStore.CreateWorkspaceAsync(
            new Workspace
            {
                OwnerUserId = user.Id,
                Name = $"{username}'s Workspace",
                PrimaryPlatformId = platformId,
            },
            new WorkspacePlatform
            {
                PlatformId = platformId,
                Role = PlatformRole.Primary,
                PlatformChannelId = platformUserId,
            },
            ct).ConfigureAwait(false);

        return user;
    }

    private static async Task<HttpResponseMessage> SignInViaMockTwitchAsync(
        HttpClient client,
        MockTwitchOAuthServer mockTwitch,
        string subject,
        string username,
        CancellationToken ct)
    {
        HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct).ConfigureAwait(false);
        _ = loginRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
        string state = query["state"].ToString();
        string nonce = query["nonce"].ToString();

        mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
            issuer: baseUrl,
            audience: "instance-cid",
            subject: subject,
            preferredUsername: username,
            nonce: nonce);

        return await client.GetAsync(
            new Uri($"/auth/twitch/callback?code=code-{Uri.EscapeDataString(subject)}&state={Uri.EscapeDataString(state)}", UriKind.Relative),
            ct).ConfigureAwait(false);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        Match match = AntiforgeryInputRegex().Match(html);
        _ = match.Success.Should().BeTrue("HTML should contain an antiforgery token");
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="(?<value>[^"]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryInputRegex();

    private sealed class InMemoryPanelSessionService(IEventBus eventBus) : IPanelSessionService
    {
        private readonly Dictionary<Guid, PanelUserSession> _sessions = [];

        public void SeedActiveSession(Guid panelUserId, Guid sessionId) =>
            this._sessions[sessionId] = new PanelUserSession
            {
                Id = sessionId,
                PanelUserId = panelUserId,
                ProviderId = "twitch",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenAtUtc = DateTimeOffset.UtcNow,
            };

        public bool IsSessionActive(Guid sessionId) =>
            this._sessions.TryGetValue(sessionId, out PanelUserSession? s) && s.RevokedAtUtc is null;

        public ValueTask<PanelUserSession> CreateSessionAsync(
            Guid panelUserId,
            string providerId,
            string? remoteIpAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            PanelUserSession session = new()
            {
                Id = Guid.NewGuid(),
                PanelUserId = panelUserId,
                ProviderId = providerId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenAtUtc = DateTimeOffset.UtcNow,
                RemoteIpAddress = remoteIpAddress,
                UserAgent = userAgent,
            };
            this._sessions[session.Id] = session;
            return ValueTask.FromResult(session);
        }

        public ValueTask<PanelUserSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._sessions.GetValueOrDefault(sessionId));

        public ValueTask<IReadOnlyList<PanelUserSession>> GetActiveSessionsForUserAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PanelUserSession>>([.. this._sessions.Values.Where(s => s.PanelUserId == panelUserId && s.RevokedAtUtc is null)]);

        public ValueTask<bool> IsSessionValidAsync(Guid sessionId, Guid? expectedPanelUserId = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                this._sessions.TryGetValue(sessionId, out PanelUserSession? s)
                && s.RevokedAtUtc is null
                && (!expectedPanelUserId.HasValue || s.PanelUserId == expectedPanelUserId.Value));

        public async ValueTask<bool> RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            if (!this._sessions.TryGetValue(sessionId, out PanelUserSession? s) || s.RevokedAtUtc is not null)
            {
                return false;
            }

            s.RevokedAtUtc = DateTimeOffset.UtcNow;
            await eventBus.PublishAsync(new UserSessionsRevokedEvent(s.PanelUserId, ExcludedSessionId: null), cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async ValueTask<int> RevokeOtherSessionsAsync(Guid currentUserId, Guid currentSessionId, CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (PanelUserSession s in this._sessions.Values.Where(x => x.PanelUserId == currentUserId && x.Id != currentSessionId && x.RevokedAtUtc is null))
            {
                s.RevokedAtUtc = DateTimeOffset.UtcNow;
                count++;
            }

            await eventBus.PublishAsync(new UserSessionsRevokedEvent(currentUserId, ExcludedSessionId: currentSessionId), cancellationToken).ConfigureAwait(false);
            return count;
        }

        public async ValueTask<int> RevokeAllSessionsForUserAsync(Guid targetUserId, CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (PanelUserSession s in this._sessions.Values.Where(x => x.PanelUserId == targetUserId && x.RevokedAtUtc is null))
            {
                s.RevokedAtUtc = DateTimeOffset.UtcNow;
                count++;
            }

            await eventBus.PublishAsync(new UserSessionsRevokedEvent(targetUserId, ExcludedSessionId: null), cancellationToken).ConfigureAwait(false);
            return count;
        }
    }

    private sealed class StubAuthStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class StubAntiforgeryStateProvider : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken? GetAntiforgeryToken() =>
            new("test-antiforgery-token", "__RequestVerificationToken");
    }

    private sealed class StubMfaPolicyService(EffectiveMfaPolicy policy) : IMfaPolicyService
    {
        public EffectiveMfaPolicy ComputeEffectivePolicy(InstanceSettings instanceSettings, Workspace? workspace = null) => policy;

        public ValueTask<EffectiveMfaPolicy> GetEffectivePolicyAsync(Guid? workspaceId = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy);

        public ValueTask<EffectiveMfaPolicy> GetEffectivePolicyForUserAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            Guid? workspaceId = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy);

        public ValueTask<bool> IsLoginMfaRequiredAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            Guid? workspaceId = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class StubPanelUserStore(List<PanelUser> users) : IPanelUserStore
    {
        public ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(users.Count > 0);
        public ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(users.Any(static u => u.IsRoot));
        public ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(users.FirstOrDefault(u => u.Id == userId));
        public ValueTask<PanelUser?> GetByLoginIdentityAsync(string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PanelUser?>(null);
        public ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PanelUser>>(users);
        public ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserLoginIdentity>>([]);
        public ValueTask<PanelUser> CreateUserAsync(PanelUser user, UserLoginIdentity? initialIdentity = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(user);
        public ValueTask<PanelUser> UpdateUserAsync(PanelUser user, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(user);
        public ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(UserLoginIdentity identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(identity);
        public ValueTask<bool> UnlinkLoginIdentityAsync(Guid panelUserId, string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class StubAdminMfaCredentialStore : IMfaCredentialStore
    {
        public ValueTask<bool> HasAnyCredentialsAsync(Guid panelUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<IReadOnlyList<MfaCredentialSummary>> GetSummariesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<MfaCredentialSummary>>([]);
        public ValueTask<Guid> SaveTotpAsync(Guid panelUserId, TotpSecretPayload payload, IReadOnlyList<string> recoveryCodeHashes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<TotpSecretPayload?> GetTotpAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TotpSecretPayload?>(null);
        public ValueTask<bool> VerifyAndConsumeRecoveryCodeAsync(Guid panelUserId, string code, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
        public ValueTask<int> GetRemainingRecoveryCodesCountAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
        public ValueTask UpsertPasskeyAsync(Guid panelUserId, PasskeyCredentialPayload passkey, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<PasskeyCredentialPayload>> GetPasskeysAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PasskeyCredentialPayload>>([]);
        public ValueTask<PasskeyCredentialPayload?> FindPasskeyAsync(Guid panelUserId, byte[] credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PasskeyCredentialPayload?>(null);
        public ValueTask<(Guid PanelUserId, PasskeyCredentialPayload Passkey)?> FindPasskeyByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<(Guid, PasskeyCredentialPayload)?>(null);
        public ValueTask<bool> RemovePasskeyAsync(Guid panelUserId, byte[] credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
        public ValueTask<bool> DeleteCredentialAsync(Guid panelUserId, Guid credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
        public ValueTask<int> ClearAllForUserAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
    }

    private sealed class StubPlatformOAuthProvider(string platformId) : IPlatformOAuthProvider
    {
        public string PlatformId { get; } = platformId;
        public IReadOnlyList<string> DefaultLoginScopes => ["openid", "user:read:email"];
        public IReadOnlyList<string> DefaultBroadcasterScopes => ["chat:read", "chat:edit"];
        public IReadOnlyList<string> DefaultBotScopes => ["user:bot", "chat:read"];
        public Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IReadOnlyCollection<string>? scopes, string state, bool forceVerify = false, string? nonce = null) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, Uri redirectUri, string? expectedNonce = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask RevokeTokenAsync(string clientId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubInstanceSettingsService(InstanceSettings settings) : IInstanceSettingsService
    {
        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged
        {
            add { }
            remove { }
        }

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(settings);
        public ValueTask UpdateAsync(InstanceSettings updated, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubInstanceOAuthConfigService : IInstanceOAuthConfigService
    {
        public ValueTask<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>>(new Dictionary<string, PlatformOAuthAppCredentials>());
        public ValueTask<PlatformOAuthAppCredentials?> GetAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);
        public ValueTask SetAsync(string platformId, PlatformOAuthAppCredentials credentials, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
        public ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class StubOidcProviderStore(List<ExternalOidcProvider> providers) : IExternalOidcProviderStore
    {
        public ValueTask<ExternalOidcProvider?> GetByIdAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(providers.FirstOrDefault(p => p.Id == providerId));
        public ValueTask<ExternalOidcProvider?> GetBySlugAsync(Guid? workspaceId, string slug, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(providers.FirstOrDefault(p => p.WorkspaceId == workspaceId && string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase)));
        public ValueTask<IReadOnlyList<ExternalOidcProvider>> GetProvidersAsync(Guid? workspaceId, bool enabledOnly = false, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<ExternalOidcProvider>>(providers);
        public ValueTask<ExternalOidcProvider> UpsertAsync(ExternalOidcProvider provider, string? plaintextClientSecret = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(provider);
        public ValueTask<(bool Success, string? ClientSecret, CredentialStatus Status)> TryGetClientSecretAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<(bool, string?, CredentialStatus)>((false, null, CredentialStatus.Active));
        public ValueTask<bool> DeleteAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class StubWorkspaceSessionContext : IWorkspaceSessionContext
    {
        public IReadOnlyList<AccessibleWorkspace> AccessibleWorkspaces => [];
        public Guid? ActiveWorkspaceId => null;
        public AccessibleWorkspace? ActiveWorkspace => null;
        public event EventHandler? WorkspaceChanged
        {
            add { }
            remove { }
        }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RefreshAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<bool> SelectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class TempDaemonDirectories : IDisposable
    {
        public TempDaemonDirectories()
        {
            this.Root = Path.Combine(Path.GetTempPath(), $"sa-sess-test-{Guid.NewGuid():N}");
            this.ConfigDir = Path.Combine(this.Root, "config");
            this.DataDir = Path.Combine(this.Root, "data");
            this.LogDir = Path.Combine(this.Root, "logs");
            this.PluginsDir = Path.Combine(this.Root, "plugins");
            this.BackupDir = Path.Combine(this.Root, "backups");
        }

        public string Root { get; }
        public string ConfigDir { get; }
        public string DataDir { get; }
        public string LogDir { get; }
        public string PluginsDir { get; }
        public string BackupDir { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Root))
            {
                try
                {
                    Directory.Delete(this.Root, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private sealed class SessionWebTestApplicationFactory(
        TempDaemonDirectories dirs,
        TwitchOAuthEndpointOptions twitchOptions,
        FakeTimeProvider fakeTimeProvider) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _ = builder.UseEnvironment("Testing");
            _ = builder.UseSetting("NoWeb", "false");
            _ = builder.UseSetting("Bootstrap:Paths:ConfigDir", dirs.ConfigDir);
            _ = builder.UseSetting("Bootstrap:Paths:DataDir", dirs.DataDir);
            _ = builder.UseSetting("Bootstrap:Paths:LogDir", dirs.LogDir);
            _ = builder.UseSetting("Bootstrap:Paths:PluginsDir", dirs.PluginsDir);
            _ = builder.UseSetting("Bootstrap:Paths:BackupDir", dirs.BackupDir);
            _ = builder.UseSetting("Bootstrap:Node:InstanceId", $"sess-test-{Guid.NewGuid():N}");
            _ = builder.UseSetting("Bootstrap:Sentry:Enabled", "false");

            _ = builder.ConfigureServices(services =>
            {
                _ = services.RemoveAll<TimeProvider>();
                _ = services.AddSingleton<TimeProvider>(fakeTimeProvider);
                _ = services.PostConfigure<CookieAuthenticationOptions>(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    options => options.TimeProvider = fakeTimeProvider);

                _ = services.Configure<TwitchOAuthEndpointOptions>(opts =>
                {
                    opts.AuthorizeEndpoint = twitchOptions.AuthorizeEndpoint;
                    opts.TokenEndpoint = twitchOptions.TokenEndpoint;
                    opts.ValidateEndpoint = twitchOptions.ValidateEndpoint;
                    opts.RevokeEndpoint = twitchOptions.RevokeEndpoint;
                    opts.UserInfoEndpoint = twitchOptions.UserInfoEndpoint;
                    opts.OpenIdConfigurationEndpoint = twitchOptions.OpenIdConfigurationEndpoint;
                });
            });
        }
    }

    private sealed class MockTwitchOAuthServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly RSA _rsa;
        private readonly RsaSecurityKey _signingKey;

        private MockTwitchOAuthServer(WebApplication app, string baseUrl, RSA rsa, RsaSecurityKey signingKey)
        {
            this._app = app;
            this.BaseUrl = baseUrl;
            this._rsa = rsa;
            this._signingKey = signingKey;
            this.EndpointOptions = new TwitchOAuthEndpointOptions
            {
                AuthorizeEndpoint = $"{baseUrl}/oauth2/authorize",
                TokenEndpoint = $"{baseUrl}/oauth2/token",
                ValidateEndpoint = $"{baseUrl}/oauth2/validate",
                RevokeEndpoint = $"{baseUrl}/oauth2/revoke",
                UserInfoEndpoint = $"{baseUrl}/oauth2/userinfo",
                OpenIdConfigurationEndpoint = $"{baseUrl}/.well-known/openid-configuration",
            };
        }

        public string BaseUrl { get; }
        public TwitchOAuthEndpointOptions EndpointOptions { get; }
        public Func<string, string>? IdTokenFactory { get; set; }

        public static async Task<MockTwitchOAuthServer> StartAsync(CancellationToken cancellationToken)
        {
            RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-twitch-sess-key" };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
            WebApplication app = builder.Build();

            MockTwitchOAuthServer? holder = null;

            _ = app.MapGet("/.well-known/openid-configuration", () =>
            {
                string baseUrl = holder!.BaseUrl;
                return Results.Json(new
                {
                    issuer = baseUrl,
                    authorization_endpoint = $"{baseUrl}/oauth2/authorize",
                    token_endpoint = $"{baseUrl}/oauth2/token",
                    userinfo_endpoint = $"{baseUrl}/oauth2/userinfo",
                    jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                });
            });

            _ = app.MapGet("/.well-known/jwks.json", () =>
            {
                RSAParameters parameters = rsa.ExportParameters(false);
                return Results.Json(new
                {
                    keys = new[]
                    {
                        new
                        {
                            kty = "RSA",
                            use = "sig",
                            kid = signingKey.KeyId,
                            alg = "RS256",
                            n = Base64UrlEncoder.Encode(parameters.Modulus!),
                            e = Base64UrlEncoder.Encode(parameters.Exponent!),
                        },
                    },
                });
            });

            _ = app.MapPost("/oauth2/token", () =>
            {
                string? idToken = holder!.IdTokenFactory?.Invoke(holder.BaseUrl);
                string[] scopes = idToken is not null ? ["openid", "chat:read"] : ["chat:read"];
                return Results.Json(new
                {
                    access_token = "mock-access-token",
                    refresh_token = "mock-refresh-token",
                    expires_in = 3600,
                    scope = scopes,
                    id_token = idToken,
                    token_type = "bearer",
                });
            });

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            string baseUrl = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .First()
                .TrimEnd('/');

            holder = new MockTwitchOAuthServer(app, baseUrl, rsa, signingKey);
            return holder;
        }

        public string CreateSignedIdToken(
            string issuer,
            string audience,
            string subject,
            string preferredUsername,
            string nonce)
        {
            List<Claim> claims =
            [
                new("sub", subject),
                new("preferred_username", preferredUsername),
                new("nonce", nonce),
            ];

            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = issuer,
                Audience = audience,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(this._signingKey, SecurityAlgorithms.RsaSha256),
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        public async ValueTask DisposeAsync()
        {
            await this._app.StopAsync().ConfigureAwait(false);
            await this._app.DisposeAsync().ConfigureAwait(false);
            this._rsa.Dispose();
        }
    }
}

