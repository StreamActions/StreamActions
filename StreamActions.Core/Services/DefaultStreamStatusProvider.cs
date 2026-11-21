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

using StreamActions.Core.Abstractions;
using System.Collections.Concurrent;

namespace StreamActions.Core.Services;

/// <summary>
/// Default in-memory implementation of <see cref="IStreamStatusProvider"/> that reports streams as offline until platform adapters publish status updates.
/// </summary>
public sealed class DefaultStreamStatusProvider : IStreamStatusProvider
{
    /// <summary>
    /// Cached per-(workspace, platform) stream status snapshots.
    /// </summary>
    private readonly ConcurrentDictionary<StreamStatusKey, StreamStatusState> _states = new();

    /// <summary>
    /// Updates the cached live status and game/category for a workspace platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="isLive">A value indicating whether the stream is currently live.</param>
    /// <param name="gameCategory">The current game or category name, if available.</param>
    public void SetPlatformStatus(Guid workspaceId, string platformId, bool isLive, string? gameCategory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        StreamStatusKey key = new(workspaceId, platformId.Trim());
        string? normalizedCategory = string.IsNullOrWhiteSpace(gameCategory) ? null : gameCategory.Trim();
        this._states[key] = new StreamStatusState(isLive, normalizedCategory);
    }

    /// <inheritdoc/>
    public ValueTask<bool> IsStreamLiveAsync(Guid workspaceId, string? platformId = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(platformId))
        {
            StreamStatusKey key = new(workspaceId, platformId.Trim());
            return ValueTask.FromResult(this._states.TryGetValue(key, out StreamStatusState state) && state.IsLive);
        }

        foreach (KeyValuePair<StreamStatusKey, StreamStatusState> entry in this._states)
        {
            if (entry.Key.WorkspaceId == workspaceId && entry.Value.IsLive)
            {
                return ValueTask.FromResult(true);
            }
        }

        return ValueTask.FromResult(false);
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetCurrentGameCategoryAsync(Guid workspaceId, string? platformId = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(platformId))
        {
            StreamStatusKey key = new(workspaceId, platformId.Trim());
            return ValueTask.FromResult(this._states.TryGetValue(key, out StreamStatusState state) ? state.GameCategory : null);
        }

        foreach (KeyValuePair<StreamStatusKey, StreamStatusState> entry in this._states)
        {
            if (entry.Key.WorkspaceId == workspaceId && entry.Value.IsLive && entry.Value.GameCategory is not null)
            {
                return ValueTask.FromResult<string?>(entry.Value.GameCategory);
            }
        }

        foreach (KeyValuePair<StreamStatusKey, StreamStatusState> entry in this._states)
        {
            if (entry.Key.WorkspaceId == workspaceId && entry.Value.GameCategory is not null)
            {
                return ValueTask.FromResult<string?>(entry.Value.GameCategory);
            }
        }

        return ValueTask.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<string>> GetActiveStreamPlatformIdsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        List<string> active = [];
        foreach (KeyValuePair<StreamStatusKey, StreamStatusState> entry in this._states)
        {
            if (entry.Key.WorkspaceId == workspaceId && entry.Value.IsLive)
            {
                active.Add(entry.Key.PlatformId);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<string>>(active.AsReadOnly());
    }

    /// <summary>
    /// Case-insensitive composite key identifying a workspace platform status entry.
    /// </summary>
    /// <param name="WorkspaceId">The workspace identifier.</param>
    /// <param name="PlatformId">The platform identifier.</param>
    private readonly record struct StreamStatusKey(Guid WorkspaceId, string PlatformId) : IEquatable<StreamStatusKey>
    {
        /// <inheritdoc/>
        public bool Equals(StreamStatusKey other) =>
            this.WorkspaceId == other.WorkspaceId
            && string.Equals(this.PlatformId, other.PlatformId, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(this.WorkspaceId, StringComparer.OrdinalIgnoreCase.GetHashCode(this.PlatformId));
    }

    /// <summary>
    /// Snapshot of a platform stream's live state and active game/category.
    /// </summary>
    /// <param name="IsLive">Whether the stream is currently live.</param>
    /// <param name="GameCategory">The active game or category name.</param>
    private readonly record struct StreamStatusState(bool IsLive, string? GameCategory);
}
