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

using Microsoft.Extensions.Caching.Hybrid;

namespace StreamActions.Database.Services;

/// <summary>
/// Wraps <see cref="HybridCache"/> for workspace-scoped hot-path lookups with tag-based invalidation.
/// </summary>
public sealed class WorkspaceCacheService
{
    /// <summary>
    /// The underlying hybrid cache instance.
    /// </summary>
    private readonly HybridCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceCacheService"/> class.
    /// </summary>
    /// <param name="cache">The hybrid cache instance.</param>
    public WorkspaceCacheService(HybridCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        this._cache = cache;
    }

    /// <summary>
    /// Builds the cache tag for a workspace domain.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="domain">The logical cache domain (for example, <c>commands</c> or <c>permissions</c>).</param>
    /// <returns>The formatted cache tag.</returns>
    public static string GetDomainTag(Guid workspaceId, string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return $"workspace:{workspaceId}:{domain.ToLowerInvariant()}";
    }

    /// <summary>
    /// Gets an entry from cache or creates it using <paramref name="factory"/>, tagged with the workspace domain tag.
    /// </summary>
    /// <typeparam name="T">The cached value type.</typeparam>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="domain">The logical cache domain.</param>
    /// <param name="key">The unique key within the workspace domain.</param>
    /// <param name="factory">The factory invoked on cache miss.</param>
    /// <param name="options">Optional cache entry options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The cached or newly created value.</returns>
    public ValueTask<T> GetOrCreateAsync<T>(
        Guid workspaceId,
        string domain,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        string tag = GetDomainTag(workspaceId, domain);
        string fullKey = $"{tag}:{key.ToLowerInvariant()}";

        return this._cache.GetOrCreateAsync(
            fullKey,
            factory,
            options,
            [tag],
            cancellationToken);
    }

    /// <summary>
    /// Invalidates all cached entries for a workspace domain using <see cref="HybridCache.RemoveByTagAsync(string, CancellationToken)"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="domain">The logical cache domain.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous invalidation.</returns>
    public ValueTask InvalidateAsync(
        Guid workspaceId,
        string domain,
        CancellationToken cancellationToken = default)
    {
        string tag = GetDomainTag(workspaceId, domain);
        return this._cache.RemoveByTagAsync(tag, cancellationToken);
    }
}
