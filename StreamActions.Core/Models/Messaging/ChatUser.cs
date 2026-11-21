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

namespace StreamActions.Core.Models.Messaging;

/// <summary>
/// Immutable concrete representation of an <see cref="IChatUser"/>.
/// </summary>
/// <param name="PlatformId">The platform identifier (for example, <c>twitch</c>).</param>
/// <param name="PlatformUserId">The platform-specific user identifier.</param>
/// <param name="Username">The user's login username.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Designations">The user's current platform designations in the channel.</param>
public sealed record ChatUser(
    string PlatformId,
    string PlatformUserId,
    string Username,
    string DisplayName,
    PlatformUserDesignations Designations = PlatformUserDesignations.None) : IChatUser;
