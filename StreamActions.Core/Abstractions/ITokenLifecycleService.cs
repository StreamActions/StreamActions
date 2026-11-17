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

using StreamActions.Core.Models;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Manages single-flight OAuth token refreshes and proactive background token validation/renewal.
/// </summary>
public interface ITokenLifecycleService
{
    /// <summary>
    /// Ensures a valid, non-expiring OAuth token is available for the requested workspace, platform, and role,
    /// coalescing concurrent refresh attempts per credential using a single-flight lock.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for instance-level credentials.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="forceRefresh"><see langword="true"/> to bypass the expiration window check and force a refresh; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="TokenLifecycleResult"/> containing the active token payload or failure details.</returns>
    ValueTask<TokenLifecycleResult> EnsureValidTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a proactive maintenance pass across all stored credentials, refreshing tokens within the renewal window
    /// and periodically validating older active credentials against their platform OAuth provider.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous maintenance pass.</returns>
    ValueTask RunMaintenancePassAsync(CancellationToken cancellationToken = default);
}
