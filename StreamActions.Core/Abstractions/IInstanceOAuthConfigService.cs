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
/// Manages encrypted instance-wide platform OAuth application credentials persisted in instance settings.
/// </summary>
public interface IInstanceOAuthConfigService
{
    /// <summary>
    /// Retrieves all configured platform OAuth application credentials keyed by normalized lowercase platform identifier.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only dictionary mapping platform identifiers to their <see cref="PlatformOAuthAppCredentials"/>.</returns>
    ValueTask<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the configured OAuth application credentials for the specified platform.
    /// </summary>
    /// <param name="platformId">The platform identifier (for example, <c>twitch</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="PlatformOAuthAppCredentials"/>, or <see langword="null"/> if not configured.</returns>
    ValueTask<PlatformOAuthAppCredentials?> GetAsync(string platformId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates the instance-wide OAuth application credentials for the specified platform.
    /// </summary>
    /// <param name="platformId">The platform identifier (for example, <c>twitch</c>).</param>
    /// <param name="credentials">The OAuth application credentials to encrypt and persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous save operation.</returns>
    ValueTask SetAsync(string platformId, PlatformOAuthAppCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the configured OAuth application credentials for the specified platform.
    /// </summary>
    /// <param name="platformId">The platform identifier (for example, <c>twitch</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if credentials for the platform existed and were removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken = default);
}
