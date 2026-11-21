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

using System.Text;

namespace StreamActions.Core.Templates;

/// <summary>
/// Base abstract AST node for parsed curly-brace command templates.
/// </summary>
public abstract record TemplateNode
{
    /// <summary>
    /// Evaluates this AST node asynchronously against the supplied execution <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    public abstract ValueTask<TagResult> EvaluateAsync(ITagEvaluationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Represents an immutable literal text segment in a parsed template.
/// </summary>
/// <param name="Text">The unescaped literal text.</param>
public sealed record LiteralNode(string Text) : TemplateNode
{
    /// <summary>
    /// Evaluates this literal node, recording its character length against the context buffer ceiling.
    /// </summary>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A completed <see cref="TagResult"/> containing <see cref="Text"/>.</returns>
    public override ValueTask<TagResult> EvaluateAsync(ITagEvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.IsCanceled)
        {
            return ValueTask.FromResult(TagResult.Incomplete);
        }

        string value = this.Text ?? string.Empty;
        context.AddBufferChars(value.Length);
        return ValueTask.FromResult(TagResult.Complete(value));
    }
}

/// <summary>
/// Represents a sequence of child <see cref="TemplateNode"/> segments concatenated during evaluation.
/// </summary>
/// <param name="Children">The ordered child AST nodes.</param>
public sealed record CompositeNode(IReadOnlyList<TemplateNode> Children) : TemplateNode
{
    /// <summary>
    /// Evaluates each child sequentially, bubbling <see cref="TagResult.IsComplete"/> = <see langword="false"/> if any child evaluates as incomplete, and enforcing <see cref="TemplateExecutionLimits.MaxEvaluationBufferChars"/>.
    /// </summary>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The concatenated <see cref="TagResult"/>.</returns>
    public override async ValueTask<TagResult> EvaluateAsync(ITagEvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.IsCanceled)
        {
            return TagResult.Incomplete;
        }

        if (this.Children.Count == 0)
        {
            return TagResult.Complete(string.Empty);
        }

        StringBuilder builder = new();
        bool allComplete = true;

        foreach (TemplateNode child in this.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.IsCanceled)
            {
                return TagResult.Incomplete;
            }

            TagResult childResult = await child.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            if (context.IsCanceled)
            {
                return TagResult.Incomplete;
            }

            if (!childResult.IsComplete)
            {
                allComplete = false;
            }

            if (builder.Length + childResult.Value.Length > context.MaxEvaluationBufferChars)
            {
                throw new TagEvaluationException(
                    TagEvaluationFailureReason.MaxEvaluationBufferExceeded,
                    $"Template evaluation buffer exceeded {context.MaxEvaluationBufferChars} characters.");
            }

            _ = builder.Append(childResult.Value);
        }

        return new TagResult(builder.ToString(), allComplete);
    }
}

/// <summary>
/// Represents an executable <c>{tagName ...}</c> AST node resolved via <see cref="ITagRegistry"/>.
/// </summary>
/// <param name="TagName">The tag identifier.</param>
/// <param name="Arguments">The unevaluated argument AST nodes passed to the tag handler.</param>
public sealed record TagNode(string TagName, IReadOnlyList<TemplateNode> Arguments) : TemplateNode
{
    /// <summary>
    /// Enforces runtime tag evaluation count and AST nesting depth limits, resolves the matching <see cref="ICommandTagHandler"/>, and executes it.
    /// </summary>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="TagResult"/> returned by the resolved handler, or <see cref="TagResult.Incomplete"/> if canceled or unregistered.</returns>
    public override async ValueTask<TagResult> EvaluateAsync(ITagEvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.IsCanceled)
        {
            return TagResult.Incomplete;
        }

        context.EnterTagDepth();
        try
        {
            context.IncrementEvaluationCount();

            ICommandTagHandler? handler = context.TagRegistry.Resolve(this.TagName, context.PlatformId);
            if (handler is null)
            {
                return TagResult.Incomplete;
            }

            TagResult result = await handler.EvaluateAsync(context, this.Arguments, cancellationToken).ConfigureAwait(false);
            if (context.IsCanceled)
            {
                return TagResult.Incomplete;
            }

            context.AddBufferChars(result.Value.Length);
            return result;
        }
        finally
        {
            context.ExitTagDepth();
        }
    }
}
