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
/// Detects missing OAuth scopes on platform tokens relative to active plugins and platform scope requirements.
/// </summary>
public interface IScopeDriftDetector
{
    /// <summary>
    /// Aggregates all required OAuth scopes for the specified platform.
    /// </summary>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="additionalRequiredScopes">Optional additional scopes required by the caller.</param>
    /// <returns>A deduplicated list of required scope names.</returns>
    IReadOnlyList<string> GetRequiredScopes(
        string platformId,
        IEnumerable<string>? additionalRequiredScopes = null);

    /// <summary>
    /// Compares the scopes granted in <paramref name="tokenPayload"/> against the required scopes for <paramref name="platformId"/>.
    /// </summary>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenPayload">The decrypted token payload, or <see langword="null"/> if no token is available.</param>
    /// <param name="additionalRequiredScopes">Optional additional scopes required by the caller.</param>
    /// <returns>A <see cref="ScopeDriftReport"/> describing missing scopes and the incremental upgrade union.</returns>
    ScopeDriftReport DetectDrift(
        string platformId,
        PlatformTokenPayload? tokenPayload,
        IEnumerable<string>? additionalRequiredScopes = null);

    /// <summary>
    /// Compares <paramref name="grantedScopes"/> against the required scopes for <paramref name="platformId"/>.
    /// </summary>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="grantedScopes">The currently granted scopes.</param>
    /// <param name="additionalRequiredScopes">Optional additional scopes required by the caller.</param>
    /// <returns>A <see cref="ScopeDriftReport"/> describing missing scopes and the incremental upgrade union.</returns>
    ScopeDriftReport DetectDrift(
        string platformId,
        IReadOnlyCollection<string>? grantedScopes,
        IEnumerable<string>? additionalRequiredScopes = null);
}
