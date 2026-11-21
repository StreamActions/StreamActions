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
/// Represents the evaluated output of a <see cref="TemplateNode"/> or <see cref="ICommandTagHandler"/>, indicating whether evaluation completed with a usable value.
/// </summary>
/// <param name="Value">The evaluated string value (empty when incomplete).</param>
/// <param name="IsComplete"><see langword="true"/> if evaluation produced a complete value; <see langword="false"/> if a value was missing, skipped, or canceled.</param>
public readonly record struct TagResult(string Value, bool IsComplete)
{
    /// <summary>
    /// Gets an incomplete <see cref="TagResult"/> with an empty string value.
    /// </summary>
    public static TagResult Incomplete { get; } = new(string.Empty, false);

    /// <summary>
    /// Gets the evaluated string value, normalized to non-null.
    /// </summary>
    public string Value { get; init; } = Value ?? string.Empty;

    /// <summary>
    /// Creates a completed <see cref="TagResult"/> containing <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The evaluated string value.</param>
    /// <returns>A completed <see cref="TagResult"/>.</returns>
    public static TagResult Complete(string value) =>
        new(value ?? string.Empty, true);

    /// <summary>
    /// Creates a completed <see cref="TagResult"/> if <paramref name="value"/> is non-null and non-empty; otherwise returns <see cref="Incomplete"/>.
    /// </summary>
    /// <param name="value">The optional string value to inspect.</param>
    /// <returns>A completed <see cref="TagResult"/> when <paramref name="value"/> is non-empty; otherwise, <see cref="Incomplete"/>.</returns>
    public static TagResult FromNonEmpty(string? value) =>
        string.IsNullOrEmpty(value)
            ? Incomplete
            : new(value, true);
}
