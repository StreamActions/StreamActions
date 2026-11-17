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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MfaPolicyService"/>.
/// </summary>
public sealed class MfaPolicyServiceTests
{
    #region ComputeEffectivePolicy

    /// <summary>
    /// Verifies that instance-level method disabling cannot be overridden by a workspace and yields passkeys-only when passkeys remain allowed.
    /// </summary>
    [Fact]
    [Trait("Member", "ComputeEffectivePolicy")]
    public void ComputeEffectivePolicyWhenInstanceDisablesTotpCannotBeOverriddenByWorkspace()
    {
        (MfaPolicyService service, _, _, _, _) = CreateSut();
        InstanceSettings instanceSettings = new()
        {
            MfaPasskeyAvailability = MfaMethodAvailability.Allowed,
            MfaTotpAvailability = MfaMethodAvailability.Disabled,
            LoginMfaRequirement = PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy = StepUpSudoPolicy.Optional,
        };
        Workspace workspace = new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Name = "Channel",
            PrimaryPlatformId = "twitch",
            MfaPasskeyAvailability = MfaMethodAvailability.Allowed,
            MfaTotpAvailability = MfaMethodAvailability.Required,
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForModerators,
            StepUpSudoPolicy = StepUpSudoPolicy.Required,
        };

        EffectiveMfaPolicy effective = service.ComputeEffectivePolicy(instanceSettings, workspace);

        _ = effective.PasskeyAvailability.Should().Be(MfaMethodAvailability.Allowed);
        _ = effective.TotpAvailability.Should().Be(MfaMethodAvailability.Disabled);
        _ = effective.IsPasskeysOnly.Should().BeTrue();
        _ = effective.IsPasskeyAllowed.Should().BeTrue();
        _ = effective.IsTotpAllowed.Should().BeFalse();
        _ = effective.IsMfaDisabled.Should().BeFalse();
        _ = effective.LoginMfaRequirement.Should().Be(PanelLoginMfaRequirement.RequiredForModerators);
        _ = effective.StepUpSudoPolicy.Should().Be(StepUpSudoPolicy.Required);
        _ = effective.IsStepUpRequired.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that disabling both passkeys and TOTP marks MFA as disabled and strictest-wins applies to <see cref="StepUpSudoPolicy"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "ComputeEffectivePolicy")]
    public void ComputeEffectivePolicyWhenBothMethodsDisabledMarksMfaDisabledAndPreservesStepUpPolicy()
    {
        (MfaPolicyService service, _, _, _, _) = CreateSut();
        InstanceSettings instanceSettings = new()
        {
            MfaPasskeyAvailability = MfaMethodAvailability.Disabled,
            MfaTotpAvailability = MfaMethodAvailability.Allowed,
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForAll,
            StepUpSudoPolicy = StepUpSudoPolicy.Required,
        };
        Workspace workspace = new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Name = "Channel",
            PrimaryPlatformId = "twitch",
            MfaPasskeyAvailability = MfaMethodAvailability.Required,
            MfaTotpAvailability = MfaMethodAvailability.Disabled,
            LoginMfaRequirement = PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy = StepUpSudoPolicy.Optional,
        };

        EffectiveMfaPolicy effective = service.ComputeEffectivePolicy(instanceSettings, workspace);

        _ = effective.IsMfaDisabled.Should().BeTrue();
        _ = effective.IsPasskeysOnly.Should().BeFalse();
        _ = effective.LoginMfaRequirement.Should().Be(PanelLoginMfaRequirement.RequiredForAll);
        _ = effective.StepUpSudoPolicy.Should().Be(StepUpSudoPolicy.Required);
        _ = effective.IsStepUpRequired.Should().BeTrue();
    }

    #endregion

    #region GetEffectivePolicyForUserAsync

    /// <summary>
    /// Verifies that <see cref="MfaPolicyService.GetEffectivePolicyForUserAsync"/> merges the strictest policy across all accessible workspaces.
    /// </summary>
    [Fact]
    [Trait("Member", "GetEffectivePolicyForUserAsync")]
    public async Task GetEffectivePolicyForUserAsyncMergesStrictestAccessibleWorkspacePolicies()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (MfaPolicyService service, StubInstanceSettingsService settings, _, _, StubWorkspaceAccessService accessService) = CreateSut();
        settings.Settings = new InstanceSettings
        {
            MfaPasskeyAvailability = MfaMethodAvailability.Allowed,
            MfaTotpAvailability = MfaMethodAvailability.Allowed,
            LoginMfaRequirement = PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy = StepUpSudoPolicy.Optional,
        };

        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "mod" };
        Workspace ws1 = new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Name = "WS1",
            PrimaryPlatformId = "twitch",
            MfaTotpAvailability = MfaMethodAvailability.Disabled,
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForModerators,
            StepUpSudoPolicy = StepUpSudoPolicy.Optional,
        };
        Workspace ws2 = new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Name = "WS2",
            PrimaryPlatformId = "twitch",
            MfaTotpAvailability = MfaMethodAvailability.Allowed,
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForRootAndStreamers,
            StepUpSudoPolicy = StepUpSudoPolicy.Required,
        };

        accessService.AccessibleWorkspaces =
        [
            new AccessibleWorkspace(ws1, false, []),
            new AccessibleWorkspace(ws2, false, []),
        ];

        EffectiveMfaPolicy effective = await service.GetEffectivePolicyForUserAsync(user, [], cancellationToken: ct);

        _ = effective.IsPasskeysOnly.Should().BeTrue();
        _ = effective.LoginMfaRequirement.Should().Be(PanelLoginMfaRequirement.RequiredForModerators);
        _ = effective.IsStepUpRequired.Should().BeTrue();
    }

    #endregion

    #region IsLoginMfaRequiredAsync

    /// <summary>
    /// Verifies <see cref="PanelLoginMfaRequirement.RequiredForRootAndStreamers"/> across root, global admin, workspace owner, and non-owner roles.
    /// </summary>
    [Fact]
    [Trait("Member", "IsLoginMfaRequiredAsync")]
    public async Task IsLoginMfaRequiredAsyncWhenRequiredForRootAndStreamersAppliesToRootAdminsAndOwnersOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (MfaPolicyService service, StubInstanceSettingsService settings, StubWorkspaceStore workspaceStore, _, StubWorkspaceAccessService accessService) = CreateSut();
        settings.Settings = new InstanceSettings
        {
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForRootAndStreamers,
        };

        PanelUser rootUser = new() { Id = Guid.NewGuid(), DisplayName = "root", IsRoot = true };
        PanelUser adminUser = new() { Id = Guid.NewGuid(), DisplayName = "admin", IsGlobalAdmin = true };
        PanelUser ownerUser = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        PanelUser viewerUser = new() { Id = Guid.NewGuid(), DisplayName = "viewer" };

        Workspace ownedWs = new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUser.Id,
            Name = "Streamer WS",
            PrimaryPlatformId = "twitch",
        };
        workspaceStore.Workspaces[ownedWs.Id] = ownedWs;

        accessService.AccessibleWorkspaces = [new AccessibleWorkspace(ownedWs, true, [])];
        bool ownerRequired = await service.IsLoginMfaRequiredAsync(ownerUser, [], cancellationToken: ct);

        accessService.AccessibleWorkspaces = [new AccessibleWorkspace(ownedWs, false, [])];
        bool viewerRequired = await service.IsLoginMfaRequiredAsync(viewerUser, [], cancellationToken: ct);

        accessService.AccessibleWorkspaces = [];
        bool rootRequired = await service.IsLoginMfaRequiredAsync(rootUser, [], cancellationToken: ct);
        bool adminRequired = await service.IsLoginMfaRequiredAsync(adminUser, [], cancellationToken: ct);

        _ = rootRequired.Should().BeTrue();
        _ = adminRequired.Should().BeTrue();
        _ = ownerRequired.Should().BeTrue();
        _ = viewerRequired.Should().BeFalse();
    }

    /// <summary>
    /// Verifies <see cref="PanelLoginMfaRequirement.RequiredForModerators"/> triggers for both role-roster moderators and mapped-group moderators.
    /// </summary>
    [Fact]
    [Trait("Member", "IsLoginMfaRequiredAsync")]
    public async Task IsLoginMfaRequiredAsyncWhenRequiredForModeratorsChecksRosterAndMappedPermissionGroups()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (MfaPolicyService service, StubInstanceSettingsService settings, StubWorkspaceStore workspaceStore, StubPermissionStore permissionStore, StubWorkspaceAccessService accessService) = CreateSut();
        settings.Settings = new InstanceSettings
        {
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForModerators,
        };

        Guid workspaceId = Guid.NewGuid();
        Workspace ws = new()
        {
            Id = workspaceId,
            OwnerUserId = Guid.NewGuid(),
            Name = "Channel",
            PrimaryPlatformId = "twitch",
        };
        workspaceStore.Workspaces[workspaceId] = ws;
        accessService.AccessibleWorkspaces = [new AccessibleWorkspace(ws, false, [])];

        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "mod" };
        UserLoginIdentity identity = new()
        {
            Id = Guid.NewGuid(),
            PanelUserId = user.Id,
            ProviderId = "twitch",
            ProviderUserId = "mod-1",
            ProviderUsername = "moduser",
        };

        // Case 1: regular user -> false
        _ = (await service.IsLoginMfaRequiredAsync(user, [identity], cancellationToken: ct)).Should().BeFalse();

        // Case 2: roster moderator -> true
        permissionStore.Roster[(workspaceId, "twitch", "mod-1")] = new WorkspacePlatformRoleRoster
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            PlatformId = "twitch",
            PlatformUserId = "mod-1",
            Designations = PlatformUserDesignations.Moderator,
        };
        _ = (await service.IsLoginMfaRequiredAsync(user, [identity], cancellationToken: ct)).Should().BeTrue();

        // Case 3: no roster flag, but member of permission group mapped to Moderator -> true
        permissionStore.Roster.Clear();
        Guid modGroupId = Guid.NewGuid();
        permissionStore.Groups[workspaceId] =
        [
            new PermissionGroup
            {
                Id = modGroupId,
                WorkspaceId = workspaceId,
                Name = "Moderators",
                MappedPlatformDesignation = nameof(PlatformUserDesignations.Moderator),
            },
        ];
        permissionStore.UserGroupIds[(workspaceId, "twitch", "mod-1")] = [modGroupId];
        _ = (await service.IsLoginMfaRequiredAsync(user, [identity], cancellationToken: ct)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies <see cref="PanelLoginMfaRequirement.RequiredForAll"/> applies to every user unless all MFA methods are disabled.
    /// </summary>
    [Fact]
    [Trait("Member", "IsLoginMfaRequiredAsync")]
    public async Task IsLoginMfaRequiredAsyncWhenRequiredForAllRequiresMfaForAnyUserUnlessDisabled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (MfaPolicyService service, StubInstanceSettingsService settings, _, _, _) = CreateSut();
        PanelUser viewer = new() { Id = Guid.NewGuid(), DisplayName = "viewer" };

        settings.Settings = new InstanceSettings
        {
            MfaPasskeyAvailability = MfaMethodAvailability.Allowed,
            MfaTotpAvailability = MfaMethodAvailability.Allowed,
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForAll,
        };
        _ = (await service.IsLoginMfaRequiredAsync(viewer, [], cancellationToken: ct)).Should().BeTrue();

        settings.Settings = new InstanceSettings
        {
            MfaPasskeyAvailability = MfaMethodAvailability.Disabled,
            MfaTotpAvailability = MfaMethodAvailability.Disabled,
            LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForAll,
        };
        _ = (await service.IsLoginMfaRequiredAsync(viewer, [], cancellationToken: ct)).Should().BeFalse();
    }

    #endregion

    /// <summary>
    /// Creates an isolated <see cref="MfaPolicyService"/> instance and its test stubs.
    /// </summary>
    /// <returns>The service under test and its stub dependencies.</returns>
    private static (
        MfaPolicyService Service,
        StubInstanceSettingsService SettingsService,
        StubWorkspaceStore WorkspaceStore,
        StubPermissionStore PermissionStore,
        StubWorkspaceAccessService AccessService) CreateSut()
    {
        StubInstanceSettingsService settingsService = new();
        StubWorkspaceStore workspaceStore = new();
        StubPermissionStore permissionStore = new();
        StubWorkspaceAccessService accessService = new();
        MfaPolicyService service = new(settingsService, workspaceStore, permissionStore, accessService);
        return (service, settingsService, workspaceStore, permissionStore, accessService);
    }

    /// <summary>
    /// In-memory stub for <see cref="IInstanceSettingsService"/>.
    /// </summary>
    private sealed class StubInstanceSettingsService : IInstanceSettingsService
    {
        /// <inheritdoc />
        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Gets or sets the settings returned by <see cref="GetAsync"/>.
        /// </summary>
        public InstanceSettings Settings { get; set; } = new();

        /// <inheritdoc />
        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Settings);

        /// <inheritdoc />
        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default)
        {
            this.Settings = settings;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// In-memory stub for <see cref="IWorkspaceStore"/>.
    /// </summary>
    private sealed class StubWorkspaceStore : IWorkspaceStore
    {
        /// <summary>
        /// Gets the dictionary of workspaces keyed by identifier.
        /// </summary>
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];

        /// <inheritdoc />
        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values.Where(w => w.OwnerUserId == ownerUserId)]);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values]);

        /// <inheritdoc />
        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// In-memory stub for <see cref="IPermissionStore"/>.
    /// </summary>
    private sealed class StubPermissionStore : IPermissionStore
    {
        /// <summary>
        /// Gets the roster entries keyed by workspace, platform, and platform user ID.
        /// </summary>
        public Dictionary<(Guid WorkspaceId, string PlatformId, string PlatformUserId), WorkspacePlatformRoleRoster> Roster { get; } = [];

        /// <summary>
        /// Gets the user group memberships keyed by workspace, platform, and platform user ID.
        /// </summary>
        public Dictionary<(Guid WorkspaceId, string PlatformId, string PlatformUserId), List<Guid>> UserGroupIds { get; } = [];

        /// <summary>
        /// Gets the permission groups keyed by workspace ID.
        /// </summary>
        public Dictionary<Guid, List<PermissionGroup>> Groups { get; } = [];

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroup>>(
                workspaceId.HasValue && this.Groups.TryGetValue(workspaceId.Value, out List<PermissionGroup>? list) ? list : []);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Guid>>(
                this.UserGroupIds.TryGetValue((workspaceId, platformId, platformUserId), out List<Guid>? ids) ? ids : []);

        /// <inheritdoc />
        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Roster.GetValueOrDefault((workspaceId, platformId, platformUserId)));

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// In-memory stub for <see cref="IWorkspaceAccessService"/>.
    /// </summary>
    private sealed class StubWorkspaceAccessService : IWorkspaceAccessService
    {
        /// <summary>
        /// Gets or sets the accessible workspaces returned by <see cref="GetAccessibleWorkspacesAsync"/>.
        /// </summary>
        public IReadOnlyList<AccessibleWorkspace> AccessibleWorkspaces { get; set; } = [];

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<AccessibleWorkspace>> GetAccessibleWorkspacesAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            CancellationToken ct = default) =>
            ValueTask.FromResult(this.AccessibleWorkspaces);

        /// <inheritdoc />
        public ValueTask<bool> CanAccessWorkspaceAsync(
            Guid workspaceId,
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> HasPermissionInWorkspaceAsync(
            Guid workspaceId,
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            string permissionKey,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
