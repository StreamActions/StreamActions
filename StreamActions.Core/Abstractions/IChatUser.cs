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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Represents a platform chat user participating in a channel.
/// </summary>
public interface IChatUser
{
    /// <summary>
    /// Gets the platform identifier (for example, <c>twitch</c>).
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the platform-specific user identifier.
    /// </summary>
    string PlatformUserId { get; }

    /// <summary>
    /// Gets the user's login username.
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Gets the user's display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the user's current platform designations in the channel.
    /// </summary>
    PlatformUserDesignations Designations { get; }

    /// <summary>
    /// Converts this chat user to a <see cref="PlatformUserIdentity"/> for permission evaluation.
    /// </summary>
    /// <returns>A <see cref="PlatformUserIdentity"/> representing this chat user.</returns>
    PlatformUserIdentity ToIdentity() => new(this.PlatformId, this.PlatformUserId, this.Username, this.Designations);
}
