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
/// Provides access to permission groups, grants, and tri-state permission evaluation.
/// </summary>
public interface IPermissionStore
{
    /// <summary>
    /// Retrieves permission groups for the specified workspace or global scope.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier, or <see langword="null"/> for global groups.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of matching <see cref="PermissionGroup"/> records.</returns>
    ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permission grants assigned to a permission group.
    /// </summary>
    /// <param name="groupId">The permission group identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="PermissionGrant"/> records.</returns>
    ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates the effective <see cref="PermissionState"/> for a permission key across the specified groups.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="groupIds">The group identifiers to evaluate.</param>
    /// <param name="permissionKey">The permission key to evaluate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The effective <see cref="PermissionState"/>.</returns>
    ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default);
}
