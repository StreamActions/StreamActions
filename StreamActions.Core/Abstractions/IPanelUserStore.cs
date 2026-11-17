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
/// Provides persistence operations for <see cref="PanelUser"/> accounts and their linked <see cref="UserLoginIdentity"/> records.
/// </summary>
public interface IPanelUserStore
{
    /// <summary>
    /// Determines whether any <see cref="PanelUser"/> accounts exist in the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if at least one panel user exists; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a root operator <see cref="PanelUser"/> exists in the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if at least one root panel user exists; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a <see cref="PanelUser"/> by its unique identifier.
    /// </summary>
    /// <param name="userId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="PanelUser"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the <see cref="PanelUser"/> linked to the specified external login identity.
    /// </summary>
    /// <param name="providerId">The identity provider identifier.</param>
    /// <param name="providerUserId">The subject or user identifier issued by the provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="PanelUser"/>, or <see langword="null"/> if no matching identity exists.</returns>
    ValueTask<PanelUser?> GetByLoginIdentityAsync(
        string providerId,
        string providerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all <see cref="PanelUser"/> accounts ordered by creation timestamp.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="PanelUser"/> records.</returns>
    ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all external login identities linked to the specified <see cref="PanelUser"/>.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="UserLoginIdentity"/> records.</returns>
    ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new <see cref="PanelUser"/> and optionally links an initial <see cref="UserLoginIdentity"/> atomically.
    /// </summary>
    /// <param name="user">The panel user to create.</param>
    /// <param name="initialIdentity">An optional initial login identity to link to the new user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="PanelUser"/>.</returns>
    ValueTask<PanelUser> CreateUserAsync(
        PanelUser user,
        UserLoginIdentity? initialIdentity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing <see cref="PanelUser"/> account.
    /// </summary>
    /// <param name="user">The updated panel user fields.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="PanelUser"/>.</returns>
    ValueTask<PanelUser> UpdateUserAsync(PanelUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a new external <see cref="UserLoginIdentity"/> to a <see cref="PanelUser"/> or updates its username if already linked to the same user.
    /// </summary>
    /// <param name="identity">The login identity to link.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="UserLoginIdentity"/>.</returns>
    ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(
        UserLoginIdentity identity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlinks an external login identity from the specified <see cref="PanelUser"/>.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="providerId">The identity provider identifier.</param>
    /// <param name="providerUserId">The subject or user identifier issued by the provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a linked identity was removed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> UnlinkLoginIdentityAsync(
        Guid panelUserId,
        string providerId,
        string providerUserId,
        CancellationToken cancellationToken = default);
}
