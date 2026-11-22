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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles the <c>{upper &lt;str&gt;}</c> tag, converting text to uppercase using invariant culture.
/// </summary>
public sealed class UpperTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "upper";

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

        TagResult input = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return input.IsComplete
            ? TagResult.Complete(input.Value.ToUpperInvariant())
            : TagResult.Incomplete;
    }
}

/// <summary>
/// Handles the <c>{lower &lt;str&gt;}</c> tag, converting text to lowercase using invariant culture.
/// </summary>
public sealed class LowerTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "lower";

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "The {lower} template tag intentionally converts text to lowercase using invariant culture.")]
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

        TagResult input = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return input.IsComplete
            ? TagResult.Complete(input.Value.ToLowerInvariant())
            : TagResult.Incomplete;
    }
}

/// <summary>
/// Handles the <c>{replace &lt;str&gt; &lt;find&gt; &lt;repl&gt;}</c> tag using ordinal string replacement.
/// </summary>
public sealed class ReplaceTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "replace";

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

        TagResult source = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!source.IsComplete)
        {
            return TagResult.Incomplete;
        }

        TagResult find = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!find.IsComplete)
        {
            return TagResult.Incomplete;
        }

        string replacement = string.Empty;
        if (unevaluatedArgs.Count >= 3)
        {
            TagResult replResult = await unevaluatedArgs[2].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!replResult.IsComplete)
            {
                return TagResult.Incomplete;
            }

            replacement = replResult.Value;
        }

        return string.IsNullOrEmpty(find.Value)
            ? TagResult.Complete(source.Value)
            : TagResult.Complete(source.Value.Replace(find.Value, replacement, StringComparison.Ordinal));
    }
}

/// <summary>
/// Handles the <c>{url_encode &lt;str&gt;}</c> tag using <see cref="Uri.EscapeDataString(string)"/>.
/// </summary>
public sealed class UrlEncodeTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "url_encode";

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

        TagResult input = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return input.IsComplete
            ? TagResult.Complete(Uri.EscapeDataString(input.Value))
            : TagResult.Incomplete;
    }
}

/// <summary>
/// Handles the <c>{repeat &lt;count&gt; &lt;str&gt;}</c> tag with a bounded repetition cap.
/// </summary>
public sealed class RepeatTagHandler : ICommandTagHandler
{
    /// <summary>
    /// Maximum repetition count allowed by a single <c>{repeat}</c> invocation.
    /// </summary>
    public const int MaxRepeatCount = 30;

    /// <inheritdoc />
    public string TagName => "repeat";

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

        TagResult countResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!countResult.IsComplete
            || !int.TryParse(countResult.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int requestedCount))
        {
            return TagResult.Incomplete;
        }

        TagResult textResult = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!textResult.IsComplete)
        {
            return TagResult.Incomplete;
        }

        int count = Math.Clamp(requestedCount, 0, MaxRepeatCount);
        if (count == 0 || textResult.Value.Length == 0)
        {
            return TagResult.Complete(string.Empty);
        }

        long projectedLength = (long)count * textResult.Value.Length;
        if (projectedLength > context.MaxEvaluationBufferChars)
        {
            throw new TagEvaluationException(
                TagEvaluationFailureReason.MaxEvaluationBufferExceeded,
                $"Repeated string output of {projectedLength} characters exceeds maximum buffer of {context.MaxEvaluationBufferChars}.");
        }

        StringBuilder builder = new((int)projectedLength);
        for (int i = 0; i < count; i++)
        {
            _ = builder.Append(textResult.Value);
        }

        return TagResult.Complete(builder.ToString());
    }
}

/// <summary>
/// Handles the <c>{nl}</c> tag, emitting a newline character.
/// </summary>
public sealed class NewLineTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "nl";

    /// <inheritdoc />
    public ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(TagResult.Complete("\n"));
    }
}
