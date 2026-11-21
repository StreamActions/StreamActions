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
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Templates;
using System.Text;
using Xunit;

namespace StreamActions.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="TagRegistry"/>, <see cref="TagResult"/>, <see cref="CompositeNode"/>, <see cref="TagNode"/>, and <see cref="TemplateEvaluator"/>.
/// </summary>
public sealed class TagRegistryAndEvaluatorTests
{
    #region TagResult

    [Fact]
    [Trait("Member", "TagResult")]
    public void TagResultFactoryMethodsCreateExpectedStates()
    {
        TagResult incomplete = TagResult.Incomplete;
        TagResult completeEmpty = TagResult.Complete(string.Empty);
        TagResult fromNull = TagResult.FromNonEmpty(null);
        TagResult fromEmpty = TagResult.FromNonEmpty(string.Empty);
        TagResult fromValue = TagResult.FromNonEmpty("hello");

        _ = incomplete.IsComplete.Should().BeFalse();
        _ = incomplete.Value.Should().BeEmpty();

        _ = completeEmpty.IsComplete.Should().BeTrue();
        _ = completeEmpty.Value.Should().BeEmpty();

        _ = fromNull.IsComplete.Should().BeFalse();
        _ = fromEmpty.IsComplete.Should().BeFalse();

        _ = fromValue.IsComplete.Should().BeTrue();
        _ = fromValue.Value.Should().Be("hello");
    }

    #endregion

    #region Resolve

    [Fact]
    [Trait("Member", "Resolve")]
    public void ResolvePlatformAndUniversalLayersPrefersPlatformOverrideCaseInsensitively()
    {
        TagRegistry registry = new(
        [
            new StubTagHandler("game", null, "universal"),
            new StubTagHandler("game", "twitch", "twitch-only"),
            new StubTagHandler("discord_only", "discord", "discord-val"),
        ]);

        ICommandTagHandler? twitchGame = registry.Resolve("GAME", "TWITCH");
        ICommandTagHandler? youtubeGame = registry.Resolve("game", "youtube");
        ICommandTagHandler? universalGame = registry.Resolve("game", null);
        ICommandTagHandler? discordOnTwitch = registry.Resolve("discord_only", "twitch");

        _ = twitchGame.Should().NotBeNull();
        _ = twitchGame!.PlatformId.Should().Be("twitch");
        _ = youtubeGame.Should().NotBeNull();
        _ = youtubeGame!.PlatformId.Should().BeNull();
        _ = universalGame.Should().NotBeNull();
        _ = universalGame!.PlatformId.Should().BeNull();
        _ = discordOnTwitch.Should().BeNull();

        _ = registry.GetRegisteredTagNames(null).Should().Equal("game");
        _ = registry.GetRegisteredTagNames("discord").Should().Equal("discord_only", "game");
    }

    #endregion

    #region EvaluateAsync

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncCompositeNodeWithIncompleteChildBubblesIncomplete()
    {
        TemplateParseResult parsed = TemplateParser.Parse("Hello {1}!");
        TagEvaluationContext context = TemplateTestHarness.CreateContext(
            TemplateTestHarness.CreateChatCommand(arguments: []));

        TagResult result = await TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: TestContext.Current.CancellationToken);

        _ = result.IsComplete.Should().BeFalse();
        _ = result.Value.Should().Be("Hello !");
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncSecondOrderTagInjectionInArgumentDoesNotReParseOrExecuteTags()
    {
        TemplateTestHarness.InMemoryCounterStore counterStore = new();
        ChatCommand command = TemplateTestHarness.CreateChatCommand(arguments: ["{sender} {count \"+1\" \"deaths\"}"]);
        TagEvaluationContext context = TemplateTestHarness.CreateContext(
            command,
            dependencies: new TagEvaluationDependencies { CounterStore = counterStore });

        TemplateParseResult parsed = TemplateParser.Parse("Echo: {1}");
        TagResult result = await TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: TestContext.Current.CancellationToken);

        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().Be("Echo: {sender} {count \"+1\" \"deaths\"}");
        _ = counterStore.MutationCount.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncOrShortCircuitsLazilyDoesNotExecuteSkippedBranch()
    {
        TemplateTestHarness.InMemoryCounterStore counterStore = new();
        ChatCommand command = TemplateTestHarness.CreateChatCommand(arguments: ["Alice"]);
        TagEvaluationContext context = TemplateTestHarness.CreateContext(
            command,
            dependencies: new TagEvaluationDependencies { CounterStore = counterStore });

        TemplateParseResult parsed = TemplateParser.Parse("{or {1} {count \"+1\" \"deaths\"}}");
        TagResult result = await TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: TestContext.Current.CancellationToken);

        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().Be("Alice");
        _ = counterStore.MutationCount.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncIfShortCircuitsLazilyAndBranchesOnIncompleteCondition()
    {
        TemplateTestHarness.InMemoryCounterStore counterStore = new();

        ChatCommand matchedCommand = TemplateTestHarness.CreateChatCommand(arguments: ["go"]);
        TagEvaluationContext matchedContext = TemplateTestHarness.CreateContext(
            matchedCommand,
            dependencies: new TagEvaluationDependencies { CounterStore = counterStore });

        TemplateParseResult parsed = TemplateParser.Parse("{if {eq {1} \"go\"} \"ok\" {count \"+1\" \"deaths\"}}");
        TagResult matchedResult = await TemplateEvaluator.EvaluateAsync(parsed.Root, matchedContext, cancellationToken: TestContext.Current.CancellationToken);

        _ = matchedResult.IsComplete.Should().BeTrue();
        _ = matchedResult.Value.Should().Be("ok");
        _ = counterStore.MutationCount.Should().Be(0);

        ChatCommand missingArgCommand = TemplateTestHarness.CreateChatCommand(arguments: []);
        TagEvaluationContext incompleteCondContext = TemplateTestHarness.CreateContext(
            missingArgCommand,
            dependencies: new TagEvaluationDependencies { CounterStore = counterStore });

        TemplateParseResult incompleteBranch = TemplateParser.Parse("{if {1} {count \"+1\" \"deaths\"} \"fallback\"}");
        TagResult fallbackResult = await TemplateEvaluator.EvaluateAsync(incompleteBranch.Root, incompleteCondContext, cancellationToken: TestContext.Current.CancellationToken);

        _ = fallbackResult.IsComplete.Should().BeTrue();
        _ = fallbackResult.Value.Should().Be("fallback");
        _ = counterStore.MutationCount.Should().Be(0);
    }

    [Theory]
    [InlineData("{if {eq \"Hi\" \"hi\"} \"yes\" \"no\"}", "yes")]
    [InlineData("{if {ne \"a\" \"b\"} \"yes\" \"no\"}", "yes")]
    [InlineData("{if {gt 10 2} \"yes\" \"no\"}", "yes")]
    [InlineData("{if {lt 2 10} \"yes\" \"no\"}", "yes")]
    [InlineData("{if {contains \"hello world\" \"WORLD\"} \"yes\" \"no\"}", "yes")]
    [InlineData("{if eq \"x\" \"x\" \"yes\" \"no\"}", "yes")]
    [InlineData("{if 15 gt 10 \"yes\" \"no\"}", "yes")]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncIfBinaryOperatorsEvaluateExpectedBranch(string template, string expected)
    {
        TagEvaluationContext context = TemplateTestHarness.CreateContext();
        TemplateParseResult parsed = TemplateParser.Parse(template);

        TagResult result = await TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: TestContext.Current.CancellationToken);

        _ = result.IsComplete.Should().BeTrue();
        _ = result.Value.Should().Be(expected);
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncExceedingMaxTagEvaluationsThrowsTagEvaluationException()
    {
        StringBuilder template = new();
        for (int i = 0; i < TemplateExecutionLimits.MaxTagEvaluationsPerExecution + 1; i++)
        {
            _ = template.Append("{sender}");
        }

        TemplateParseResult parsed = TemplateParser.Parse(template.ToString());
        TagEvaluationContext context = TemplateTestHarness.CreateContext();

        Func<Task> act = () => TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: TestContext.Current.CancellationToken).AsTask();

        TagEvaluationException ex = (await act.Should().ThrowAsync<TagEvaluationException>().ConfigureAwait(true)).Which;
        _ = ex.Reason.Should().Be(TagEvaluationFailureReason.MaxTagEvaluationsExceeded);
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncExceedingMaxEvaluationBufferCharsThrowsTagEvaluationException()
    {
        string chunk = new('a', 5_000);
        CompositeNode node = new(
        [
            new LiteralNode(chunk),
            new LiteralNode(chunk),
            new LiteralNode(chunk),
            new LiteralNode(chunk),
        ]);
        TagEvaluationContext context = TemplateTestHarness.CreateContext();

        Func<Task> act = () => TemplateEvaluator.EvaluateAsync(node, context, cancellationToken: TestContext.Current.CancellationToken).AsTask();

        TagEvaluationException ex = (await act.Should().ThrowAsync<TagEvaluationException>().ConfigureAwait(true)).Which;
        _ = ex.Reason.Should().Be(TagEvaluationFailureReason.MaxEvaluationBufferExceeded);
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncManualAstExceedingMaxDepthThrowsTagEvaluationException()
    {
        TemplateNode current = new TagNode("sender", []);
        for (int i = 0; i < TemplateExecutionLimits.MaxAstNestingDepth; i++)
        {
            current = new TagNode("upper", [current]);
        }

        TagEvaluationContext context = TemplateTestHarness.CreateContext();

        Func<Task> act = () => TemplateEvaluator.EvaluateAsync(current, context, cancellationToken: TestContext.Current.CancellationToken).AsTask();

        TagEvaluationException ex = (await act.Should().ThrowAsync<TagEvaluationException>().ConfigureAwait(true)).Which;
        _ = ex.Reason.Should().Be(TagEvaluationFailureReason.MaxAstNestingDepthExceeded);
    }

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncSlowHandlerExceedingTimeoutThrowsTimeoutTagEvaluationException()
    {
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        TagRegistry registry = new(
        [
            new DelayedTagHandler("slow", TimeSpan.FromSeconds(10), fakeTime),
        ]);
        TagEvaluationContext context = TemplateTestHarness.CreateContext(registry: registry, timeProvider: fakeTime);
        TemplateParseResult parsed = TemplateParser.Parse("{slow}");

        ValueTask<TagResult> evalTask = TemplateEvaluator.EvaluateAsync(parsed.Root, context, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        fakeTime.Advance(TimeSpan.FromSeconds(5));

        Func<Task> act = () => evalTask.AsTask();

        TagEvaluationException ex = (await act.Should().ThrowAsync<TagEvaluationException>().ConfigureAwait(true)).Which;
        _ = ex.Reason.Should().Be(TagEvaluationFailureReason.Timeout);
    }

    [Fact]
    [Trait("Member", "GetOrComputeMemoizedAsync")]
    public async Task GetOrComputeMemoizedAsyncRepeatedKeyInvokesFactoryOnce()
    {
        TagEvaluationContext context = TemplateTestHarness.CreateContext();
        int invocations = 0;

        string first = await context.GetOrComputeMemoizedAsync(
            "shared-key",
            _ =>
            {
                invocations++;
                return ValueTask.FromResult("computed-value");
            },
            TestContext.Current.CancellationToken);

        string second = await context.GetOrComputeMemoizedAsync(
            "shared-key",
            _ =>
            {
                invocations++;
                return ValueTask.FromResult("should-not-run");
            },
            TestContext.Current.CancellationToken);

        _ = first.Should().Be("computed-value");
        _ = second.Should().Be("computed-value");
        _ = invocations.Should().Be(1);
    }

    #endregion

    /// <summary>
    /// Simple stub tag handler returning a fixed string.
    /// </summary>
    /// <param name="tagName">The tag identifier.</param>
    /// <param name="platformId">Optional platform scope.</param>
    /// <param name="output">Fixed output string.</param>
    private sealed class StubTagHandler(string tagName, string? platformId, string output) : ICommandTagHandler
    {
        /// <inheritdoc />
        public string TagName { get; } = tagName;

        /// <inheritdoc />
        public string? PlatformId { get; } = platformId;

        /// <inheritdoc />
        public ValueTask<TagResult> EvaluateAsync(
            ITagEvaluationContext context,
            IReadOnlyList<TemplateNode> unevaluatedArgs,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(TagResult.Complete(output));
    }

    /// <summary>
    /// Delayed tag handler driven by <see cref="TimeProvider"/> for deterministic timeout tests.
    /// </summary>
    /// <param name="tagName">The tag identifier.</param>
    /// <param name="delay">The simulated execution delay.</param>
    /// <param name="timeProvider">The fake time provider.</param>
    private sealed class DelayedTagHandler(string tagName, TimeSpan delay, TimeProvider timeProvider) : ICommandTagHandler
    {
        /// <inheritdoc />
        public string TagName { get; } = tagName;

        /// <inheritdoc />
        public async ValueTask<TagResult> EvaluateAsync(
            ITagEvaluationContext context,
            IReadOnlyList<TemplateNode> unevaluatedArgs,
            CancellationToken cancellationToken)
        {
            await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            return TagResult.Complete("done");
        }
    }
}
