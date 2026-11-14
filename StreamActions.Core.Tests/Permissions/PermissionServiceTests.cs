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

namespace StreamActions.Core.Tests.Permissions;

public sealed class PermissionServiceTests
{
    /// <summary>
    /// Fixed workspace identifier used across test cases.
    /// </summary>
    private static readonly Guid _workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Fixed panel user identifier representing the workspace owner.
    /// </summary>
    private static readonly Guid _ownerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Creates a <see cref="PermissionService"/> test harness with an in-memory store, registry, cache, and event bus.
    /// </summary>
    /// <returns>A tuple containing the system under test and its supporting test doubles.</returns>
    private static (PermissionService Service, FakePermissionStore Store, PermissionRegistry Registry, InMemoryEventBus EventBus, ServiceProvider Provider) CreateSut()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        FakePermissionStore store = new();
        store.Workspaces[_workspaceId] = new()
        {
            Id = _workspaceId,
            OwnerUserId = _ownerUserId,
            Name = "Test Workspace",
            PrimaryPlatformId = "twitch",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        PermissionRegistry registry = new();
        InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionService service = new(store, registry, cache, eventBus);

        return (service, store, registry, eventBus, provider);
    }

    #region EvaluateAsync

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenActorIsRootOrGlobalAdminAllowsDespiteTier1AndTier2Denies()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid globalGroupId = Guid.NewGuid();
            store.GlobalGroups[globalGroupId] = new() { Id = globalGroupId, WorkspaceId = null, Name = "Restricted" };
            store.WorkspaceGlobalAssignments.Add(new() { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = globalGroupId });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = globalGroupId, PermissionKey = BuiltInPermissionKeys.FeatureCustomCommands, State = PermissionState.Deny });

            PlatformUserIdentity rootActor = new("twitch", "u1", "root", IsRoot: true);
            PlatformUserIdentity adminActor = new("twitch", "u2", "admin", IsGlobalAdmin: true);

            PermissionEvaluationResult rootResult = await service.EvaluateAsync(_workspaceId, rootActor, BuiltInPermissionKeys.FeatureCustomCommands, TestContext.Current.CancellationToken);
            PermissionEvaluationResult adminResult = await service.EvaluateAsync(_workspaceId, adminActor, BuiltInPermissionKeys.FeatureCustomCommands, TestContext.Current.CancellationToken);

            _ = rootResult.IsAllowed.Should().BeTrue();
            _ = rootResult.EffectiveState.Should().Be(PermissionState.Allow);
            _ = adminResult.IsAllowed.Should().BeTrue();
            _ = adminResult.EffectiveState.Should().Be(PermissionState.Allow);
        }
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenGlobalGroupDeniesBlocksBroadcasterAndOwner()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid globalGroupId = Guid.NewGuid();
            store.GlobalGroups[globalGroupId] = new() { Id = globalGroupId, WorkspaceId = null, Name = "Suspended" };
            store.WorkspaceGlobalAssignments.Add(new() { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = globalGroupId });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = globalGroupId, PermissionKey = BuiltInPermissionKeys.ActionRunCommand, State = PermissionState.Deny });

            PlatformUserIdentity broadcaster = new("twitch", "b1", "streamer", PlatformUserDesignations.Broadcaster, PanelUserId: _ownerUserId);

            PermissionEvaluationResult result = await service.EvaluateAsync(_workspaceId, broadcaster, BuiltInPermissionKeys.ActionRunCommand, TestContext.Current.CancellationToken);

            _ = result.IsAllowed.Should().BeFalse();
            _ = result.EffectiveState.Should().Be(PermissionState.Deny);
            _ = result.DenyingGroupId.Should().Be(globalGroupId);
            _ = result.DenyingGroupName.Should().Be("Suspended");
        }
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenGloballyGatedFeatureNotAllowedInTier1DeniesEvenForBroadcaster()
    {
        (PermissionService service, FakePermissionStore store, PermissionRegistry registry, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            registry.Register(new("plugin.custom.gated", "Plugins", "Gated", "Globally gated plugin permission.", IsGloballyGated: true));
            PlatformUserIdentity broadcaster = new("twitch", "b1", "streamer", PlatformUserDesignations.Broadcaster, PanelUserId: _ownerUserId);

            PermissionEvaluationResult builtInResult = await service.EvaluateAsync(_workspaceId, broadcaster, BuiltInPermissionKeys.FeatureCustomCommands, TestContext.Current.CancellationToken);
            PermissionEvaluationResult customGatedResult = await service.EvaluateAsync(_workspaceId, broadcaster, "plugin.custom.gated", TestContext.Current.CancellationToken);

            _ = builtInResult.IsAllowed.Should().BeFalse();
            _ = builtInResult.EffectiveState.Should().Be(PermissionState.Deny);
            _ = customGatedResult.IsAllowed.Should().BeFalse();
            _ = customGatedResult.EffectiveState.Should().Be(PermissionState.Deny);

            Guid globalGroupId = Guid.NewGuid();
            store.GlobalGroups[globalGroupId] = new() { Id = globalGroupId, WorkspaceId = null, Name = "Standard" };
            store.WorkspaceGlobalAssignments.Add(new() { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = globalGroupId });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = globalGroupId, PermissionKey = BuiltInPermissionKeys.FeatureCustomCommands, State = PermissionState.Allow });

            await service.InvalidateGlobalAsync(TestContext.Current.CancellationToken);
            PermissionEvaluationResult allowedAfterGrant = await service.EvaluateAsync(_workspaceId, broadcaster, BuiltInPermissionKeys.FeatureCustomCommands, TestContext.Current.CancellationToken);

            _ = allowedAfterGrant.IsAllowed.Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenWorkspaceOwnerOrBroadcasterBypassesTier2Denies()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid everyoneGroupId = Guid.NewGuid();
            store.WorkspaceGroups[everyoneGroupId] = new() { Id = everyoneGroupId, WorkspaceId = _workspaceId, Name = "Everyone", MappedPlatformDesignation = "Everyone" };
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = everyoneGroupId, PermissionKey = BuiltInPermissionKeys.UiSettingsManage, State = PermissionState.Deny });

            PlatformUserIdentity broadcaster = new("twitch", "b1", "streamer", PlatformUserDesignations.Broadcaster);
            PlatformUserIdentity owner = new("twitch", "o1", "owner", PlatformUserDesignations.None, PanelUserId: _ownerUserId);

            PermissionEvaluationResult broadcasterResult = await service.EvaluateAsync(_workspaceId, broadcaster, BuiltInPermissionKeys.UiSettingsManage, TestContext.Current.CancellationToken);
            PermissionEvaluationResult ownerResult = await service.EvaluateAsync(_workspaceId, owner, BuiltInPermissionKeys.UiSettingsManage, TestContext.Current.CancellationToken);

            _ = broadcasterResult.IsAllowed.Should().BeTrue();
            _ = ownerResult.IsAllowed.Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenTier2GroupsConflictDenyOverridesAllowAndNoGrantDefaultsToDeny()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid modGroupId = Guid.NewGuid();
            Guid vipGroupId = Guid.NewGuid();
            store.WorkspaceGroups[modGroupId] = new() { Id = modGroupId, WorkspaceId = _workspaceId, Name = "Moderators", MappedPlatformDesignation = "Moderator" };
            store.WorkspaceGroups[vipGroupId] = new() { Id = vipGroupId, WorkspaceId = _workspaceId, Name = "VIPs", MappedPlatformDesignation = "Vip" };

            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = modGroupId, PermissionKey = BuiltInPermissionKeys.ModerationTimeout, State = PermissionState.Allow });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = vipGroupId, PermissionKey = BuiltInPermissionKeys.ModerationTimeout, State = PermissionState.Deny });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = modGroupId, PermissionKey = BuiltInPermissionKeys.ModerationBan, State = PermissionState.Inherit });

            PlatformUserIdentity modOnly = new("twitch", "m1", "moduser", PlatformUserDesignations.Moderator);
            PlatformUserIdentity modAndVip = new("twitch", "mv1", "modvip", PlatformUserDesignations.Moderator | PlatformUserDesignations.Vip);

            PermissionEvaluationResult modTimeout = await service.EvaluateAsync(_workspaceId, modOnly, BuiltInPermissionKeys.ModerationTimeout, TestContext.Current.CancellationToken);
            PermissionEvaluationResult modVipTimeout = await service.EvaluateAsync(_workspaceId, modAndVip, BuiltInPermissionKeys.ModerationTimeout, TestContext.Current.CancellationToken);
            PermissionEvaluationResult modBan = await service.EvaluateAsync(_workspaceId, modOnly, BuiltInPermissionKeys.ModerationBan, TestContext.Current.CancellationToken);

            _ = modTimeout.IsAllowed.Should().BeTrue();
            _ = modTimeout.DenyingGroupId.Should().BeNull();
            _ = modTimeout.DenyingGroupName.Should().BeNull();
            _ = modVipTimeout.IsAllowed.Should().BeFalse();
            _ = modVipTimeout.EffectiveState.Should().Be(PermissionState.Deny);
            _ = modVipTimeout.DenyingGroupId.Should().Be(vipGroupId);
            _ = modVipTimeout.DenyingGroupName.Should().Be("VIPs");
            _ = modBan.IsAllowed.Should().BeFalse();
            _ = modBan.EffectiveState.Should().Be(PermissionState.Inherit);
            _ = modBan.DenyingGroupId.Should().BeNull();
            _ = modBan.DenyingGroupName.Should().BeNull();
        }
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncMergesRoleRosterAndCustomGroupsAndAppliesUserOverrides()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid modGroupId = Guid.NewGuid();
            Guid customGroupId = Guid.NewGuid();
            store.WorkspaceGroups[modGroupId] = new() { Id = modGroupId, WorkspaceId = _workspaceId, Name = "Moderators", MappedPlatformDesignation = "Moderator" };
            store.WorkspaceGroups[customGroupId] = new() { Id = customGroupId, WorkspaceId = _workspaceId, Name = "Trusted" };

            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = modGroupId, PermissionKey = BuiltInPermissionKeys.UiCommandsEdit, State = PermissionState.Allow });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = customGroupId, PermissionKey = BuiltInPermissionKeys.ActionTriggerAlert, State = PermissionState.Allow });

            store.RoleRosters.Add(new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _workspaceId,
                PlatformId = "twitch",
                PlatformUserId = "u100",
                Username = "panelmod",
                Designations = PlatformUserDesignations.Moderator,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            store.GroupMembers.Add(new()
            {
                Id = Guid.NewGuid(),
                GroupId = customGroupId,
                PlatformId = "twitch",
                PlatformUserId = "u100",
                AddedAtUtc = DateTimeOffset.UtcNow,
            });
            store.UserOverrides.Add(new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _workspaceId,
                PlatformId = "twitch",
                PlatformUserId = "u100",
                PermissionKey = BuiltInPermissionKeys.ActionTriggerSoundboard,
                State = PermissionState.Allow,
            });
            store.UserOverrides.Add(new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _workspaceId,
                PlatformId = "twitch",
                PlatformUserId = "u100",
                PermissionKey = BuiltInPermissionKeys.ActionTriggerAlert,
                State = PermissionState.Deny,
            });

            PlatformUserIdentity actor = new("twitch", "u100", "panelmod", PlatformUserDesignations.None);

            bool canEditCommands = await service.IsAllowedAsync(_workspaceId, actor, BuiltInPermissionKeys.UiCommandsEdit, TestContext.Current.CancellationToken);
            bool canSoundboard = await service.IsAllowedAsync(_workspaceId, actor, BuiltInPermissionKeys.ActionTriggerSoundboard, TestContext.Current.CancellationToken);
            bool canAlert = await service.IsAllowedAsync(_workspaceId, actor, BuiltInPermissionKeys.ActionTriggerAlert, TestContext.Current.CancellationToken);

            _ = canEditCommands.Should().BeTrue();
            _ = canSoundboard.Should().BeTrue();
            _ = canAlert.Should().BeFalse();
        }
    }

    #endregion

    #region InvalidateWorkspaceAsync

    [Fact]
    [Trait("Member", "InvalidateWorkspaceAsync")]
    public async Task InvalidateWorkspaceAsyncEvictsCacheAndPublishesInvalidationEvent()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            TaskCompletionSource<PermissionsInvalidatedEvent> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using IDisposable sub = eventBus.Subscribe<PermissionsInvalidatedEvent>((evt, cancellationToken) =>
            {
                _ = received.TrySetResult(evt);
                return ValueTask.CompletedTask;
            });

            Guid everyoneGroupId = Guid.NewGuid();
            store.WorkspaceGroups[everyoneGroupId] = new() { Id = everyoneGroupId, WorkspaceId = _workspaceId, Name = "Everyone", MappedPlatformDesignation = "Everyone" };
            PlatformUserIdentity viewer = new("twitch", "v1", "viewer");

            bool beforeGrant = await service.IsAllowedAsync(_workspaceId, viewer, BuiltInPermissionKeys.ActionRunCommand, TestContext.Current.CancellationToken);
            _ = beforeGrant.Should().BeFalse();

            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = everyoneGroupId, PermissionKey = BuiltInPermissionKeys.ActionRunCommand, State = PermissionState.Allow });

            // Cached value still returns false before invalidation.
            bool cachedBeforeInvalidation = await service.IsAllowedAsync(_workspaceId, viewer, BuiltInPermissionKeys.ActionRunCommand, TestContext.Current.CancellationToken);
            _ = cachedBeforeInvalidation.Should().BeFalse();

            await service.InvalidateWorkspaceAsync(_workspaceId, TestContext.Current.CancellationToken);

            bool afterInvalidation = await service.IsAllowedAsync(_workspaceId, viewer, BuiltInPermissionKeys.ActionRunCommand, TestContext.Current.CancellationToken);
            PermissionsInvalidatedEvent publishedEvent = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            _ = afterInvalidation.Should().BeTrue();
            _ = publishedEvent.WorkspaceId.Should().Be(_workspaceId);
        }
    }

    #endregion

    #region InvalidateGlobalAsync

    [Fact]
    [Trait("Member", "InvalidateGlobalAsync")]
    public async Task InvalidateGlobalAsyncPublishesNullWorkspaceEvent()
    {
        (PermissionService service, _, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            TaskCompletionSource<PermissionsInvalidatedEvent> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using IDisposable sub = eventBus.Subscribe<PermissionsInvalidatedEvent>((evt, cancellationToken) =>
            {
                _ = received.TrySetResult(evt);
                return ValueTask.CompletedTask;
            });

            await service.InvalidateGlobalAsync(TestContext.Current.CancellationToken);

            PermissionsInvalidatedEvent publishedEvent = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            _ = publishedEvent.WorkspaceId.Should().BeNull();
        }
    }

    #endregion

    /// <summary>
    /// In-memory <see cref="IPermissionStore"/> test double for <see cref="PermissionServiceTests"/>.
    /// </summary>
    private sealed class FakePermissionStore : IPermissionStore
    {
        /// <summary>
        /// Gets the in-memory workspaces keyed by identifier.
        /// </summary>
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];

        /// <summary>
        /// Gets the in-memory global permission groups keyed by identifier.
        /// </summary>
        public Dictionary<Guid, PermissionGroup> GlobalGroups { get; } = [];

        /// <summary>
        /// Gets the in-memory workspace permission groups keyed by identifier.
        /// </summary>
        public Dictionary<Guid, PermissionGroup> WorkspaceGroups { get; } = [];

        /// <summary>
        /// Gets the in-memory workspace-to-global-group assignments.
        /// </summary>
        public List<WorkspaceGlobalGroupAssignment> WorkspaceGlobalAssignments { get; } = [];

        /// <summary>
        /// Gets the in-memory permission grants.
        /// </summary>
        public List<PermissionGrant> Grants { get; } = [];

        /// <summary>
        /// Gets the in-memory permission group memberships.
        /// </summary>
        public List<PermissionGroupMember> GroupMembers { get; } = [];

        /// <summary>
        /// Gets the in-memory per-user permission overrides.
        /// </summary>
        public List<UserPermissionOverride> UserOverrides { get; } = [];

        /// <summary>
        /// Gets the in-memory platform role roster entries.
        /// </summary>
        public List<WorkspacePlatformRoleRoster> RoleRosters { get; } = [];

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PermissionGroup> result = workspaceId.HasValue
                ? [.. this.WorkspaceGroups.Values.Where(g => g.WorkspaceId == workspaceId.Value)]
                : [.. this.GlobalGroups.Values];
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGrant>>([.. this.Grants.Where(g => g.GroupId == groupId)]);

        /// <inheritdoc />
        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PermissionState.Inherit);

        /// <inheritdoc />
        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            HashSet<Guid> assignedIds = [.. this.WorkspaceGlobalAssignments.Where(a => a.WorkspaceId == workspaceId).Select(a => a.GlobalGroupId)];
            IReadOnlyList<PermissionGroup> result = [.. this.GlobalGroups.Values.Where(g => assignedIds.Contains(g.Id))];
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default)
        {
            HashSet<Guid> wsGroupIds = [.. this.WorkspaceGroups.Values.Where(g => g.WorkspaceId == workspaceId).Select(g => g.Id)];
            IReadOnlyList<Guid> result = [.. this.GroupMembers
                .Where(m => wsGroupIds.Contains(m.GroupId)
                    && string.Equals(m.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.GroupId)];
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserPermissionOverride>>([.. this.UserOverrides.Where(o =>
                o.WorkspaceId == workspaceId
                && string.Equals(o.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(o.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase))]);

        /// <inheritdoc />
        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.RoleRosters.FirstOrDefault(r =>
                r.WorkspaceId == workspaceId
                && string.Equals(r.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase)));

        /// <inheritdoc />
        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(group);

        /// <inheritdoc />
        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroupMember>>([.. this.GroupMembers.Where(m => m.GroupId == groupId)]);

        /// <inheritdoc />
        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
