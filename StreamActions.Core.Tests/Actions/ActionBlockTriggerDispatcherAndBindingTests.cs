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
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Actions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Templates;
using StreamActions.Core.Tests.Commands;
using System.Globalization;
using Xunit;

namespace StreamActions.Core.Tests.Actions;

public sealed class ActionBlockTriggerDispatcherAndBindingTests
{
    #region DispatchEventTriggerAsync

    [Fact]
    [Trait("Member", "DispatchEventTriggerAsync")]
    public async Task DispatchEventTriggerAsyncWhenMultiPredicateProfileMatchesExecutesAttachedTaskWithMappedInputs()
    {
        using ActionBlockTestRig rig = new();
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        rig.StreamStatusProvider.SetPlatformStatus(workspaceId, "twitch", isLive: true, gameCategory: "Minecraft");

        ActionBlockBindingResult bound = await rig.BindingService.CreateProfileWithNewTaskAsync(
            workspaceId,
            "Sub Online Minecraft",
            new ProfileTriggerDefinition { TriggerId = BuiltInTriggerIds.Subscription, PlatformId = "twitch" },
            "Sub Alert Task",
            predicates: new ActionBlockConditionGroup(
            [
                new("Stream.IsOnline", ActionBlockComparisonOperator.Equals, "true", ActionBlockLogicalCombinator.And),
                new("Stream.Game", ActionBlockComparisonOperator.Equals, "Minecraft"),
            ]),
            TestContext.Current.CancellationToken);

        _ = bound.Task.InputParameters.Select(static p => p.Name).Should().Contain(["subscriberLogin", "tier", "cumulativeMonths"]);

        bound.Task.Blocks = [new SendMessageActionBlock("Thanks {var subscriberDisplayName} for tier {var tier}!")];
        _ = await rig.ActionBlockStore.UpsertTaskAsync(bound.Task, TestContext.Current.CancellationToken);

        IReadOnlyList<ActionExecutionTrace> traces = await rig.TriggerDispatcher.DispatchEventTriggerAsync(
            new ActionBlockTriggerEvent(
                workspaceId,
                BuiltInTriggerIds.Subscription,
                "twitch",
                "chan-1",
                "streamer",
                new PlatformUserIdentity("twitch", "u-1", "alice", PlatformUserDesignations.Subscriber),
                Message: null,
                new Dictionary<string, string>
                {
                    ["subscriberLogin"] = "alice",
                    ["subscriberDisplayName"] = "Alice",
                    ["tier"] = "1000",
                    ["cumulativeMonths"] = "6",
                }),
            TestContext.Current.CancellationToken);

        _ = traces.Should().ContainSingle().Which.Succeeded.Should().BeTrue();
        _ = rig.SentOutboundMessages.Should().ContainSingle().Which.Message.Should().Be("Thanks Alice for tier 1000!");

        // Change game -> predicate should no longer match.
        rig.StreamStatusProvider.SetPlatformStatus(workspaceId, "twitch", isLive: true, gameCategory: "Valorant");
        IReadOnlyList<ActionExecutionTrace> secondTraces = await rig.TriggerDispatcher.DispatchEventTriggerAsync(
            new ActionBlockTriggerEvent(
                workspaceId,
                BuiltInTriggerIds.Subscription,
                "twitch",
                "chan-1",
                "streamer",
                ActorIdentity: null,
                Message: null,
                new Dictionary<string, string> { ["subscriberDisplayName"] = "Bob", ["tier"] = "1000" }),
            TestContext.Current.CancellationToken);

        _ = secondTraces.Should().BeEmpty();
    }

    #endregion

    #region EvaluateDueSchedulesAsync

    [Fact]
    [Trait("Member", "EvaluateDueSchedulesAsync")]
    public async Task EvaluateDueSchedulesAsyncEnforcesThirtyMinutePostStreamGraceAndOfflineSchedulePermission()
    {
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using ActionBlockTestRig rig = new(fakeTime);
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        WorkspaceActionBlockTask timerTask = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "Reminder",
            Blocks = [new SendMessageActionBlock("Check out socials!")],
        }, TestContext.Current.CancellationToken);

        _ = await rig.ActionBlockStore.UpsertProfileAsync(new WorkspaceActionBlockProfile
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "Every 5m",
            IsEnabled = true,
            AttachedTaskId = timerTask.Id,
            Trigger = new ProfileTriggerDefinition
            {
                TriggerId = BuiltInTriggerIds.TimerOrCron,
                ScheduleMode = ProfileTriggerScheduleMode.Interval,
                IntervalSeconds = 300,
            },
        }, TestContext.Current.CancellationToken);

        // 1. Live stream fires immediately.
        rig.StreamStatusProvider.SetPlatformStatus(workspaceId, "twitch", isLive: true);
        rig.TriggerDispatcher.RecordStreamStatusChange(workspaceId, isLive: true);
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(1);

        // 2. Stream goes offline -> still fires inside 30-minute post-stream grace period.
        rig.StreamStatusProvider.SetPlatformStatus(workspaceId, "twitch", isLive: false);
        rig.TriggerDispatcher.RecordStreamStatusChange(workspaceId, isLive: false);
        fakeTime.Advance(TimeSpan.FromMinutes(15));
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(1);

        // 3. Past 30-minute grace period -> paused by default without feature.action_blocks.offline_schedule.
        fakeTime.Advance(TimeSpan.FromMinutes(20));
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        // 4. Grant feature.action_blocks.offline_schedule on Tier 1 Global Group -> fires off-stream.
        await rig.AddGlobalGroupForWorkspaceAsync(
            workspaceId,
            new PermissionGroup { Id = Guid.CreateVersion7(), Name = "Pro Plan" },
            new PermissionGrant
            {
                Id = Guid.CreateVersion7(),
                PermissionKey = BuiltInPermissionKeys.FeatureActionBlocksOfflineSchedule,
                State = PermissionState.Allow,
            });

        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    [Trait("Member", "EvaluateDueSchedulesAsync")]
    public async Task EvaluateDueSchedulesAsyncWhenOneShotScheduleFiresOncePersistsFiredStateAndSurvivesRestart()
    {
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using ActionBlockTestRig rig = new(fakeTime);
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        ITriggerDescriptor? descriptor = rig.Catalog.GetTrigger(BuiltInTriggerIds.OneShotSchedule);
        _ = descriptor.Should().NotBeNull();
        _ = descriptor!.CategoryPath.Should().Equal("Schedules");
        _ = descriptor.DisplayName.Should().Be("One-Shot Date / Time");
        _ = descriptor.SupportsSchedule.Should().BeTrue();
        _ = descriptor.OutputParameters.Select(static p => p.Name).Should().Contain(["scheduledAtUtc", "firedAtUtc", "isStreamLive"]);

        DateTimeOffset scheduledAt = new(2026, 6, 1, 12, 30, 0, TimeSpan.Zero);

        WorkspaceActionBlockTask oneShotTask = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "One-Shot Announcement",
            Blocks = [new SendMessageActionBlock("Scheduled {var scheduledAtUtc} fired {var firedAtUtc}")],
        }, TestContext.Current.CancellationToken);

        WorkspaceActionBlockProfile profile = await rig.ActionBlockStore.UpsertProfileAsync(new WorkspaceActionBlockProfile
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "Special Event Reminder",
            IsEnabled = true,
            AttachedTaskId = oneShotTask.Id,
            Trigger = new ProfileTriggerDefinition
            {
                TriggerId = BuiltInTriggerIds.OneShotSchedule,
                ScheduleMode = ProfileTriggerScheduleMode.OneShot,
                ScheduledAtUtc = scheduledAt,
            },
        }, TestContext.Current.CancellationToken);

        rig.StreamStatusProvider.SetPlatformStatus(workspaceId, "twitch", isLive: true);
        rig.TriggerDispatcher.RecordStreamStatusChange(workspaceId, isLive: true);

        // Before ScheduledAtUtc -> does not fire.
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        fakeTime.Advance(TimeSpan.FromMinutes(29));
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        _ = rig.SentOutboundMessages.Should().BeEmpty();

        // At/after ScheduledAtUtc -> fires once with scheduledAtUtc + firedAtUtc populated.
        fakeTime.Advance(TimeSpan.FromSeconds(65));
        DateTimeOffset expectedFiredAt = fakeTime.GetUtcNow();
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        _ = rig.SentOutboundMessages.Should().ContainSingle().Which.Message.Should().Be(
            $"Scheduled {scheduledAt.ToString("O", CultureInfo.InvariantCulture)} fired {expectedFiredAt.ToString("O", CultureInfo.InvariantCulture)}");

        WorkspaceActionBlockProfile? persisted = await rig.ActionBlockStore.GetProfileByIdAsync(workspaceId, profile.Id, TestContext.Current.CancellationToken);
        _ = persisted.Should().NotBeNull();
        _ = persisted!.IsEnabled.Should().BeFalse();
        _ = persisted.Trigger.OneShotFiredAtUtc.Should().Be(expectedFiredAt);

        // Subsequent ticks do not fire again.
        fakeTime.Advance(TimeSpan.FromMinutes(5));
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        // Fresh ActionBlockTriggerDispatcher instance (simulating daemon restart) does not re-fire.
        using ActionBlockTriggerDispatcher restartedDispatcher = new(
            rig.ActionBlockStore,
            rig.FlowRunner,
            new TagRegistry(CoreTagRegistryFactory.CreateDefaultHandlers()),
            rig.PermissionService,
            rig.StreamStatusProvider,
            timeProvider: fakeTime);
        restartedDispatcher.RecordStreamStatusChange(workspaceId, isLive: true);
        _ = (await restartedDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        // Even if re-enabled without clearing OneShotFiredAtUtc, it will not fire a second time.
        persisted.IsEnabled = true;
        _ = await rig.ActionBlockStore.UpsertProfileAsync(persisted, TestContext.Current.CancellationToken);
        _ = (await restartedDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        _ = rig.SentOutboundMessages.Should().ContainSingle();
    }

    [Fact]
    [Trait("Member", "EvaluateDueSchedulesAsync")]
    public async Task EvaluateDueSchedulesAsyncWhenOneShotScheduleIsOffStreamEnforcesOfflineSchedulePermission()
    {
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using ActionBlockTestRig rig = new(fakeTime);
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        WorkspaceActionBlockTask oneShotTask = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "Offline One-Shot",
            Blocks = [new SendMessageActionBlock("Off-stream one-shot!")],
        }, TestContext.Current.CancellationToken);

        WorkspaceActionBlockProfile profile = await rig.ActionBlockStore.UpsertProfileAsync(new WorkspaceActionBlockProfile
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "Offline Announcement",
            IsEnabled = true,
            AttachedTaskId = oneShotTask.Id,
            Trigger = new ProfileTriggerDefinition
            {
                TriggerId = BuiltInTriggerIds.OneShotSchedule,
                ScheduleMode = ProfileTriggerScheduleMode.OneShot,
                ScheduledAtUtc = new DateTimeOffset(2026, 6, 1, 12, 5, 0, TimeSpan.Zero),
            },
        }, TestContext.Current.CancellationToken);

        rig.StreamStatusProvider.SetPlatformStatus(workspaceId, "twitch", isLive: false);
        fakeTime.Advance(TimeSpan.FromMinutes(10));

        // Off-stream without feature.action_blocks.offline_schedule -> blocked and remains armed.
        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        _ = profile.IsEnabled.Should().BeTrue();
        _ = profile.Trigger.OneShotFiredAtUtc.Should().BeNull();

        // Grant feature.action_blocks.offline_schedule -> fires once and disables.
        await rig.AddGlobalGroupForWorkspaceAsync(
            workspaceId,
            new PermissionGroup { Id = Guid.CreateVersion7(), Name = "Pro Plan" },
            new PermissionGrant
            {
                Id = Guid.CreateVersion7(),
                PermissionKey = BuiltInPermissionKeys.FeatureActionBlocksOfflineSchedule,
                State = PermissionState.Allow,
            });

        _ = (await rig.TriggerDispatcher.EvaluateDueSchedulesAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        _ = rig.SentOutboundMessages.Should().ContainSingle().Which.Message.Should().Be("Off-stream one-shot!");
        _ = profile.IsEnabled.Should().BeFalse();
        _ = profile.Trigger.OneShotFiredAtUtc.Should().Be(fakeTime.GetUtcNow());
    }

    #endregion

    #region CronScheduleExpression

    [Fact]
    [Trait("Member", "CronScheduleExpression")]
    public void CronScheduleExpressionMatchesAndFindsNextOccurrence()
    {
        bool parsed = CronScheduleExpression.TryParse("*/15 12 * * 1-5", out CronScheduleExpression? cron);

        _ = parsed.Should().BeTrue();
        _ = cron.Should().NotBeNull();
        _ = cron!.Matches(new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero)).Should().BeTrue(); // Monday 12:30 UTC
        _ = cron.Matches(new DateTimeOffset(2026, 6, 1, 12, 31, 0, TimeSpan.Zero)).Should().BeFalse();

        DateTimeOffset? next = cron.GetNextOccurrenceUtc(new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero));
        _ = next.Should().Be(new DateTimeOffset(2026, 6, 1, 12, 45, 0, TimeSpan.Zero));
    }

    #endregion

    #region ActionBlockCatalogAndUpgrade

    [Fact]
    [Trait("Member", "BuildActionCategoryTree")]
    public async Task ActionBlockCatalogBuildsMultiLevelCategoryTreeAndUpgradeCommandWorks()
    {
        using ActionBlockTestRig rig = new();
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        IReadOnlyList<ActionCategoryNode> tree = rig.Catalog.BuildActionCategoryTree();
        ActionCategoryNode coreNode = tree.Should().ContainSingle(static n => n.Name == "Core").Subject;
        ActionCategoryNode storageNode = coreNode.Children.Should().ContainSingle(static n => n.Name == "Storage").Subject;
        _ = storageNode.Children.Should().ContainSingle(static n => n.Name == "Key-Value")
            .Which.Actions.Should().Contain(static a => a.ActionId == "core.kv.set");

        _ = rig.Catalog.SearchActions("Key-Value").Should().Contain(static a => a.ActionId == "core.kv.set");

        CustomCommand legacyCmd = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Trigger = "!shoutout",
            RoutineJson = "Go follow {1}!",
            IsEnabled = true,
        };

        ActionBlockBindingResult upgraded = await rig.BindingService.UpgradeCustomCommandToActionBlocksAsync(
            workspaceId,
            legacyCmd,
            disableOriginalCommand: true,
            TestContext.Current.CancellationToken);

        _ = upgraded.Profile.Trigger.TriggerId.Should().Be(BuiltInTriggerIds.ChatCommand);
        _ = upgraded.Profile.Trigger.CommandTrigger.Should().Be("shoutout");
        _ = upgraded.Task.Blocks.Should().ContainSingle()
            .Which.Should().BeOfType<SendMessageActionBlock>()
            .Which.Message.Should().Be("Go follow {1}!");
        _ = legacyCmd.IsEnabled.Should().BeFalse();
    }

    #endregion
}

