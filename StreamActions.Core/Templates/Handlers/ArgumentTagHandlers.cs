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
/// Handles 1-based positional command argument tags <c>{1}</c> through <c>{9}</c>, returning <see cref="TagResult.Incomplete"/> when missing or empty.
/// </summary>
public sealed class PositionalArgumentTagHandler : ICommandTagHandler
{
    /// <summary>
    /// The zero-based index into <see cref="ITagEvaluationContext.Arguments"/>.
    /// </summary>
    private readonly int _zeroBasedIndex;

    /// <summary>
    /// Initializes a new <see cref="PositionalArgumentTagHandler"/> for a 1-based argument index (<c>1..9</c>).
    /// </summary>
    /// <param name="oneBasedIndex">The 1-based argument position.</param>
    public PositionalArgumentTagHandler(int oneBasedIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(oneBasedIndex, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(oneBasedIndex, 9);

        this._zeroBasedIndex = oneBasedIndex - 1;
        this.TagName = oneBasedIndex.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public string TagName { get; }

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return this._zeroBasedIndex >= context.Arguments.Count
            ? ValueTask.FromResult(TagResult.Incomplete)
            : ValueTask.FromResult(TagResult.FromNonEmpty(context.Arguments[this._zeroBasedIndex]));
    }
}

/// <summary>
/// Handles the <c>{args [startIndex]}</c> tag, returning joined or raw command arguments starting at an optional 1-based index.
/// </summary>
public sealed class ArgsTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "args";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        int startIndex = 1;
        if (unevaluatedArgs.Count > 0)
        {
            TagResult indexResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!indexResult.IsComplete
                || !int.TryParse(indexResult.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex))
            {
                return TagResult.Incomplete;
            }

            startIndex = Math.Max(1, parsedIndex);
        }

        if (startIndex == 1 && !string.IsNullOrEmpty(context.RawArguments))
        {
            return TagResult.Complete(context.RawArguments);
        }

        int zeroBasedStart = startIndex - 1;
        if (zeroBasedStart >= context.Arguments.Count)
        {
            return TagResult.Incomplete;
        }

        string joined = string.Join(' ', context.Arguments.Skip(zeroBasedStart));
        return TagResult.FromNonEmpty(joined);
    }
}
