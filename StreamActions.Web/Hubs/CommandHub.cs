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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;
using StreamActions.Web.Security;
using System.Security.Claims;

namespace StreamActions.Web.Hubs;

/// <summary>
/// SignalR hub for broadcasting command configuration changes.
/// </summary>
[Authorize(Policy = AuthClaims.PanelAuthenticatedPolicy)]
public sealed class CommandHub : Hub<ICommandClient>
{
    /// <summary>
    /// The panel user persistence store.
    /// </summary>
    private readonly IPanelUserStore _panelUserStore;

    /// <summary>
    /// The workspace access evaluation service.
    /// </summary>
    private readonly IWorkspaceAccessService _workspaceAccessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHub"/> class.
    /// </summary>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    public CommandHub(
        IPanelUserStore panelUserStore,
        IWorkspaceAccessService workspaceAccessService)
    {
        ArgumentNullException.ThrowIfNull(panelUserStore);
        ArgumentNullException.ThrowIfNull(workspaceAccessService);

        this._panelUserStore = panelUserStore;
        this._workspaceAccessService = workspaceAccessService;
    }

    /// <summary>
    /// Gets the SignalR group name for a workspace's command notifications.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>The SignalR group name.</returns>
    public static string WorkspaceGroupName(Guid workspaceId) => $"workspace:{workspaceId:N}";

    /// <summary>
    /// Joins the caller to the command notification group for the specified workspace after verifying access.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="HubException">Thrown when the caller is unauthenticated or lacks access to <paramref name="workspaceId"/>.</exception>
    public async Task JoinWorkspaceAsync(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new HubException("Invalid workspace identifier.");
        }

        Guid? panelUserId = AuthClaims.GetPanelUserId(this.Context.User);
        if (!panelUserId.HasValue)
        {
            throw new HubException("Authentication is required to join a workspace command group.");
        }

        PanelUser user = await this._panelUserStore
            .GetByIdAsync(panelUserId.Value, this.Context.ConnectionAborted)
            .ConfigureAwait(false)
            ?? throw new HubException("Authenticated user account was not found.");

        IReadOnlyList<UserLoginIdentity> identities = await this._panelUserStore
            .GetLoginIdentitiesAsync(user.Id, this.Context.ConnectionAborted)
            .ConfigureAwait(false);
        IReadOnlyList<string> oidcGroups = AuthClaims.GetOidcGroups(this.Context.User);
        string? authProvider = this.Context.User?.FindFirstValue(AuthClaims.AuthProviderClaimType);
        bool isMfaVerified = AuthClaims.HasTrueClaim(this.Context.User, AuthClaims.MfaVerifiedClaimType);

        bool canAccess = await this._workspaceAccessService
            .CanAccessWorkspaceAsync(workspaceId, user, identities, oidcGroups, authProvider, isMfaVerified, this.Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (!canAccess)
        {
            throw new HubException("You are not authorized to access this workspace.");
        }

        await this.Groups
            .AddToGroupAsync(this.Context.ConnectionId, WorkspaceGroupName(workspaceId), this.Context.ConnectionAborted)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the caller from the command notification group for the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task LeaveWorkspaceAsync(Guid workspaceId) => this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, WorkspaceGroupName(workspaceId));
}
