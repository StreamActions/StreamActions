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

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{var &lt;name&gt; [value]}</c> tag for reading and writing routine-scoped variables on <see cref="ITagEvaluationContext"/>.
/// </summary>
public sealed class VarTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "var";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        TagResult nameResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!nameResult.IsComplete || string.IsNullOrWhiteSpace(nameResult.Value))
        {
            return TagResult.Incomplete;
        }

        string variableName = nameResult.Value.Trim();

        if (unevaluatedArgs.Count >= 2)
        {
            TagResult valueResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!valueResult.IsComplete || context.IsCanceled)
            {
                return TagResult.Incomplete;
            }

            context.SetVariable(variableName, valueResult.Value);
            return TagResult.Complete(string.Empty);
        }

        return context.TryGetVariable(variableName, out string? existingValue)
            ? TagResult.Complete(existingValue)
            : TagResult.Incomplete;
    }
}

/// <summary>
/// Handles the <c>{cancel [reason]}</c> tag, marking the evaluation context as canceled and short-circuiting subsequent nodes.
/// </summary>
public sealed class CancelTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "cancel";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);

        string? reason = null;
        if (unevaluatedArgs.Count > 0)
        {
            TagResult reasonResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (reasonResult.IsComplete && !string.IsNullOrWhiteSpace(reasonResult.Value))
            {
                reason = reasonResult.Value;
            }
        }

        context.Cancel(reason);
        return TagResult.Incomplete;
    }
}
