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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Templates;
using StreamActions.Core.Tests.Commands;
using Xunit;

namespace StreamActions.Core.Tests.Templates;

/// <summary>
/// Unit and integration tests for Milestone 09 universal Core <c>{tag}</c> handlers, abstractions, and context extensions.
/// </summary>
public sealed class AdditionalCoreTagHandlersTests
{
    #region ComparisonTags

    /// <summary>
    /// Verifies standalone <c>{eq}</c>, <c>{ne}</c>, <c>{gt}</c>, <c>{lt}</c>, and <c>{contains}</c> evaluation semantics.
    /// </summary>
    /// <param name="template">The template expression.</param>
    /// <param name="expected">The expected boolean string result.</param>
    [Theory]
    [InlineData("{eq \"Alice\" \"alice\"}", "true")]
    [InlineData("{eq \"Alice\" \"Bob\"}", "false")]
    [InlineData("{ne \"Alice\" \"Bob\"}", "true")]
    [InlineData("{ne \"Alice\" \"alice\"}", "false")]
    [InlineData("{gt 10 2}", "true")]
    [InlineData("{gt 2 10}", "false")]
    [InlineData("{gt \"zebra\" \"apple\"}", "true")]
    [InlineData("{lt 2 10}", "true")]
    [InlineData("{lt 10 2}", "false")]
    [InlineData("{lt \"apple\" \"zebra\"}", "true")]
    [InlineData("{contains \"Hello World\" \"world\"}", "true")]
    [InlineData("{contains \"Hello World\" \"missing\"}", "false")]
    [Trait("Member", "ComparisonTagHandler")]
    public async Task ComparisonTagsEvaluateExpectedBooleanResults(string template, string expected)
    {
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext();

        TagResult result = await EvaluateAsync(template, ctx);

        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().Be(expected);
    }

    /// <summary>
    /// Verifies standalone comparison tags return <see cref="TagResult.Incomplete"/> when fewer than two arguments are supplied.
    /// </summary>
    /// <param name="template">The incomplete comparison template.</param>
    [Theory]
    [InlineData("{eq \"onlyone\"}")]
    [InlineData("{ne \"onlyone\"}")]
    [InlineData("{gt 1}")]
    [InlineData("{lt 1}")]
    [InlineData("{contains \"abc\"}")]
    [Trait("Member", "ComparisonTagHandler")]
    public async Task ComparisonTagsWithMissingOperandReturnIncomplete(string template)
    {
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext();

        TagResult result = await EvaluateAsync(template, ctx);

        _ = result.IsComplete.Should().BeFalse();
    }

    #endregion

    #region RankAccountAgeChatterAndTip

    /// <summary>
    /// Verifies <c>{sender_rank}</c> and <c>{rank}</c> resolve via <see cref="ILoyaltyRankProvider"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "SenderRankAndRank")]
    public async Task RankTagsResolveTitlesAndHandleMissingRanksOrProviders()
    {
        StubLoyaltyRankProvider rankProvider = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["user-42"] = "Knight",
            ["bob"] = "Wizard",
        });

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(arguments: [], senderUsername: "alice", senderDisplayName: "Alice"),
            dependencies: new TagEvaluationDependencies { LoyaltyRankProvider = rankProvider });

        _ = (await EvaluateAsync("{sender_rank}", ctx)).Value.Should().Be("Knight Alice");
        _ = (await EvaluateAsync("{rank}", ctx)).Value.Should().Be("Knight");
        _ = (await EvaluateAsync("{rank \"@bob\"}", ctx)).Value.Should().Be("Wizard");
        _ = (await EvaluateAsync("{rank \"unranked\"}", ctx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{rank {1}}", ctx)).IsComplete.Should().BeFalse();

        TagEvaluationContext noRankSenderCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(senderUsername: "newbie", senderDisplayName: "Newbie"),
            dependencies: new TagEvaluationDependencies { LoyaltyRankProvider = new StubLoyaltyRankProvider(new Dictionary<string, string>()) });
        TagResult unrankedSenderResult = await EvaluateAsync("{sender_rank}", noRankSenderCtx);
        _ = unrankedSenderResult.IsComplete.Should().BeTrue();
        _ = unrankedSenderResult.Value.Should().Be("Newbie");

        TagEvaluationContext missingProviderCtx = TemplateTestHarness.CreateContext();
        _ = (await EvaluateAsync("{sender_rank}", missingProviderCtx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{rank \"bob\"}", missingProviderCtx)).IsComplete.Should().BeFalse();
    }

    /// <summary>
    /// Verifies <c>{account_age}</c> formats UTC creation date and elapsed duration deterministically across timezones.
    /// </summary>
    [Fact]
    [Trait("Member", "AccountAgeTagHandler")]
    public async Task AccountAgeTagFormatsUtcDateAndDurationDeterministically()
    {
        DateTimeOffset now = new(2026, 7, 15, 14, 30, 0, TimeSpan.Zero);
        FakeTimeProvider fakeTime = new(now);
        StubPlatformUserLookup lookup = new(new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase)
        {
            ["user-42"] = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            ["bob"] = new(2026, 7, 15, 16, 25, 0, TimeSpan.FromHours(2)),
        });

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { PlatformUserLookup = lookup },
            timeProvider: fakeTime);

        TagResult senderAge = await EvaluateAsync("{account_age}", ctx);
        _ = senderAge.IsComplete.Should().BeTrue();
        _ = senderAge.Value.Should().Be("2026-07-10 (5d 2h 30m 0s)");

        TagResult targetAge = await EvaluateAsync("{account_age \"@bob\"}", ctx);
        _ = targetAge.IsComplete.Should().BeTrue();
        _ = targetAge.Value.Should().Be("2026-07-15 (5m 0s)");

        _ = (await EvaluateAsync("{account_age \"unknown\"}", ctx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{account_age}", TemplateTestHarness.CreateContext())).IsComplete.Should().BeFalse();
    }

    /// <summary>
    /// Verifies <c>{random_chatter}</c> selects from non-lurking active chatters via <see cref="IChannelChatterProvider"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "RandomChatterTagHandler")]
    public async Task RandomChatterTagSelectsActiveNonLurkingChatter()
    {
        StubChannelChatterProvider chatterProvider = new(["Alice", "Bob"], ["Lurker"]);
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { ChannelChatterProvider = chatterProvider });

        TagResult result = await EvaluateAsync("{random_chatter}", ctx);

        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().BeOneOf("Alice", "Bob");
        _ = chatterProvider.LastExcludeLurking.Should().BeTrue();

        TagEvaluationContext emptyCtx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { ChannelChatterProvider = new StubChannelChatterProvider([], ["Lurker"]) });
        _ = (await EvaluateAsync("{random_chatter}", emptyCtx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{random_chatter}", TemplateTestHarness.CreateContext())).IsComplete.Should().BeFalse();
    }

    /// <summary>
    /// Verifies <c>{last_tip}</c> resolves via <see cref="ITipProvider"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "LastTipTagHandler")]
    public async Task LastTipTagResolvesLastTipSummaryOrReturnsIncomplete()
    {
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { TipProvider = new StubTipProvider("Bob tipped $5.00") });

        TagResult result = await EvaluateAsync("{last_tip}", ctx);
        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().Be("Bob tipped $5.00");

        TagEvaluationContext emptyCtx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { TipProvider = new StubTipProvider(null) });
        _ = (await EvaluateAsync("{last_tip}", emptyCtx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{last_tip}", TemplateTestHarness.CreateContext())).IsComplete.Should().BeFalse();
    }

    #endregion

    #region LoyaltyAndEconomy

    /// <summary>
    /// Verifies <c>{points}</c> and <c>{pointname}</c> handle existing profiles, missing profiles, singular/plural rules, and missing stores.
    /// </summary>
    [Fact]
    [Trait("Member", "PointsAndPointName")]
    public async Task PointsAndPointNameTagsEvaluateBalancesAndCurrencyPluralization()
    {
        CommandTestHarness.InMemoryViewerLoyaltyStore loyaltyStore = new();
        Guid wsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _ = await loyaltyStore.GetOrCreateProfileAsync(wsId, "twitch", "bob-id", "bob", "Bob", TestContext.Current.CancellationToken);
        _ = await loyaltyStore.AdjustPointsAsync(wsId, "twitch", "bob-id", 150, TestContext.Current.CancellationToken);

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(workspaceId: wsId),
            dependencies: new TagEvaluationDependencies { ViewerLoyaltyStore = loyaltyStore });

        // Sender with no profile yet defaults to "0"
        TagResult senderZero = await EvaluateAsync("{points}", ctx);
        _ = senderZero.IsComplete.Should().BeTrue();
        _ = senderZero.Value.Should().Be("0");

        // Lookup by login for target user
        TagResult bobPoints = await EvaluateAsync("{points \"@bob\"}", ctx);
        _ = bobPoints.IsComplete.Should().BeTrue();
        _ = bobPoints.Value.Should().Be("150");

        // Missing store -> Incomplete
        _ = (await EvaluateAsync("{points}", TemplateTestHarness.CreateContext())).IsComplete.Should().BeFalse();

        // Default pointname ("point" / "points")
        _ = (await EvaluateAsync("{pointname}", ctx)).Value.Should().Be("points");
        _ = (await EvaluateAsync("{pointname 1}", ctx)).Value.Should().Be("point");
        _ = (await EvaluateAsync("{pointname -1}", ctx)).Value.Should().Be("point");
        _ = (await EvaluateAsync("{pointname 0}", ctx)).Value.Should().Be("points");
        _ = (await EvaluateAsync("{pointname 25}", ctx)).Value.Should().Be("points");

        // Custom currency name
        TagEvaluationContext customCtx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { LoyaltySettingsProvider = new StubLoyaltySettingsProvider("star", "stars") });
        _ = (await EvaluateAsync("{pointname 1}", customCtx)).Value.Should().Be("star");
        _ = (await EvaluateAsync("{pointname -1}", customCtx)).Value.Should().Be("star");
        _ = (await EvaluateAsync("{pointname 5}", customCtx)).Value.Should().Be("stars");
    }

    /// <summary>
    /// Verifies <c>{price}</c> and <c>{pay}</c> read command cost and reward points from <see cref="ICommandRepository"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "PriceAndPayTags")]
    public async Task PriceAndPayTagsReadCurrentOrNamedCommandEconomyMetadata()
    {
        Guid wsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        TemplateTestHarness.StubCommandRepository repo = new(
        [
            new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = wsId, Trigger = "spin", CostPoints = 50, RewardPoints = 10, IsEnabled = true },
            new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = wsId, Trigger = "gift", CostPoints = 0, RewardPoints = 75, IsEnabled = true },
        ]);

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(trigger: "spin", workspaceId: wsId),
            dependencies: new TagEvaluationDependencies { CommandRepository = repo });

        _ = (await EvaluateAsync("{price}", ctx)).Value.Should().Be("50");
        _ = (await EvaluateAsync("{pay}", ctx)).Value.Should().Be("10");
        _ = (await EvaluateAsync("{price \"!gift\"}", ctx)).Value.Should().Be("0");
        _ = (await EvaluateAsync("{pay \"gift\"}", ctx)).Value.Should().Be("75");
        _ = (await EvaluateAsync("{price \"unknown\"}", ctx)).IsComplete.Should().BeFalse();
        _ = (await EvaluateAsync("{price}", TemplateTestHarness.CreateContext())).IsComplete.Should().BeFalse();
    }

    /// <summary>
    /// Verifies <c>{points_add}</c>, <c>{points_take}</c>, <c>{points_take_or_cancel}</c>, and <c>{points_transfer}</c>.
    /// </summary>
    [Fact]
    [Trait("Member", "PointsMutationTags")]
    public async Task PointsMutationTagsModifyBalancesAtomicallyAndCancelOnInsufficientFunds()
    {
        Guid wsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        CommandTestHarness.InMemoryViewerLoyaltyStore store = new();
        _ = await store.GetOrCreateProfileAsync(wsId, "twitch", "user-42", "alice", "Alice", TestContext.Current.CancellationToken);
        _ = await store.GetOrCreateProfileAsync(wsId, "twitch", "bob-id", "bob", "Bob", TestContext.Current.CancellationToken);

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(workspaceId: wsId),
            dependencies: new TagEvaluationDependencies { ViewerLoyaltyStore = store });

        TagResult addResult = await EvaluateAsync("{points_add 100}", ctx);
        _ = addResult.IsComplete.Should().BeTrue();
        _ = addResult.Value.Should().BeEmpty();
        _ = store.GetPoints(wsId, "twitch", "user-42").Should().Be(100);

        TagResult takeResult = await EvaluateAsync("{points_take 30 \"@alice\"}", ctx);
        _ = takeResult.IsComplete.Should().BeTrue();
        _ = takeResult.Value.Should().BeEmpty();
        _ = store.GetPoints(wsId, "twitch", "user-42").Should().Be(70);

        // Transfer 25 from sender (alice) to bob
        TagResult transferOk = await EvaluateAsync("{points_transfer 25 \"@bob\"}", ctx);
        _ = transferOk.IsComplete.Should().BeTrue();
        _ = transferOk.Value.Should().BeEmpty();
        _ = store.GetPoints(wsId, "twitch", "user-42").Should().Be(45);
        _ = store.GetPoints(wsId, "twitch", "bob-id").Should().Be(25);

        // Transfer exceeding balance fails without crediting recipient
        TagResult transferFail = await EvaluateAsync("{points_transfer 500 \"bob\" \"alice\"}", ctx);
        _ = transferFail.IsComplete.Should().BeFalse();
        _ = store.GetPoints(wsId, "twitch", "user-42").Should().Be(45);
        _ = store.GetPoints(wsId, "twitch", "bob-id").Should().Be(25);

        // points_take_or_cancel succeeds when balance is sufficient
        TagResult takeOrCancelOk = await EvaluateAsync("{points_take_or_cancel 20}", ctx);
        _ = takeOrCancelOk.IsComplete.Should().BeTrue();
        _ = ctx.IsCanceled.Should().BeFalse();
        _ = store.GetPoints(wsId, "twitch", "user-42").Should().Be(25);

        // points_take_or_cancel cancels context and returns Incomplete when insufficient
        TagResult takeOrCancelFail = await EvaluateAsync("{points_take_or_cancel 50}Should not run", ctx);
        _ = takeOrCancelFail.IsComplete.Should().BeFalse();
        _ = ctx.IsCanceled.Should().BeTrue();
        _ = store.GetPoints(wsId, "twitch", "user-42").Should().Be(25);
    }

    #endregion

    #region KvAppendAndRedemptionContext

    /// <summary>
    /// Verifies <c>{kv_append}</c> creates and appends newline-separated entries in <see cref="IWorkspaceKeyValueStore"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "KvAppendTagHandler")]
    public async Task KvAppendTagCreatesAndAppendsNewlineSeparatedEntries()
    {
        TemplateTestHarness.InMemoryKeyValueStore kvStore = new();
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { KeyValueStore = kvStore });

        TagResult first = await EvaluateAsync("{kv_append files \"quotes.txt\" lines \"Line 1\"}", ctx);
        TagResult second = await EvaluateAsync("{kv_append files \"quotes.txt\" lines \"Line 2\"}", ctx);
        TagResult read = await EvaluateAsync("{kv_get files \"quotes.txt\" lines}", ctx);

        _ = first.IsComplete.Should().BeTrue();
        _ = first.Value.Should().BeEmpty();
        _ = second.IsComplete.Should().BeTrue();
        _ = second.Value.Should().BeEmpty();
        _ = read.Value.Should().Be("Line 1\nLine 2");

        _ = (await EvaluateAsync("{kv_append files s k v}", TemplateTestHarness.CreateContext())).IsComplete.Should().BeFalse();
    }

    /// <summary>
    /// Verifies <see cref="ChannelPointRedemptionContext"/> is exposed on <see cref="ITagEvaluationContext"/> and <see cref="TagEvaluationContext"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "ChannelPointRedemptionContext")]
    public void TagEvaluationContextExposesChannelPointRedemptionContextAndResolvesServicesViaDi()
    {
        ChannelPointRedemptionContext redemption = new(
            "red-1",
            "rew-1",
            "Hydrate",
            500,
            "user-42",
            "alice",
            "Alice",
            "UNFULFILLED",
            "Drink water",
            "Stay hydrated!");

        ServiceCollection services = new();
        _ = services.AddSingleton<ITipProvider>(new StubTipProvider("DI Tip"));
        using ServiceProvider provider = services.BuildServiceProvider();

        TagEvaluationContext ctx = new(
            TemplateTestHarness.CreateChatCommand(),
            CoreTagRegistryFactory.CreateDefaultRegistry(),
            serviceProvider: provider,
            channelPointRedemption: redemption);

        _ = typeof(ITagEvaluationContext).GetProperty(nameof(ITagEvaluationContext.ChannelPointRedemption)).Should().NotBeNull();
        _ = ctx.Should().BeAssignableTo<ITagEvaluationContext>();
        _ = ctx.ChannelPointRedemption.Should().Be(redemption);
        _ = ctx.GetService<ITipProvider>().Should().NotBeNull();
    }

    /// <summary>
    /// Verifies <see cref="CommandDispatcher"/> passes <see cref="IViewerLoyaltyStore"/> into <see cref="TagEvaluationDependencies"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "CommandDispatcher")]
    public async Task CommandDispatcherWiresViewerLoyaltyStoreIntoTagEvaluationDependencies()
    {
        await using CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        CancellationToken ct = TestContext.Current.CancellationToken;

        _ = await rig.LoyaltyStore.GetOrCreateProfileAsync(
            CommandTestHarness.DefaultWorkspaceId,
            "twitch",
            "viewer-1",
            "viewer1",
            "ViewerOne",
            ct);
        _ = await rig.LoyaltyStore.AdjustPointsAsync(
            CommandTestHarness.DefaultWorkspaceId,
            "twitch",
            "viewer-1",
            325,
            ct);

        await rig.CommandRepository.UpsertAsync(
            new CustomCommand
            {
                Id = Guid.NewGuid(),
                WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                Trigger = "mypoints",
                CostPoints = 25,
                RewardPoints = 0,
                RoutineJson = "You have {points} {pointname {points}} (cost {price}).",
                IsEnabled = true,
            },
            ct);

        ChatCommand invocation = CommandTestHarness.CreateChatCommand(
            trigger: "mypoints",
            userId: "viewer-1",
            username: "viewer1",
            displayName: "ViewerOne");

        CommandDispatchResult result = await rig.Dispatcher.DispatchAsync(invocation, ct);

        _ = result.Executed.Should().BeTrue();
        _ = rig.OutboundDispatcher.Messages.Should().ContainSingle();
        _ = rig.OutboundDispatcher.Messages[0].Message.Should().Be("You have 300 points (cost 25).");
    }

    #endregion

    #region CompatibilityActionTags

    /// <summary>
    /// Verifies <c>{delaycommand}</c> and <c>{delaysay}</c> enforce the max 3 delayed actions cap, chain depth, and cycle prevention.
    /// </summary>
    [Fact]
    [Trait("Member", "DelayedActionTags")]
    public async Task DelayCommandAndDelaySayEnforceMaxThreeDelayedActionsDepthAndCycleGuards()
    {
        RecordingDelayedCommandScheduler cmdScheduler = new();
        RecordingCrossPlatformMessageDispatcher msgDispatcher = new();
        TagEvaluationDependencies deps = new()
        {
            DelayedCommandScheduler = cmdScheduler,
            CrossPlatformMessageDispatcher = msgDispatcher,
        };

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(trigger: "rootcmd"),
            dependencies: deps);

        // 2 delaysays + 1 delaycommand = 3 allowed; 4th delayed action is rejected
        TagResult combined = await EvaluateAsync(
            "{delaysay 2 \"First\"}{delaycommand 5 nextcmd arg1}{delaysay 10 \"Third\"}{delaysay 15 \"Fourth\"}{delaycommand 20 extra}",
            ctx);

        _ = combined.IsComplete.Should().BeFalse();
        _ = ctx.DelayedActionCount.Should().Be(TemplateExecutionLimits.MaxDelayedActionsPerExecution);
        _ = msgDispatcher.ScheduledMessages.Should().HaveCount(2);
        _ = msgDispatcher.ScheduledMessages[0].Delay.Should().Be(TimeSpan.FromSeconds(2));
        _ = msgDispatcher.ScheduledMessages[0].Message.Should().Be("First");
        _ = msgDispatcher.ScheduledMessages[1].Delay.Should().Be(TimeSpan.FromSeconds(10));
        _ = cmdScheduler.ScheduledCommands.Should().ContainSingle();
        _ = cmdScheduler.ScheduledCommands[0].Delay.Should().Be(TimeSpan.FromSeconds(5));
        _ = cmdScheduler.ScheduledCommands[0].Invocation.Trigger.Should().Be("nextcmd");
        _ = cmdScheduler.ScheduledCommands[0].Invocation.Arguments.Should().Equal("arg1");

        // Cyclic self-invocation rejected
        TagEvaluationContext cycleCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(trigger: "selfcmd"),
            dependencies: deps);
        _ = (await EvaluateAsync("{delaycommand 1 selfcmd}", cycleCtx)).IsComplete.Should().BeFalse();
        _ = cmdScheduler.ScheduledCommands.Should().ContainSingle();

        // MaxCommandChainDepth rejected
        TagEvaluationContext maxDepthCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(trigger: "deep"),
            dependencies: deps,
            chainDepth: TemplateExecutionLimits.MaxCommandChainDepth);
        _ = (await EvaluateAsync("{delaycommand 1 leaf}", maxDepthCtx)).IsComplete.Should().BeFalse();
    }

    /// <summary>
    /// Verifies <c>{twitchsay}</c> and <c>{discordsay}</c> dispatch messages via <see cref="ICrossPlatformMessageDispatcher"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "CrossPlatformSayTags")]
    public async Task TwitchSayAndDiscordSayDispatchCrossPlatformMessages()
    {
        RecordingCrossPlatformMessageDispatcher dispatcher = new();
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(channelName: "streamer_chan"),
            dependencies: new TagEvaluationDependencies { CrossPlatformMessageDispatcher = dispatcher });

        TagResult twitchResult = await EvaluateAsync("{twitchsay \"Hello Twitch!\"}", ctx);
        TagResult discordResult = await EvaluateAsync("{discordsay \"general\" \"Hello Discord!\"}", ctx);

        _ = twitchResult.IsComplete.Should().BeTrue();
        _ = twitchResult.Value.Should().BeEmpty();
        _ = discordResult.IsComplete.Should().BeTrue();
        _ = discordResult.Value.Should().BeEmpty();

        _ = dispatcher.ImmediateMessages.Should().HaveCount(2);
        _ = dispatcher.ImmediateMessages[0].Should().Be(("twitch", "streamer_chan", "Hello Twitch!"));
        _ = dispatcher.ImmediateMessages[1].Should().Be(("discord", "general", "Hello Discord!"));
    }

    /// <summary>
    /// Verifies <c>{alert}</c>, <c>{alerttext}</c>, and <c>{playsound}</c> dispatch valid payloads and reject XSS/CSS breakouts.
    /// </summary>
    [Fact]
    [Trait("Member", "OverlayAlertTags")]
    public async Task AlertAlertTextAndPlaySoundDispatchAndRejectUnsafeCssOrXss()
    {
        RecordingOverlayAlertDispatcher overlayDispatcher = new();
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { OverlayAlertDispatcher = overlayDispatcher });

        TagResult alertOk = await EvaluateAsync("{alert \"horn.webm\" 4.5 0.8 \"color: #fff;\" \"Sub hype!\"}", ctx);
        TagResult alertTextOk = await EvaluateAsync("{alerttext -d 6 -c \"font-size: 24px;\" \"Welcome!\" }", ctx);
        TagResult soundOk = await EvaluateAsync("{playsound \"airhorn|0.5\"}", ctx);

        _ = alertOk.IsComplete.Should().BeTrue();
        _ = alertTextOk.IsComplete.Should().BeTrue();
        _ = soundOk.IsComplete.Should().BeTrue();

        _ = overlayDispatcher.Alerts.Should().ContainSingle().Which.Should().Be(("horn.webm", 4.5d, 0.8d, "color: #fff;", "Sub hype!"));
        _ = overlayDispatcher.AlertTexts.Should().ContainSingle().Which.Should().Be(("Welcome!", 6d, "font-size: 24px;"));
        _ = overlayDispatcher.Sounds.Should().ContainSingle().Which.Should().Be(("airhorn", 0.5d));
    }

    /// <summary>
    /// Verifies <c>{alert}</c> and <c>{alerttext}</c> reject <c>&lt;/style&gt;</c>, <c>@import</c>, <c>javascript:</c>, and <c>&lt;script</c>.
    /// </summary>
    /// <param name="unsafePayload">The unsafe CSS or HTML payload.</param>
    [Theory]
    [InlineData("</style><script>alert(1)</script>")]
    [InlineData("@import url('https://evil.example/a.css');")]
    [InlineData("background: url(javascript:alert(1))")]
    [InlineData("<script src='x'>")]
    [Trait("Member", "OverlayAlertTags")]
    public async Task AlertTagsRejectUnsafeCssAndMessagePayloads(string unsafePayload)
    {
        RecordingOverlayAlertDispatcher overlayDispatcher = new();
        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            dependencies: new TagEvaluationDependencies { OverlayAlertDispatcher = overlayDispatcher });

        TagResult badAlertCss = await EvaluateAsync($"{{alert \"clip.webm\" 5 1 \"{unsafePayload}\" \"ok\"}}", ctx);
        TagResult badAlertMsg = await EvaluateAsync($"{{alert \"clip.webm\" 5 1 \"color:red\" \"{unsafePayload}\"}}", ctx);
        TagResult badAlertTextCss = await EvaluateAsync($"{{alerttext -c \"{unsafePayload}\" \"hi\"}}", ctx);
        TagResult badAlertTextMsg = await EvaluateAsync($"{{alerttext \"{unsafePayload}\"}}", ctx);

        _ = badAlertCss.IsComplete.Should().BeFalse();
        _ = badAlertMsg.IsComplete.Should().BeFalse();
        _ = badAlertTextCss.IsComplete.Should().BeFalse();
        _ = badAlertTextMsg.IsComplete.Should().BeFalse();
        _ = overlayDispatcher.Alerts.Should().BeEmpty();
        _ = overlayDispatcher.AlertTexts.Should().BeEmpty();
    }

    #endregion

    /// <summary>
    /// Helper to parse and evaluate a template string against <paramref name="context"/>.
    /// </summary>
    /// <param name="template">The raw template string.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    private static ValueTask<TagResult> EvaluateAsync(string template, TagEvaluationContext context)
    {
        TemplateParseResult parsed = TemplateParser.Parse(template, context.TagRegistry, context.PlatformId);
        return TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Stub <see cref="ILoyaltyRankProvider"/> returning rank titles from an in-memory map.
    /// </summary>
    /// <param name="ranks">Map of user ID or login to rank title.</param>
    private sealed class StubLoyaltyRankProvider(IReadOnlyDictionary<string, string> ranks) : ILoyaltyRankProvider
    {
        /// <inheritdoc />
        public ValueTask<string?> GetRankTitleAsync(Guid workspaceId, string platformId, string platformUserIdOrLogin, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ranks.TryGetValue(platformUserIdOrLogin, out string? title) ? title : null);
    }

    /// <summary>
    /// Stub <see cref="IPlatformUserLookup"/> returning account creation timestamps from an in-memory map.
    /// </summary>
    /// <param name="createdDates">Map of user ID or login to creation timestamp.</param>
    private sealed class StubPlatformUserLookup(IReadOnlyDictionary<string, DateTimeOffset> createdDates) : IPlatformUserLookup
    {
        /// <inheritdoc />
        public ValueTask<DateTimeOffset?> GetAccountCreatedAtAsync(Guid workspaceId, string platformId, string userIdOrLogin, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<DateTimeOffset?>(createdDates.TryGetValue(userIdOrLogin, out DateTimeOffset created) ? created : null);
    }

    /// <summary>
    /// Stub <see cref="IChannelChatterProvider"/> returning configured active and lurking chatters.
    /// </summary>
    /// <param name="activeChatters">Non-lurking active chatters.</param>
    /// <param name="lurkingChatters">Additional lurking chatters.</param>
    private sealed class StubChannelChatterProvider(IReadOnlyList<string> activeChatters, IReadOnlyList<string> lurkingChatters) : IChannelChatterProvider
    {
        /// <summary>
        /// Gets the last value passed for <c>excludeLurking</c>.
        /// </summary>
        public bool? LastExcludeLurking { get; private set; }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<string>> GetActiveChattersAsync(Guid workspaceId, string platformId, bool excludeLurking = true, CancellationToken cancellationToken = default)
        {
            this.LastExcludeLurking = excludeLurking;
            IReadOnlyList<string> result = excludeLurking
                ? activeChatters
                : [.. activeChatters, .. lurkingChatters];
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Stub <see cref="ITipProvider"/> returning a configurable last tip summary.
    /// </summary>
    /// <param name="summary">The summary string, or <see langword="null"/>.</param>
    private sealed class StubTipProvider(string? summary) : ITipProvider
    {
        /// <inheritdoc />
        public ValueTask<string?> GetLastTipSummaryAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(summary);
    }

    /// <summary>
    /// Stub <see cref="ILoyaltySettingsProvider"/> returning configured currency names.
    /// </summary>
    /// <param name="singular">The singular currency name.</param>
    /// <param name="plural">The plural currency name.</param>
    private sealed class StubLoyaltySettingsProvider(string singular, string plural) : ILoyaltySettingsProvider
    {
        /// <inheritdoc />
        public ValueTask<(string Singular, string Plural)> GetCurrencyNameAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult((singular, plural));
    }

    /// <summary>
    /// Recording <see cref="IDelayedCommandScheduler"/> test double.
    /// </summary>
    private sealed class RecordingDelayedCommandScheduler : IDelayedCommandScheduler
    {
        /// <summary>
        /// Gets all scheduled commands in call order.
        /// </summary>
        public List<(ICommandInvocation Invocation, TimeSpan Delay)> ScheduledCommands { get; } = [];

        /// <inheritdoc />
        public ValueTask ScheduleCommandAsync(ICommandInvocation invocation, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            this.ScheduledCommands.Add((invocation, delay));
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Recording <see cref="ICrossPlatformMessageDispatcher"/> test double.
    /// </summary>
    private sealed class RecordingCrossPlatformMessageDispatcher : ICrossPlatformMessageDispatcher
    {
        /// <summary>
        /// Gets all immediate cross-platform messages in call order.
        /// </summary>
        public List<(string PlatformId, string? Channel, string Message)> ImmediateMessages { get; } = [];

        /// <summary>
        /// Gets all scheduled delayed messages in call order.
        /// </summary>
        public List<(TimeSpan Delay, string PlatformId, string Channel, string Message)> ScheduledMessages { get; } = [];

        /// <inheritdoc />
        public ValueTask SendCrossPlatformMessageAsync(Guid workspaceId, string targetPlatformId, string? targetChannel, string message, CancellationToken cancellationToken = default)
        {
            this.ImmediateMessages.Add((targetPlatformId, targetChannel, message));
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask ScheduleMessageAsync(Guid workspaceId, string platformId, string channelName, TimeSpan delay, string message, CancellationToken cancellationToken = default)
        {
            this.ScheduledMessages.Add((delay, platformId, channelName, message));
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Recording <see cref="IOverlayAlertDispatcher"/> test double.
    /// </summary>
    private sealed class RecordingOverlayAlertDispatcher : IOverlayAlertDispatcher
    {
        /// <summary>
        /// Gets all dispatched media alerts.
        /// </summary>
        public List<(string File, double Duration, double Volume, string? Css, string? Message)> Alerts { get; } = [];

        /// <summary>
        /// Gets all dispatched text alerts.
        /// </summary>
        public List<(string Message, double Duration, string? Css)> AlertTexts { get; } = [];

        /// <summary>
        /// Gets all dispatched soundboard hooks.
        /// </summary>
        public List<(string Hook, double Volume)> Sounds { get; } = [];

        /// <inheritdoc />
        public ValueTask DispatchAlertAsync(Guid workspaceId, string file, double durationSeconds = 5d, double volume = 1d, string? css = null, string? message = null, CancellationToken cancellationToken = default)
        {
            this.Alerts.Add((file, durationSeconds, volume, css, message));
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DispatchAlertTextAsync(Guid workspaceId, string message, double durationSeconds = 5d, string? css = null, CancellationToken cancellationToken = default)
        {
            this.AlertTexts.Add((message, durationSeconds, css));
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DispatchSoundAsync(Guid workspaceId, string hook, double volume = 1d, CancellationToken cancellationToken = default)
        {
            this.Sounds.Add((hook, volume));
            return ValueTask.CompletedTask;
        }
    }
}
