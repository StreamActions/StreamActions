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
using StreamActions.Core.Actions;
using StreamActions.Core.Templates;
using Xunit;

namespace StreamActions.Core.Tests.Actions;

public sealed class ActionBlockConditionEvaluatorTests
{
    #region EvaluateComparison

    [Theory]
    [Trait("Member", "EvaluateComparison")]
    [InlineData("42", ActionBlockComparisonOperator.Equals, "42.0", true)]
    [InlineData("Vip", ActionBlockComparisonOperator.Equals, "vip", true)]
    [InlineData("abc", ActionBlockComparisonOperator.NotEquals, "xyz", true)]
    [InlineData("10", ActionBlockComparisonOperator.GreaterThan, "2", true)]
    [InlineData("10", ActionBlockComparisonOperator.GreaterThanOrEqual, "10", true)]
    [InlineData("2", ActionBlockComparisonOperator.LessThan, "10", true)]
    [InlineData("5", ActionBlockComparisonOperator.LessThanOrEqual, "5", true)]
    [InlineData("Hello StreamActions", ActionBlockComparisonOperator.Contains, "streamactions", true)]
    [InlineData("Hello StreamActions", ActionBlockComparisonOperator.NotContains, "offline", true)]
    [InlineData("VIP", ActionBlockComparisonOperator.In, "[\"Subscriber\",\"VIP\",\"Moderator\"]", true)]
    [InlineData("Follower", ActionBlockComparisonOperator.NotIn, "Subscriber, VIP", true)]
    [InlineData("sub_tier_3000", ActionBlockComparisonOperator.MatchesRegex, "^sub_tier_[123]000$", true)]
    [InlineData("guest", ActionBlockComparisonOperator.NotMatchesRegex, "^mod_.*$", true)]
    [InlineData("10", ActionBlockComparisonOperator.GreaterThan, "20", false)]
    [InlineData("abc", ActionBlockComparisonOperator.Contains, "zzz", false)]
    public void EvaluateComparisonAllTwelveOperatorsReturnsExpectedResult(
        string left,
        ActionBlockComparisonOperator op,
        string right,
        bool expected)
    {
        bool result = ActionBlockConditionEvaluator.EvaluateComparison(left, op, right);

        _ = result.Should().Be(expected);
    }

    #endregion

    #region EvaluateAsync

    [Fact]
    [Trait("Member", "EvaluateAsync")]
    public async Task EvaluateAsyncWhenStructuredClausesUseIndentGroupingAndCombinatorsEvaluatesCorrectly()
    {
        ActionBlockConditionGroup group = new(
            Clauses:
            [
                new("Stream.IsOnline", ActionBlockComparisonOperator.Equals, "true", ActionBlockLogicalCombinator.And, IndentLevel: 0),
                new("User.Designation", ActionBlockComparisonOperator.In, "VIP, Moderator", ActionBlockLogicalCombinator.Or, IndentLevel: 1),
                new("Tier", ActionBlockComparisonOperator.GreaterThanOrEqual, "2", ActionBlockLogicalCombinator.And, IndentLevel: 1),
            ],
            UseFreeform: false);

        Dictionary<string, string> state = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Stream.IsOnline"] = "true",
            ["User.Designation"] = "Follower",
            ["Tier"] = "3",
        };

        bool result = await ActionBlockConditionEvaluator.EvaluateAsync(
            group,
            (expr, _) => ValueTask.FromResult(state.GetValueOrDefault(expr, expr)),
            TestContext.Current.CancellationToken);

        _ = result.Should().BeTrue();

        state["Stream.IsOnline"] = "false";
        bool offlineResult = await ActionBlockConditionEvaluator.EvaluateAsync(
            group,
            (expr, _) => ValueTask.FromResult(state.GetValueOrDefault(expr, expr)),
            TestContext.Current.CancellationToken);

        _ = offlineResult.Should().BeFalse();
    }

    [Theory]
    [Trait("Member", "EvaluateAsync")]
    [InlineData("Stream.IsOnline == true AND (User.Designation In [\"VIP\",\"Moderator\"] OR Tier >= 2)", true)]
    [InlineData("NOT (Stream.Game == \"Minecraft\") AND Bits > 100", true)]
    [InlineData("Stream.Game == \"Just Chatting\" AND Bits < 50", false)]
    public async Task EvaluateAsyncWhenFreeformModeEnabledEvaluatesParenthesesAndOperators(
        string expression,
        bool expected)
    {
        ActionBlockConditionGroup group = new(
            Clauses: [],
            FreeformExpression: expression,
            UseFreeform: true);

        Dictionary<string, string> state = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Stream.IsOnline"] = "true",
            ["Stream.Game"] = "Just Chatting",
            ["User.Designation"] = "Follower",
            ["Tier"] = "2",
            ["Bits"] = "500",
        };

        bool result = await ActionBlockConditionEvaluator.EvaluateAsync(
            group,
            (expr, _) => ValueTask.FromResult(state.GetValueOrDefault(expr, expr)),
            TestContext.Current.CancellationToken);

        _ = result.Should().Be(expected);
    }

    #endregion

    #region Validate

    [Fact]
    [Trait("Member", "Validate")]
    public void ValidateWhenFreeformHasInvalidSyntaxOrUnknownTagReturnsDiagnostics()
    {
        TagRegistry registry = new(CoreTagRegistryFactory.CreateDefaultHandlers());

        ActionBlockConditionGroup invalidSyntax = new([], FreeformExpression: "(Stream.IsOnline == true", UseFreeform: true);
        ActionBlockConditionGroup invalidTag = new([], FreeformExpression: "{sneder} == \"alice\"", UseFreeform: true);
        ActionBlockConditionGroup invalidRegex = new([new("msg", ActionBlockComparisonOperator.MatchesRegex, "[unclosed")]);

        ConditionValidationResult syntaxResult = ActionBlockConditionEvaluator.Validate(invalidSyntax, registry);
        ConditionValidationResult tagResult = ActionBlockConditionEvaluator.Validate(invalidTag, registry);
        ConditionValidationResult regexResult = ActionBlockConditionEvaluator.Validate(invalidRegex, registry);

        _ = syntaxResult.IsValid.Should().BeFalse();
        _ = syntaxResult.Diagnostics.Should().NotBeEmpty();

        _ = tagResult.IsValid.Should().BeFalse();
        _ = tagResult.Diagnostics.Should().Contain(static d => d.Message.Contains("sneder", StringComparison.OrdinalIgnoreCase));

        _ = regexResult.IsValid.Should().BeFalse();
        _ = regexResult.Diagnostics.Should().Contain(static d => d.Message.Contains("regex", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}

