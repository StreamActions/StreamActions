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

using StreamActions.Core.Abstractions;

namespace StreamActions.Core.Models;

/// <summary>
/// Event published on <see cref="IEventBus"/> when one or more web control panel sessions for a user are revoked.
/// </summary>
/// <param name="PanelUserId">The panel user identifier whose sessions were revoked.</param>
/// <param name="ExcludedSessionId">
/// An optional session identifier to keep active (for example, the caller's current session during "Log Out of All Other Sessions"),
/// or <see langword="null"/> when all sessions for the user were revoked.
/// </param>
public sealed record UserSessionsRevokedEvent(
    Guid PanelUserId,
    Guid? ExcludedSessionId = null);

