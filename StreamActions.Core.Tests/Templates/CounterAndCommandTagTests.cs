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
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Core.Templates;
using Xunit;

namespace StreamActions.Core.Tests.Templates;

/// <summary>
/// Unit tests for counter tags (<c>{count}</c>, <c>{counter_get}</c>, <c>{counter_total}</c>, <c>{count_reset}</c>) and command tags (<c>{botcmd}</c>, <c>{commands_list}</c>).
/// </summary>
public sealed class CounterAndCommandTagTests
{
    #region CountTag

    [Fact]
    [Trait("Member", "CountTag")]
    public async Task CountTagParameterizedBehaviorHandlesReadMutatePermissionsNoPrefixAndLocalization()
    {
        TemplateTestHarness.InMemoryCounterStore counterStore = new();
        Guid workspaceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        _ = await counterStore.SetAsync(workspaceId, "deaths", 10, CounterScopeMode.Global, string.Empty, TestContext.Current.CancellationToken);

        IStreamActionsLocalizer localizer = CreateRealEnUsLocalizer();
        TagEvaluationDependencies deps = new() { CounterStore = counterStore, Localizer = localizer };
        TemplateNode countAst = TemplateParser.Parse("{count}").Root;
        TemplateNode noPrefixAst = TemplateParser.Parse("{count -noprefix}").Root;

        // 1. No arg -> reads without mutating
        TagEvaluationContext readCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("deaths", [], workspaceId: workspaceId),
            dependencies: deps,
            boundCounterName: "deaths",
            counterUsageBehavior: CounterUsageBehavior.Parameterized,
            canMutateBoundCounter: true);
        TagResult readResult = await TemplateEvaluator.EvaluateAsync(countAst, readCtx, cancellationToken: TestContext.Current.CancellationToken);
        _ = readResult.Value.Should().Be("10");
        _ = readCtx.LastCounterMutation!.Mutated.Should().BeFalse();

        // 2. Unprivileged viewer passing "+" -> reads without mutating
        TagEvaluationContext viewerCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("deaths", ["+"], workspaceId: workspaceId),
            dependencies: deps,
            boundCounterName: "deaths",
            counterUsageBehavior: CounterUsageBehavior.Parameterized,
            canMutateBoundCounter: false);
        TagResult viewerResult = await TemplateEvaluator.EvaluateAsync(countAst, viewerCtx, cancellationToken: TestContext.Current.CancellationToken);
        _ = viewerResult.Value.Should().Be("10");
        _ = viewerCtx.LastCounterMutation!.Mutated.Should().BeFalse();

        // 3. Moderator passing "+" -> increments by 1 + localized prefix "[Increased] "
        TagEvaluationContext incCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("deaths", ["+"], workspaceId: workspaceId),
            dependencies: deps,
            boundCounterName: "deaths",
            counterUsageBehavior: CounterUsageBehavior.Parameterized,
            canMutateBoundCounter: true);
        TagResult incResult = await TemplateEvaluator.EvaluateAsync(countAst, incCtx, cancellationToken: TestContext.Current.CancellationToken);
        _ = incResult.Value.Should().Be("[Increased] 11");
        _ = incCtx.LastCounterMutation!.Kind.Should().Be(CounterMutationKind.Incremented);

        // 4. Moderator passing "-" -> decrements by 1 + localized prefix "[Decreased] "
        TagEvaluationContext decCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("deaths", ["-"], workspaceId: workspaceId),
            dependencies: deps,
            boundCounterName: "deaths",
            counterUsageBehavior: CounterUsageBehavior.Parameterized,
            canMutateBoundCounter: true);
        TagResult decResult = await TemplateEvaluator.EvaluateAsync(countAst, decCtx, cancellationToken: TestContext.Current.CancellationToken);
        _ = decResult.Value.Should().Be("[Decreased] 10");
        _ = decCtx.LastCounterMutation!.Kind.Should().Be(CounterMutationKind.Decremented);

        // 5. Moderator passing non-negative integer -> sets value + localized prefix "[Set] "
        TagEvaluationContext setCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("deaths", ["42"], workspaceId: workspaceId),
            dependencies: deps,
            boundCounterName: "deaths",
            counterUsageBehavior: CounterUsageBehavior.Parameterized,
            canMutateBoundCounter: true);
        TagResult setResult = await TemplateEvaluator.EvaluateAsync(countAst, setCtx, cancellationToken: TestContext.Current.CancellationToken);
        _ = setResult.Value.Should().Be("[Set] 42");
        _ = setCtx.LastCounterMutation!.Kind.Should().Be(CounterMutationKind.Set);

        // 6. -noprefix suppresses prefix
        TagEvaluationContext noPrefixCtx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("deaths", ["+"], workspaceId: workspaceId),
            dependencies: deps,
            boundCounterName: "deaths",
            counterUsageBehavior: CounterUsageBehavior.Parameterized,
            canMutateBoundCounter: true);
        TagResult noPrefixResult = await TemplateEvaluator.EvaluateAsync(noPrefixAst, noPrefixCtx, cancellationToken: TestContext.Current.CancellationToken);
        _ = noPrefixResult.Value.Should().Be("43");
    }

    [Theory]
    [InlineData(CounterUsageBehavior.IncrementOnUse, new string[0], "6")]
    [InlineData(CounterUsageBehavior.DecrementOnUse, new string[0], "4")]
    [InlineData(CounterUsageBehavior.ReadOnly, new string[0], "5")]
    [InlineData(CounterUsageBehavior.SetFromArg1, new[] { "99" }, "99")]
    [InlineData(CounterUsageBehavior.ResetToZero, new string[0], "0")]
    [Trait("Member", "CountTag")]
    public async Task CountTagNonParameterizedBoundBehaviorsBehaveAsExpected(
        CounterUsageBehavior behavior,
        string[] args,
        string expectedOutput)
    {
        TemplateTestHarness.InMemoryCounterStore counterStore = new();
        Guid workspaceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        _ = await counterStore.SetAsync(workspaceId, "wins", 5, CounterScopeMode.Global, string.Empty, TestContext.Current.CancellationToken);

        TagEvaluationContext context = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("wins", args, workspaceId: workspaceId),
            dependencies: new TagEvaluationDependencies { CounterStore = counterStore },
            boundCounterName: "wins",
            counterUsageBehavior: behavior,
            canMutateBoundCounter: true);

        TemplateNode ast = TemplateParser.Parse("{count}").Root;
        TagResult result = await TemplateEvaluator.EvaluateAsync(ast, context, cancellationToken: TestContext.Current.CancellationToken);

        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().Be(expectedOutput);
    }

    [Fact]
    [Trait("Member", "CountTag")]
    public async Task CountTagPerGameScopeAndInlineAdjustmentsWorkCorrectly()
    {
        TemplateTestHarness.InMemoryCounterStore counterStore = new();
        Guid workspaceId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        _ = await counterStore.SetAsync(workspaceId, "deaths", 7, CounterScopeMode.PerGame, "Elden Ring", TestContext.Current.CancellationToken);
        _ = await counterStore.SetAsync(workspaceId, "deaths", 3, CounterScopeMode.PerGame, "Hades", TestContext.Current.CancellationToken);

        TagEvaluationDependencies deps = new()
        {
            CounterStore = counterStore,
            StreamStatusProvider = new TemplateTestHarness.StubStreamStatusProvider("Elden Ring"),
            Localizer = new TemplateTestHarness.StubLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["responses.counter.increased"] = "[Increased] ",
                ["responses.counter.decreased"] = "[Decreased] ",
                ["responses.counter.set"] = "[Set] ",
            }),
        };

        TagEvaluationContext ctx = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("test", workspaceId: workspaceId),
            dependencies: deps);

        TagResult inc = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{count \"+1\" \"deaths\"}").Root, ctx, cancellationToken: TestContext.Current.CancellationToken);
        TagResult dec = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{count -noprefix \"-1\" \"deaths\"}").Root, ctx, cancellationToken: TestContext.Current.CancellationToken);
        TagResult set = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{count \"=5\" \"deaths\"}").Root, ctx, cancellationToken: TestContext.Current.CancellationToken);
        TagResult hadesGet = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{counter_get \"deaths\" \"Hades\"}").Root, ctx, cancellationToken: TestContext.Current.CancellationToken);
        TagResult total = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{counter_total \"deaths\"}").Root, ctx, cancellationToken: TestContext.Current.CancellationToken);
        TagResult reset = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{count_reset \"deaths\"}").Root, ctx, cancellationToken: TestContext.Current.CancellationToken);

        _ = inc.Value.Should().Be("[Increased] 8");
        _ = dec.Value.Should().Be("7");
        _ = set.Value.Should().Be("[Set] 5");
        _ = hadesGet.Value.Should().Be("3");
        _ = total.Value.Should().Be("8");
        _ = reset.Value.Should().Be("0");
    }

    #endregion

    #region BotCmdAndCommandsList

    [Fact]
    [Trait("Member", "BotCmd")]
    public async Task BotCmdEnforcesMaxChainDepthAndCyclePrevention()
    {
        TemplateTestHarness.StubCommandDispatcher dispatcher = new();
        TagEvaluationDependencies deps = new() { CommandDispatcher = dispatcher };
        TemplateNode ast = TemplateParser.Parse("{botcmd shoutout \"bob\"}").Root;

        // Normal depth 0 -> dispatches child command
        TagEvaluationContext okContext = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("welcome"),
            dependencies: deps,
            chainDepth: 0,
            visitedCommandTriggers: ["welcome"]);
        TagResult okResult = await TemplateEvaluator.EvaluateAsync(ast, okContext, cancellationToken: TestContext.Current.CancellationToken);

        _ = okResult.IsComplete.Should().BeTrue();
        _ = dispatcher.Invocations.Should().ContainSingle();
        _ = dispatcher.Invocations[0].Trigger.Should().Be("shoutout");
        _ = dispatcher.Invocations[0].Arguments.Should().Equal("bob");

        // Cycle prevention -> blocked without invoking dispatcher
        dispatcher.Invocations.Clear();
        TagEvaluationContext cycleContext = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("shoutout"),
            dependencies: deps,
            chainDepth: 1,
            visitedCommandTriggers: ["welcome", "shoutout"]);
        TagResult cycleResult = await TemplateEvaluator.EvaluateAsync(ast, cycleContext, cancellationToken: TestContext.Current.CancellationToken);

        _ = cycleResult.IsComplete.Should().BeFalse();
        _ = dispatcher.Invocations.Should().BeEmpty();

        // Depth limit (>= 3) -> blocked without invoking dispatcher
        TagEvaluationContext depthContext = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand("other"),
            dependencies: deps,
            chainDepth: TemplateExecutionLimits.MaxCommandChainDepth,
            visitedCommandTriggers: ["other"]);
        TagResult depthResult = await TemplateEvaluator.EvaluateAsync(ast, depthContext, cancellationToken: TestContext.Current.CancellationToken);

        _ = depthResult.IsComplete.Should().BeFalse();
        _ = dispatcher.Invocations.Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "CommandsList")]
    public async Task CommandsListFiltersEnabledAndPublicCommands()
    {
        Guid workspaceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        TemplateTestHarness.StubCommandRepository repo = new(
        [
            new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Trigger = "so", IsEnabled = true, IsHiddenFromPublicPortal = false },
            new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Trigger = "discord", IsEnabled = true, IsHiddenFromPublicPortal = false },
            new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Trigger = "secret", IsEnabled = true, IsHiddenFromPublicPortal = true },
            new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Trigger = "disabled", IsEnabled = false, IsHiddenFromPublicPortal = false },
        ]);

        TagEvaluationContext context = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(workspaceId: workspaceId),
            dependencies: new TagEvaluationDependencies { CommandRepository = repo });

        TagResult defaultPrefix = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{commands_list}").Root, context, cancellationToken: TestContext.Current.CancellationToken);
        TagResult customPrefix = await TemplateEvaluator.EvaluateAsync(
            TemplateParser.Parse("{commands_list \"?\"}").Root, context, cancellationToken: TestContext.Current.CancellationToken);

        _ = defaultPrefix.IsComplete.Should().BeTrue();
        _ = defaultPrefix.Value.Should().Be("!discord, !so");
        _ = customPrefix.Value.Should().Be("?discord, ?so");
    }

    #endregion

    /// <summary>
    /// Creates a real <see cref="StreamActionsLocalizer"/> backed by the repository's <c>locales</c> directory.
    /// </summary>
    /// <returns>An <see cref="IStreamActionsLocalizer"/> loading <c>locales/en-US/core.json</c>.</returns>
    private static StreamActionsLocalizer CreateRealEnUsLocalizer()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider sp = services.BuildServiceProvider();
        HybridCache cache = sp.GetRequiredService<HybridCache>();
        string localesPath = Path.Combine(AppContext.BaseDirectory, "locales");

        return new StreamActionsLocalizer(
            cache,
            Options.Create(new LocalizationOptions { LocalesDirectory = localesPath, DefaultCulture = "en-US" }),
            NullLogger<StreamActionsLocalizer>.Instance);
    }
}
