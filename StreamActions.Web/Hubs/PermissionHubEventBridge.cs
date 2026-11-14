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
using Microsoft.Extensions.Hosting;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Permissions;

namespace StreamActions.Web.Hubs;

/// <summary>
/// Hosted service that forwards <see cref="PermissionsInvalidatedEvent"/> notifications from <see cref="IEventBus"/>
/// to connected SignalR clients on <see cref="PermissionHub"/>.
/// </summary>
public sealed class PermissionHubEventBridge : IHostedService, IDisposable
{
    /// <summary>
    /// The event bus used to subscribe to permission invalidation events.
    /// </summary>
    private readonly IEventBus _eventBus;

    /// <summary>
    /// The SignalR hub context for <see cref="PermissionHub"/>.
    /// </summary>
    private readonly IHubContext<PermissionHub, IPermissionClient> _hubContext;

    /// <summary>
    /// The active event bus subscription handle, or <see langword="null"/> when stopped.
    /// </summary>
    private IDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionHubEventBridge"/> class.
    /// </summary>
    /// <param name="eventBus">The event bus to subscribe to.</param>
    /// <param name="hubContext">The SignalR hub context for <see cref="PermissionHub"/>.</param>
    public PermissionHubEventBridge(
        IEventBus eventBus,
        IHubContext<PermissionHub, IPermissionClient> hubContext)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(hubContext);

        this._eventBus = eventBus;
        this._hubContext = hubContext;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this._subscription = this._eventBus.Subscribe<PermissionsInvalidatedEvent>(this.HandlePermissionsInvalidatedAsync);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this._subscription?.Dispose();
        this._subscription = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._subscription?.Dispose();
        this._subscription = null;
    }

    /// <summary>
    /// Forwards a <see cref="PermissionsInvalidatedEvent"/> to the affected workspace SignalR group or all clients.
    /// </summary>
    /// <param name="evt">The permission invalidation event.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous broadcast.</returns>
    private async ValueTask HandlePermissionsInvalidatedAsync(
        PermissionsInvalidatedEvent evt,
        CancellationToken cancellationToken)
    {
        if (evt.WorkspaceId is Guid workspaceId)
        {
            await this._hubContext.Clients
                .Group(PermissionHub.WorkspaceGroupName(workspaceId))
                .OnPermissionsInvalidatedAsync(workspaceId)
                .ConfigureAwait(false);
            return;
        }

        await this._hubContext.Clients.All
            .OnPermissionsInvalidatedAsync(Guid.Empty)
            .ConfigureAwait(false);
    }
}
