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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Metadata;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using StreamActions.Daemon;
using StreamActions.Daemon.Platforms;
using StreamActions.Web.Components.Pages;
using StreamActions.Web.Hubs;
using StreamActions.Web.Security;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;
using XunitTestContext = Xunit.TestContext;

namespace StreamActions.Web.Tests;

public sealed partial class WorkspaceAndTokenTests
{
    #region WorkspaceSessionContext

    [Fact]
    [Trait("Member", "InitializeAsync")]
    public async Task WorkspaceSessionContext_SwitchesBetweenAccessibleWorkspacesAndRejectsUnauthorizedWorkspace()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid ownerId = Guid.NewGuid();
        Guid modId = Guid.NewGuid();
        Guid wsTwitchId = Guid.NewGuid();
        Guid wsKickId = Guid.NewGuid();
        Guid wsPrivateId = Guid.NewGuid();

        PanelUser modUser = new()
        {
            Id = modId,
            DisplayName = "channel_mod",
            IsRoot = false,
            IsGlobalAdmin = false,
        };

        InMemoryWorkspaceStore workspaceStore = new();
        workspaceStore.Workspaces[wsTwitchId] = new Workspace { Id = wsTwitchId, OwnerUserId = ownerId, Name = "Alpha Twitch", PrimaryPlatformId = "twitch", CreatedAtUtc = DateTimeOffset.UtcNow };
        workspaceStore.Workspaces[wsKickId] = new Workspace { Id = wsKickId, OwnerUserId = modId, Name = "Mod Kick", PrimaryPlatformId = "kick", CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1) };
        workspaceStore.Workspaces[wsPrivateId] = new Workspace { Id = wsPrivateId, OwnerUserId = ownerId, Name = "Private Channel", PrimaryPlatformId = "youtube", CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(2) };

        InMemoryPanelUserStore userStore = new();
        userStore.Users[modId] = modUser;
        userStore.Identities[modId] =
        [
            new UserLoginIdentity { Id = Guid.NewGuid(), PanelUserId = modId, ProviderId = "kick", ProviderUserId = "mod-kick-1", ProviderUsername = "channel_mod" },
        ];

        StubWorkspaceAccessService accessService = new(new Dictionary<Guid, HashSet<Guid>>
        {
            [modId] = [wsTwitchId, wsKickId],
        }, workspaceStore);

        ClaimsPrincipal principal = AuthClaims.BuildClaimsPrincipal(modUser, authProvider: "kick");
        StubAuthenticationStateProvider authStateProvider = new(principal);

        using WorkspaceSessionContext session = new(authStateProvider, userStore, accessService);
        await session.InitializeAsync(ct);

        // Prefers owned workspace matching sa:auth_provider ("kick").
        _ = session.AccessibleWorkspaces.Should().HaveCount(2);
        _ = session.ActiveWorkspaceId.Should().Be(wsKickId);

        bool switchedToTwitch = await session.SelectWorkspaceAsync(wsTwitchId, ct);
        _ = switchedToTwitch.Should().BeTrue();
        _ = session.ActiveWorkspaceId.Should().Be(wsTwitchId);

        bool switchedToUnauthorized = await session.SelectWorkspaceAsync(wsPrivateId, ct);
        _ = switchedToUnauthorized.Should().BeFalse();
        _ = session.ActiveWorkspaceId.Should().Be(wsTwitchId);
    }

    [Fact]
    [Trait("Member", "InitializeAsync")]
    public async Task WorkspaceSessionContext_WhenWorkspaceCookieIsPresentAndAccessible_SelectsCookieWorkspace()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        Guid ws1Id = Guid.NewGuid();
        Guid ws2Id = Guid.NewGuid();

        PanelUser user = new() { Id = userId, DisplayName = "owner" };
        InMemoryWorkspaceStore workspaceStore = new();
        workspaceStore.Workspaces[ws1Id] = new Workspace { Id = ws1Id, OwnerUserId = userId, Name = "First", PrimaryPlatformId = "twitch" };
        workspaceStore.Workspaces[ws2Id] = new Workspace { Id = ws2Id, OwnerUserId = userId, Name = "Second", PrimaryPlatformId = "youtube" };

        InMemoryPanelUserStore userStore = new();
        userStore.Users[userId] = user;
        userStore.Identities[userId] = [];

        StubWorkspaceAccessService accessService = new(new Dictionary<Guid, HashSet<Guid>>
        {
            [userId] = [ws1Id, ws2Id],
        }, workspaceStore);

        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Cookie = $"{AuthClaims.WorkspaceCookieName}={ws2Id:D}";
        HttpContextAccessor accessor = new() { HttpContext = httpContext };

        using WorkspaceSessionContext session = new(
            new StubAuthenticationStateProvider(AuthClaims.BuildClaimsPrincipal(user, authProvider: "twitch")),
            userStore,
            accessService,
            accessor);

        await session.InitializeAsync(ct);

        _ = session.ActiveWorkspaceId.Should().Be(ws2Id);
    }

    #endregion

    #region PermissionHub

    [Fact]
    [Trait("Member", "JoinWorkspaceAsync")]
    public async Task PermissionHub_JoinWorkspaceAsync_ThrowsHubExceptionWhenUnauthorizedAndSucceedsWhenAuthorized()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        Guid allowedWorkspaceId = Guid.NewGuid();
        Guid forbiddenWorkspaceId = Guid.NewGuid();

        PanelUser user = new() { Id = userId, DisplayName = "streamer" };
        InMemoryPanelUserStore userStore = new();
        userStore.Users[userId] = user;
        userStore.Identities[userId] = [];

        InMemoryWorkspaceStore workspaceStore = new();
        workspaceStore.Workspaces[allowedWorkspaceId] = new Workspace { Id = allowedWorkspaceId, OwnerUserId = userId, Name = "Allowed", PrimaryPlatformId = "twitch" };
        workspaceStore.Workspaces[forbiddenWorkspaceId] = new Workspace { Id = forbiddenWorkspaceId, OwnerUserId = Guid.NewGuid(), Name = "Forbidden", PrimaryPlatformId = "twitch" };

        StubWorkspaceAccessService accessService = new(new Dictionary<Guid, HashSet<Guid>>
        {
            [userId] = [allowedWorkspaceId],
        }, workspaceStore);

        TrackingGroupManager groups = new();

        using PermissionHub unauthorizedHub = new(userStore, accessService)
        {
            Context = new StubHubCallerContext(AuthClaims.BuildClaimsPrincipal(user), "conn-1", ct),
            Groups = groups,
        };

        Func<Task> joinForbidden = () => unauthorizedHub.JoinWorkspaceAsync(forbiddenWorkspaceId);
        _ = await joinForbidden.Should().ThrowAsync<HubException>();
        _ = groups.AddedConnections.Should().BeEmpty();

        using PermissionHub unauthenticatedHub = new(userStore, accessService)
        {
            Context = new StubHubCallerContext(new ClaimsPrincipal(new ClaimsIdentity()), "conn-2", ct),
            Groups = groups,
        };

        Func<Task> joinUnauthenticated = () => unauthenticatedHub.JoinWorkspaceAsync(allowedWorkspaceId);
        _ = await joinUnauthenticated.Should().ThrowAsync<HubException>();

        await unauthorizedHub.JoinWorkspaceAsync(allowedWorkspaceId);
        _ = groups.AddedConnections.Should().ContainSingle(x => x.ConnectionId == "conn-1" && x.GroupName == PermissionHub.WorkspaceGroupName(allowedWorkspaceId));
    }

    #endregion

    #region WorkspacePlatformsComponent

    [Fact]
    [Trait("Member", "WorkspacePlatforms")]
    public void WorkspacePlatforms_RendersScopeDriftBannerForceVerifyPersonalBotLinkAndBroadcasterToggle()
    {
        Guid ownerId = Guid.NewGuid();
        Guid workspaceId = Guid.NewGuid();
        PanelUser owner = new() { Id = ownerId, DisplayName = "streamer_owner" };

        Workspace workspace = new()
        {
            Id = workspaceId,
            OwnerUserId = ownerId,
            Name = "Streamer Channel",
            PrimaryPlatformId = "twitch",
            AllowCustomBotAccountOverride = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        WorkspacePlatform twitchPlatform = new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            PlatformId = "twitch",
            Role = PlatformRole.Primary,
            PlatformChannelId = "1001",
            CustomClientId = "custom-cid",
            EncryptedCustomClientSecret = "protected-secret-ciphertext",
            Metadata = new PlatformMetadata { UseBroadcasterAsBot = false },
        };

        InMemoryWorkspaceStore workspaceStore = new();
        workspaceStore.Workspaces[workspaceId] = workspace;
        workspaceStore.Platforms[(workspaceId, "twitch")] = twitchPlatform;

        InMemoryPanelUserStore userStore = new();
        userStore.Users[ownerId] = owner;
        userStore.Identities[ownerId] =
        [
            new UserLoginIdentity { Id = Guid.NewGuid(), PanelUserId = ownerId, ProviderId = "twitch", ProviderUserId = "1001", ProviderUsername = "streamer_owner" },
        ];

        InMemoryCredentialStore credStore = new();
        credStore.Save(workspaceId, "twitch", TokenRole.Broadcaster, new PlatformTokenPayload(
            "broadcaster-access",
            "broadcaster-refresh",
            DateTimeOffset.UtcNow.AddHours(4),
            ["chat:read", "user:read:email"],
            "bearer",
            "1001",
            "streamer_owner"));

        StubInstanceSettingsService settingsService = new(new InstanceSettings
        {
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceWithOptionalOverride),
            InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.DefaultWithOptionalOverride),
        });

        ServiceCollection cacheServices = new();
        _ = cacheServices.AddHybridCache();
        using ServiceProvider cacheProvider = cacheServices.BuildServiceProvider();
        HybridCache cache = cacheProvider.GetRequiredService<HybridCache>();
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        StubPermissionStore permStore = new(workspace);
        GrantGlobalFeature(permStore, workspaceId, BuiltInPermissionKeys.FeatureCustomBotAccount, PermissionState.Allow);
        PermissionService permService = new(permStore, new PermissionRegistry(), cache, bus);

        PluginManager pluginManager = new(
            [
                new StubScopePlugin(
                    "twitch.polls",
                    "twitch",
                    new Dictionary<string, IReadOnlyCollection<string>>
                    {
                        ["twitch"] = ["channel:manage:polls", "chat:read"],
                    }),
            ],
            cacheProvider,
            bus,
            NullLoggerFactory.Instance);
        ScopeDriftDetector driftDetector = new(pluginManager, []);
        BotCredentialResolver botResolver = new(settingsService, workspaceStore, credStore, permService);

        StubWorkspaceSessionContext sessionContext = new(new AccessibleWorkspace(workspace, true, [twitchPlatform]));

        using BunitContext context = new();
        _ = context.AddAuthorization().SetAuthorized("streamer_owner").SetPolicies(AuthClaims.PanelAuthenticatedPolicy);
        _ = context.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthenticationStateProvider(AuthClaims.BuildClaimsPrincipal(owner)));
        _ = context.Services.AddSingleton<IWorkspaceSessionContext>(sessionContext);
        _ = context.Services.AddSingleton<IWorkspaceStore>(workspaceStore);
        _ = context.Services.AddSingleton<IPlatformCredentialStore>(credStore);
        _ = context.Services.AddSingleton<IBotCredentialResolver>(botResolver);
        _ = context.Services.AddSingleton<IScopeDriftDetector>(driftDetector);
        _ = context.Services.AddSingleton<IInstanceSettingsService>(settingsService);
        _ = context.Services.AddSingleton<IPanelUserStore>(userStore);
        _ = context.Services.AddSingleton<IPermissionService>(permService);
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IMfaPolicyService>(new MfaPolicyService(
            settingsService,
            workspaceStore,
            permStore,
            new StubWorkspaceAccessService(new Dictionary<Guid, HashSet<Guid>> { [ownerId] = [workspaceId] }, workspaceStore)));
        _ = context.Services.AddSingleton(TimeProvider.System);

        IRenderedComponent<WorkspacePlatforms> component = context.Render<WorkspacePlatforms>();

        // Drift banner + incremental upgrade link + force_verify=true personal bot link + callback URI + write-only secret.
        _ = component.Markup.Should().Contain("Missing Platform Scopes — Click to Upgrade Permissions");
        _ = component.Markup.Should().Contain("channel:manage:polls");
        _ = component.Markup.Should().Contain("flow=broadcaster");
        _ = component.Markup.Should().Contain("scopes=chat%3Aread%20user%3Aread%3Aemail%20channel%3Amanage%3Apolls");
        _ = component.Markup.Should().Contain("flow=bot");
        _ = component.Markup.Should().Contain("force_verify=true");
        _ = component.Markup.Should().Contain("/auth/twitch/callback");
        _ = component.Markup.Should().Contain("custom-cid");
        _ = component.Markup.Should().NotContain("protected-secret-ciphertext");
        _ = component.Markup.Should().Contain("flow=link");

        // Toggle "Use Broadcaster as Bot" -> resolves WorkspaceBroadcasterFallback.
        component.Find("#use-broadcaster-bot-twitch").Change(true);
        _ = workspaceStore.Platforms[(workspaceId, "twitch")].Metadata.UseBroadcasterAsBot.Should().BeTrue();
        _ = component.Markup.Should().Contain(nameof(BotCredentialSource.WorkspaceBroadcasterFallback));
    }

    #endregion

    #region PermissionsComponent

    [Fact]
    [Trait("Member", "Permissions")]
    public void Permissions_AutomaticallyBindsToActiveWorkspaceAndUpdatesOnWorkspaceChanged()
    {
        Guid ws1Id = Guid.NewGuid();
        Guid ws2Id = Guid.NewGuid();
        Workspace ws1 = new() { Id = ws1Id, OwnerUserId = Guid.NewGuid(), Name = "Channel One", PrimaryPlatformId = "twitch" };
        Workspace ws2 = new() { Id = ws2Id, OwnerUserId = Guid.NewGuid(), Name = "Channel Two", PrimaryPlatformId = "kick" };

        StubWorkspaceSessionContext sessionContext = new(
            new AccessibleWorkspace(ws1, true, []),
            new AccessibleWorkspace(ws2, true, []));

        StubPermissionStore permStore = new(ws1, ws2);
        ServiceCollection cacheServices = new();
        _ = cacheServices.AddHybridCache();
        using ServiceProvider cacheProvider = cacheServices.BuildServiceProvider();
        HybridCache cache = cacheProvider.GetRequiredService<HybridCache>();
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionRegistry registry = new();
        PermissionService permService = new(permStore, registry, cache, bus);

        using BunitContext context = new();
        _ = context.AddAuthorization().SetAuthorized("streamer").SetPolicies(AuthClaims.PanelAuthenticatedPolicy);
        _ = context.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthenticationStateProvider(
            AuthClaims.BuildClaimsPrincipal(new PanelUser { Id = ws1.OwnerUserId, DisplayName = "streamer" })));
        _ = context.Services.AddSingleton<IWorkspaceSessionContext>(sessionContext);
        _ = context.Services.AddSingleton<IPermissionStore>(permStore);
        _ = context.Services.AddSingleton<IPermissionService>(permService);
        _ = context.Services.AddSingleton<IPermissionRegistry>(registry);
        _ = context.Services.AddSingleton<IEventBus>(bus);
        _ = context.Services.AddSingleton<IMfaPolicyService>(new MfaPolicyService(
            new StubInstanceSettingsService(new InstanceSettings()),
            new InMemoryWorkspaceStore(),
            permStore,
            new StubWorkspaceAccessService([], new InMemoryWorkspaceStore())));
        _ = context.Services.AddSingleton(TimeProvider.System);

        IRenderedComponent<Permissions> component = context.Render<Permissions>();
        _ = component.Markup.Should().Contain("Channel One");

        _ = sessionContext.TriggerSelect(ws2Id);
        component.WaitForAssertion(() => _ = component.Markup.Should().Contain("Channel Two"));
    }

    #endregion

    #region WorkspaceSwitcherEndpoint

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task WorkspaceSelectAndBroadcasterIncrementalScopes_EnforceAuthorizationAndPersistCookie()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using WorkspaceWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions);

            IInstanceOAuthConfigService oauthConfig = factory.Services.GetRequiredService<IInstanceOAuthConfigService>();
            await oauthConfig.SetAsync("twitch", new PlatformOAuthAppCredentials("inst-cid", "inst-secret"), ct);

            IPanelUserStore userStore = factory.Services.GetRequiredService<IPanelUserStore>();
            PanelUser streamerA = await userStore.CreateUserAsync(
                new PanelUser { DisplayName = "streamer_a" },
                new UserLoginIdentity { ProviderId = "twitch", ProviderUserId = "a-1", ProviderUsername = "streamer_a" },
                ct);
            PanelUser streamerB = await userStore.CreateUserAsync(
                new PanelUser { DisplayName = "streamer_b" },
                new UserLoginIdentity { ProviderId = "twitch", ProviderUserId = "b-1", ProviderUsername = "streamer_b" },
                ct);

            IWorkspaceStore workspaceStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            Workspace wsA1 = await workspaceStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = streamerA.Id, Name = "A Primary", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "a-1" },
                ct);
            Workspace wsA2 = await workspaceStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = streamerA.Id, Name = "A Secondary", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "a-2" },
                ct);
            Workspace wsB = await workspaceStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = streamerB.Id, Name = "B Private", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "b-1" },
                ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            // Unauthenticated POST /auth/workspace/select challenges (redirects to /login).
            using FormUrlEncodedContent emptyForm = new([]);
            HttpResponseMessage unauthSelect = await client.PostAsync(new Uri("/auth/workspace/select", UriKind.Relative), emptyForm, ct);
            _ = unauthSelect.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = unauthSelect.Headers.Location!.AbsolutePath.Should().Be("/login");

            // Sign in as streamerA via real OAuth login flow -> sets StreamActions.Workspace cookie to wsA1.
            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> loginQuery = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            string state = loginQuery["state"].ToString();
            string nonce = loginQuery["nonce"].ToString();
            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(baseUrl, "inst-cid", "a-1", "streamer_a", nonce);

            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-a&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);
            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.GetValues("Set-Cookie")
                .Should().Contain(c => c.Contains($"{AuthClaims.WorkspaceCookieName}={wsA1.Id:D}", StringComparison.Ordinal));

            HttpResponseMessage platformsGet = await client.GetAsync(new Uri("/workspace/platforms", UriKind.Relative), ct);
            _ = platformsGet.StatusCode.Should().Be(HttpStatusCode.OK);
            string html = await platformsGet.Content.ReadAsStringAsync(ct);
            string antiforgery = ExtractAntiforgeryToken(html);

            // Select wsA2 -> 302 + sets StreamActions.Workspace cookie to wsA2.
            using FormUrlEncodedContent selectAllowedForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                new KeyValuePair<string, string>("workspaceId", wsA2.Id.ToString("D")),
                new KeyValuePair<string, string>("returnUrl", "/workspace/platforms"),
            ]);
            HttpResponseMessage selectAllowedResp = await client.PostAsync(new Uri("/auth/workspace/select", UriKind.Relative), selectAllowedForm, ct);
            _ = selectAllowedResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = selectAllowedResp.Headers.Location!.ToString().Should().Be("/workspace/platforms");
            _ = selectAllowedResp.Headers.GetValues("Set-Cookie").Should().Contain(c => c.Contains($"{AuthClaims.WorkspaceCookieName}={wsA2.Id:D}", StringComparison.Ordinal));

            // Select wsB (unauthorized for streamerA) -> 403 Forbidden.
            using FormUrlEncodedContent selectForbiddenForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                new KeyValuePair<string, string>("workspaceId", wsB.Id.ToString("D")),
                new KeyValuePair<string, string>("returnUrl", "/workspace/platforms"),
            ]);
            HttpResponseMessage selectForbiddenResp = await client.PostAsync(new Uri("/auth/workspace/select", UriKind.Relative), selectForbiddenForm, ct);
            _ = selectForbiddenResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = selectForbiddenResp.Headers.Location!.AbsolutePath.Should().Be("/access-denied");
            _ = selectForbiddenResp.Headers.Contains("Set-Cookie").Should().BeFalse();

            // flow=broadcaster supports custom scopes query param for incremental re-auth.
            HttpResponseMessage broadcasterAuthRedirect = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=broadcaster&workspaceId={wsA1.Id:D}&scopes=chat%3Aread+channel%3Amanage%3Apolls", UriKind.Relative),
                ct);
            _ = broadcasterAuthRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> broadcasterQuery = QueryHelpers.ParseQuery(broadcasterAuthRedirect.Headers.Location!.Query);
            _ = broadcasterQuery["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Should().BeEquivalentTo(["chat:read", "channel:manage:polls"]);

            // flow=bot enforces force_verify=true.
            HttpResponseMessage botAuthRedirect = await client.GetAsync(
                new Uri($"/auth/twitch/login?flow=bot&workspaceId={wsA1.Id:D}", UriKind.Relative),
                ct);
            _ = botAuthRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> botQuery = QueryHelpers.ParseQuery(botAuthRedirect.Headers.Location!.Query);
            _ = botQuery["force_verify"].ToString().Should().Be("true");
        }
    }

    #endregion

    private static string ExtractAntiforgeryToken(string html)
    {
        Match match = AntiforgeryTokenRegex().Match(html);
        _ = match.Success.Should().BeTrue("expected antiforgery hidden input in HTML");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTokenRegex();

    private static void GrantGlobalFeature(StubPermissionStore permissionStore, Guid workspaceId, string permissionKey, PermissionState state)
    {
        Guid globalGroupId = Guid.NewGuid();
        permissionStore.GlobalGroups[globalGroupId] = new PermissionGroup { Id = globalGroupId, WorkspaceId = null, Name = "Plan" };
        permissionStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment { Id = Guid.NewGuid(), WorkspaceId = workspaceId, GlobalGroupId = globalGroupId });
        permissionStore.Grants.Add(new PermissionGrant { Id = Guid.NewGuid(), GroupId = globalGroupId, PermissionKey = permissionKey, State = state });
    }

    private sealed class TempDaemonDirectories : IDisposable
    {
        public TempDaemonDirectories()
        {
            this.RootDir = Path.Combine(Path.GetTempPath(), $"sa-ws-tests-{Guid.NewGuid():N}");
            this.ConfigDir = Path.Combine(this.RootDir, "config");
            this.DataDir = Path.Combine(this.RootDir, "data");
            this.LogDir = Path.Combine(this.RootDir, "logs");
            this.PluginsDir = Path.Combine(this.RootDir, "plugins");
            this.BackupDir = Path.Combine(this.RootDir, "backups");
            this.KeyRingDir = Path.Combine(this.DataDir, "keys");
            _ = Directory.CreateDirectory(this.RootDir);
        }

        public string RootDir { get; }
        public string ConfigDir { get; }
        public string DataDir { get; }
        public string LogDir { get; }
        public string PluginsDir { get; }
        public string BackupDir { get; }
        public string KeyRingDir { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.RootDir))
                {
                    Directory.Delete(this.RootDir, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class WorkspaceWebTestApplicationFactory(
        TempDaemonDirectories dirs,
        TwitchOAuthEndpointOptions twitchOptions) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _ = builder.UseEnvironment("Development");
            _ = builder.UseSetting("Bootstrap:Database:Provider", "Sqlite");
            _ = builder.UseSetting("Bootstrap:Database:ConnectionString", $"Data Source={Path.Combine(dirs.DataDir, "test.db")}");
            _ = builder.UseSetting("Bootstrap:Paths:ConfigDir", dirs.ConfigDir);
            _ = builder.UseSetting("Bootstrap:Paths:DataDir", dirs.DataDir);
            _ = builder.UseSetting("Bootstrap:Paths:LogDir", dirs.LogDir);
            _ = builder.UseSetting("Bootstrap:Paths:PluginsDir", dirs.PluginsDir);
            _ = builder.UseSetting("Bootstrap:Paths:BackupDir", dirs.BackupDir);
            _ = builder.UseSetting("Bootstrap:DataProtection:KeyRingDir", dirs.KeyRingDir);
            _ = builder.UseSetting("Bootstrap:Sentry:Enabled", "false");

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

        private static readonly string[] _defaultOAuthScopes = ["openid", "chat:read"];

        public static async Task<MockTwitchOAuthServer> StartAsync(CancellationToken cancellationToken)
        {
            RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-ws-key" };

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
                return Results.Json(new
                {
                    access_token = "mock-access-token",
                    refresh_token = "mock-refresh-token",
                    expires_in = 3600,
                    scope = _defaultOAuthScopes,
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

        public string CreateSignedIdToken(string issuer, string audience, string subject, string preferredUsername, string nonce)
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

    private sealed class StubAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class StubWorkspaceSessionContext(params AccessibleWorkspace[] workspaces) : IWorkspaceSessionContext
    {
        private readonly List<AccessibleWorkspace> _workspaces = [.. workspaces];
        public Guid? ActiveWorkspaceId { get; private set; } = workspaces.Length > 0 ? workspaces[0].Workspace.Id : null;
        public AccessibleWorkspace? ActiveWorkspace { get; private set; } = workspaces.Length > 0 ? workspaces[0] : null;
        public IReadOnlyList<AccessibleWorkspace> AccessibleWorkspaces => this._workspaces;
        public event EventHandler? WorkspaceChanged;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<bool> SelectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.TriggerSelect(workspaceId));

        public bool TriggerSelect(Guid workspaceId)
        {
            AccessibleWorkspace? match = this._workspaces.FirstOrDefault(w => w.Workspace.Id == workspaceId);
            if (match is null)
            {
                return false;
            }

            this.ActiveWorkspace = match;
            this.ActiveWorkspaceId = match.Workspace.Id;
            this.WorkspaceChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public ValueTask RefreshAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubWorkspaceAccessService(
        Dictionary<Guid, HashSet<Guid>> allowedWorkspacesByUser,
        IWorkspaceStore workspaceStore) : IWorkspaceAccessService
    {
        public async ValueTask<IReadOnlyList<AccessibleWorkspace>> GetAccessibleWorkspacesAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            CancellationToken ct = default)
        {
            HashSet<Guid> allowed = allowedWorkspacesByUser.GetValueOrDefault(user.Id) ?? [];
            List<AccessibleWorkspace> list = [];
            foreach (Guid wsId in allowed)
            {
                Workspace? ws = await workspaceStore.GetByIdAsync(wsId, ct).ConfigureAwait(false);
                if (ws is not null)
                {
                    IReadOnlyList<WorkspacePlatform> platforms = await workspaceStore.GetPlatformsAsync(wsId, ct).ConfigureAwait(false);
                    list.Add(new AccessibleWorkspace(ws, ws.OwnerUserId == user.Id, platforms));
                }
            }

            return list;
        }

        public ValueTask<bool> CanAccessWorkspaceAsync(Guid workspaceId, PanelUser user, IReadOnlyList<UserLoginIdentity> identities, IReadOnlyCollection<string>? oidcGroupClaims = null, CancellationToken ct = default) =>
            ValueTask.FromResult((allowedWorkspacesByUser.GetValueOrDefault(user.Id) ?? []).Contains(workspaceId));

        public ValueTask<bool> HasPermissionInWorkspaceAsync(Guid workspaceId, PanelUser user, IReadOnlyList<UserLoginIdentity> identities, string permissionKey, IReadOnlyCollection<string>? oidcGroupClaims = null, CancellationToken ct = default) =>
            this.CanAccessWorkspaceAsync(workspaceId, user, identities, oidcGroupClaims, ct);
    }

    private sealed class InMemoryPanelUserStore : IPanelUserStore
    {
        public Dictionary<Guid, PanelUser> Users { get; } = [];
        public Dictionary<Guid, List<UserLoginIdentity>> Identities { get; } = [];

        public ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Users.Count > 0);
        public ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Users.Values.Any(static u => u.IsRoot));
        public ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Users.GetValueOrDefault(userId));
        public ValueTask<PanelUser?> GetByLoginIdentityAsync(string providerId, string providerUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<PanelUser?>(null);
        public ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PanelUser>>([.. this.Users.Values]);
        public ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserLoginIdentity>>(this.Identities.GetValueOrDefault(panelUserId) ?? []);
        public ValueTask<PanelUser> CreateUserAsync(PanelUser user, UserLoginIdentity? initialIdentity = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(user);
        public ValueTask<PanelUser> UpdateUserAsync(PanelUser user, CancellationToken cancellationToken = default) => ValueTask.FromResult(user);
        public ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(UserLoginIdentity identity, CancellationToken cancellationToken = default) => ValueTask.FromResult(identity);
        public ValueTask<bool> UnlinkLoginIdentityAsync(Guid panelUserId, string providerId, string providerUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class InMemoryWorkspaceStore : IWorkspaceStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];
        public Dictionary<(Guid WorkspaceId, string PlatformId), WorkspacePlatform> Platforms { get; } = [];

        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values.Where(w => w.OwnerUserId == ownerUserId)]);

        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values]);

        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);

        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            this.Workspaces[workspace.Id] = workspace;
            return ValueTask.FromResult(workspace);
        }

        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.Remove(workspaceId));

        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>([.. this.Platforms.Where(kvp => kvp.Key.WorkspaceId == workspaceId).Select(static kvp => kvp.Value)]);

        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default)
        {
            foreach (KeyValuePair<(Guid WorkspaceId, string PlatformId), WorkspacePlatform> kvp in this.Platforms)
            {
                if (kvp.Key.WorkspaceId == workspaceId && string.Equals(kvp.Key.PlatformId, platformId, StringComparison.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult<WorkspacePlatform?>(kvp.Value);
                }
            }

            return ValueTask.FromResult<WorkspacePlatform?>(null);
        }

        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default)
        {
            this.Platforms[(platform.WorkspaceId, platform.PlatformId.ToUpperInvariant())] = platform;
            return ValueTask.FromResult(platform);
        }

        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Platforms[(workspaceId, platformId.ToUpperInvariant())]);

        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);

        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Platforms.Remove((workspaceId, platformId.ToUpperInvariant())));
    }

    private sealed class InMemoryCredentialStore : IPlatformCredentialStore
    {
        private readonly Dictionary<(Guid? WorkspaceId, string PlatformId, TokenRole Role), (PlatformCredential Record, PlatformTokenPayload Payload)> _items = [];

        public void Save(Guid? workspaceId, string platformId, TokenRole role, PlatformTokenPayload payload, CredentialStatus status = CredentialStatus.Active)
        {
            string normalized = platformId.ToUpperInvariant();
            this._items[(workspaceId, normalized, role)] = (new PlatformCredential
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                PlatformId = normalized,
                TokenRole = role,
                Status = status,
                ExpiresAtUtc = payload.ExpiresAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, payload);
        }

        public ValueTask<PlatformCredential?> GetCredentialRecordAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._items.TryGetValue((workspaceId, platformId.ToUpperInvariant(), tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry) ? entry.Record : null);

        public ValueTask<IReadOnlyList<PlatformCredential>> GetCredentialsForWorkspaceAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([.. this._items.Values.Where(v => v.Record.WorkspaceId == workspaceId).Select(static v => v.Record)]);

        public ValueTask<IReadOnlyList<PlatformCredential>> GetAllCredentialsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([.. this._items.Values.Select(static v => v.Record)]);

        public ValueTask<(bool Found, CredentialStatus Status, PlatformTokenPayload? Payload)> TryGetTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            this._items.TryGetValue((workspaceId, platformId.ToUpperInvariant(), tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry)
                ? ValueTask.FromResult<(bool, CredentialStatus, PlatformTokenPayload?)>((true, entry.Record.Status, entry.Payload))
                : ValueTask.FromResult<(bool, CredentialStatus, PlatformTokenPayload?)>((false, CredentialStatus.Revoked, null));

        public ValueTask<PlatformCredential> SaveTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, PlatformTokenPayload payload, CredentialStatus status = CredentialStatus.Active, CancellationToken cancellationToken = default)
        {
            this.Save(workspaceId, platformId, tokenRole, payload, status);
            return ValueTask.FromResult(this._items[(workspaceId, platformId.ToUpperInvariant(), tokenRole)].Record);
        }

        public ValueTask<bool> UpdateStatusAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CredentialStatus status, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> DeleteTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
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

    private sealed class StubPermissionStore(params Workspace[] workspaces) : IPermissionStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = workspaces.ToDictionary(static w => w.Id);
        public Dictionary<Guid, PermissionGroup> GlobalGroups { get; } = [];
        public List<WorkspaceGlobalGroupAssignment> WorkspaceGlobalAssignments { get; } = [];
        public List<PermissionGrant> Grants { get; } = [];

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([]);
        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGrant>>([.. this.Grants.Where(g => g.GroupId == groupId)]);
        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) => ValueTask.FromResult(PermissionState.Inherit);
        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            HashSet<Guid> assigned = [.. this.WorkspaceGlobalAssignments.Where(a => a.WorkspaceId == workspaceId).Select(a => a.GlobalGroupId)];
            return ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this.GlobalGroups.Values.Where(g => assigned.Contains(g.Id))]);
        }

        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<Guid>>([]);
        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<UserPermissionOverride>>([]);
        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<WorkspacePlatformRoleRoster?>(null);
        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default) => ValueTask.FromResult(group);
        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGroupMember>>([]);
        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubScopePlugin(
        string id,
        string? requiredPlatformId,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> requiredPlatformScopes) : IPlugin
    {
        public string Id { get; } = id;
        public string Name { get; } = id;
        public Version Version { get; } = new(1, 0, 0);
        public string? RequiredPlatformId { get; } = requiredPlatformId;
        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> RequiredPlatformScopes { get; } = requiredPlatformScopes;
        public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ShutdownAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubPlatformOAuthProvider(string platformId) : IPlatformOAuthProvider
    {
        public string PlatformId { get; } = platformId;
        public Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IReadOnlyCollection<string>? scopes, string state, bool forceVerify = false, string? nonce = null) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, Uri redirectUri, string? expectedNonce = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask RevokeTokenAsync(string clientId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TrackingGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedConnections { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            this.AddedConnections.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubHubCallerContext(
        ClaimsPrincipal user,
        string connectionId,
        CancellationToken connectionAborted) : HubCallerContext
    {
        public override string ConnectionId { get; } = connectionId;
        public override string? UserIdentifier => this.User.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal User { get; } = user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override Microsoft.AspNetCore.Http.Features.IFeatureCollection Features => throw new NotSupportedException();
        public override CancellationToken ConnectionAborted { get; } = connectionAborted;
        public override void Abort() { }
    }
}
