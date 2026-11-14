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
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class PermissionStoreTests
{
    #region SeedBuiltInWorkspaceGroupsAsync
    [Fact]
    [Trait("Member", "SeedBuiltInWorkspaceGroupsAsync")]
    public async Task SeedBuiltInWorkspaceGroupsAsync_WhenCalledTwice_SeedsSevenBuiltInGroupsIdempotently()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        List<PermissionsInvalidatedEvent> events = [];
        using IDisposable sub = eventBus.Subscribe<PermissionsInvalidatedEvent>((evt, _) =>
        {
            events.Add(evt);
            return ValueTask.CompletedTask;
        });

        PermissionStore store = new(factory, TimeProvider.System, eventBus);

        await store.SeedBuiltInWorkspaceGroupsAsync(workspaceId, ct);
        await store.SeedBuiltInWorkspaceGroupsAsync(workspaceId, ct);

        IReadOnlyList<PermissionGroup> groups = await store.GetGroupsAsync(workspaceId, ct);
        _ = groups.Should().HaveCount(7);
        _ = groups.Select(static g => (g.Name, g.MappedPlatformDesignation)).Should().BeEquivalentTo(
        [
            ("Broadcaster", "broadcaster"),
            ("Moderator", "moderator"),
            ("VIP", "vip"),
            ("Subscriber", "subscriber"),
            ("Regular", "regular"),
            ("Follower", "follower"),
            ("Everyone", "everyone"),
        ]);
        _ = events.Should().ContainSingle(e => e.WorkspaceId == workspaceId);
    }
    #endregion

    #region UpsertGroupAsync
    [Fact]
    [Trait("Member", "UpsertGroupAsync")]
    public async Task UpsertGroupAsync_CreateUpdateDelete_PersistsAndPublishesInvalidation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        List<PermissionsInvalidatedEvent> events = [];
        using IDisposable sub = eventBus.Subscribe<PermissionsInvalidatedEvent>((evt, _) =>
        {
            events.Add(evt);
            return ValueTask.CompletedTask;
        });

        PermissionStore store = new(factory, TimeProvider.System, eventBus);

        PermissionGroup created = await store.UpsertGroupAsync(new PermissionGroup
        {
            WorkspaceId = workspaceId,
            Name = "  Custom Mods  ",
            MappedPlatformDesignation = "moderator",
        }, ct);

        _ = created.Id.Should().NotBe(Guid.Empty);
        _ = created.Name.Should().Be("Custom Mods");

        created.Name = "Senior Mods";
        PermissionGroup updated = await store.UpsertGroupAsync(created, ct);
        _ = updated.Name.Should().Be("Senior Mods");

        IReadOnlyList<PermissionGroup> groups = await store.GetGroupsAsync(workspaceId, ct);
        _ = groups.Should().ContainSingle(g => g.Id == created.Id && g.Name == "Senior Mods");

        await store.DeleteGroupAsync(created.Id, ct);
        _ = (await store.GetGroupsAsync(workspaceId, ct)).Should().BeEmpty();
        _ = events.Should().HaveCount(3);
        _ = events.Should().AllSatisfy(e => e.WorkspaceId.Should().Be(workspaceId));
    }
    #endregion

    #region SetGrantAsync
    [Fact]
    [Trait("Member", "SetGrantAsync")]
    public async Task SetGrantAsync_AndEvaluatePermissionAsync_NormalizesKeysAndAppliesDenyPrecedence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionStore store = new(factory, TimeProvider.System, eventBus);

        PermissionGroup allowGroup = await store.UpsertGroupAsync(new PermissionGroup
        {
            WorkspaceId = workspaceId,
            Name = "AllowGroup",
        }, ct);
        PermissionGroup denyGroup = await store.UpsertGroupAsync(new PermissionGroup
        {
            WorkspaceId = workspaceId,
            Name = "DenyGroup",
        }, ct);

        await store.SetGrantAsync(allowGroup.Id, "  CMD.Ping ", PermissionState.Allow, ct);
        await store.SetGrantAsync(denyGroup.Id, "cmd.ping", PermissionState.Allow, ct);

        IReadOnlyList<PermissionGrant> grants = await store.GetGrantsAsync(allowGroup.Id, ct);
        _ = grants.Should().ContainSingle(g => g.PermissionKey == "cmd.ping" && g.State == PermissionState.Allow);

        PermissionState allowResult = await store.EvaluatePermissionAsync(workspaceId, [allowGroup.Id], "CMD.PING", ct);
        _ = allowResult.Should().Be(PermissionState.Allow);

        await store.SetGrantAsync(denyGroup.Id, "CMD.PING", PermissionState.Deny, ct);

        PermissionState denyResult = await store.EvaluatePermissionAsync(workspaceId, [allowGroup.Id, denyGroup.Id], "cmd.ping", ct);
        _ = denyResult.Should().Be(PermissionState.Deny);

        PermissionState inheritResult = await store.EvaluatePermissionAsync(workspaceId, [allowGroup.Id], "cmd.unknown", ct);
        _ = inheritResult.Should().Be(PermissionState.Inherit);
    }
    #endregion

    #region AssignWorkspaceToGlobalGroupAsync
    [Fact]
    [Trait("Member", "AssignWorkspaceToGlobalGroupAsync")]
    public async Task AssignWorkspaceToGlobalGroupAsync_AssignAndRemove_WorksIdempotently()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionStore store = new(factory, TimeProvider.System, eventBus);

        PermissionGroup globalGroup = await store.UpsertGroupAsync(new PermissionGroup
        {
            WorkspaceId = null,
            Name = "Pro Tier",
        }, ct);

        await store.AssignWorkspaceToGlobalGroupAsync(workspaceId, globalGroup.Id, ct);
        await store.AssignWorkspaceToGlobalGroupAsync(workspaceId, globalGroup.Id, ct);

        IReadOnlyList<PermissionGroup> assigned = await store.GetGlobalGroupsForWorkspaceAsync(workspaceId, ct);
        _ = assigned.Should().ContainSingle(g => g.Id == globalGroup.Id);

        await store.RemoveWorkspaceFromGlobalGroupAsync(workspaceId, globalGroup.Id, ct);
        _ = (await store.GetGlobalGroupsForWorkspaceAsync(workspaceId, ct)).Should().BeEmpty();
    }
    #endregion

    #region AddGroupMemberAsync
    [Fact]
    [Trait("Member", "AddGroupMemberAsync")]
    public async Task AddGroupMemberAsync_UpsertAndRemove_NormalizesIdentifiers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionStore store = new(factory, TimeProvider.System, eventBus);

        PermissionGroup group = await store.UpsertGroupAsync(new PermissionGroup
        {
            WorkspaceId = workspaceId,
            Name = "VIPs",
        }, ct);

        await store.AddGroupMemberAsync(new PermissionGroupMember
        {
            GroupId = group.Id,
            PlatformId = " Twitch ",
            PlatformUserId = " User123 ",
            LastKnownUsername = " OldName ",
        }, ct);

        await store.AddGroupMemberAsync(new PermissionGroupMember
        {
            GroupId = group.Id,
            PlatformId = "twitch",
            PlatformUserId = "user123",
            LastKnownUsername = " NewName ",
        }, ct);

        IReadOnlyList<PermissionGroupMember> members = await store.GetGroupMembersAsync(group.Id, ct);
        _ = members.Should().ContainSingle(m =>
            m.PlatformId == "twitch"
            && m.PlatformUserId == "user123"
            && m.LastKnownUsername == "newname");

        IReadOnlyList<Guid> userGroups = await store.GetUserGroupIdsAsync(workspaceId, "TWITCH", "USER123", ct);
        _ = userGroups.Should().ContainSingle(id => id == group.Id);

        await store.RemoveGroupMemberAsync(group.Id, "TWITCH", "USER123", ct);
        _ = (await store.GetGroupMembersAsync(group.Id, ct)).Should().BeEmpty();
        _ = (await store.GetUserGroupIdsAsync(workspaceId, "twitch", "user123", ct)).Should().BeEmpty();
    }
    #endregion

    #region SetUserOverrideAsync
    [Fact]
    [Trait("Member", "SetUserOverrideAsync")]
    public async Task SetUserOverrideAsync_Upsert_NormalizesAndUpdatesState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionStore store = new(factory, TimeProvider.System, eventBus);

        await store.SetUserOverrideAsync(new UserPermissionOverride
        {
            WorkspaceId = workspaceId,
            PlatformId = "Twitch",
            PlatformUserId = "User42",
            PermissionKey = "CMD.Shoutout",
            State = PermissionState.Allow,
        }, ct);

        await store.SetUserOverrideAsync(new UserPermissionOverride
        {
            WorkspaceId = workspaceId,
            PlatformId = "twitch",
            PlatformUserId = "user42",
            PermissionKey = "cmd.shoutout",
            State = PermissionState.Deny,
        }, ct);

        IReadOnlyList<UserPermissionOverride> overrides = await store.GetUserOverridesAsync(workspaceId, "TWITCH", "USER42", ct);
        _ = overrides.Should().ContainSingle(o =>
            o.PlatformId == "twitch"
            && o.PlatformUserId == "user42"
            && o.PermissionKey == "cmd.shoutout"
            && o.State == PermissionState.Deny);
    }
    #endregion

    #region UpsertRoleRosterAsync
    [Fact]
    [Trait("Member", "UpsertRoleRosterAsync")]
    public async Task UpsertRoleRosterAsync_Upsert_NormalizesAndUpdatesDesignations()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        PermissionStore store = new(factory, timeProvider, eventBus);

        await store.UpsertRoleRosterAsync(new WorkspacePlatformRoleRoster
        {
            WorkspaceId = workspaceId,
            PlatformId = "Twitch",
            PlatformUserId = "Mod01",
            Username = "ModUser",
            Designations = PlatformUserDesignations.Subscriber,
        }, ct);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await store.UpsertRoleRosterAsync(new WorkspacePlatformRoleRoster
        {
            WorkspaceId = workspaceId,
            PlatformId = "twitch",
            PlatformUserId = "mod01",
            Username = "ModUserRenamed",
            Designations = PlatformUserDesignations.Moderator | PlatformUserDesignations.Subscriber,
        }, ct);

        WorkspacePlatformRoleRoster? roster = await store.GetPlatformRoleRosterAsync(workspaceId, "TWITCH", "MOD01", ct);
        _ = roster.Should().NotBeNull();
        _ = roster!.Username.Should().Be("moduserrenamed");
        _ = roster.Designations.Should().Be(PlatformUserDesignations.Moderator | PlatformUserDesignations.Subscriber);
        _ = roster.UpdatedAtUtc.Should().Be(new DateTimeOffset(2026, 11, 14, 12, 5, 0, TimeSpan.Zero));
    }
    #endregion

    #region PermissionServiceEndToEnd
    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task PermissionService_EndToEndWithSqliteStore_EvaluatesGlobalAndWorkspacePermissions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionStore store = new(factory, TimeProvider.System, eventBus);

        ServiceCollection services = new();
        _ = services.AddHybridCache();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        HybridCache cache = serviceProvider.GetRequiredService<HybridCache>();
        PermissionRegistry registry = new();
        PermissionService permissionService = new(store, registry, cache, eventBus);

        await store.SeedBuiltInWorkspaceGroupsAsync(workspaceId, ct);
        IReadOnlyList<PermissionGroup> workspaceGroups = await store.GetGroupsAsync(workspaceId, ct);
        PermissionGroup modGroup = workspaceGroups.Single(static g => g.Name == "Moderator");

        PermissionGroup globalGroup = await store.UpsertGroupAsync(new PermissionGroup
        {
            WorkspaceId = null,
            Name = "Global Features",
        }, ct);
        await store.AssignWorkspaceToGlobalGroupAsync(workspaceId, globalGroup.Id, ct);
        await store.SetGrantAsync(globalGroup.Id, "feature.commands", PermissionState.Allow, ct);
        await store.SetGrantAsync(modGroup.Id, "feature.commands", PermissionState.Allow, ct);

        await store.UpsertRoleRosterAsync(new WorkspacePlatformRoleRoster
        {
            WorkspaceId = workspaceId,
            PlatformId = "twitch",
            PlatformUserId = "viewer1",
            Username = "viewer1",
            Designations = PlatformUserDesignations.Moderator,
        }, ct);

        PlatformUserIdentity actor = new(
            PlatformId: "twitch",
            PlatformUserId: "viewer1",
            Username: "viewer1",
            Designations: PlatformUserDesignations.None);

        Workspace? workspace = await store.GetWorkspaceAsync(workspaceId, ct);
        _ = workspace.Should().NotBeNull();

        PermissionEvaluationResult allowed = await permissionService.EvaluateAsync(workspaceId, actor, "feature.commands", ct);
        _ = allowed.IsAllowed.Should().BeTrue();

        await store.SetUserOverrideAsync(new UserPermissionOverride
        {
            WorkspaceId = workspaceId,
            PlatformId = "twitch",
            PlatformUserId = "viewer1",
            PermissionKey = "feature.commands",
            State = PermissionState.Deny,
        }, ct);
        await permissionService.InvalidateWorkspaceAsync(workspaceId, ct);

        PermissionEvaluationResult denied = await permissionService.EvaluateAsync(workspaceId, actor, "feature.commands", ct);
        _ = denied.IsAllowed.Should().BeFalse();
        _ = denied.EffectiveState.Should().Be(PermissionState.Deny);
    }
    #endregion
}
