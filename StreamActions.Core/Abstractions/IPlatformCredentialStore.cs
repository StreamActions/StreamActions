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
using StreamActions.Core.Models.Entities;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides encrypted persistence operations for <see cref="PlatformCredential"/> OAuth tokens.
/// </summary>
public interface IPlatformCredentialStore
{
    /// <summary>
    /// Retrieves the metadata record for a stored platform credential without decrypting its token payload.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for instance-level default bot credentials.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="PlatformCredential"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<PlatformCredential?> GetCredentialRecordAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all stored credential metadata records for a workspace or instance scope.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for instance-level credentials.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="PlatformCredential"/> records.</returns>
    ValueTask<IReadOnlyList<PlatformCredential>> GetCredentialsForWorkspaceAsync(
        Guid? workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all stored credential metadata records across the instance and all workspaces.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of all <see cref="PlatformCredential"/> records.</returns>
    ValueTask<IReadOnlyList<PlatformCredential>> GetAllCredentialsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve and decrypt a stored <see cref="PlatformTokenPayload"/>, marking the credential as
    /// <see cref="CredentialStatus.KeyRingMismatchRequiresReauth"/> on decryption failure without throwing.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for instance-level credentials.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating whether a record was found, its <see cref="CredentialStatus"/>, and the decrypted <see cref="PlatformTokenPayload"/> when available.</returns>
    ValueTask<(bool Found, CredentialStatus Status, PlatformTokenPayload? Payload)> TryGetTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts and persists a <see cref="PlatformTokenPayload"/> for a platform and token role.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for instance-level credentials.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="payload">The token payload to encrypt and persist.</param>
    /// <param name="status">The credential lifecycle status to store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="PlatformCredential"/>.</returns>
    ValueTask<PlatformCredential> SaveTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        PlatformTokenPayload payload,
        CredentialStatus status = CredentialStatus.Active,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the lifecycle status of an existing stored platform credential.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for instance-level credentials.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="status">The new <see cref="CredentialStatus"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a matching credential existed and was updated; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> UpdateStatusAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CredentialStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stored platform credential.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for instance-level credentials.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="tokenRole">The token role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the credential existed and was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CancellationToken cancellationToken = default);
}
