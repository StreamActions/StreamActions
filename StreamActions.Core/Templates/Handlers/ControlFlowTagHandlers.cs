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

using System.Globalization;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{or &lt;expr1&gt; &lt;expr2&gt; ...}</c> tag with lazy left-to-right short-circuiting, returning the first complete non-empty result.
/// </summary>
public sealed class OrTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "or";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        foreach (TemplateNode arg in unevaluatedArgs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.IsCanceled)
            {
                return TagResult.Incomplete;
            }

            TagResult result = await arg.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (context.IsCanceled)
            {
                return TagResult.Incomplete;
            }

            if (result.IsComplete && !string.IsNullOrEmpty(result.Value))
            {
                return result;
            }
        }

        return TagResult.Incomplete;
    }
}

/// <summary>
/// Handles the <c>{if &lt;cond&gt; &lt;then&gt; [else]}</c> tag with lazy branching, supporting both truthy conditions and binary <c>eq</c>/<c>ne</c>/<c>gt</c>/<c>lt</c>/<c>contains</c> expressions.
/// </summary>
public sealed class IfTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "if";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        if (unevaluatedArgs.Count < 2)
        {
            return TagResult.Incomplete;
        }

        bool conditionMatched;
        TemplateNode thenNode;
        TemplateNode? elseNode;

        if (unevaluatedArgs.Count >= 4
            && unevaluatedArgs[0] is LiteralNode prefixOpNode
            && ComparisonTagHandler.IsOperator(prefixOpNode.Text))
        {
            conditionMatched = await EvaluateBinaryConditionAsync(
                context,
                prefixOpNode.Text,
                unevaluatedArgs[1],
                unevaluatedArgs[2],
                cancellationToken).ConfigureAwait(false);
            thenNode = unevaluatedArgs[3];
            elseNode = unevaluatedArgs.Count > 4 ? unevaluatedArgs[4] : null;
        }
        else if (unevaluatedArgs.Count >= 4
            && unevaluatedArgs[1] is LiteralNode infixOpNode
            && ComparisonTagHandler.IsOperator(infixOpNode.Text))
        {
            conditionMatched = await EvaluateBinaryConditionAsync(
                context,
                infixOpNode.Text,
                unevaluatedArgs[0],
                unevaluatedArgs[2],
                cancellationToken).ConfigureAwait(false);
            thenNode = unevaluatedArgs[3];
            elseNode = unevaluatedArgs.Count > 4 ? unevaluatedArgs[4] : null;
        }
        else
        {
            TagResult condResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            conditionMatched = condResult.IsComplete && IsTruthy(condResult.Value);
            thenNode = unevaluatedArgs[1];
            elseNode = unevaluatedArgs.Count > 2 ? unevaluatedArgs[2] : null;
        }

        if (context.IsCanceled)
        {
            return TagResult.Incomplete;
        }

        if (conditionMatched)
        {
            return await thenNode.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        }

        if (elseNode is not null)
        {
            return await elseNode.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return TagResult.Complete(string.Empty);
    }

    /// <summary>
    /// Evaluates a binary comparison condition, returning <see langword="false"/> if either operand evaluates as incomplete.
    /// </summary>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="operatorName">The comparison operator name.</param>
    /// <param name="leftNode">The left-hand operand AST node.</param>
    /// <param name="rightNode">The right-hand operand AST node.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if both operands are complete and satisfy the operator; otherwise, <see langword="false"/>.</returns>
    private static async ValueTask<bool> EvaluateBinaryConditionAsync(
        ITagEvaluationContext context,
        string operatorName,
        TemplateNode leftNode,
        TemplateNode rightNode,
        CancellationToken cancellationToken)
    {
        TagResult left = await leftNode.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!left.IsComplete || context.IsCanceled)
        {
            return false;
        }

        TagResult right = await rightNode.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!right.IsComplete || context.IsCanceled)
        {
            return false;
        }

        return ComparisonTagHandler.EvaluateComparison(operatorName, left.Value, right.Value);
    }

    /// <summary>
    /// Determines whether an evaluated condition string is truthy.
    /// </summary>
    /// <param name="value">The evaluated string.</param>
    /// <returns><see langword="true"/> unless blank, <c>false</c>, <c>0</c>, <c>no</c>, or <c>off</c>.</returns>
    private static bool IsTruthy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        return !string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(trimmed, "0", StringComparison.Ordinal)
            && !string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(trimmed, "off", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Handles standalone binary comparison tags (<c>{eq}</c>, <c>{ne}</c>, <c>{gt}</c>, <c>{lt}</c>, <c>{contains}</c>), returning <c>"true"</c> or <c>"false"</c>.
/// </summary>
/// <param name="tagName">The comparison operator tag name.</param>
public sealed class ComparisonTagHandler(string tagName) : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName { get; } = tagName;

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count < 2)
        {
            return TagResult.Incomplete;
        }

        TagResult left = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!left.IsComplete || context.IsCanceled)
        {
            return TagResult.Incomplete;
        }

        TagResult right = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!right.IsComplete || context.IsCanceled)
        {
            return TagResult.Incomplete;
        }

        return TagResult.Complete(EvaluateComparison(this.TagName, left.Value, right.Value) ? "true" : "false");
    }

    /// <summary>
    /// Determines whether <paramref name="token"/> is a supported binary comparison operator.
    /// </summary>
    /// <param name="token">The operator token to inspect.</param>
    /// <returns><see langword="true"/> if supported; otherwise, <see langword="false"/>.</returns>
    internal static bool IsOperator(string token) =>
        string.Equals(token, "eq", StringComparison.OrdinalIgnoreCase)
        || string.Equals(token, "ne", StringComparison.OrdinalIgnoreCase)
        || string.Equals(token, "gt", StringComparison.OrdinalIgnoreCase)
        || string.Equals(token, "lt", StringComparison.OrdinalIgnoreCase)
        || string.Equals(token, "contains", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Evaluates a binary comparison between <paramref name="left"/> and <paramref name="right"/>.
    /// </summary>
    /// <param name="op">The comparison operator name.</param>
    /// <param name="left">The left-hand string value.</param>
    /// <param name="right">The right-hand string value.</param>
    /// <returns><see langword="true"/> if the condition holds; otherwise, <see langword="false"/>.</returns>
    internal static bool EvaluateComparison(string op, string left, string right)
    {
        if (string.Equals(op, "eq", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(op, "ne", StringComparison.OrdinalIgnoreCase))
        {
            return !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(op, "contains", StringComparison.OrdinalIgnoreCase))
        {
            return left.Contains(right, StringComparison.OrdinalIgnoreCase);
        }

        bool isLeftNumber = double.TryParse(left.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double leftNumber);
        bool isRightNumber = double.TryParse(right.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double rightNumber);

        if (string.Equals(op, "gt", StringComparison.OrdinalIgnoreCase))
        {
            return isLeftNumber && isRightNumber
                ? leftNumber > rightNumber
                : StringComparer.OrdinalIgnoreCase.Compare(left, right) > 0;
        }

        if (string.Equals(op, "lt", StringComparison.OrdinalIgnoreCase))
        {
            return isLeftNumber && isRightNumber
                ? leftNumber < rightNumber
                : StringComparer.OrdinalIgnoreCase.Compare(left, right) < 0;
        }

        return false;
    }
}
