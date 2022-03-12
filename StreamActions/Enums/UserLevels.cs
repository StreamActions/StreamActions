/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

using System;

namespace StreamActions.Enums
{
    /// <summary>
    /// An enum set representing all the different levels of permissions a user can have.
    /// This can either be from Twitch or custom made by the user.
    /// </summary>
    [Flags]
    public enum UserLevels
    {
        /// <summary>
        /// Twitch default permission, nothing a viewer.
        /// </summary>
        Viewer = 0,

        /// <summary>
        /// Twitch Subscriber.
        /// </summary>
        Subscriber = 1,

        /// <summary>
        /// Twitch VIP.
        /// </summary>
        VIP = 2,

        /// <summary>
        /// Twitch Moderator
        /// </summary>
        Moderator = 4,

        /// <summary>
        /// Twitch Admin.
        /// </summary>
        TwitchAdmin = 8,

        /// <summary>
        /// Twitch Staff.
        /// </summary>
        TwitchStaff = 16,

        /// <summary>
        /// Twitch Broadcaster.
        /// </summary>
        Broadcaster = 32
    }
}
