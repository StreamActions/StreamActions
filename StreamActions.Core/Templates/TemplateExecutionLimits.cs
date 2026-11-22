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
/// Defines safety ceilings enforced during template parsing and runtime evaluation.
/// </summary>
public static class TemplateExecutionLimits
{
    /// <summary>
    /// Maximum allowed AST tag nesting depth during both parsing and evaluation.
    /// </summary>
    public const int MaxAstNestingDepth = 16;

    /// <summary>
    /// Maximum allowed nested sub-command dispatch depth via <c>{botcmd}</c>.
    /// </summary>
    public const int MaxCommandChainDepth = 3;

    /// <summary>
    /// Maximum number of tag evaluations permitted within a single template execution.
    /// </summary>
    public const int MaxTagEvaluationsPerExecution = 64;

    /// <summary>
    /// Maximum number of characters permitted in evaluation buffers during a single template execution.
    /// </summary>
    public const int MaxEvaluationBufferChars = 16_384;

    /// <summary>
    /// Maximum number of delayed actions (<c>{delaycommand}</c> and <c>{delaysay}</c>) permitted per template evaluation.
    /// </summary>
    public const int MaxDelayedActionsPerExecution = 3;

    /// <summary>
    /// Default timeout applied to template evaluation when no explicit timeout is specified.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
}
