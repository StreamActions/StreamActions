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

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Daemon;
using StreamActions.Daemon.Platforms;
using StreamActions.Web.Security;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace StreamActions.Web.Tests;

public sealed partial class AuthIntegrationTests
{
    private static readonly string[] _chatReadScopes = ["chat:read"];

    #region AuthorizationEnforcement

    [Theory]
    [Trait("Member", "MapStreamActionsWeb")]
    [InlineData("/permissions")]
    [InlineData("/admin/global-permissions")]
    [InlineData("/admin/auth")]
    public async Task ProtectedPages_WhenUnauthenticated_RedirectToLogin(string path)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using AuthWebTestApplicationFactory factory = new(dirs);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative), ct);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        _ = response.Headers.Location.Should().NotBeNull();
        _ = response.Headers.Location!.AbsolutePath.Should().Be("/login");
    }

    [Theory]
    [Trait("Member", "MapStreamActionsWeb")]
    [InlineData("/hubs/bot-status")]
    [InlineData("/hubs/moderation")]
    [InlineData("/hubs/commands")]
    [InlineData("/hubs/permissions")]
    public async Task SignalRHubs_WhenUnauthenticated_ReturnUnauthorized(string hubPath)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using AuthWebTestApplicationFactory factory = new(dirs);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

        HttpResponseMessage response = await client.GetAsync(new Uri(hubPath, UriKind.Relative), ct);

        _ = response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Member", "MapStreamActionsWeb")]
    public async Task HomeRoute_WhenUnauthenticated_StillReturnsOk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using AuthWebTestApplicationFactory factory = new(dirs);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative), ct);

        _ = response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region PlatformOAuthFlows

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task SetupOAuthFlow_VerifiesBootstrapTokenCreatesRootUserAndWorkspaceSignsInAndLogsOut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);
            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            // 1. Verify bootstrap token with OAuth app credentials via /setup form.
            HttpResponseMessage setupGet = await client.GetAsync(new Uri("/setup", UriKind.Relative), ct);
            string setupHtml = await setupGet.Content.ReadAsStringAsync(ct);
            string setupAntiforgery = ExtractAntiforgeryToken(setupHtml);

            IBootstrapTokenService bootstrapTokens = factory.Services.GetRequiredService<IBootstrapTokenService>();
            string rawToken = bootstrapTokens.RequestOrReuseActiveToken().Token!;

            using FormUrlEncodedContent setupForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", setupAntiforgery),
                new KeyValuePair<string, string>("bootstrapToken", rawToken),
                new KeyValuePair<string, string>("platformId", "twitch"),
                new KeyValuePair<string, string>("clientId", "twitch-setup-cid"),
                new KeyValuePair<string, string>("clientSecret", "twitch-setup-secret"),
            ]);

            HttpResponseMessage verifyResponse = await client.PostAsync(new Uri("/setup/verify", UriKind.Relative), setupForm, ct);
            _ = verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Revisit /setup?verified=true and verify "Continue with Twitch" link is present.
            HttpResponseMessage verifiedSetupGet = await client.GetAsync(new Uri("/setup?verified=true", UriKind.Relative), ct);
            string verifiedHtml = await verifiedSetupGet.Content.ReadAsStringAsync(ct);
            _ = verifiedHtml.Should().Contain("Continue with Twitch");
            _ = verifiedHtml.Should().Contain("/auth/twitch/login?flow=setup");

            // 3. Start setup OAuth flow.
            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=setup", UriKind.Relative), ct);
            _ = loginRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Uri authorizeUri = loginRedirect.Headers.Location!;
            _ = authorizeUri.GetLeftPart(UriPartial.Path).Should().Be($"{mockTwitch.BaseUrl}/oauth2/authorize");

            Dictionary<string, StringValues> authQuery = QueryHelpers.ParseQuery(authorizeUri.Query);
            string state = authQuery["state"].ToString();
            string nonce = authQuery["nonce"].ToString();
            _ = state.Should().NotBeNullOrWhiteSpace();
            _ = nonce.Should().NotBeNullOrWhiteSpace();

            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "twitch-setup-cid",
                subject: "root-twitch-100",
                preferredUsername: "root_streamer",
                nonce: nonce,
                email: "root@example.com");

            // 4. Complete callback -> Root user + Workspace + Broadcaster credential created & signed in.
            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=setup-code-1&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);
            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.Location!.ToString().Should().Be("/");
            _ = callbackResponse.Headers.GetValues("Set-Cookie")
                .Should().Contain(c => c.StartsWith($"{AuthClaims.AuthCookieName}=", StringComparison.Ordinal));

            IPanelUserStore userStore = factory.Services.GetRequiredService<IPanelUserStore>();
            PanelUser? rootUser = await userStore.GetByLoginIdentityAsync("twitch", "root-twitch-100", ct);
            _ = rootUser.Should().NotBeNull();
            _ = rootUser!.IsRoot.Should().BeTrue();
            _ = rootUser.IsGlobalAdmin.Should().BeTrue();

            IWorkspaceStore workspaceStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            IReadOnlyList<Workspace> workspaces = await workspaceStore.GetByOwnerAsync(rootUser.Id, ct);
            _ = workspaces.Should().ContainSingle();

            IPlatformCredentialStore credStore = factory.Services.GetRequiredService<IPlatformCredentialStore>();
            PlatformCredential? broadcasterCred = await credStore.GetCredentialRecordAsync(workspaces[0].Id, "twitch", TokenRole.Broadcaster, ct);
            _ = broadcasterCred.Should().NotBeNull();

            // 5. Protected admin routes now return 200 OK.
            HttpResponseMessage adminAuthResponse = await client.GetAsync(new Uri("/admin/auth", UriKind.Relative), ct);
            _ = adminAuthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. Logout clears auth cookie and redirects to /login.
            string adminHtml = await adminAuthResponse.Content.ReadAsStringAsync(ct);
            string logoutToken = ExtractAntiforgeryToken(adminHtml);
            using FormUrlEncodedContent logoutForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", logoutToken),
            ]);
            HttpResponseMessage logoutResponse = await client.PostAsync(new Uri("/auth/logout", UriKind.Relative), logoutForm, ct);
            _ = logoutResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = logoutResponse.Headers.Location!.ToString().Should().Be("/login");

            HttpResponseMessage afterLogout = await client.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = afterLogout.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = afterLogout.Headers.Location!.AbsolutePath.Should().Be("/login");
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task LoginOAuthFlow_WhenRegistrationClosedAndUninvited_RedirectsToAccessDenied()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            await SeedInstanceOAuthAndSettingsAsync(factory.Services, allowOpenRegistration: false, InstanceOAuthAppPolicy.InstanceOnly, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            string state = query["state"].ToString();
            string nonce = query["nonce"].ToString();

            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "stranger-999",
                preferredUsername: "stranger",
                nonce: nonce);

            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-stranger&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);

            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.Location!.ToString().Should().Be("/access-denied");
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task LoginOAuthFlow_WhenModeratorHasUiPanelAccess_SignsInAndDeniesAdminRoute()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            await SeedInstanceOAuthAndSettingsAsync(factory.Services, allowOpenRegistration: false, InstanceOAuthAppPolicy.InstanceOnly, ct);
            Workspace ws = await SeedWorkspaceWithOwnerAsync(factory.Services, ct);

            IPermissionStore permStore = factory.Services.GetRequiredService<IPermissionStore>();
            IReadOnlyList<PermissionGroup> wsGroups = await permStore.GetGroupsAsync(ws.Id, ct);
            PermissionGroup modGroup = wsGroups.First(static g => string.Equals(g.MappedPlatformDesignation, nameof(PlatformUserDesignations.Moderator), StringComparison.OrdinalIgnoreCase));
            await permStore.SetGrantAsync(modGroup.Id, BuiltInPermissionKeys.UiPanelAccess, PermissionState.Allow, ct);
            await permStore.UpsertRoleRosterAsync(new WorkspacePlatformRoleRoster
            {
                WorkspaceId = ws.Id,
                PlatformId = "twitch",
                PlatformUserId = "mod-twitch-55",
                Designations = PlatformUserDesignations.Moderator,
            }, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            string state = query["state"].ToString();
            string nonce = query["nonce"].ToString();

            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "mod-twitch-55",
                preferredUsername: "channel_mod",
                nonce: nonce);

            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-mod&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);

            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.Location!.ToString().Should().Be("/");

            HttpResponseMessage permissionsResponse = await client.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            HttpResponseMessage adminResponse = await client.GetAsync(new Uri("/admin/auth", UriKind.Relative), ct);
            _ = adminResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = adminResponse.Headers.Location!.AbsolutePath.Should().Be("/access-denied");
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task LoginOAuthFlow_WhenModeratorHasExplicitDeny_RedirectsToAccessDenied()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            await SeedInstanceOAuthAndSettingsAsync(factory.Services, allowOpenRegistration: false, InstanceOAuthAppPolicy.InstanceOnly, ct);
            Workspace ws = await SeedWorkspaceWithOwnerAsync(factory.Services, ct);

            IPermissionStore permStore = factory.Services.GetRequiredService<IPermissionStore>();
            IReadOnlyList<PermissionGroup> wsGroups = await permStore.GetGroupsAsync(ws.Id, ct);
            PermissionGroup modGroup = wsGroups.First(static g => string.Equals(g.MappedPlatformDesignation, nameof(PlatformUserDesignations.Moderator), StringComparison.OrdinalIgnoreCase));
            await permStore.SetGrantAsync(modGroup.Id, BuiltInPermissionKeys.UiPanelAccess, PermissionState.Allow, ct);
            await permStore.UpsertRoleRosterAsync(new WorkspacePlatformRoleRoster
            {
                WorkspaceId = ws.Id,
                PlatformId = "twitch",
                PlatformUserId = "denied-mod-77",
                Designations = PlatformUserDesignations.Moderator,
            }, ct);
            await permStore.SetUserOverrideAsync(new UserPermissionOverride
            {
                WorkspaceId = ws.Id,
                PlatformId = "twitch",
                PlatformUserId = "denied-mod-77",
                PermissionKey = BuiltInPermissionKeys.UiPanelAccess,
                State = PermissionState.Deny,
            }, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            string state = query["state"].ToString();
            string nonce = query["nonce"].ToString();

            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "denied-mod-77",
                preferredUsername: "denied_mod",
                nonce: nonce);

            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-denied&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);

            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.Location!.ToString().Should().Be("/access-denied");
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task LoginOAuthFlow_WhenRequireStreamerOAuthApp_AcceptsInterstitialPostAndSavesCustomApp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            await SeedInstanceOAuthAndSettingsAsync(factory.Services, allowOpenRegistration: true, InstanceOAuthAppPolicy.RequireStreamerOAuthApp, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage loginPageResponse = await client.GetAsync(new Uri("/login", UriKind.Relative), ct);
            _ = loginPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            string loginHtml = await loginPageResponse.Content.ReadAsStringAsync(ct);
            _ = loginHtml.Should().Contain("name=\"customClientId\"");
            _ = loginHtml.Should().Contain("name=\"customClientSecret\"");
            string antiforgeryToken = ExtractAntiforgeryToken(loginHtml);

            using FormUrlEncodedContent postForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
                new KeyValuePair<string, string>("flow", "login"),
                new KeyValuePair<string, string>("customClientId", "streamer-custom-cid"),
                new KeyValuePair<string, string>("customClientSecret", "streamer-custom-secret"),
            ]);

            HttpResponseMessage startResponse = await client.PostAsync(new Uri("/auth/twitch/login", UriKind.Relative), postForm, ct);
            _ = startResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(startResponse.Headers.Location!.Query);
            _ = query["client_id"].ToString().Should().Be("streamer-custom-cid");
            string state = query["state"].ToString();
            string nonce = query["nonce"].ToString();

            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "streamer-custom-cid",
                subject: "streamer-open-1",
                preferredUsername: "open_streamer",
                nonce: nonce);

            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-streamer&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);

            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.Location!.ToString().Should().Be("/");

            IPanelUserStore userStore = factory.Services.GetRequiredService<IPanelUserStore>();
            PanelUser? streamer = await userStore.GetByLoginIdentityAsync("twitch", "streamer-open-1", ct);
            _ = streamer.Should().NotBeNull();

            IWorkspaceStore wsStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            IReadOnlyList<Workspace> workspaces = await wsStore.GetByOwnerAsync(streamer!.Id, ct);
            _ = workspaces.Should().ContainSingle();

            PlatformOAuthAppCredentials? savedCustomApp = await wsStore.GetCustomOAuthAppAsync(workspaces[0].Id, "twitch", ct);
            _ = savedCustomApp.Should().NotBeNull();
            _ = savedCustomApp!.ClientId.Should().Be("streamer-custom-cid");
            _ = savedCustomApp.ClientSecret.Should().Be("streamer-custom-secret");
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task BotOAuthFlow_AlwaysSendsForceVerifyTrueAndSavesBotChatCredential()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            await SeedInstanceOAuthAndSettingsAsync(factory.Services, allowOpenRegistration: false, InstanceOAuthAppPolicy.InstanceOnly, ct);
            Workspace ws = await SeedWorkspaceWithOwnerAsync(factory.Services, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            // Sign in as the workspace owner first.
            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> loginQuery = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            string loginNonce = loginQuery["nonce"].ToString();
            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "owner-twitch-1",
                preferredUsername: "owner_user",
                nonce: loginNonce);
            _ = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=c1&state={Uri.EscapeDataString(loginQuery["state"].ToString())}", UriKind.Relative),
                ct);

            // Start bot account flow.
            HttpResponseMessage botStart = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=bot&workspaceId={ws.Id}", UriKind.Relative),
                ct);
            _ = botStart.StatusCode.Should().Be(HttpStatusCode.Redirect);

            Dictionary<string, StringValues> botQuery = QueryHelpers.ParseQuery(botStart.Headers.Location!.Query);
            _ = botQuery["force_verify"].ToString().Should().Be("true");
            string botState = botQuery["state"].ToString();

            mockTwitch.IdTokenFactory = null;
            mockTwitch.ValidatedUserId = "bot-twitch-999";
            mockTwitch.ValidatedLogin = "my_channel_bot";

            HttpResponseMessage botCallback = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=c-bot&state={Uri.EscapeDataString(botState)}", UriKind.Relative),
                ct);
            _ = botCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);

            IPlatformCredentialStore credStore = factory.Services.GetRequiredService<IPlatformCredentialStore>();
            (bool found, CredentialStatus status, PlatformTokenPayload? payload) = await credStore.TryGetTokenAsync(ws.Id, "twitch", TokenRole.BotChat, ct);
            _ = found.Should().BeTrue();
            _ = status.Should().Be(CredentialStatus.Active);
            _ = payload.Should().NotBeNull();
            _ = payload!.PlatformUserId.Should().Be("bot-twitch-999");
            _ = payload.PlatformUsername.Should().Be("my_channel_bot");
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task PlatformCallback_WhenStateTampered_ReturnsBadRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);
            await SeedInstanceOAuthAndSettingsAsync(factory.Services, allowOpenRegistration: false, InstanceOAuthAppPolicy.InstanceOnly, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            _ = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri("/auth/twitch/callback?code=code-1&state=tampered-state", UriKind.Relative),
                ct);

            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task TwitchPlatformOAuthProvider_ExposesDefaultScopesUsedByAuthEndpoints()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            await SeedInstanceOAuthAndSettingsAsync(factory.Services, allowOpenRegistration: false, InstanceOAuthAppPolicy.InstanceOnly, ct);
            Workspace ws = await SeedWorkspaceWithOwnerAsync(factory.Services, ct);

            IPlatformOAuthProvider twitchProvider = factory.Services
                .GetServices<IPlatformOAuthProvider>()
                .Single(static p => string.Equals(p.PlatformId, "twitch", StringComparison.OrdinalIgnoreCase));

            _ = twitchProvider.DefaultLoginScopes.Should().BeEquivalentTo(["openid", "user:read:email"]);
            _ = twitchProvider.DefaultBroadcasterScopes.Should().Contain(["channel:bot", "moderator:manage:banned_users"]);
            _ = twitchProvider.DefaultBotScopes.Should().BeEquivalentTo(["user:read:chat", "user:write:chat", "user:bot", "chat:read", "chat:edit"]);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> loginQuery = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            _ = loginQuery["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Should().BeEquivalentTo(twitchProvider.DefaultLoginScopes);

            string loginNonce = loginQuery["nonce"].ToString();
            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "owner-twitch-1",
                preferredUsername: "owner_user",
                nonce: loginNonce);
            _ = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=c1&state={Uri.EscapeDataString(loginQuery["state"].ToString())}", UriKind.Relative),
                ct);

            HttpResponseMessage broadcasterRedirect = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=broadcaster&workspaceId={ws.Id}", UriKind.Relative),
                ct);
            Dictionary<string, StringValues> broadcasterQuery = QueryHelpers.ParseQuery(broadcasterRedirect.Headers.Location!.Query);
            _ = broadcasterQuery["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Should().BeEquivalentTo(twitchProvider.DefaultBroadcasterScopes);

            HttpResponseMessage botRedirect = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=bot&workspaceId={ws.Id}", UriKind.Relative),
                ct);
            Dictionary<string, StringValues> botQuery = QueryHelpers.ParseQuery(botRedirect.Headers.Location!.Query);
            _ = botQuery["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Should().BeEquivalentTo(twitchProvider.DefaultBotScopes);
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task InstanceLoginWithStreamerApiOAuth_UsesInstanceCredentialsForLoginAndRequiresWorkspaceCredentialsForBroadcasterAndBot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using AuthWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            await SeedInstanceOAuthAndSettingsAsync(
                factory.Services,
                allowOpenRegistration: false,
                InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth,
                ct);
            Workspace ws = await SeedWorkspaceWithOwnerAsync(factory.Services, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            // 1. Login flow succeeds using instance-cid without workspace custom OAuth app.
            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            _ = loginRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> loginQuery = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            _ = loginQuery["client_id"].ToString().Should().Be("instance-cid");

            string loginNonce = loginQuery["nonce"].ToString();
            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "owner-twitch-1",
                preferredUsername: "owner_user",
                nonce: loginNonce);
            _ = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=c1&state={Uri.EscapeDataString(loginQuery["state"].ToString())}", UriKind.Relative),
                ct);

            // 2. Without workspace custom OAuth app, broadcaster and bot flows do not fall back to instance-cid.
            HttpResponseMessage missingBroadcaster = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=broadcaster&workspaceId={ws.Id}", UriKind.Relative),
                ct);
            _ = missingBroadcaster.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = missingBroadcaster.Headers.Location!.ToString().Should().Contain("error=oauth_not_configured");

            HttpResponseMessage missingBot = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=bot&workspaceId={ws.Id}", UriKind.Relative),
                ct);
            _ = missingBot.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = missingBot.Headers.Location!.ToString().Should().Contain("error=oauth_not_configured");

            // 3. Once workspace custom OAuth app is configured, broadcaster and bot flows use workspace client_id.
            IWorkspaceStore wsStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            _ = await wsStore.SetCustomOAuthAppAsync(ws.Id, "twitch", "streamer-api-cid", "streamer-api-secret", cancellationToken: ct);

            HttpResponseMessage broadcasterStart = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=broadcaster&workspaceId={ws.Id}", UriKind.Relative),
                ct);
            _ = broadcasterStart.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> broadcasterQuery = QueryHelpers.ParseQuery(broadcasterStart.Headers.Location!.Query);
            _ = broadcasterQuery["client_id"].ToString().Should().Be("streamer-api-cid");

            HttpResponseMessage botStart = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=bot&workspaceId={ws.Id}", UriKind.Relative),
                ct);
            _ = botStart.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> botQuery = QueryHelpers.ParseQuery(botStart.Headers.Location!.Query);
            _ = botQuery["client_id"].ToString().Should().Be("streamer-api-cid");
        }
    }

    #endregion

    private static async Task SeedInstanceOAuthAndSettingsAsync(
        IServiceProvider services,
        bool allowOpenRegistration,
        InstanceOAuthAppPolicy policy,
        CancellationToken ct)
    {
        IInstanceSettingsService settingsService = services.GetRequiredService<IInstanceSettingsService>();
        InstanceSettings settings = await settingsService.GetAsync(ct).ConfigureAwait(false);
        settings.AllowOpenRegistration = allowOpenRegistration;
        settings.InstanceOAuthAppPolicy = policy.ToString();
        await settingsService.UpdateAsync(settings, ct).ConfigureAwait(false);

        IInstanceOAuthConfigService oauthService = services.GetRequiredService<IInstanceOAuthConfigService>();
        await oauthService.SetAsync("twitch", new PlatformOAuthAppCredentials("instance-cid", "instance-secret"), ct).ConfigureAwait(false);
    }

    private static async Task<Workspace> SeedWorkspaceWithOwnerAsync(IServiceProvider services, CancellationToken ct)
    {
        IPanelUserStore userStore = services.GetRequiredService<IPanelUserStore>();
        PanelUser owner = await userStore.CreateUserAsync(
            new PanelUser { DisplayName = "OwnerUser" },
            new UserLoginIdentity { ProviderId = "twitch", ProviderUserId = "owner-twitch-1", ProviderUsername = "owner_user" },
            ct).ConfigureAwait(false);

        IWorkspaceStore wsStore = services.GetRequiredService<IWorkspaceStore>();
        return await wsStore.CreateWorkspaceAsync(
            new Workspace
            {
                OwnerUserId = owner.Id,
                Name = "Owner Channel",
                PrimaryPlatformId = "twitch",
            },
            new WorkspacePlatform
            {
                PlatformId = "twitch",
                Role = PlatformRole.Primary,
                PlatformChannelId = "owner-twitch-1",
            },
            ct).ConfigureAwait(false);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        Match match = AntiforgeryInputRegex().Match(html);
        _ = match.Success.Should().BeTrue("HTML should contain an antiforgery token");
        return match.Groups["value"].Value;
    }

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="(?<value>[^"]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryInputRegex();

    private sealed class TempDaemonDirectories : IDisposable
    {
        public TempDaemonDirectories()
        {
            this.Root = Path.Combine(Path.GetTempPath(), $"sa-auth-test-{Guid.NewGuid():N}");
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

    private sealed class AuthWebTestApplicationFactory(
        TempDaemonDirectories dirs,
        TwitchOAuthEndpointOptions? twitchOptions = null) : WebApplicationFactory<Program>
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
            _ = builder.UseSetting("Bootstrap:Node:InstanceId", $"auth-test-{Guid.NewGuid():N}");
            _ = builder.UseSetting("Bootstrap:Sentry:Enabled", "false");

            if (twitchOptions is not null)
            {
                _ = builder.ConfigureServices(services => _ = services.Configure<TwitchOAuthEndpointOptions>(opts =>
                    {
                        opts.AuthorizeEndpoint = twitchOptions.AuthorizeEndpoint;
                        opts.TokenEndpoint = twitchOptions.TokenEndpoint;
                        opts.ValidateEndpoint = twitchOptions.ValidateEndpoint;
                        opts.RevokeEndpoint = twitchOptions.RevokeEndpoint;
                        opts.UserInfoEndpoint = twitchOptions.UserInfoEndpoint;
                        opts.OpenIdConfigurationEndpoint = twitchOptions.OpenIdConfigurationEndpoint;
                    }));
            }
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
        public string ValidatedUserId { get; set; } = "user-1";
        public string ValidatedLogin { get; set; } = "user_login";

        public static async Task<MockTwitchOAuthServer> StartAsync(CancellationToken cancellationToken)
        {
            RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-twitch-auth-key" };

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

            _ = app.MapGet("/oauth2/validate", () => Results.Json(new
            {
                client_id = "instance-cid",
                login = holder!.ValidatedLogin,
                scopes = _chatReadScopes,
                user_id = holder.ValidatedUserId,
                expires_in = 3500,
            }));

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
            string nonce,
            string? email = null)
        {
            List<Claim> claims =
            [
                new("sub", subject),
                new("preferred_username", preferredUsername),
                new("nonce", nonce),
            ];

            if (!string.IsNullOrWhiteSpace(email))
            {
                claims.Add(new Claim("email", email));
            }

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
