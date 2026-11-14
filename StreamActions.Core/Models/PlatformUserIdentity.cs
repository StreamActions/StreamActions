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

namespace StreamActions.Core.Models;

/// <summary>
/// Represents an authenticated or chat-originated actor identity for permission evaluation.
/// </summary>
/// <param name="PlatformId">The platform identifier (for example, <c>twitch</c>).</param>
/// <param name="PlatformUserId">The platform-specific user identifier.</param>
/// <param name="Username">The actor's username.</param>
/// <param name="Designations">The actor's current platform designations.</param>
/// <param name="PanelUserId">The optional linked panel user identifier.</param>
/// <param name="IsRoot">A value indicating whether the actor is the instance root user.</param>
/// <param name="IsGlobalAdmin">A value indicating whether the actor is a global instance administrator.</param>
public sealed record PlatformUserIdentity(
    string PlatformId,
    string PlatformUserId,
    string Username,
    PlatformUserDesignations Designations = PlatformUserDesignations.None,
    Guid? PanelUserId = null,
    bool IsRoot = false,
    bool IsGlobalAdmin = false);
