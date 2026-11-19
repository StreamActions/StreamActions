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

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenOidcGroupClaimMatchesGroupAllowsCaseInsensitivelyAndTrimmed()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid oidcGroupId = Guid.NewGuid();
            store.WorkspaceGroups[oidcGroupId] = new()
            {
                Id = oidcGroupId,
                WorkspaceId = _workspaceId,
                Name = "OIDC Staff",
                MappedOidcGroupClaim = "  stream-staff  ",
            };
            store.Grants.Add(new()
            {
                Id = Guid.NewGuid(),
                GroupId = oidcGroupId,
                PermissionKey = BuiltInPermissionKeys.UiPanelAccess,
                State = PermissionState.Allow,
            });

            PlatformUserIdentity matchingActor = new(
                "oidc",
                "sub-123",
                "alice",
                OidcGroupClaims: ["viewers", "  STREAM-STAFF "]);

            PlatformUserIdentity nonMatchingActor = new(
                "oidc",
                "sub-456",
                "bob",
                OidcGroupClaims: ["viewers", "other-group"]);

            PermissionEvaluationResult matched = await service.EvaluateAsync(
                _workspaceId,
                matchingActor,
                BuiltInPermissionKeys.UiPanelAccess,
                TestContext.Current.CancellationToken);

            PermissionEvaluationResult unmatched = await service.EvaluateAsync(
                _workspaceId,
                nonMatchingActor,
                BuiltInPermissionKeys.UiPanelAccess,
                TestContext.Current.CancellationToken);

            _ = matched.IsAllowed.Should().BeTrue();
            _ = matched.EffectiveState.Should().Be(PermissionState.Allow);
            _ = unmatched.IsAllowed.Should().BeFalse();
            _ = unmatched.EffectiveState.Should().Be(PermissionState.Inherit);
        }
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenOidcGroupClaimGroupExplicitlyDeniesDeniesEvenIfAnotherGroupAllows()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid modGroupId = Guid.NewGuid();
            store.WorkspaceGroups[modGroupId] = new()
            {
                Id = modGroupId,
                WorkspaceId = _workspaceId,
                Name = "Moderators",
                MappedPlatformDesignation = "Moderator",
            };
            store.Grants.Add(new()
            {
                Id = Guid.NewGuid(),
                GroupId = modGroupId,
                PermissionKey = BuiltInPermissionKeys.UiSecretsView,
                State = PermissionState.Allow,
            });

            Guid restrictedOidcGroupId = Guid.NewGuid();
            store.WorkspaceGroups[restrictedOidcGroupId] = new()
            {
                Id = restrictedOidcGroupId,
                WorkspaceId = _workspaceId,
                Name = "Restricted OIDC Contractors",
                MappedOidcGroupClaim = "contractors",
            };
            store.Grants.Add(new()
            {
                Id = Guid.NewGuid(),
                GroupId = restrictedOidcGroupId,
                PermissionKey = BuiltInPermissionKeys.UiSecretsView,
                State = PermissionState.Deny,
            });

            PlatformUserIdentity actor = new(
                "twitch",
                "u-99",
                "contractor_mod",
                Designations: PlatformUserDesignations.Moderator,
                OidcGroupClaims: ["Contractors"]);

            PermissionEvaluationResult result = await service.EvaluateAsync(
                _workspaceId,
                actor,
                BuiltInPermissionKeys.UiSecretsView,
                TestContext.Current.CancellationToken);

            _ = result.IsAllowed.Should().BeFalse();
            _ = result.EffectiveState.Should().Be(PermissionState.Deny);
            _ = result.DenyingGroupId.Should().Be(restrictedOidcGroupId);
            _ = result.DenyingGroupName.Should().Be("Restricted OIDC Contractors");
        }
    }

    #endregion

    #region ExplainWorkspacePermissionsAsync

    [Fact]
    [Trait("Member", "ExplainWorkspacePermissionsAsync")]
    public async Task ExplainWorkspacePermissionsAsyncOmitsInheritGroupsIncludesDefaultStateAndRedactsGlobalGroupNamesForNonRoot()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid defaultPlanId = Guid.NewGuid();
            Guid inheritPlanId = Guid.NewGuid();
            Guid restrictedPlanId = Guid.NewGuid();

            store.GlobalGroups[defaultPlanId] = new() { Id = defaultPlanId, WorkspaceId = null, Name = BuiltInPermissionKeys.DefaultWorkspacePlanGroupName };
            store.GlobalGroups[inheritPlanId] = new() { Id = inheritPlanId, WorkspaceId = null, Name = "Silent Inherit Plan" };
            store.GlobalGroups[restrictedPlanId] = new() { Id = restrictedPlanId, WorkspaceId = null, Name = "Secret Enterprise Tier" };

            store.WorkspaceGlobalAssignments.Add(new() { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = defaultPlanId });
            store.WorkspaceGlobalAssignments.Add(new() { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = inheritPlanId });
            store.WorkspaceGlobalAssignments.Add(new() { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = restrictedPlanId });

            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = defaultPlanId, PermissionKey = BuiltInPermissionKeys.FeatureCustomCommands, State = PermissionState.Allow });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = inheritPlanId, PermissionKey = BuiltInPermissionKeys.FeatureCustomCommands, State = PermissionState.Inherit });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = restrictedPlanId, PermissionKey = BuiltInPermissionKeys.FeatureQuotes, State = PermissionState.Deny });

            IReadOnlyList<PermissionAuditBreakdown> rootBreakdowns = await service.ExplainWorkspacePermissionsAsync(
                _workspaceId,
                viewerIsRootOrGlobalAdmin: true,
                TestContext.Current.CancellationToken);

            PermissionAuditBreakdown rootCommands = rootBreakdowns.Single(static b => b.Descriptor.Key == BuiltInPermissionKeys.FeatureCustomCommands);
            _ = rootCommands.IsAllowed.Should().BeTrue();
            _ = rootCommands.EffectiveState.Should().Be(PermissionState.Allow);
            _ = rootCommands.DeterminedByTier.Should().Be(PermissionDeterminingTier.GlobalTier1);
            _ = rootCommands.DefaultStateDisplay.Should().Be("Implicit Deny (Overridable Deny)");
            _ = rootCommands.Contributions.Should().NotContain(static c => c.SourceName == "Silent Inherit Plan");
            _ = rootCommands.Contributions.Should().Contain(c =>
                c.SourceName == BuiltInPermissionKeys.DefaultWorkspacePlanGroupName
                && c.State == PermissionState.Allow
                && c.IsGlobalTier
                && c.GroupId == defaultPlanId);
            _ = rootCommands.Contributions.Should().ContainSingle(static c =>
                c.IsDefaultState
                && c.SourceName == "Default State"
                && c.DisplayState == "Implicit Deny (Overridable Deny)");

            PermissionAuditBreakdown rootDangerous = rootBreakdowns.Single(static b => b.Descriptor.Key == BuiltInPermissionKeys.FeatureScriptingClearScript);
            _ = rootDangerous.IsAllowed.Should().BeFalse();
            _ = rootDangerous.DeterminedByTier.Should().Be(PermissionDeterminingTier.DefaultState);
            _ = rootDangerous.Contributions.Should().ContainSingle().Which.IsDefaultState.Should().BeTrue();

            PermissionAuditBreakdown rootRunCmd = rootBreakdowns.Single(static b => b.Descriptor.Key == BuiltInPermissionKeys.ActionRunCommand);
            _ = rootRunCmd.DefaultState.Should().Be(PermissionState.Allow);
            _ = rootRunCmd.DefaultStateDisplay.Should().Be("Default State: Allow");
            _ = rootRunCmd.Contributions.Should().ContainSingle(static c =>
                c.IsDefaultState && c.DisplayState == "Default State: Allow");

            PermissionAuditBreakdown nonRootQuotes = await service.ExplainWorkspacePermissionAsync(
                _workspaceId,
                BuiltInPermissionKeys.FeatureQuotes,
                viewerIsRootOrGlobalAdmin: false,
                TestContext.Current.CancellationToken);

            _ = nonRootQuotes.IsAllowed.Should().BeFalse();
            _ = nonRootQuotes.DeterminedByTier.Should().Be(PermissionDeterminingTier.GlobalTier1);
            _ = nonRootQuotes.SummaryReason.Should().NotContain("Secret Enterprise Tier");
            _ = nonRootQuotes.Contributions.Should().Contain(static c =>
                c.IsGlobalTier
                && c.SourceName == "Global Permissions"
                && c.State == PermissionState.Deny
                && c.GroupId == null);
            _ = nonRootQuotes.Contributions.Should().NotContain(static c => c.SourceName == "Secret Enterprise Tier");
        }
    }

    #endregion

    #region ExplainUserPermissionsAsync

    [Fact]
    [Trait("Member", "ExplainUserPermissionsAsync")]
    public async Task ExplainUserPermissionsAsyncAttributesGlobalVsWorkspaceVsDefaultTiersOmitsInheritAndRedactsGlobalNamesForWorkspaceAdmin()
    {
        (PermissionService service, FakePermissionStore store, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            Guid globalPlanId = Guid.NewGuid();
            Guid modGroupId = Guid.NewGuid();
            Guid vipGroupId = Guid.NewGuid();
            Guid customDenyGroupId = Guid.NewGuid();

            store.GlobalGroups[globalPlanId] = new() { Id = globalPlanId, WorkspaceId = null, Name = "Internal VIP Global Plan" };
            store.WorkspaceGlobalAssignments.Add(new() { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = globalPlanId });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = globalPlanId, PermissionKey = BuiltInPermissionKeys.ActionTriggerAlert, State = PermissionState.Deny });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = globalPlanId, PermissionKey = BuiltInPermissionKeys.FeatureCustomCommands, State = PermissionState.Allow });

            store.WorkspaceGroups[modGroupId] = new() { Id = modGroupId, WorkspaceId = _workspaceId, Name = "Moderator", MappedPlatformDesignation = "moderator" };
            store.WorkspaceGroups[vipGroupId] = new() { Id = vipGroupId, WorkspaceId = _workspaceId, Name = "VIP", MappedPlatformDesignation = "vip" };
            store.WorkspaceGroups[customDenyGroupId] = new() { Id = customDenyGroupId, WorkspaceId = _workspaceId, Name = "NoBanMods" };

            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = modGroupId, PermissionKey = BuiltInPermissionKeys.ModerationTimeout, State = PermissionState.Allow });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = vipGroupId, PermissionKey = BuiltInPermissionKeys.ModerationTimeout, State = PermissionState.Inherit });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = modGroupId, PermissionKey = BuiltInPermissionKeys.ModerationBan, State = PermissionState.Allow });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = customDenyGroupId, PermissionKey = BuiltInPermissionKeys.ModerationBan, State = PermissionState.Deny });
            store.Grants.Add(new() { Id = Guid.NewGuid(), GroupId = modGroupId, PermissionKey = BuiltInPermissionKeys.ActionTriggerAlert, State = PermissionState.Allow });

            store.GroupMembers.Add(new()
            {
                Id = Guid.NewGuid(),
                GroupId = customDenyGroupId,
                PlatformId = "twitch",
                PlatformUserId = "mod1",
                AddedAtUtc = DateTimeOffset.UtcNow,
            });

            PlatformUserIdentity modActor = new(
                "twitch",
                "mod1",
                "mod_one",
                Designations: PlatformUserDesignations.Moderator | PlatformUserDesignations.Vip);

            IReadOnlyList<PermissionAuditBreakdown> wsAdminBreakdowns = await service.ExplainUserPermissionsAsync(
                _workspaceId,
                modActor,
                viewerIsRootOrGlobalAdmin: false,
                TestContext.Current.CancellationToken);

            // 1. Allowed by Workspace Tier 2 (Moderator Allow, VIP Inherit omitted).
            PermissionAuditBreakdown timeoutBreakdown = wsAdminBreakdowns.Single(static b => b.Descriptor.Key == BuiltInPermissionKeys.ModerationTimeout);
            _ = timeoutBreakdown.IsAllowed.Should().BeTrue();
            _ = timeoutBreakdown.DeterminedByTier.Should().Be(PermissionDeterminingTier.WorkspaceTier2);
            _ = timeoutBreakdown.Contributions.Should().Contain(c => c.SourceName == "Moderator" && c.State == PermissionState.Allow && !c.IsGlobalTier && c.GroupId == modGroupId);
            _ = timeoutBreakdown.Contributions.Should().NotContain(static c => c.SourceName == "VIP");
            _ = timeoutBreakdown.Contributions.Should().ContainSingle(static c => c.IsDefaultState && c.DisplayState == "Implicit Deny (Overridable Deny)");

            // 2. Denied by Workspace Tier 2 (Custom group Deny overrides Moderator Allow).
            PermissionAuditBreakdown banBreakdown = wsAdminBreakdowns.Single(static b => b.Descriptor.Key == BuiltInPermissionKeys.ModerationBan);
            _ = banBreakdown.IsAllowed.Should().BeFalse();
            _ = banBreakdown.EffectiveState.Should().Be(PermissionState.Deny);
            _ = banBreakdown.DeterminedByTier.Should().Be(PermissionDeterminingTier.WorkspaceTier2);
            _ = banBreakdown.Contributions.Should().Contain(static c => c.SourceName == "Moderator" && c.State == PermissionState.Allow);
            _ = banBreakdown.Contributions.Should().Contain(static c => c.SourceName == "NoBanMods" && c.State == PermissionState.Deny);

            // 3. Denied by Global Tier 1 (Global group name redacted to "Global Permissions" for workspace admin).
            PermissionAuditBreakdown alertBreakdown = wsAdminBreakdowns.Single(static b => b.Descriptor.Key == BuiltInPermissionKeys.ActionTriggerAlert);
            _ = alertBreakdown.IsAllowed.Should().BeFalse();
            _ = alertBreakdown.EffectiveState.Should().Be(PermissionState.Deny);
            _ = alertBreakdown.DeterminedByTier.Should().Be(PermissionDeterminingTier.GlobalTier1);
            _ = alertBreakdown.SummaryReason.Should().NotContain("Internal VIP Global Plan");
            _ = alertBreakdown.Contributions.Should().Contain(static c =>
                c.IsGlobalTier
                && c.SourceName == "Global Permissions"
                && c.State == PermissionState.Deny
                && c.GroupId == null);
            _ = alertBreakdown.Contributions.Should().NotContain(static c => c.SourceName == "Internal VIP Global Plan");

            // 4. Denied by Default State (no group grants ui.secrets.manage to Moderator).
            PermissionAuditBreakdown secretsBreakdown = wsAdminBreakdowns.Single(static b => b.Descriptor.Key == BuiltInPermissionKeys.UiSecretsManage);
            _ = secretsBreakdown.IsAllowed.Should().BeFalse();
            _ = secretsBreakdown.EffectiveState.Should().Be(PermissionState.Inherit);
            _ = secretsBreakdown.DeterminedByTier.Should().Be(PermissionDeterminingTier.DefaultState);
            _ = secretsBreakdown.Contributions.Should().ContainSingle(static c =>
                c.IsDefaultState && c.DisplayState == "Implicit Deny (Overridable Deny)");

            // 5. Root viewer sees the unredacted Global Group name and GroupId.
            PermissionAuditBreakdown rootAlertBreakdown = await service.ExplainUserPermissionAsync(
                _workspaceId,
                modActor,
                BuiltInPermissionKeys.ActionTriggerAlert,
                viewerIsRootOrGlobalAdmin: true,
                TestContext.Current.CancellationToken);
            _ = rootAlertBreakdown.SummaryReason.Should().Contain("Internal VIP Global Plan");
            _ = rootAlertBreakdown.Contributions.Should().Contain(c =>
                c.IsGlobalTier
                && c.SourceName == "Internal VIP Global Plan"
                && c.GroupId == globalPlanId);
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
        public ValueTask SetGrantsBatchAsync(Guid groupId, IReadOnlyDictionary<string, PermissionState> grants, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask<PermissionGroup> SeedDefaultGlobalGroupAsync(IReadOnlyCollection<string>? enabledDangerousFeatureKeys = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PermissionGroup { Id = Guid.NewGuid(), Name = BuiltInPermissionKeys.DefaultWorkspacePlanGroupName });

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
