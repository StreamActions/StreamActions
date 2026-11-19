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
/// Provides persistence, caching, validation, and revocation operations for <see cref="PanelUserSession"/> records.
/// </summary>
public interface IPanelSessionService
{
    /// <summary>
    /// Creates and persists a new active <see cref="PanelUserSession"/> for the specified panel user.
    /// </summary>
    /// <param name="panelUserId">The owning panel user identifier.</param>
    /// <param name="providerId">The authentication provider identifier used to create the session.</param>
    /// <param name="remoteIpAddress">The optional client IP address.</param>
    /// <param name="userAgent">The optional client <c>User-Agent</c> header.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created <see cref="PanelUserSession"/>.</returns>
    ValueTask<PanelUserSession> CreateSessionAsync(
        Guid panelUserId,
        string providerId,
        string? remoteIpAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a <see cref="PanelUserSession"/> by its identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="PanelUserSession"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<PanelUserSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all non-revoked <see cref="PanelUserSession"/> records for the specified panel user.
    /// </summary>
    /// <param name="panelUserId">The owning panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of active <see cref="PanelUserSession"/> records.</returns>
    ValueTask<IReadOnlyList<PanelUserSession>> GetActiveSessionsForUserAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified session exists, has not been revoked, and optionally belongs to <paramref name="expectedPanelUserId"/>.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="expectedPanelUserId">An optional expected owning panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the session is active and valid; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsSessionValidAsync(
        Guid sessionId,
        Guid? expectedPanelUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a single session by its identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if an active session was found and revoked; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all active sessions for <paramref name="currentUserId"/> except <paramref name="currentSessionId"/>.
    /// </summary>
    /// <param name="currentUserId">The panel user identifier.</param>
    /// <param name="currentSessionId">The current session identifier to preserve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of active sessions revoked.</returns>
    ValueTask<int> RevokeOtherSessionsAsync(
        Guid currentUserId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all active sessions for <paramref name="targetUserId"/>.
    /// </summary>
    /// <param name="targetUserId">The target panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of active sessions revoked.</returns>
    ValueTask<int> RevokeAllSessionsForUserAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}

