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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Actions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Scripting;
using StreamActions.Core.Services;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class ActionBlockAndScriptStoreTests
{
    #region ActionBlockStore

    [Fact]
    [Trait("Member", "UpsertTaskAsync")]
    public async Task UpsertTaskAsync_WhenBlockCountExceedsTierOneQuota_ThrowsQuotaExceededException()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        using ServiceProvider sp = services.BuildServiceProvider();
        HybridCache cache = sp.GetRequiredService<HybridCache>();

        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionRegistry registry = new();
        PermissionStore permissionStore = new(factory, TimeProvider.System, eventBus, registry);
        PermissionGroup defaultGlobalGroup = await permissionStore.SeedDefaultGlobalGroupAsync(cancellationToken: TestContext.Current.CancellationToken);
        await permissionStore.AssignWorkspaceToGlobalGroupAsync(workspaceId, defaultGlobalGroup.Id, TestContext.Current.CancellationToken);
        await permissionStore.SeedBuiltInWorkspaceGroupsAsync(workspaceId, TestContext.Current.CancellationToken);

        WorkspaceCacheService cacheService = new(cache);
        ActionBlockStore actionBlockStore = new(factory, cacheService, permissionStore);

        // Default Workspace Plan seeds MaxActionBlocksPerFlow = 25.
        List<ActionBlock> tooManyBlocks = [.. Enumerable.Range(0, 26).Select(static i => (ActionBlock)new SendMessageActionBlock($"Msg {i}"))];

        Func<Task> act = async () => _ = await actionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            WorkspaceId = workspaceId,
            Name = "Oversized Task",
            Blocks = tooManyBlocks,
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = await act.Should().ThrowAsync<ActionFlowQuotaExceededException>();
    }

    [Fact]
    [Trait("Member", "UpsertProfileAsync")]
    public async Task ActionBlockStore_PersistsProfilesAndPolymorphicTasksAcrossCacheInvalidation()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        using ServiceProvider sp = services.BuildServiceProvider();
        HybridCache cache = sp.GetRequiredService<HybridCache>();

        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionRegistry registry = new();
        PermissionStore permissionStore = new(factory, TimeProvider.System, eventBus, registry);
        ActionBlockStore actionBlockStore = new(factory, new WorkspaceCacheService(cache), permissionStore);

        WorkspaceActionBlockTask savedTask = await actionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            WorkspaceId = workspaceId,
            Name = "Welcome Flow",
            InputParameters = [new("user", "User")],
            Blocks = [new SetVariableActionBlock("x", "1"), new SendMessageActionBlock("Hi {var x}")],
        }, TestContext.Current.CancellationToken);

        WorkspaceActionBlockProfile savedProfile = await actionBlockStore.UpsertProfileAsync(new WorkspaceActionBlockProfile
        {
            WorkspaceId = workspaceId,
            Name = "On Follow Welcome",
            Trigger = new ProfileTriggerDefinition { TriggerId = BuiltInTriggerIds.Follow },
            AttachedTaskId = savedTask.Id,
            ParameterMappings = new Dictionary<string, string> { ["user"] = "{var followerLogin}" },
        }, TestContext.Current.CancellationToken);

        _ = (await actionBlockStore.GetTaskByNameAsync(workspaceId, "Welcome Flow", TestContext.Current.CancellationToken))!
            .Blocks.Should().HaveCount(2);
        _ = (await actionBlockStore.GetProfileByNameAsync(workspaceId, "On Follow Welcome", TestContext.Current.CancellationToken))!
            .AttachedTaskId.Should().Be(savedTask.Id);

        _ = await actionBlockStore.SetProfileEnabledAsync(workspaceId, savedProfile.Id, false, TestContext.Current.CancellationToken);
        _ = (await actionBlockStore.GetProfileByIdAsync(workspaceId, savedProfile.Id, TestContext.Current.CancellationToken))!
            .IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "UpsertProfileAsync")]
    public async Task ActionBlockStore_OneShotScheduleProperties_RoundTripThroughSqlite()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        using ServiceProvider sp = services.BuildServiceProvider();
        HybridCache cache = sp.GetRequiredService<HybridCache>();

        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionRegistry registry = new();
        PermissionStore permissionStore = new(factory, TimeProvider.System, eventBus, registry);
        ActionBlockStore actionBlockStore = new(factory, new WorkspaceCacheService(cache), permissionStore);

        WorkspaceActionBlockTask savedTask = await actionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            WorkspaceId = workspaceId,
            Name = "One-Shot Task",
            Blocks = [new SendMessageActionBlock("Scheduled ping")],
        }, TestContext.Current.CancellationToken);

        DateTimeOffset scheduledAt = new(2026, 7, 15, 18, 45, 30, TimeSpan.Zero);
        DateTimeOffset firedAt = new(2026, 7, 15, 18, 45, 35, TimeSpan.Zero);

        WorkspaceActionBlockProfile savedProfile = await actionBlockStore.UpsertProfileAsync(new WorkspaceActionBlockProfile
        {
            WorkspaceId = workspaceId,
            Name = "One-Shot Reminder",
            IsEnabled = true,
            AttachedTaskId = savedTask.Id,
            Trigger = new ProfileTriggerDefinition
            {
                TriggerId = BuiltInTriggerIds.OneShotSchedule,
                ScheduleMode = ProfileTriggerScheduleMode.OneShot,
                ScheduledAtUtc = scheduledAt,
            },
        }, TestContext.Current.CancellationToken);

        WorkspaceActionBlockProfile? initialLoaded = await actionBlockStore.GetProfileByIdAsync(workspaceId, savedProfile.Id, TestContext.Current.CancellationToken);
        _ = initialLoaded.Should().NotBeNull();
        _ = initialLoaded!.Trigger.TriggerId.Should().Be(BuiltInTriggerIds.OneShotSchedule);
        _ = initialLoaded.Trigger.ScheduleMode.Should().Be(ProfileTriggerScheduleMode.OneShot);
        _ = initialLoaded.Trigger.ScheduledAtUtc.Should().Be(scheduledAt);
        _ = initialLoaded.Trigger.OneShotFiredAtUtc.Should().BeNull();

        savedProfile.Trigger.OneShotFiredAtUtc = firedAt;
        savedProfile.IsEnabled = false;
        _ = await actionBlockStore.UpsertProfileAsync(savedProfile, TestContext.Current.CancellationToken);

        // Read using a fresh cache + store to guarantee EF Core SQLite persistence round-trip.
        ServiceCollection freshServices = new();
        _ = freshServices.AddHybridCache();
        using ServiceProvider freshSp = freshServices.BuildServiceProvider();
        ActionBlockStore freshStore = new(factory, new WorkspaceCacheService(freshSp.GetRequiredService<HybridCache>()), permissionStore);

        WorkspaceActionBlockProfile? reloaded = await freshStore.GetProfileByIdAsync(workspaceId, savedProfile.Id, TestContext.Current.CancellationToken);
        _ = reloaded.Should().NotBeNull();
        _ = reloaded!.IsEnabled.Should().BeFalse();
        _ = reloaded.Trigger.ScheduleMode.Should().Be(ProfileTriggerScheduleMode.OneShot);
        _ = reloaded.Trigger.ScheduledAtUtc.Should().Be(scheduledAt);
        _ = reloaded.Trigger.OneShotFiredAtUtc.Should().Be(firedAt);
    }

    #endregion

    #region WorkspaceScriptStoreAndPermissionTracker

    [Fact]
    [Trait("Member", "DeleteAsync")]
    public async Task WorkspaceScriptStore_WhenLastCommandReferenceDeleted_CleansUpPermissionGrantsAndRegistry()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        using ServiceProvider sp = services.BuildServiceProvider();
        HybridCache cache = sp.GetRequiredService<HybridCache>();

        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionRegistry registry = new();
        PermissionStore permissionStore = new(factory, TimeProvider.System, eventBus, registry);
        await permissionStore.SeedBuiltInWorkspaceGroupsAsync(workspaceId, TestContext.Current.CancellationToken);

        WorkspaceCommandPermissionTracker tracker = new(factory, permissionStore, registry);
        WorkspaceScriptStore scriptStore = new(factory, new WorkspaceCacheService(cache), tracker);

        WorkspaceScript script = await scriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = workspaceId,
            Path = "scripts/commands/heist.js",
            Code = "$.bind('command', function(e) {});",
            RegisteredCommands =
            [
                new ScriptCommandRegistration("!heist", PlatformUserDesignations.Subscriber | PlatformUserDesignations.Moderator),
            ],
        }, TestContext.Current.CancellationToken);

        _ = registry.GetByKey("command.heist").Should().NotBeNull();

        using (StreamActionsDbContext db = factory.CreateDbContext())
        {
            int grantCount = await db.PermissionGrants
                .CountAsync(g => g.PermissionKey == "command.heist", TestContext.Current.CancellationToken);
            _ = grantCount.Should().Be(2);
        }

        // Delete script -> zero references remain -> grants and registry entry are automatically deleted.
        bool deleted = await scriptStore.DeleteAsync(workspaceId, script.Id, TestContext.Current.CancellationToken);
        _ = deleted.Should().BeTrue();
        _ = registry.GetByKey("command.heist").Should().BeNull();

        using StreamActionsDbContext verifyDb = factory.CreateDbContext();
        int remainingGrants = await verifyDb.PermissionGrants
            .CountAsync(g => g.PermissionKey == "command.heist", TestContext.Current.CancellationToken);
        _ = remainingGrants.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "UpsertAsync")]
    public async Task WorkspaceScriptStore_WhenPathContainsTraversal_ThrowsArgumentException()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        using ServiceProvider sp = services.BuildServiceProvider();
        HybridCache cache = sp.GetRequiredService<HybridCache>();

        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionRegistry registry = new();
        PermissionStore permissionStore = new(factory, TimeProvider.System, eventBus, registry);
        WorkspaceCommandPermissionTracker tracker = new(factory, permissionStore, registry);
        WorkspaceScriptStore scriptStore = new(factory, new WorkspaceCacheService(cache), tracker);

        Func<Task> act = async () => _ = await scriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = workspaceId,
            Path = "../secrets/steal.js",
            Code = "return 1;",
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion
}

