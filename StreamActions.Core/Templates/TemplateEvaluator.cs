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

namespace StreamActions.Core.Templates;

/// <summary>
/// Evaluates parsed <see cref="TemplateNode"/> ASTs with bounded execution timeouts powered by <see cref="ITagEvaluationContext.TimeProvider"/>.
/// </summary>
public static class TemplateEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="node"/> against <paramref name="context"/>, enforcing an execution timeout (defaulting to <see cref="TemplateExecutionLimits.DefaultTimeout"/>).
    /// </summary>
    /// <param name="node">The root AST node to evaluate.</param>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="timeout">Optional execution timeout override.</param>
    /// <param name="cancellationToken">A token to monitor for external cancellation requests.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    /// <exception cref="TagEvaluationException">Thrown when execution exceeds timeout or runtime safety caps.</exception>
    public static async ValueTask<TagResult> EvaluateAsync(
        TemplateNode node,
        ITagEvaluationContext context,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        TimeSpan effectiveTimeout = timeout ?? TemplateExecutionLimits.DefaultTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            throw new TagEvaluationException(
                TagEvaluationFailureReason.Timeout,
                $"Template evaluation timed out after {effectiveTimeout}.");
        }

        using CancellationTokenSource timeoutCts = new(effectiveTimeout, context.TimeProvider);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await node.EvaluateAsync(context, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TagEvaluationException(
                TagEvaluationFailureReason.Timeout,
                $"Template evaluation exceeded timeout of {effectiveTimeout}.",
                ex);
        }
    }
}
