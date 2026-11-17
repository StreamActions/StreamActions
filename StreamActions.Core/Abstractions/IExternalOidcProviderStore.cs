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
/// Provides persistence and secret management operations for <see cref="ExternalOidcProvider"/> configurations.
/// </summary>
public interface IExternalOidcProviderStore
{
    /// <summary>
    /// Retrieves an <see cref="ExternalOidcProvider"/> by its unique identifier.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="ExternalOidcProvider"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<ExternalOidcProvider?> GetByIdAsync(Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an <see cref="ExternalOidcProvider"/> by scope (<see langword="null"/> for instance-wide) and slug.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier, or <see langword="null"/> for an instance-wide provider.</param>
    /// <param name="slug">The provider slug.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="ExternalOidcProvider"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<ExternalOidcProvider?> GetBySlugAsync(
        Guid? workspaceId,
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all configured external OIDC providers for instance or workspace scope.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier, or <see langword="null"/> for instance-wide providers.</param>
    /// <param name="enabledOnly"><see langword="true"/> to return only enabled providers; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of matching <see cref="ExternalOidcProvider"/> records.</returns>
    ValueTask<IReadOnlyList<ExternalOidcProvider>> GetProvidersAsync(
        Guid? workspaceId,
        bool enabledOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates an <see cref="ExternalOidcProvider"/>, encrypting <paramref name="plaintextClientSecret"/> when supplied.
    /// </summary>
    /// <param name="provider">The provider configuration to persist.</param>
    /// <param name="plaintextClientSecret">
    /// The plaintext OIDC client secret to encrypt; required for new providers, or <see langword="null"/> to preserve the existing secret on updates.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="ExternalOidcProvider"/>.</returns>
    ValueTask<ExternalOidcProvider> UpsertAsync(
        ExternalOidcProvider provider,
        string? plaintextClientSecret = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to decrypt the client secret for an <see cref="ExternalOidcProvider"/> without throwing on key-ring mismatch.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing decryption success, the plaintext client secret if successful, and the resulting <see cref="CredentialStatus"/>.</returns>
    ValueTask<(bool Success, string? ClientSecret, CredentialStatus Status)> TryGetClientSecretAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an <see cref="ExternalOidcProvider"/> by its identifier.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the provider existed and was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(Guid providerId, CancellationToken cancellationToken = default);
}
