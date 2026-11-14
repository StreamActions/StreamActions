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
/// Manages encrypted workspace-scoped custom secrets (<c>$secret.NAME</c>).
/// </summary>
public interface ISecretVault
{
    /// <summary>
    /// Decrypts and retrieves a workspace secret value if it is active and unexpired.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The secret name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The decrypted secret value, or <see langword="null"/> if not found or unavailable.</returns>
    ValueTask<string?> GetSecretValueAsync(Guid workspaceId, string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts and stores a workspace secret with allowed host patterns and optional expiration.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The secret name.</param>
    /// <param name="plaintextValue">The plaintext secret value to encrypt.</param>
    /// <param name="allowedHostPatterns">The allowed host patterns for outbound injection.</param>
    /// <param name="expiresAtUtc">An optional UTC expiration timestamp.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous write operation.</returns>
    ValueTask SetSecretAsync(Guid workspaceId, string secretName, string plaintextValue, IReadOnlyCollection<string> allowedHostPatterns, DateTimeOffset? expiresAtUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace secret.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The secret name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the secret was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteSecretAsync(Guid workspaceId, string secretName, CancellationToken cancellationToken = default);
}
