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
/// Defines the severity level of a <see cref="TemplateDiagnostic"/> emitted by <see cref="TemplateParser"/>.
/// </summary>
public enum TemplateDiagnosticSeverity
{
    /// <summary>
    /// A non-fatal warning such as an unrecognized tag name.
    /// </summary>
    Warning = 0,

    /// <summary>
    /// A syntax or depth-limit error indicating the template is malformed.
    /// </summary>
    Error = 1,
}

/// <summary>
/// Identifies the specific diagnostic condition detected during template parsing.
/// </summary>
public enum TemplateDiagnosticCode
{
    /// <summary>
    /// An opening <c>{</c> brace was not closed with a matching <c>}</c> brace.
    /// </summary>
    UnclosedBrace = 0,

    /// <summary>
    /// An unescaped closing <c>}</c> brace appeared without a matching opening <c>{</c> brace.
    /// </summary>
    UnexpectedClosingBrace = 1,

    /// <summary>
    /// An opening double quote <c>"</c> inside a tag argument was not closed before the end of input.
    /// </summary>
    UnclosedQuote = 2,

    /// <summary>
    /// An empty <c>{}</c> tag expression appeared without a tag name.
    /// </summary>
    EmptyTag = 3,

    /// <summary>
    /// Nested tag depth exceeded <see cref="TemplateExecutionLimits.MaxAstNestingDepth"/>.
    /// </summary>
    MaxNestingDepthExceeded = 4,

    /// <summary>
    /// A tag name is not registered in the supplied <see cref="ITagRegistry"/>.
    /// </summary>
    UnknownTag = 5,
}

/// <summary>
/// Represents a structured syntax or validation diagnostic produced by <see cref="TemplateParser"/>.
/// </summary>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Code">The diagnostic code.</param>
/// <param name="Message">The human-readable diagnostic message.</param>
/// <param name="Position">The zero-based character index in the source template.</param>
/// <param name="Length">The character length of the offending span.</param>
public sealed record TemplateDiagnostic(
    TemplateDiagnosticSeverity Severity,
    TemplateDiagnosticCode Code,
    string Message,
    int Position,
    int Length);

/// <summary>
/// Represents the parsed AST root and any diagnostics emitted by <see cref="TemplateParser"/>.
/// </summary>
/// <param name="Root">The root <see cref="TemplateNode"/> representing the parsed template.</param>
/// <param name="Diagnostics">The ordered list of parse diagnostics (errors and warnings).</param>
public sealed record TemplateParseResult(
    TemplateNode Root,
    IReadOnlyList<TemplateDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets a value indicating whether any diagnostic has <see cref="TemplateDiagnosticSeverity.Error"/> severity.
    /// </summary>
    public bool HasErrors => this.Diagnostics.Any(static diagnostic => diagnostic.Severity == TemplateDiagnosticSeverity.Error);

    /// <summary>
    /// Gets a value indicating whether the parsed template has no error diagnostics.
    /// </summary>
    public bool IsValid => !this.HasErrors;
}
