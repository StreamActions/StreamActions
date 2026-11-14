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

namespace StreamActions.Web.Hubs;

/// <summary>
/// Defines client-side SignalR callbacks for permission invalidation notifications.
/// </summary>
public interface IPermissionClient
{
    /// <summary>
    /// Notifies connected clients that permissions for a workspace have been invalidated.
    /// </summary>
    /// <param name="workspaceId">The identifier of the affected workspace.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnPermissionsInvalidatedAsync(Guid workspaceId);
}
