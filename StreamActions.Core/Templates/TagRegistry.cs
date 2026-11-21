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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Templates;

/// <summary>
/// Thread-safe layered registry mapping <c>(platformId, tagName)</c> pairs to <see cref="ICommandTagHandler"/> instances.
/// </summary>
public sealed class TagRegistry : ITagRegistry
{
    /// <summary>
    /// Concurrent lookup table keyed by normalized <c>(platformKey, tagName)</c> pairs.
    /// </summary>
    private readonly ConcurrentDictionary<TagRegistryKey, ICommandTagHandler> _handlers = new();

    /// <summary>
    /// Initializes an empty <see cref="TagRegistry"/>.
    /// </summary>
    public TagRegistry()
    {
    }

    /// <summary>
    /// Initializes a <see cref="TagRegistry"/> pre-populated with <paramref name="handlers"/>.
    /// </summary>
    /// <param name="handlers">The tag handlers to register.</param>
    public TagRegistry(IEnumerable<ICommandTagHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        foreach (ICommandTagHandler handler in handlers)
        {
            this.Register(handler);
        }
    }

    /// <summary>
    /// Registers or replaces an <see cref="ICommandTagHandler"/> in the registry.
    /// </summary>
    /// <param name="handler">The handler instance to register.</param>
    public void Register(ICommandTagHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler.TagName);

        TagRegistryKey key = new(NormalizePlatformId(handler.PlatformId), handler.TagName.Trim());
        this._handlers[key] = handler;
    }

    /// <inheritdoc />
    public ICommandTagHandler? Resolve(string tagName, string? platformId = null)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        string trimmedTag = tagName.Trim();
        string normalizedPlatform = NormalizePlatformId(platformId);

        if (normalizedPlatform.Length > 0
            && this._handlers.TryGetValue(new TagRegistryKey(normalizedPlatform, trimmedTag), out ICommandTagHandler? platformHandler))
        {
            return platformHandler;
        }

        return this._handlers.TryGetValue(new TagRegistryKey(string.Empty, trimmedTag), out ICommandTagHandler? universalHandler)
            ? universalHandler
            : null;
    }

    /// <inheritdoc />
    public bool IsRegistered(string tagName, string? platformId = null) =>
        this.Resolve(tagName, platformId) is not null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetRegisteredTagNames(string? platformId = null)
    {
        string normalizedPlatform = NormalizePlatformId(platformId);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        foreach (TagRegistryKey key in this._handlers.Keys)
        {
            if (key.PlatformKey.Length == 0
                || (normalizedPlatform.Length > 0 && string.Equals(key.PlatformKey, normalizedPlatform, StringComparison.OrdinalIgnoreCase)))
            {
                _ = names.Add(key.TagName);
            }
        }

        string[] sorted = [.. names];
        Array.Sort(sorted, StringComparer.OrdinalIgnoreCase);
        return sorted.AsReadOnly();
    }

    /// <summary>
    /// Normalizes a platform identifier to lowercase invariant or empty string when universal.
    /// </summary>
    /// <param name="platformId">The raw platform identifier.</param>
    /// <returns>The normalized platform key, or <see cref="string.Empty"/> for universal handlers.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    private static string NormalizePlatformId(string? platformId) =>
        string.IsNullOrWhiteSpace(platformId)
            ? string.Empty
            : platformId.Trim().ToLowerInvariant();

    /// <summary>
    /// Case-insensitive composite dictionary key for layered tag resolution.
    /// </summary>
    /// <param name="PlatformKey">The normalized platform identifier, or empty for universal.</param>
    /// <param name="TagName">The tag identifier.</param>
    private readonly record struct TagRegistryKey(string PlatformKey, string TagName) : IEquatable<TagRegistryKey>
    {
        /// <summary>
        /// Determines case-insensitive equality for both platform key and tag name.
        /// </summary>
        /// <param name="other">The key to compare against.</param>
        /// <returns><see langword="true"/> if both keys match case-insensitively; otherwise, <see langword="false"/>.</returns>
        public bool Equals(TagRegistryKey other) =>
            string.Equals(this.PlatformKey, other.PlatformKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.TagName, other.TagName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Computes a case-insensitive hash code combining platform key and tag name.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.PlatformKey),
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.TagName));
    }
}
