/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
    /// Type of stream status possible
    /// </summary>
    [Flags]
    public enum StreamStatuses
    {
        /// <summary>
        /// Stream is online.
        /// </summary>
        Online = 0,

        /// <summary>
        /// Stream is offline.
        /// </summary>
        Offline = 1
    }
}
