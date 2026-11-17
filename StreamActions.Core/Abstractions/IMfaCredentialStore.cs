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
/// Provides encrypted persistence operations for <see cref="PanelUserMfaCredential"/> records (TOTP secrets, recovery code hashes, and WebAuthn passkeys).
/// </summary>
public interface IMfaCredentialStore
{
    /// <summary>
    /// Determines whether the specified panel user has at least one enrolled MFA credential.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the user has one or more MFA credentials; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> HasAnyCredentialsAsync(Guid panelUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves safe metadata summaries for all MFA credentials enrolled by the specified panel user.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="MfaCredentialSummary"/> items.</returns>
    ValueTask<IReadOnlyList<MfaCredentialSummary>> GetSummariesAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts and persists a TOTP secret and its SHA-256 hashed one-time recovery codes, replacing any prior TOTP credential for the user.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="payload">The plaintext TOTP secret payload to encrypt.</param>
    /// <param name="recoveryCodeHashes">The SHA-256 hex hashes of the generated recovery codes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted credential identifier.</returns>
    ValueTask<Guid> SaveTotpAsync(
        Guid panelUserId,
        TotpSecretPayload payload,
        IReadOnlyList<string> recoveryCodeHashes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and decrypts the enrolled TOTP secret payload for the specified panel user, if present and decryptable.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The decrypted <see cref="TotpSecretPayload"/>, or <see langword="null"/> if absent or undecryptable.</returns>
    ValueTask<TotpSecretPayload?> GetTotpAsync(Guid panelUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a one-time recovery code in constant time and atomically consumes it if valid.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="code">The recovery code submitted by the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a valid unused recovery code matched and was consumed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> VerifyAndConsumeRecoveryCodeAsync(
        Guid panelUserId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of unused recovery codes remaining for the specified panel user.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The remaining recovery code count.</returns>
    ValueTask<int> GetRemainingRecoveryCodesCountAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts and inserts or updates a WebAuthn passkey credential for the specified panel user.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="passkey">The passkey credential payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous save operation.</returns>
    ValueTask UpsertPasskeyAsync(
        Guid panelUserId,
        PasskeyCredentialPayload passkey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and decrypts all WebAuthn passkeys registered for the specified panel user.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="PasskeyCredentialPayload"/> records.</returns>
    ValueTask<IReadOnlyList<PasskeyCredentialPayload>> GetPasskeysAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds and decrypts a specific passkey registered for the specified panel user by its credential ID.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="credentialId">The WebAuthn credential identifier bytes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="PasskeyCredentialPayload"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<PasskeyCredentialPayload?> FindPasskeyAsync(
        Guid panelUserId,
        byte[] credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the panel user and decrypted passkey associated with the specified WebAuthn credential ID.
    /// </summary>
    /// <param name="credentialId">The WebAuthn credential identifier bytes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The owning panel user identifier and passkey payload, or <see langword="null"/> if not found.</returns>
    ValueTask<(Guid PanelUserId, PasskeyCredentialPayload Passkey)?> FindPasskeyByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a WebAuthn passkey credential for the specified panel user by its credential ID.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="credentialId">The WebAuthn credential identifier bytes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a matching passkey was removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> RemovePasskeyAsync(
        Guid panelUserId,
        byte[] credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific MFA credential owned by the specified panel user.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="credentialId">The database credential row identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the credential existed and was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteCredentialAsync(
        Guid panelUserId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all enrolled MFA credentials for the specified panel user (used for break-glass recovery and admin unlock).
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of MFA credential records removed.</returns>
    ValueTask<int> ClearAllForUserAsync(Guid panelUserId, CancellationToken cancellationToken = default);
}
