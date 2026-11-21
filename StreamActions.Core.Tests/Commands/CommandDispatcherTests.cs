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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Models.Metadata;
using StreamActions.Core.Permissions;
using StreamActions.Core.Templates;
using Xunit;

namespace StreamActions.Core.Tests.Commands;

/// <summary>
/// Comprehensive unit tests for <see cref="CommandDispatcher"/> and DI registration.
/// </summary>
public sealed class CommandDispatcherTests
{
    #region RoutingAndOverrides

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncRoutesBotAndActiveBotUsernameAndLocalizedSubCommandTriggers()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            rig.WorkspaceStore.Platforms[(CommandTestHarness.DefaultWorkspaceId, "twitch")].Metadata.BotUsername = "MyCoolBot";
            await rig.Localizer.SetWorkspaceCommandTriggersAsync(
                CommandTestHarness.DefaultWorkspaceId,
                "bot.command",
                ["comando"],
                TestContext.Current.CancellationToken);

            // 1. Standard !bot command
            CommandDispatchResult r1 = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "add", "!one", "One"], "command add !one One", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // 2. !<activeBotUsername> cmd
            CommandDispatchResult r2 = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("mycoolbot", ["cmd", "add", "!two", "Two"], "cmd add !two Two", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // 3. !<activeBotUsername> <localized trigger>
            CommandDispatchResult r3 = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("mycoolbot", ["comando", "add", "!three", "Three"], "comando add !three Three", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            _ = r1.Executed.Should().BeTrue();
            _ = r2.Executed.Should().BeTrue();
            _ = r3.Executed.Should().BeTrue();
            _ = (await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "one", TestContext.Current.CancellationToken)).Should().NotBeNull();
            _ = (await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "two", TestContext.Current.CancellationToken)).Should().NotBeNull();
            _ = (await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "three", TestContext.Current.CancellationToken)).Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData(true, true, "Custom response")]
    [InlineData(false, true, "Built-in overrideable")]
    [InlineData(true, false, "Built-in locked")]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncRespectsAllowCustomOverrideForTopLevelBuiltInCommands(
        bool customEnabled,
        bool allowCustomOverride,
        string expectedOutput)
    {
        StubTopLevelBuiltInHandler builtIn = allowCustomOverride
            ? new OverrideableBuiltInHandler()
            : new LockedBuiltInHandler();

        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig([builtIn]);
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = builtIn.DefaultSubCommandTrigger,
                    RoutineJson = CommandRoutine.FromPlainText("Custom response").ToJson(),
                    IsEnabled = customEnabled,
                },
                TestContext.Current.CancellationToken);

            CommandDispatchResult result = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand(builtIn.DefaultSubCommandTrigger),
                TestContext.Current.CancellationToken);

            _ = result.Executed.Should().BeTrue();
            _ = rig.OutboundDispatcher.Messages.Should().ContainSingle(m => m.Message == expectedOutput);
        }
    }

    #endregion

    #region GatesAndPermissions

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncEnforcesFeatureFlagEnabledPlatformAndStreamStatusGates()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            CustomCommand cmd = new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                Trigger = "status",
                RoutineJson = CommandRoutine.FromPlainText("Live!").ToJson(),
                IsEnabled = false,
                StreamStatusRequirement = StreamStatusRequirement.OnlineOnly,
                Metadata = new CommandMetadata { AllowedPlatforms = ["twitch"] },
            };
            await rig.CommandRepository.UpsertAsync(cmd, TestContext.Current.CancellationToken);

            // Disabled
            CommandDispatchResult disabledResult = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("status", platformId: "twitch"),
                TestContext.Current.CancellationToken);
            _ = disabledResult.Status.Should().Be(CommandDispatchStatus.Disabled);

            // Wrong platform
            cmd.IsEnabled = true;
            await rig.CommandRepository.UpsertAsync(cmd, TestContext.Current.CancellationToken);
            CommandDispatchResult wrongPlatformResult = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("status", platformId: "discord"),
                TestContext.Current.CancellationToken);
            _ = wrongPlatformResult.Status.Should().Be(CommandDispatchStatus.PlatformNotAllowed);

            // OnlineOnly while offline
            CommandDispatchResult offlineResult = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("status", platformId: "twitch"),
                TestContext.Current.CancellationToken);
            _ = offlineResult.Status.Should().Be(CommandDispatchStatus.StreamStatusMismatch);

            // OnlineOnly while online -> executes!
            rig.StreamStatusProvider.SetPlatformStatus(CommandTestHarness.DefaultWorkspaceId, "twitch", true);
            CommandDispatchResult onlineResult = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("status", platformId: "twitch"),
                TestContext.Current.CancellationToken);
            _ = onlineResult.Status.Should().Be(CommandDispatchStatus.Executed);

            // OfflineOnly while online -> rejected!
            cmd.StreamStatusRequirement = StreamStatusRequirement.OfflineOnly;
            await rig.CommandRepository.UpsertAsync(cmd, TestContext.Current.CancellationToken);
            CommandDispatchResult offlineOnlyRejected = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("status", platformId: "twitch"),
                TestContext.Current.CancellationToken);
            _ = offlineOnlyRejected.Status.Should().Be(CommandDispatchStatus.StreamStatusMismatch);

            // FeatureCustomCommands denied at Tier 1 global group -> rejected!
            _ = rig.PermissionStore.Grants.RemoveAll(static g => g.PermissionKey == BuiltInPermissionKeys.FeatureCustomCommands);
            await rig.PermissionService.InvalidateGlobalAsync(TestContext.Current.CancellationToken);
            cmd.StreamStatusRequirement = StreamStatusRequirement.Both;
            await rig.CommandRepository.UpsertAsync(cmd, TestContext.Current.CancellationToken);

            CommandDispatchResult featureDenied = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("status", platformId: "twitch"),
                TestContext.Current.CancellationToken);
            _ = featureDenied.Status.Should().Be(CommandDispatchStatus.PermissionDenied);
        }
    }

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncEvaluatesCommandPermissionWithInheritFallbackToActionRunCommand()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig(seedDefaultPermissions: false);
        await using (rig.ConfigureAwait(false))
        {
            await rig.PermissionStore.SetGrantAsync(rig.GlobalGroupId, BuiltInPermissionKeys.FeatureCustomCommands, PermissionState.Allow, TestContext.Current.CancellationToken);
            // ActionRunCommand is NOT granted yet; command.special IS explicitly allowed for Everyone.
            await rig.PermissionStore.SetGrantAsync(rig.EveryoneGroupId, "command.special", PermissionState.Allow, TestContext.Current.CancellationToken);

            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "special", RoutineJson = "Special!", IsEnabled = true },
                TestContext.Current.CancellationToken);
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "normal", RoutineJson = "Normal!", IsEnabled = true },
                TestContext.Current.CancellationToken);

            CommandDispatchResult specialResult = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("special"),
                TestContext.Current.CancellationToken);
            CommandDispatchResult normalDenied = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("normal"),
                TestContext.Current.CancellationToken);

            _ = specialResult.Status.Should().Be(CommandDispatchStatus.Executed);
            _ = normalDenied.Status.Should().Be(CommandDispatchStatus.PermissionDenied);

            // Grant ActionRunCommand -> inherited normal command now executes, while explicit Deny on command.special blocks special.
            await rig.PermissionStore.SetGrantAsync(rig.EveryoneGroupId, BuiltInPermissionKeys.ActionRunCommand, PermissionState.Allow, TestContext.Current.CancellationToken);
            await rig.PermissionStore.SetGrantAsync(rig.EveryoneGroupId, "command.special", PermissionState.Deny, TestContext.Current.CancellationToken);
            await rig.PermissionService.InvalidateWorkspaceAsync(CommandTestHarness.DefaultWorkspaceId, TestContext.Current.CancellationToken);

            CommandDispatchResult normalAllowed = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("normal"),
                TestContext.Current.CancellationToken);
            CommandDispatchResult specialDenied = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("special"),
                TestContext.Current.CancellationToken);

            _ = normalAllowed.Status.Should().Be(CommandDispatchStatus.Executed);
            _ = specialDenied.Status.Should().Be(CommandDispatchStatus.PermissionDenied);
        }
    }

    #endregion

    #region CooldownsAndAliases

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncSharesCooldownBucketAcrossAliasesAndEnforcesChatAndWhisperCooldowns()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            CustomCommand cmd = new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                Trigger = "roll",
                AliasesJson = "[\"dice\",\"r\"]",
                GlobalCooldownSeconds = 10,
                UserCooldownSeconds = 30,
                RoutineJson = CommandRoutine.FromPlainText("Rolled!").ToJson(),
                IsEnabled = true,
            };
            await rig.CommandRepository.UpsertAsync(cmd, TestContext.Current.CancellationToken);

            // 1. User A executes via primary trigger !roll in chat.
            CommandDispatchResult first = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("roll", userId: "user-a", username: "a"),
                TestContext.Current.CancellationToken);
            _ = first.Status.Should().Be(CommandDispatchStatus.Executed);

            // 2. User B tries alias !dice immediately -> blocked by Global Cooldown (shared bucket!).
            CommandDispatchResult globalBlocked = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("dice", userId: "user-b", username: "b"),
                TestContext.Current.CancellationToken);
            _ = globalBlocked.Status.Should().Be(CommandDispatchStatus.OnGlobalCooldown);

            // 3. Advance 11 seconds -> global cooldown elapsed; User A tries via WHISPER alias !r -> blocked by User Cooldown (#48)!
            rig.TimeProvider.Advance(TimeSpan.FromSeconds(11));
            CommandDispatchResult whisperUserBlocked = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateWhisperCommand("r", userId: "user-a", username: "a"),
                TestContext.Current.CancellationToken);
            _ = whisperUserBlocked.Status.Should().Be(CommandDispatchStatus.OnUserCooldown);

            // 4. User B executes via WHISPER !dice -> allowed, and starts global cooldown again!
            CommandDispatchResult whisperUserB = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateWhisperCommand("dice", userId: "user-b", username: "b"),
                TestContext.Current.CancellationToken);
            _ = whisperUserB.Status.Should().Be(CommandDispatchStatus.Executed);

            // 5. Advance 20 seconds (31s total since User A) -> User A can run again!
            rig.TimeProvider.Advance(TimeSpan.FromSeconds(20));
            CommandDispatchResult userAAgain = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("roll", userId: "user-a", username: "a"),
                TestContext.Current.CancellationToken);
            _ = userAAgain.Status.Should().Be(CommandDispatchStatus.Executed);
        }
    }

    #endregion

    #region LoyaltyAndRefunds

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncHandlesLoyaltyCostInsufficientPointsRewardAndCancelRefund()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = "spin",
                    CostPoints = 50,
                    RewardPoints = 20,
                    GlobalCooldownSeconds = 10,
                    RoutineJson = CommandRoutine.FromPlainText("{if \"{eq \"{1}\" \"abort\"}\" \"{cancel}\" \"Won!\"}").ToJson(),
                    IsEnabled = true,
                },
                TestContext.Current.CancellationToken);

            // 1. Insufficient points -> sends localized error and returns InsufficientPoints.
            rig.LoyaltyStore.SetPoints(CommandTestHarness.DefaultWorkspaceId, "twitch", "viewer-1", 30);
            CommandDispatchResult insufficient = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("spin", userId: "viewer-1"),
                TestContext.Current.CancellationToken);

            _ = insufficient.Status.Should().Be(CommandDispatchStatus.InsufficientPoints);
            _ = rig.LoyaltyStore.GetPoints(CommandTestHarness.DefaultWorkspaceId, "twitch", "viewer-1").Should().Be(30);
            _ = rig.OutboundDispatcher.Messages.Should().ContainSingle(m => m.Message.Contains("50", StringComparison.Ordinal));

            rig.OutboundDispatcher.Messages.Clear();

            // 2. {cancel} refunds CostPoints, suppresses chat output, does NOT award RewardPoints, and restores cooldown.
            rig.LoyaltyStore.SetPoints(CommandTestHarness.DefaultWorkspaceId, "twitch", "viewer-1", 100);
            CommandDispatchResult canceled = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("spin", ["abort"], "abort", userId: "viewer-1"),
                TestContext.Current.CancellationToken);

            _ = canceled.Matched.Should().BeTrue();
            _ = rig.OutboundDispatcher.Messages.Should().BeEmpty();
            _ = rig.LoyaltyStore.GetPoints(CommandTestHarness.DefaultWorkspaceId, "twitch", "viewer-1").Should().Be(100);

            // 3. Immediate normal execution succeeds (because cooldown was restored on cancel!), deducts 50, awards 20 -> 70.
            CommandDispatchResult succeeded = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("spin", ["play"], "play", userId: "viewer-1"),
                TestContext.Current.CancellationToken);

            _ = succeeded.Status.Should().Be(CommandDispatchStatus.Executed);
            _ = rig.OutboundDispatcher.Messages.Should().ContainSingle(m => m.Message == "Won!");
            _ = rig.LoyaltyStore.GetPoints(CommandTestHarness.DefaultWorkspaceId, "twitch", "viewer-1").Should().Be(70);
        }
    }

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncRefundsLoyaltyCostPointsWhenExecutionThrows()
    {
        ThrowingCounterStore throwingCounterStore = new();
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig(counterStoreOverride: throwingCounterStore);
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = "boom",
                    CostPoints = 40,
                    RewardPoints = 15,
                    BoundCounterName = "deaths",
                    CounterUsageBehavior = CounterUsageBehavior.IncrementOnUse,
                    RoutineJson = CommandRoutine.FromPlainText("Deaths: {count}").ToJson(),
                    IsEnabled = true,
                },
                TestContext.Current.CancellationToken);

            rig.LoyaltyStore.SetPoints(CommandTestHarness.DefaultWorkspaceId, "twitch", "viewer-1", 100);

            CommandDispatchResult failed = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("boom", userId: "viewer-1"),
                TestContext.Current.CancellationToken);

            _ = failed.Status.Should().Be(CommandDispatchStatus.Failed);
            _ = failed.Executed.Should().BeFalse();
            _ = rig.LoyaltyStore.GetPoints(CommandTestHarness.DefaultWorkspaceId, "twitch", "viewer-1").Should().Be(100);
            _ = rig.OutboundDispatcher.Messages.Should().BeEmpty();
        }
    }

    #endregion

    #region CountersAndDeliveryModes

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncParameterizedCounterEndToEndSupportsModMutationsAndBlocksUnprivilegedViewers()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = "death",
                    AliasesJson = "[\"deaths\"]",
                    BoundCounterName = "deaths",
                    CounterUsageBehavior = CounterUsageBehavior.Parameterized,
                    RoutineJson = CommandRoutine.FromPlainText("Deaths: {count}").ToJson(),
                    IsEnabled = true,
                },
                TestContext.Current.CancellationToken);

            // 1. Moderator: !death + -> increments to 1 with [Increased] prefix.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("death", ["+"], "+", userId: "mod-1", username: "mod", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // 2. Unprivileged viewer: !death + -> cannot mutate, reads 1 without modifying counter!
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("death", ["+"], "+", userId: "viewer-1", username: "viewer", designations: PlatformUserDesignations.None),
                TestContext.Current.CancellationToken);

            // 3. Moderator via alias !deaths 10 -> sets counter to 10 with [Set] prefix.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("deaths", ["10"], "10", userId: "mod-1", username: "mod", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // 4. Moderator: !death - -> decrements to 9 with [Decreased] prefix.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("death", ["-"], "-", userId: "mod-1", username: "mod", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // 5. Viewer: !death -> reads 9.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("death", userId: "viewer-1", username: "viewer"),
                TestContext.Current.CancellationToken);

            _ = rig.OutboundDispatcher.Messages.Select(static m => m.Message).Should().Equal(
                "Deaths: [Increased] 1",
                "Deaths: 1",
                "Deaths: [Set] 10",
                "Deaths: [Decreased] 9",
                "Deaths: 9");
        }
    }

    [Theory]
    [InlineData(CommandDeliveryMode.Chat, null, false)]
    [InlineData(CommandDeliveryMode.ThreadedReply, "msg-999", false)]
    [InlineData(CommandDeliveryMode.Whisper, null, true)]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncHonorsDeliveryModesAndTargetScope(
        CommandDeliveryMode deliveryMode,
        string? expectedReplyToId,
        bool expectedIsWhisper)
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = "deliver",
                    DeliveryMode = deliveryMode,
                    RoutineJson = CommandRoutine.FromPlainText("Delivered!").ToJson(),
                    IsEnabled = true,
                    Metadata = new CommandMetadata { TargetScope = OutboundTargetScope.AllActiveStreamPlatforms },
                },
                TestContext.Current.CancellationToken);

            ChatCommand chatCmd = CommandTestHarness.CreateChatCommand("deliver", userId: "viewer-77", messageId: "msg-999");
            CommandDispatchResult result = await rig.Dispatcher.DispatchAsync(chatCmd, TestContext.Current.CancellationToken);

            _ = result.Status.Should().Be(CommandDispatchStatus.Executed, because: result.FailureReason ?? string.Empty);
            OutboundChatMessage outbound = rig.OutboundDispatcher.Messages.Should().ContainSingle().Subject;
            _ = outbound.Message.Should().Be("Delivered!");
            _ = outbound.ReplyToMessageId.Should().Be(expectedReplyToId);
            _ = outbound.IsWhisper.Should().Be(expectedIsWhisper);
            if (expectedIsWhisper)
            {
                _ = outbound.TargetPlatformUserId.Should().Be("viewer-77");
                _ = outbound.TargetScope.Should().Be(OutboundTargetScope.OriginPlatform);
            }
            else
            {
                _ = outbound.TargetScope.Should().Be(OutboundTargetScope.AllActiveStreamPlatforms);
            }
        }
    }

    #endregion

    #region ChainingAndAstCacheIntegration

    [Fact]
    [Trait("Member", "DispatchAsync")]
    public async Task DispatchAsyncSupportsBotCmdChainingWithinDepthAndPreventsCycles()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "c1", RoutineJson = "A {botcmd c2}", IsEnabled = true },
                TestContext.Current.CancellationToken);
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "c2", RoutineJson = "B {botcmd c1}", IsEnabled = true },
                TestContext.Current.CancellationToken);

            CommandDispatchResult result = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("c1"),
                TestContext.Current.CancellationToken);

            _ = result.Status.Should().Be(CommandDispatchStatus.Executed, because: result.FailureReason ?? string.Empty);
            _ = rig.OutboundDispatcher.Messages.Select(static m => m.Message).Should().Equal("B", "A");

            // Also verify AST caching across repeated dispatches + invalidation on CommandsInvalidatedEvent.
            long compilationsBefore = rig.AstCache.CompilationCount;
            _ = await rig.Dispatcher.DispatchAsync(CommandTestHarness.CreateChatCommand("c1"), TestContext.Current.CancellationToken);
            _ = rig.AstCache.CompilationCount.Should().Be(compilationsBefore);

            TaskCompletionSource<Guid?> invalidatedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.AstCache.Invalidated += wsId =>
            {
                if (wsId == CommandTestHarness.DefaultWorkspaceId)
                {
                    _ = invalidatedTcs.TrySetResult(wsId);
                }
            };

            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "c3", RoutineJson = "C", IsEnabled = true },
                TestContext.Current.CancellationToken);
            _ = await invalidatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            _ = await rig.Dispatcher.DispatchAsync(CommandTestHarness.CreateChatCommand("c1"), TestContext.Current.CancellationToken);
            _ = rig.AstCache.CompilationCount.Should().BeGreaterThan(compilationsBefore);

            // Verify MaxCommandChainDepth = 3 cap stops at d3 and does not execute d4.
            rig.OutboundDispatcher.Messages.Clear();
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "d0", RoutineJson = "0 {botcmd d1}", IsEnabled = true },
                TestContext.Current.CancellationToken);
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "d1", RoutineJson = "1 {botcmd d2}", IsEnabled = true },
                TestContext.Current.CancellationToken);
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "d2", RoutineJson = "2 {botcmd d3}", IsEnabled = true },
                TestContext.Current.CancellationToken);
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "d3", RoutineJson = "3 {botcmd d4}", IsEnabled = true },
                TestContext.Current.CancellationToken);
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "d4", RoutineJson = "4", IsEnabled = true },
                TestContext.Current.CancellationToken);

            CommandDispatchResult chainResult = await rig.Dispatcher.DispatchAsync(CommandTestHarness.CreateChatCommand("d0"), TestContext.Current.CancellationToken);
            _ = chainResult.Executed.Should().BeTrue();
            _ = rig.OutboundDispatcher.Messages.Select(static m => m.Message).Should().Equal("3", "2", "1", "0");
        }
    }

    #endregion

    #region DependencyInjection

    [Fact]
    [Trait("Member", "AddStreamActionsCore")]
    public async Task AddStreamActionsCoreRegistersCommandPipelineServicesWithoutCaptiveDependencies()
    {
        ConfigurationBuilder configBuilder = new();
        IConfiguration configuration = configBuilder.Build();

        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddStreamActionsCore(configuration);

        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = false,
        });

        await using (provider.ConfigureAwait(false))
        {
            ITagRegistry tagRegistry = provider.GetRequiredService<ITagRegistry>();
            IBuiltInCommandRegistry builtInRegistry = provider.GetRequiredService<IBuiltInCommandRegistry>();
            CommandAstCache astCache = provider.GetRequiredService<CommandAstCache>();
            CommandCooldownTracker cooldownTracker = provider.GetRequiredService<CommandCooldownTracker>();
            ICommandDispatcher dispatcher = provider.GetRequiredService<ICommandDispatcher>();
            IPluginManager pluginManager = provider.GetRequiredService<IPluginManager>();

            _ = tagRegistry.IsRegistered("count").Should().BeTrue();
            _ = builtInRegistry.ResolveByCommandId("bot.command").Should().NotBeNull();
            _ = astCache.Should().NotBeNull();
            _ = cooldownTracker.Should().NotBeNull();
            _ = dispatcher.Should().NotBeNull();
            _ = pluginManager.GetPlugin("core.commands").Should().NotBeNull();
        }
    }

    #endregion

    /// <summary>
    /// Base stub for testing top-level built-in commands.
    /// </summary>
    private abstract class StubTopLevelBuiltInHandler : IBuiltInCommandHandler
    {
        /// <inheritdoc/>
        public abstract string CommandId { get; }

        /// <inheritdoc/>
        public abstract string DefaultSubCommandTrigger { get; }

        /// <inheritdoc/>
        public abstract IReadOnlyList<string> OptionalTopLevelTriggers { get; }

        /// <inheritdoc/>
        public abstract bool AllowCustomOverride { get; }

        /// <summary>Gets the fixed output message emitted by this stub handler.</summary>
        protected abstract string OutputMessage { get; }

        /// <inheritdoc/>
        public ValueTask ExecuteAsync(BuiltInCommandContext context, CancellationToken cancellationToken = default) =>
            context.SendReplyAsync(this.OutputMessage, cancellationToken);
    }

    /// <summary>
    /// Built-in command permitting custom command overrides.
    /// </summary>
    [Command("overrideme", AllowCustomOverride = true)]
    private sealed class OverrideableBuiltInHandler : StubTopLevelBuiltInHandler
    {
        /// <inheritdoc/>
        public override string CommandId => "test.overrideable";

        /// <inheritdoc/>
        public override string DefaultSubCommandTrigger => "overrideme";

        /// <inheritdoc/>
        public override IReadOnlyList<string> OptionalTopLevelTriggers { get; } = ["overrideme"];

        /// <inheritdoc/>
        public override bool AllowCustomOverride => true;

        /// <inheritdoc/>
        protected override string OutputMessage => "Built-in overrideable";
    }

    /// <summary>
    /// Built-in command forbidding custom command overrides.
    /// </summary>
    private sealed class LockedBuiltInHandler : StubTopLevelBuiltInHandler
    {
        /// <inheritdoc/>
        public override string CommandId => "test.locked";

        /// <inheritdoc/>
        public override string DefaultSubCommandTrigger => "lockedcmd";

        /// <inheritdoc/>
        public override IReadOnlyList<string> OptionalTopLevelTriggers { get; } = ["lockedcmd"];

        /// <inheritdoc/>
        public override bool AllowCustomOverride => false;

        /// <inheritdoc/>
        protected override string OutputMessage => "Built-in locked";
    }

    /// <summary>
    /// Counter store stub that throws on <see cref="AdjustAsync"/> to test automatic loyalty refund on execution exception.
    /// </summary>
    private sealed class ThrowingCounterStore : ICounterStore
    {
        /// <inheritdoc/>
        public ValueTask<WorkspaceCounter?> GetAsync(Guid workspaceId, string counterName, CounterScopeMode scopeMode = CounterScopeMode.Global, string gameCategory = "", CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<WorkspaceCounter?>(null);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<WorkspaceCounter>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceCounter>>([]);

        /// <inheritdoc/>
        public ValueTask<WorkspaceCounter> AdjustAsync(Guid workspaceId, string counterName, long delta, CounterScopeMode scopeMode = CounterScopeMode.Global, string gameCategory = "", CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated counter persistence failure.");

        /// <inheritdoc/>
        public ValueTask<WorkspaceCounter> SetAsync(Guid workspaceId, string counterName, long value, CounterScopeMode scopeMode = CounterScopeMode.Global, string gameCategory = "", CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated counter persistence failure.");

        /// <inheritdoc/>
        public ValueTask<bool> DeleteAsync(Guid workspaceId, string counterName, CounterScopeMode scopeMode = CounterScopeMode.Global, string gameCategory = "", CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }
}
