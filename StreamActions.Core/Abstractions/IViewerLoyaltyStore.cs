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

using StreamActions.Core.Models.Entities;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides persistence and atomic balance operations for <see cref="WorkspaceViewerProfile"/> loyalty records.
/// </summary>
public interface IViewerLoyaltyStore
{
    /// <summary>
    /// Retrieves a viewer's loyalty profile within a workspace, if one exists.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The immutable platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="WorkspaceViewerProfile"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceViewerProfile?> GetProfileAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing viewer profile or creates and initializes a new one while updating last-known identity metadata.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The immutable platform-specific user identifier.</param>
    /// <param name="login">The viewer's current login handle.</param>
    /// <param name="displayName">The viewer's current display name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The existing or newly created <see cref="WorkspaceViewerProfile"/>.</returns>
    ValueTask<WorkspaceViewerProfile> GetOrCreateProfileAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        string login,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the top viewer loyalty profiles ordered by points descending.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="count">The maximum number of leaderboard entries to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of top <see cref="WorkspaceViewerProfile"/> entries.</returns>
    ValueTask<IReadOnlyList<WorkspaceViewerProfile>> GetTopByPointsAsync(
        Guid workspaceId,
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically adjusts a viewer's loyalty points balance by <paramref name="delta"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The immutable platform-specific user identifier.</param>
    /// <param name="delta">The signed points delta to apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated <see cref="WorkspaceViewerProfile"/>.</returns>
    ValueTask<WorkspaceViewerProfile> AdjustPointsAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        long delta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks whether a viewer has at least <paramref name="cost"/> loyalty points and deducts that amount.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The immutable platform-specific user identifier.</param>
    /// <param name="cost">The non-negative points cost to deduct.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the balance was sufficient and deducted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> TrySpendPointsAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        long cost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments a viewer's accumulated watch time and chat XP.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The immutable platform-specific user identifier.</param>
    /// <param name="watchTimeSecondsDelta">The watch time increment in seconds.</param>
    /// <param name="chatXpDelta">The chat XP increment.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous update operation.</returns>
    ValueTask AddWatchTimeAndXpAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        long watchTimeSecondsDelta,
        long chatXpDelta,
        CancellationToken cancellationToken = default);
}
