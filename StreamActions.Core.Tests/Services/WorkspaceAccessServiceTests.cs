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
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class WorkspaceAccessServiceTests
{
    private static async Task<(WorkspaceAccessService Service, InMemoryWorkspaceStore WorkspaceStore, InMemoryPermissionStore PermissionStore, ServiceProvider Provider, InMemoryEventBus EventBus)> CreateSutAsync()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        InMemoryWorkspaceStore workspaceStore = new();
        InMemoryPermissionStore permissionStore = new(workspaceStore);
        PermissionRegistry registry = new();
        InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionService permissionService = new(permissionStore, registry, cache, eventBus);
        WorkspaceAccessService service = new(workspaceStore, permissionService);

        await Task.CompletedTask.ConfigureAwait(false);
        return (service, workspaceStore, permissionStore, provider, eventBus);
    }

    private static async Task<(
        WorkspaceAccessService Service,
        InMemoryWorkspaceStore WorkspaceStore,
        InMemoryPermissionStore PermissionStore,
        InMemoryInstanceSettingsService SettingsService,
        InMemoryOidcProviderStore OidcStore,
        ServiceProvider Provider,
        InMemoryEventBus EventBus)> CreateFullSutAsync()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        InMemoryWorkspaceStore workspaceStore = new();
        InMemoryPermissionStore permissionStore = new(workspaceStore);
        InMemoryInstanceSettingsService settingsService = new();
        InMemoryOidcProviderStore oidcStore = new();
        PermissionRegistry registry = new();
        InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionService permissionService = new(permissionStore, registry, cache, eventBus);
        WorkspaceAccessService service = new(workspaceStore, permissionService, settingsService, oidcStore, permissionStore);

        await Task.CompletedTask.ConfigureAwait(false);
        return (service, workspaceStore, permissionStore, settingsService, oidcStore, provider, eventBus);
    }

    #region GetAccessibleWorkspacesAsync

    [Fact]
    [Trait("Member", "GetAccessibleWorkspacesAsync")]
    public async Task GetAccessibleWorkspacesAsyncWhenUserIsRootOrGlobalAdminReturnsAllWorkspaces()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WorkspaceAccessService service, InMemoryWorkspaceStore wsStore, _, ServiceProvider provider, InMemoryEventBus eventBus) = await CreateSutAsync();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid rootUserId = Guid.NewGuid();
            Guid otherUserId = Guid.NewGuid();

            Workspace ownedWs = new() { Id = Guid.NewGuid(), OwnerUserId = rootUserId, Name = "Root Channel", PrimaryPlatformId = "twitch", CreatedAtUtc = DateTimeOffset.UtcNow };
            Workspace otherWs = new() { Id = Guid.NewGuid(), OwnerUserId = otherUserId, Name = "Other Channel", PrimaryPlatformId = "twitch", CreatedAtUtc = DateTimeOffset.UtcNow };
            wsStore.Workspaces[ownedWs.Id] = ownedWs;
            wsStore.Workspaces[otherWs.Id] = otherWs;
            wsStore.Platforms[ownedWs.Id] = [new WorkspacePlatform { Id = Guid.NewGuid(), WorkspaceId = ownedWs.Id, PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "100" }];
            wsStore.Platforms[otherWs.Id] = [new WorkspacePlatform { Id = Guid.NewGuid(), WorkspaceId = otherWs.Id, PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "200" }];

            PanelUser rootUser = new() { Id = rootUserId, DisplayName = "root", IsRoot = true };
            IReadOnlyList<AccessibleWorkspace> result = await service.GetAccessibleWorkspacesAsync(rootUser, [], ct: ct);

            _ = result.Should().HaveCount(2);
            _ = result.Should().ContainSingle(x => x.Workspace.Id == ownedWs.Id && x.IsOwner && x.Platforms.Count == 1);
            _ = result.Should().ContainSingle(x => x.Workspace.Id == otherWs.Id && !x.IsOwner && x.Platforms.Count == 1);
        }
    }

    [Fact]
    [Trait("Member", "GetAccessibleWorkspacesAsync")]
    public async Task GetAccessibleWorkspacesAsyncWhenUserHasExplicitDenyOnSecondaryIdentityDeniesAccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WorkspaceAccessService service, InMemoryWorkspaceStore wsStore, InMemoryPermissionStore permStore, ServiceProvider provider, InMemoryEventBus eventBus) = await CreateSutAsync();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid ownerId = Guid.NewGuid();
            Guid modUserId = Guid.NewGuid();
            Workspace ws = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "Streamer Channel", PrimaryPlatformId = "twitch", CreatedAtUtc = DateTimeOffset.UtcNow };
            wsStore.Workspaces[ws.Id] = ws;
            wsStore.Platforms[ws.Id] = [];

            Guid modGroupId = Guid.NewGuid();
            permStore.Groups.Add(new PermissionGroup { Id = modGroupId, WorkspaceId = ws.Id, Name = "Moderators", MappedPlatformDesignation = nameof(PlatformUserDesignations.Moderator) });
            permStore.Grants.Add(new PermissionGrant { Id = Guid.NewGuid(), GroupId = modGroupId, PermissionKey = BuiltInPermissionKeys.UiPanelAccess, State = PermissionState.Allow });
            permStore.Roster.Add(new WorkspacePlatformRoleRoster
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws.Id,
                PlatformId = "twitch",
                PlatformUserId = "tw-mod-1",
                Designations = PlatformUserDesignations.Moderator,
            });
            permStore.Overrides.Add(new UserPermissionOverride
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws.Id,
                PlatformId = "discord",
                PlatformUserId = "dc-mod-1",
                PermissionKey = BuiltInPermissionKeys.UiPanelAccess,
                State = PermissionState.Deny,
            });

            PanelUser modUser = new() { Id = modUserId, DisplayName = "ModUser" };
            List<UserLoginIdentity> identities =
            [
                new UserLoginIdentity { Id = Guid.NewGuid(), PanelUserId = modUserId, ProviderId = "twitch", ProviderUserId = "tw-mod-1", ProviderUsername = "tw_mod" },
                new UserLoginIdentity { Id = Guid.NewGuid(), PanelUserId = modUserId, ProviderId = "discord", ProviderUserId = "dc-mod-1", ProviderUsername = "dc_mod" },
            ];

            IReadOnlyList<AccessibleWorkspace> result = await service.GetAccessibleWorkspacesAsync(modUser, identities, ct: ct);
            _ = result.Should().BeEmpty();
        }
    }

    [Fact]
    [Trait("Member", "GetAccessibleWorkspacesAsync")]
    public async Task GetAccessibleWorkspacesAsyncWhenOidcGroupClaimsMatchMappedGroupGrantsAccessViaSyntheticIdentity()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WorkspaceAccessService service, InMemoryWorkspaceStore wsStore, InMemoryPermissionStore permStore, ServiceProvider provider, InMemoryEventBus eventBus) = await CreateSutAsync();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid ownerId = Guid.NewGuid();
            Guid oidcUserId = Guid.NewGuid();
            Workspace ws = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "OIDC Channel", PrimaryPlatformId = "twitch", CreatedAtUtc = DateTimeOffset.UtcNow };
            wsStore.Workspaces[ws.Id] = ws;
            wsStore.Platforms[ws.Id] = [];

            Guid oidcGroupId = Guid.NewGuid();
            permStore.Groups.Add(new PermissionGroup { Id = oidcGroupId, WorkspaceId = ws.Id, Name = "SSO Staff", MappedOidcGroupClaim = "stream-staff" });
            permStore.Grants.Add(new PermissionGrant { Id = Guid.NewGuid(), GroupId = oidcGroupId, PermissionKey = BuiltInPermissionKeys.UiPanelAccess, State = PermissionState.Allow });

            PanelUser oidcUser = new() { Id = oidcUserId, DisplayName = "OidcUser" };
            List<UserLoginIdentity> identities =
            [
                new UserLoginIdentity { Id = Guid.NewGuid(), PanelUserId = oidcUserId, ProviderId = "oidc:authentik", ProviderUserId = "sub-123", ProviderUsername = "oidc_user" },
            ];

            IReadOnlyList<AccessibleWorkspace> result = await service.GetAccessibleWorkspacesAsync(oidcUser, identities, ["stream-staff"], ct: ct);
            _ = result.Should().ContainSingle().Which.Workspace.Id.Should().Be(ws.Id);
        }
    }

    [Fact]
    [Trait("Member", "GetAccessibleWorkspacesAsync")]
    public async Task GetAccessibleWorkspacesAsyncEvaluatesOidcAndMfaAuthStatusAndCanAccessRequiresReady()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (
            WorkspaceAccessService service,
            InMemoryWorkspaceStore wsStore,
            InMemoryPermissionStore permStore,
            InMemoryInstanceSettingsService settingsService,
            InMemoryOidcProviderStore oidcStore,
            ServiceProvider provider,
            InMemoryEventBus eventBus) = await CreateFullSutAsync();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings = new InstanceSettings
            {
                ExternalOidcLoginMode = ExternalOidcLoginMode.Allowed,
                LoginMfaRequirement = PanelLoginMfaRequirement.Optional,
            };

            Guid ownerId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Workspace readyWs = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "Ready", Slug = "ready", PrimaryPlatformId = "twitch" };
            Workspace flagOidcWs = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "Flag OIDC", Slug = "flag-oidc", PrimaryPlatformId = "twitch", RequireExternalOidcLogin = true };
            Workspace scopedOidcWs = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "Scoped OIDC", Slug = "scoped-oidc", PrimaryPlatformId = "twitch" };
            Workspace mfaWs = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "MFA WS", Slug = "mfa-ws", PrimaryPlatformId = "twitch", LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForAll };

            foreach (Workspace ws in new[] { readyWs, flagOidcWs, scopedOidcWs, mfaWs })
            {
                wsStore.Workspaces[ws.Id] = ws;
                wsStore.Platforms[ws.Id] = [];
                Guid groupId = Guid.NewGuid();
                permStore.Groups.Add(new PermissionGroup { Id = groupId, WorkspaceId = ws.Id, Name = "Mods", MappedPlatformDesignation = nameof(PlatformUserDesignations.Moderator) });
                permStore.Grants.Add(new PermissionGrant { Id = Guid.NewGuid(), GroupId = groupId, PermissionKey = BuiltInPermissionKeys.UiPanelAccess, State = PermissionState.Allow });
                permStore.Roster.Add(new WorkspacePlatformRoleRoster { Id = Guid.NewGuid(), WorkspaceId = ws.Id, PlatformId = "twitch", PlatformUserId = "u-1", Designations = PlatformUserDesignations.Moderator });
            }

            oidcStore.Providers.Add(new ExternalOidcProvider { Id = Guid.NewGuid(), WorkspaceId = null, Slug = "instance-sso", DisplayName = "Instance SSO", IsEnabled = true });
            oidcStore.Providers.Add(new ExternalOidcProvider { Id = Guid.NewGuid(), WorkspaceId = scopedOidcWs.Id, Slug = "corp-okta", DisplayName = "Corp Okta", IsEnabled = true });

            PanelUser modUser = new() { Id = userId, DisplayName = "ModUser" };
            List<UserLoginIdentity> identities =
            [
                new UserLoginIdentity { Id = Guid.NewGuid(), PanelUserId = userId, ProviderId = "twitch", ProviderUserId = "u-1", ProviderUsername = "mod_user" },
            ];

            // Non-OIDC, non-MFA session: readyWs=Ready, flagOidcWs/scopedOidcWs=RequiresOidcReauth, mfaWs=RequiresMfaChallenge.
            IReadOnlyList<AccessibleWorkspace> twitchSession = await service.GetAccessibleWorkspacesAsync(
                modUser,
                identities,
                authProvider: "twitch",
                isMfaVerified: false,
                ct: ct);

            _ = twitchSession.Should().ContainSingle(w => w.Workspace.Id == readyWs.Id && w.AuthStatus == WorkspaceSessionAuthStatus.Ready);
            _ = twitchSession.Should().ContainSingle(w => w.Workspace.Id == flagOidcWs.Id
                && w.AuthStatus == WorkspaceSessionAuthStatus.RequiresOidcReauth
                && w.RequiredOidcProviderSlug == "instance-sso");
            _ = twitchSession.Should().ContainSingle(w => w.Workspace.Id == scopedOidcWs.Id
                && w.AuthStatus == WorkspaceSessionAuthStatus.RequiresOidcReauth
                && w.RequiredOidcProviderSlug == "corp-okta");
            _ = twitchSession.Should().ContainSingle(w => w.Workspace.Id == mfaWs.Id && w.AuthStatus == WorkspaceSessionAuthStatus.RequiresMfaChallenge);

            _ = (await service.CanAccessWorkspaceAsync(readyWs.Id, modUser, identities, authProvider: "twitch", isMfaVerified: false, ct: ct)).Should().BeTrue();
            _ = (await service.CanAccessWorkspaceAsync(flagOidcWs.Id, modUser, identities, authProvider: "twitch", isMfaVerified: false, ct: ct)).Should().BeFalse();
            _ = (await service.CanAccessWorkspaceAsync(mfaWs.Id, modUser, identities, authProvider: "twitch", isMfaVerified: false, ct: ct)).Should().BeFalse();

            // Instance OIDC session satisfies flagOidcWs, NOT scopedOidcWs (requires "corp-okta").
            IReadOnlyList<AccessibleWorkspace> instanceOidcSession = await service.GetAccessibleWorkspacesAsync(
                modUser,
                identities,
                authProvider: "oidc:instance-sso",
                isMfaVerified: true,
                ct: ct);

            _ = instanceOidcSession.Should().ContainSingle(w => w.Workspace.Id == flagOidcWs.Id && w.AuthStatus == WorkspaceSessionAuthStatus.Ready);
            _ = instanceOidcSession.Should().ContainSingle(w => w.Workspace.Id == scopedOidcWs.Id
                && w.AuthStatus == WorkspaceSessionAuthStatus.RequiresOidcReauth
                && w.RequiredOidcProviderSlug == "corp-okta");
            _ = instanceOidcSession.Should().ContainSingle(w => w.Workspace.Id == mfaWs.Id && w.AuthStatus == WorkspaceSessionAuthStatus.Ready);

            // Workspace-scoped OIDC session satisfies scopedOidcWs.
            _ = (await service.CanAccessWorkspaceAsync(scopedOidcWs.Id, modUser, identities, authProvider: "oidc:corp-okta", isMfaVerified: true, ct: ct)).Should().BeTrue();

            // Root user bypasses External OIDC requirement.
            PanelUser rootUser = new() { Id = Guid.NewGuid(), DisplayName = "Root", IsRoot = true };
            _ = (await service.CanAccessWorkspaceAsync(scopedOidcWs.Id, rootUser, [], authProvider: "twitch", isMfaVerified: false, ct: ct)).Should().BeTrue();
        }
    }

    #endregion

    #region CanAccessWorkspaceAsync

    [Fact]
    [Trait("Member", "CanAccessWorkspaceAsync")]
    public async Task CanAccessWorkspaceAsyncWhenUserIsOwnerReturnsTrue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WorkspaceAccessService service, InMemoryWorkspaceStore wsStore, _, ServiceProvider provider, InMemoryEventBus eventBus) = await CreateSutAsync();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid ownerId = Guid.NewGuid();
            Workspace ws = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "Owner Channel", PrimaryPlatformId = "twitch", CreatedAtUtc = DateTimeOffset.UtcNow };
            wsStore.Workspaces[ws.Id] = ws;

            PanelUser owner = new() { Id = ownerId, DisplayName = "Owner" };
            bool canAccess = await service.CanAccessWorkspaceAsync(ws.Id, owner, [], ct: ct);

            _ = canAccess.Should().BeTrue();
        }
    }

    #endregion

    #region HasPermissionInWorkspaceAsync

    [Fact]
    [Trait("Member", "HasPermissionInWorkspaceAsync")]
    public async Task HasPermissionInWorkspaceAsyncWhenSyntheticOidcIdentityExplicitlyDeniedOverridesOidcGroupAllow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (WorkspaceAccessService service, InMemoryWorkspaceStore wsStore, InMemoryPermissionStore permStore, ServiceProvider provider, InMemoryEventBus eventBus) = await CreateSutAsync();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid ownerId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Workspace ws = new() { Id = Guid.NewGuid(), OwnerUserId = ownerId, Name = "Workspace", PrimaryPlatformId = "twitch", CreatedAtUtc = DateTimeOffset.UtcNow };
            wsStore.Workspaces[ws.Id] = ws;

            Guid oidcGroupId = Guid.NewGuid();
            permStore.Groups.Add(new PermissionGroup { Id = oidcGroupId, WorkspaceId = ws.Id, Name = "SSO Editors", MappedOidcGroupClaim = "editors" });
            permStore.Grants.Add(new PermissionGrant { Id = Guid.NewGuid(), GroupId = oidcGroupId, PermissionKey = BuiltInPermissionKeys.UiCommandsEdit, State = PermissionState.Allow });
            permStore.Overrides.Add(new UserPermissionOverride
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws.Id,
                PlatformId = "oidc",
                PlatformUserId = userId.ToString("N"),
                PermissionKey = BuiltInPermissionKeys.UiCommandsEdit,
                State = PermissionState.Deny,
            });

            PanelUser user = new() { Id = userId, DisplayName = "Editor" };
            List<UserLoginIdentity> identities =
            [
                new UserLoginIdentity { Id = Guid.NewGuid(), PanelUserId = userId, ProviderId = "oidc:authentik", ProviderUserId = "sub-999", ProviderUsername = "editor" },
            ];

            bool allowed = await service.HasPermissionInWorkspaceAsync(ws.Id, user, identities, BuiltInPermissionKeys.UiCommandsEdit, ["editors"], ct);
            _ = allowed.Should().BeFalse();
        }
    }

    #endregion

    private sealed class InMemoryWorkspaceStore : IWorkspaceStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];
        public Dictionary<Guid, List<WorkspacePlatform>> Platforms { get; } = [];

        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values.Where(w => w.OwnerUserId == ownerUserId)]);

        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values]);

        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>(this.Platforms.GetValueOrDefault(workspaceId) ?? []);

        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, bool clearClientSecret = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class InMemoryPermissionStore(InMemoryWorkspaceStore workspaceStore) : IPermissionStore
    {
        public List<PermissionGroup> Groups { get; } = [];
        public List<PermissionGrant> Grants { get; } = [];
        public List<UserPermissionOverride> Overrides { get; } = [];
        public List<WorkspacePlatformRoleRoster> Roster { get; } = [];
        public List<PermissionGroupMember> Members { get; } = [];

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this.Groups.Where(g => g.WorkspaceId == workspaceId)]);

        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGrant>>([.. this.Grants.Where(g => g.GroupId == groupId)]);

        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PermissionState.Inherit);

        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            workspaceStore.GetByIdAsync(workspaceId, cancellationToken);

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([]);

        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Guid>>([.. this.Members
                .Where(m => string.Equals(m.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.GroupId)]);

        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserPermissionOverride>>([.. this.Overrides
                .Where(o => o.WorkspaceId == workspaceId
                    && string.Equals(o.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(o.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase))]);

        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Roster.FirstOrDefault(r => r.WorkspaceId == workspaceId
                && string.Equals(r.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase)));

        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetGrantsBatchAsync(Guid groupId, IReadOnlyDictionary<string, PermissionState> grants, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<PermissionGroup> SeedDefaultGlobalGroupAsync(IReadOnlyCollection<string>? enabledDangerousFeatureKeys = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PermissionGroup { Id = Guid.NewGuid(), Name = "Default Workspace Plan" });
        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGroupMember>>([]);
        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class InMemoryInstanceSettingsService : IInstanceSettingsService
    {
        public InstanceSettings Settings { get; set; } = new();

        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged
        {
            add { }
            remove { }
        }

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Settings);

        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default)
        {
            this.Settings = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryOidcProviderStore : IExternalOidcProviderStore
    {
        public List<ExternalOidcProvider> Providers { get; } = [];

        public ValueTask<ExternalOidcProvider?> GetByIdAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Providers.FirstOrDefault(p => p.Id == providerId));

        public ValueTask<ExternalOidcProvider?> GetBySlugAsync(Guid? workspaceId, string slug, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Providers.FirstOrDefault(p => p.WorkspaceId == workspaceId && string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase)));

        public ValueTask<IReadOnlyList<ExternalOidcProvider>> GetProvidersAsync(Guid? workspaceId, bool enabledOnly = false, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<ExternalOidcProvider>>([.. this.Providers
                .Where(p => p.WorkspaceId == workspaceId && (!enabledOnly || p.IsEnabled))]);

        public ValueTask<ExternalOidcProvider> UpsertAsync(ExternalOidcProvider provider, string? plaintextClientSecret = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<(bool Success, string? ClientSecret, CredentialStatus Status)> TryGetClientSecretAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
