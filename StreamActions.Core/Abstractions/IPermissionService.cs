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
using StreamActions.Core.Permissions;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Evaluates two-tier permissions for actors within a workspace and manages permission cache invalidation.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Evaluates the specified permission key for an actor within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="actor">The actor identity being evaluated.</param>
    /// <param name="permissionKey">The permission key to evaluate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="PermissionEvaluationResult"/> describing the outcome.</returns>
    ValueTask<PermissionEvaluationResult> EvaluateAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        string permissionKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an actor is allowed to perform the specified permission within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="actor">The actor identity being evaluated.</param>
    /// <param name="permissionKey">The permission key to evaluate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if allowed; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsAllowedAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        string permissionKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached permission data for a specific workspace and publishes a <see cref="PermissionsInvalidatedEvent"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to invalidate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask InvalidateWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all globally cached permission data and publishes a <see cref="PermissionsInvalidatedEvent"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask InvalidateGlobalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a detailed Tier 1 effective permission audit breakdown for a single permission key on the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to audit.</param>
    /// <param name="permissionKey">The permission key to explain.</param>
    /// <param name="viewerIsRootOrGlobalAdmin">
    /// A value indicating whether the operator viewing the audit report is a root operator or global administrator
    /// (controls whether Tier 1 global group names are shown or redacted to <c>Global Permissions</c>).
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="PermissionAuditBreakdown"/> for the permission key.</returns>
    ValueTask<PermissionAuditBreakdown> ExplainWorkspacePermissionAsync(
        Guid workspaceId,
        string permissionKey,
        bool viewerIsRootOrGlobalAdmin = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces detailed Tier 1 effective permission audit breakdowns for all registered permissions on the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to audit.</param>
    /// <param name="viewerIsRootOrGlobalAdmin">
    /// A value indicating whether the operator viewing the audit report is a root operator or global administrator
    /// (controls whether Tier 1 global group names are shown or redacted to <c>Global Permissions</c>).
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="PermissionAuditBreakdown"/> records ordered by category and key.</returns>
    ValueTask<IReadOnlyList<PermissionAuditBreakdown>> ExplainWorkspacePermissionsAsync(
        Guid workspaceId,
        bool viewerIsRootOrGlobalAdmin = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a detailed two-tier effective permission audit breakdown for a single permission key and user within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to audit.</param>
    /// <param name="actor">The target user identity being audited.</param>
    /// <param name="permissionKey">The permission key to explain.</param>
    /// <param name="viewerIsRootOrGlobalAdmin">
    /// A value indicating whether the operator viewing the audit report is a root operator or global administrator
    /// (when <see langword="false"/>, Tier 1 global group names are redacted to <c>Global Permissions</c>).
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="PermissionAuditBreakdown"/> for the user and permission key.</returns>
    ValueTask<PermissionAuditBreakdown> ExplainUserPermissionAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        string permissionKey,
        bool viewerIsRootOrGlobalAdmin = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces detailed two-tier effective permission audit breakdowns for all registered permissions for a user within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to audit.</param>
    /// <param name="actor">The target user identity being audited.</param>
    /// <param name="viewerIsRootOrGlobalAdmin">
    /// A value indicating whether the operator viewing the audit report is a root operator or global administrator
    /// (when <see langword="false"/>, Tier 1 global group names are redacted to <c>Global Permissions</c>).
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="PermissionAuditBreakdown"/> records ordered by category and key.</returns>
    ValueTask<IReadOnlyList<PermissionAuditBreakdown>> ExplainUserPermissionsAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        bool viewerIsRootOrGlobalAdmin = false,
        CancellationToken cancellationToken = default);
}
