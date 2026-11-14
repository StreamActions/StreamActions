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

    /// <summary>
    /// Retrieves a <see cref="Workspace"/> by its unique identifier.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="Workspace"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all Tier 1 global permission groups assigned to the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of assigned global <see cref="PermissionGroup"/> records.</returns>
    ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a Tier 1 global permission group to a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="globalGroupId">The global permission group identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a Tier 1 global permission group assignment from a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="globalGroupId">The global permission group identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the custom workspace group identifiers to which a platform user explicitly belongs.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of matching group identifiers.</returns>
    ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all per-user permission overrides for a platform user within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="UserPermissionOverride"/> records.</returns>
    ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the persisted platform role roster entry for a user within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="WorkspacePlatformRoleRoster"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a permission group.
    /// </summary>
    /// <param name="group">The permission group to create or update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="PermissionGroup"/>.</returns>
    ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a permission group by its identifier.
    /// </summary>
    /// <param name="groupId">The permission group identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or updates a tri-state permission grant on a group.
    /// </summary>
    /// <param name="groupId">The permission group identifier.</param>
    /// <param name="permissionKey">The permission key.</param>
    /// <param name="state">The tri-state permission rule.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all explicit members of a permission group.
    /// </summary>
    /// <param name="groupId">The permission group identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="PermissionGroupMember"/> records.</returns>
    ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a platform user as an explicit member of a permission group.
    /// </summary>
    /// <param name="member">The membership record to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a platform user from a permission group.
    /// </summary>
    /// <param name="groupId">The permission group identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or updates a per-user permission override within a workspace.
    /// </summary>
    /// <param name="userOverride">The user permission override record to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a platform role roster entry for a user within a workspace.
    /// </summary>
    /// <param name="entry">The role roster entry to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds the built-in default permission groups for a newly created workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
