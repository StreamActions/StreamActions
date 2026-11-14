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
/// Defines client-side SignalR callbacks for moderation events.
/// </summary>
public interface IModerationClient
{
    /// <summary>
    /// Notifies connected clients of a moderation event.
    /// </summary>
    /// <param name="eventJson">The serialized moderation event payload.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnModerationEventAsync(string eventJson);
}
