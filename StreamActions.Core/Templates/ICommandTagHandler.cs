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
/// Implements evaluation logic for a curly-brace template tag such as <c>{sender}</c>, <c>{if ...}</c>, or <c>{count}</c>.
/// </summary>
public interface ICommandTagHandler
{
    /// <summary>
    /// Gets the case-insensitive tag identifier handled by this instance (for example, <c>sender</c> or <c>1</c>).
    /// </summary>
    string TagName { get; }

    /// <summary>
    /// Gets the optional platform identifier that scopes this handler (for example, <c>twitch</c>), or <see langword="null"/> for universal handlers.
    /// </summary>
    string? PlatformId => null;

    /// <summary>
    /// Evaluates the tag asynchronously against <paramref name="context"/> and its <paramref name="unevaluatedArgs"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Arguments are passed unevaluated so control-flow tags such as <c>{if}</c>, <c>{or}</c>, and <c>{random}</c> can evaluate branches lazily.
    /// </para>
    /// </remarks>
    /// <param name="context">The runtime tag evaluation context.</param>
    /// <param name="unevaluatedArgs">The unevaluated AST nodes passed as tag arguments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    ValueTask<TagResult> EvaluateAsync(
        ITagEvaluationContext context,
        IReadOnlyList<TemplateNode> unevaluatedArgs,
        CancellationToken cancellationToken);
}
