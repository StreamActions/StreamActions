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
/// Represents platform-native roles and designations held by a user in a workspace channel.
/// </summary>
[Flags]
public enum PlatformUserDesignations
{
    /// <summary>
    /// No special platform designations.
    /// </summary>
    None = 0,

    /// <summary>
    /// The channel broadcaster or owner.
    /// </summary>
    Broadcaster = 1 << 0,

    /// <summary>
    /// A channel moderator.
    /// </summary>
    Moderator = 1 << 1,

    /// <summary>
    /// A channel VIP.
    /// </summary>
    Vip = 1 << 2,

    /// <summary>
    /// An active channel subscriber.
    /// </summary>
    Subscriber = 1 << 3,

    /// <summary>
    /// A recognized channel regular.
    /// </summary>
    Regular = 1 << 4,

    /// <summary>
    /// A channel follower.
    /// </summary>
    Follower = 1 << 5,
}
