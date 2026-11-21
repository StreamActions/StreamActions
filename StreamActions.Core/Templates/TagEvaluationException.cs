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
/// Categorizes runtime safeguard and execution failures thrown by template evaluation.
/// </summary>
public enum TagEvaluationFailureReason
{
    /// <summary>
    /// Evaluation exceeded <see cref="TemplateExecutionLimits.MaxTagEvaluationsPerExecution"/>.
    /// </summary>
    MaxTagEvaluationsExceeded = 0,

    /// <summary>
    /// Evaluation exceeded <see cref="TemplateExecutionLimits.MaxEvaluationBufferChars"/>.
    /// </summary>
    MaxEvaluationBufferExceeded = 1,

    /// <summary>
    /// Evaluation exceeded <see cref="TemplateExecutionLimits.MaxAstNestingDepth"/>.
    /// </summary>
    MaxAstNestingDepthExceeded = 2,

    /// <summary>
    /// Evaluation exceeded the configured execution timeout.
    /// </summary>
    Timeout = 3,

    /// <summary>
    /// Evaluation failed due to an unexpected error in a tag handler.
    /// </summary>
    EvaluationFailed = 4,
}

/// <summary>
/// Exception thrown when template evaluation violates a runtime safeguard ceiling, times out, or fails unexpectedly.
/// </summary>
/// <param name="reason">The categorized failure reason.</param>
/// <param name="message">The human-readable diagnostic message.</param>
/// <param name="innerException">The optional underlying exception.</param>
public sealed class TagEvaluationException(
    TagEvaluationFailureReason reason,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TagEvaluationException"/> class.
    /// </summary>
    public TagEvaluationException()
        : this(TagEvaluationFailureReason.EvaluationFailed, "Template evaluation failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagEvaluationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public TagEvaluationException(string message)
        : this(TagEvaluationFailureReason.EvaluationFailed, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagEvaluationException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TagEvaluationException(string message, Exception innerException)
        : this(TagEvaluationFailureReason.EvaluationFailed, message, innerException)
    {
    }

    /// <summary>
    /// Gets the categorized failure reason.
    /// </summary>
    public TagEvaluationFailureReason Reason { get; } = reason;
}
