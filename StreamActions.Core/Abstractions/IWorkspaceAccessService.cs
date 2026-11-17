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
/// Evaluates workspace visibility and panel permission access for authenticated <see cref="PanelUser"/> accounts across all linked identities.
/// </summary>
public interface IWorkspaceAccessService
{
    /// <summary>
    /// Retrieves all workspaces accessible to the specified <see cref="PanelUser"/>.
    /// </summary>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The external login identities linked to <paramref name="user"/>.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims from the user's current authentication session.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="AccessibleWorkspace"/> entries.</returns>
    ValueTask<IReadOnlyList<AccessibleWorkspace>> GetAccessibleWorkspacesAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        CancellationToken ct = default);

    /// <summary>
    /// Determines whether the specified <see cref="PanelUser"/> can access the web panel for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The external login identities linked to <paramref name="user"/>.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims from the user's current authentication session.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the user is permitted to access the workspace panel; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> CanAccessWorkspaceAsync(
        Guid workspaceId,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        CancellationToken ct = default);

    /// <summary>
    /// Determines whether the specified <see cref="PanelUser"/> has a specific permission within a workspace across all linked identities.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The external login identities linked to <paramref name="user"/>.</param>
    /// <param name="permissionKey">The permission key to evaluate.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims from the user's current authentication session.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if allowed and not explicitly denied on any linked identity; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> HasPermissionInWorkspaceAsync(
        Guid workspaceId,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        string permissionKey,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        CancellationToken ct = default);
}
