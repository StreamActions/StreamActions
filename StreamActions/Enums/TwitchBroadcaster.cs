﻿/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Enums
{
    /// <summary>
    /// An enum set representing a channels Twitch partner status.
    /// </summary>
    public enum TwitchBroadcaster
    {
        /// <summary>
        /// Normal channel.
        /// </summary>
        None,

        /// <summary>
        /// Affiliate status.
        /// </summary>
        Affiliate,

        /// <summary>
        /// Partner status.
        /// </summary>
        Partner
    }
}
