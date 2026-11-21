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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides live stream status, active stream platforms, and current game/category information for workspaces.
/// </summary>
public interface IStreamStatusProvider
{
    /// <summary>
    /// Determines whether the workspace stream is currently live on a specific platform or on any attached streaming platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">An optional platform identifier to check, or <see langword="null"/> to check if any platform is live.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the stream is live; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsStreamLiveAsync(Guid workspaceId, string? platformId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current game or category name for a workspace stream, if available.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">An optional platform identifier, or <see langword="null"/> to query the primary or first active platform.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The current game or category name, or <see langword="null"/> if offline or unset.</returns>
    ValueTask<string?> GetCurrentGameCategoryAsync(Guid workspaceId, string? platformId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the platform identifiers that are currently live for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of active live stream platform identifiers.</returns>
    ValueTask<IReadOnlyList<string>> GetActiveStreamPlatformIdsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
