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
/// Handles the <c>{math &lt;op&gt; &lt;n1&gt; [n2...]}</c> tag supporting <c>add</c>, <c>sub</c>, <c>mul</c>, <c>div</c>, <c>mod</c>, <c>pow</c>, <c>abs</c>, <c>ceil</c>, <c>floor</c>, <c>round</c>, <c>min</c>, and <c>max</c> with <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
public sealed class MathTagHandler : ICommandTagHandler
{
    /// <inheritdoc />
    public string TagName => "math";

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

        TagResult opResult = await unevaluatedArgs[0].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!opResult.IsComplete || string.IsNullOrWhiteSpace(opResult.Value))
        {
            return TagResult.Incomplete;
        }

        string op = opResult.Value.Trim();
        List<double> numbers = new(unevaluatedArgs.Count - 1);

        for (int i = 1; i < unevaluatedArgs.Count; i++)
        {
            TagResult argResult = await unevaluatedArgs[i].EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!argResult.IsComplete
                || !double.TryParse(argResult.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
            {
                return TagResult.Incomplete;
            }

            numbers.Add(num);
        }

        if (!TryCompute(op, numbers, out double computed) || double.IsNaN(computed) || double.IsInfinity(computed))
        {
            return TagResult.Incomplete;
        }

        if (computed == 0d)
        {
            computed = 0d;
        }

        return TagResult.Complete(computed.ToString("G15", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Computes the requested arithmetic operation over <paramref name="numbers"/>.
    /// </summary>
    /// <param name="op">The operation name.</param>
    /// <param name="numbers">The parsed operand numbers.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the computed value.</param>
    /// <returns><see langword="true"/> if the operation and operand count were valid; otherwise, <see langword="false"/>.</returns>
    private static bool TryCompute(string op, List<double> numbers, out double result)
    {
        result = 0d;
        if (numbers.Count == 0)
        {
            return false;
        }

        if (string.Equals(op, "abs", StringComparison.OrdinalIgnoreCase))
        {
            result = Math.Abs(numbers[0]);
            return true;
        }

        if (string.Equals(op, "ceil", StringComparison.OrdinalIgnoreCase))
        {
            result = Math.Ceiling(numbers[0]);
            return true;
        }

        if (string.Equals(op, "floor", StringComparison.OrdinalIgnoreCase))
        {
            result = Math.Floor(numbers[0]);
            return true;
        }

        if (string.Equals(op, "round", StringComparison.OrdinalIgnoreCase))
        {
            result = numbers.Count >= 2
                ? Math.Round(numbers[0], Math.Clamp((int)numbers[1], 0, 15), MidpointRounding.AwayFromZero)
                : Math.Round(numbers[0], MidpointRounding.AwayFromZero);
            return true;
        }

        if (numbers.Count < 2)
        {
            return false;
        }

        if (string.Equals(op, "add", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                acc += numbers[i];
            }

            result = acc;
            return true;
        }

        if (string.Equals(op, "sub", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                acc -= numbers[i];
            }

            result = acc;
            return true;
        }

        if (string.Equals(op, "mul", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                acc *= numbers[i];
            }

            result = acc;
            return true;
        }

        if (string.Equals(op, "div", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                if (numbers[i] == 0d)
                {
                    return false;
                }

                acc /= numbers[i];
            }

            result = acc;
            return true;
        }

        if (string.Equals(op, "mod", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                if (numbers[i] == 0d)
                {
                    return false;
                }

                acc %= numbers[i];
            }

            result = acc;
            return true;
        }

        if (string.Equals(op, "pow", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                acc = Math.Pow(acc, numbers[i]);
            }

            result = acc;
            return true;
        }

        if (string.Equals(op, "min", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                acc = Math.Min(acc, numbers[i]);
            }

            result = acc;
            return true;
        }

        if (string.Equals(op, "max", StringComparison.OrdinalIgnoreCase))
        {
            double acc = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                acc = Math.Max(acc, numbers[i]);
            }

            result = acc;
            return true;
        }

        return false;
    }
}
