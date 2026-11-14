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

using Microsoft.AspNetCore.SignalR;

namespace StreamActions.Web.Hubs;

/// <summary>
/// SignalR hub for broadcasting permission configuration changes.
/// </summary>
public sealed class PermissionHub : Hub<IPermissionClient>
{
    /// <summary>
    /// Gets the SignalR group name for a workspace's permission notifications.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>The SignalR group name.</returns>
    public static string WorkspaceGroupName(Guid workspaceId) => $"workspace:{workspaceId:N}";

    /// <summary>
    /// Joins the caller to the permission notification group for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task JoinWorkspaceAsync(Guid workspaceId) => this.Groups.AddToGroupAsync(this.Context.ConnectionId, WorkspaceGroupName(workspaceId));

    /// <summary>
    /// Removes the caller from the permission notification group for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task LeaveWorkspaceAsync(Guid workspaceId) => this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, WorkspaceGroupName(workspaceId));
}
