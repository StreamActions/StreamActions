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
using System.Security.Cryptography;

namespace StreamActions.Core.Templates.Handlers;

/// <summary>
/// Handles <c>{random &lt;opt1&gt; ...}</c> and <c>{random_choice &lt;opt1&gt; ...}</c>, lazily evaluating one cryptographically chosen argument.
/// </summary>
/// <param name="tagName">The tag alias (<c>random</c> or <c>random_choice</c>).</param>
public sealed class RandomChoiceTagHandler(string tagName = "random") : ICommandTagHandler
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
        cancellationToken.ThrowIfCancellationRequested();

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        int selectedIndex = RandomNumberGenerator.GetInt32(unevaluatedArgs.Count);
        return await unevaluatedArgs[selectedIndex].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles <c>{random_int [min] [max]}</c>, generating a cryptographically random inclusive integer formatted with <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
public sealed class RandomIntTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "random_int";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        int min = 1;
        int max = 100;

        if (unevaluatedArgs.Count == 1)
        {
            TagResult arg0 = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!arg0.IsComplete
                || !int.TryParse(arg0.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int singleMax))
            {
                return TagResult.Incomplete;
            }

            max = singleMax;
        }
        else if (unevaluatedArgs.Count >= 2)
        {
            TagResult arg0 = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!arg0.IsComplete
                || !int.TryParse(arg0.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out min))
            {
                return TagResult.Incomplete;
            }

            TagResult arg1 = await unevaluatedArgs[1].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!arg1.IsComplete
                || !int.TryParse(arg1.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out max))
            {
                return TagResult.Incomplete;
            }
        }

        if (min > max)
        {
            (min, max) = (max, min);
        }

        int value = max == int.MaxValue
            ? RandomNumberGenerator.GetInt32(min, max)
            : RandomNumberGenerator.GetInt32(min, max + 1);

        return TagResult.Complete(value.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Handles <c>{random_code &lt;len&gt;}</c>, generating a cryptographically random alphanumeric code of the requested length.
/// </summary>
public sealed class RandomCodeTagHandler : ICommandTagHandler
{
    /// <summary>
    /// Maximum allowed code length per invocation to prevent excessive allocations.
    /// </summary>
    public const int MaxCodeLength = 64;

    /// <summary>
    /// Alphanumeric character set used for generated random codes.
    /// </summary>
    private const string _codeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <inheritdoc />
    public string TagName => "random_code";

    /// <inheritdoc />
    public async ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(unevaluatedArgs);
        cancellationToken.ThrowIfCancellationRequested();

        if (unevaluatedArgs.Count == 0)
        {
            return TagResult.Incomplete;
        }

        TagResult lengthResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!lengthResult.IsComplete
            || !int.TryParse(lengthResult.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int length)
            || length <= 0)
        {
            return TagResult.Incomplete;
        }

        int cappedLength = Math.Min(length, MaxCodeLength);
        string code = RandomNumberGenerator.GetString(_codeAlphabet, cappedLength);
        return TagResult.Complete(code);
    }
}
