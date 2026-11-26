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
/// Provides persistence operations for <see cref="Workspace"/> and <see cref="WorkspacePlatform"/> records.
/// </summary>
public interface IWorkspaceStore
{
    /// <summary>
    /// Retrieves a <see cref="Workspace"/> by its unique identifier.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="Workspace"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all workspaces owned by the specified <see cref="PanelUser"/>.
    /// </summary>
    /// <param name="ownerUserId">The owner panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of owned <see cref="Workspace"/> records.</returns>
    ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all workspaces ordered by creation timestamp.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of all <see cref="Workspace"/> records.</returns>
    ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new <see cref="Workspace"/>, optionally attaches its primary <see cref="WorkspacePlatform"/>, and seeds built-in permission groups.
    /// </summary>
    /// <param name="workspace">The workspace to create.</param>
    /// <param name="primaryPlatform">An optional primary platform attachment to create atomically.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="Workspace"/>.</returns>
    ValueTask<Workspace> CreateWorkspaceAsync(
        Workspace workspace,
        WorkspacePlatform? primaryPlatform = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing <see cref="Workspace"/>.
    /// </summary>
    /// <param name="workspace">The updated workspace values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="Workspace"/>.</returns>
    ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a <see cref="Workspace"/> by its identifier.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the workspace existed and was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all platform attachments configured for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="WorkspacePlatform"/> records.</returns>
    ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific platform attachment for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="WorkspacePlatform"/>, or <see langword="null"/> if not attached.</returns>
    ValueTask<WorkspacePlatform?> GetPlatformAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a <see cref="WorkspacePlatform"/> attachment, optionally encrypting a custom OAuth client secret.
    /// </summary>
    /// <param name="platform">The platform attachment details.</param>
    /// <param name="plaintextCustomClientSecret">
    /// Optional plaintext custom client secret to encrypt; pass <see langword="null"/> to preserve any existing secret, or empty/whitespace to clear it.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="WorkspacePlatform"/>.</returns>
    ValueTask<WorkspacePlatform> UpsertPlatformAsync(
        WorkspacePlatform platform,
        string? plaintextCustomClientSecret = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or clears custom OAuth application credentials for an attached workspace platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="clientId">The custom OAuth client identifier, or <see langword="null"/>/whitespace to clear custom credentials.</param>
    /// <param name="clientSecret">
    /// The plaintext custom OAuth client secret, or <see langword="null"/>/whitespace to preserve an existing secret (or configure a public client) unless <paramref name="clearClientSecret"/> is <see langword="true"/>.
    /// </param>
    /// <param name="redirectUri">An optional explicit OAuth redirect URI override for the custom application.</param>
    /// <param name="clearClientSecret">
    /// <see langword="true"/> to explicitly clear any stored custom client secret when <paramref name="clientSecret"/> is <see langword="null"/> or whitespace; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated <see cref="WorkspacePlatform"/>.</returns>
    ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(
        Guid workspaceId,
        string platformId,
        string? clientId,
        string? clientSecret,
        Uri? redirectUri = null,
        bool clearClientSecret = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and decrypts the custom OAuth application credentials for a workspace platform, if configured and decryptable.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The decrypted <see cref="PlatformOAuthAppCredentials"/>, or <see langword="null"/> if absent or undecryptable.</returns>
    ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a platform attachment from a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the attachment existed and was removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> RemovePlatformAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken = default);
}
