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
/// Resolves <see cref="ICommandTagHandler"/> instances using layered <c>(platformId, tagName)</c> then <c>(null, tagName)</c> lookup.
/// </summary>
public interface ITagRegistry
{
    /// <summary>
    /// Resolves a handler for <paramref name="tagName"/>, preferring a platform-specific handler for <paramref name="platformId"/> before falling back to a universal handler.
    /// </summary>
    /// <param name="tagName">The case-insensitive tag identifier.</param>
    /// <param name="platformId">The optional platform identifier (for example, <c>twitch</c>).</param>
    /// <returns>The matching <see cref="ICommandTagHandler"/>, or <see langword="null"/> if not registered.</returns>
    ICommandTagHandler? Resolve(string tagName, string? platformId = null);

    /// <summary>
    /// Determines whether a handler is registered for <paramref name="tagName"/> in the specified platform or universal layer.
    /// </summary>
    /// <param name="tagName">The case-insensitive tag identifier.</param>
    /// <param name="platformId">The optional platform identifier.</param>
    /// <returns><see langword="true"/> if a matching handler exists; otherwise, <see langword="false"/>.</returns>
    bool IsRegistered(string tagName, string? platformId = null);

    /// <summary>
    /// Returns all registered tag names available for <paramref name="platformId"/> (including universal tags when <paramref name="platformId"/> is specified, or universal-only when <see langword="null"/>).
    /// </summary>
    /// <param name="platformId">The optional platform identifier.</param>
    /// <returns>A sorted, case-insensitive read-only collection of registered tag names.</returns>
    IReadOnlyCollection<string> GetRegisteredTagNames(string? platformId = null);
}
