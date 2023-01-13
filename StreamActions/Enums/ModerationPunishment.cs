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

namespace StreamActions.Enums
{
    /// <summary>
    /// Types of punishments the moderation plugin can give.
    /// </summary>
    public enum ModerationPunishment
    {
        /// <summary>
        /// User should be banned.
        /// </summary>
        Ban,

        /// <summary>
        /// User should be timed-out.
        /// </summary>
        Timeout,

        /// <summary>
        /// User should be purged.
        /// </summary>
        Purge,

        /// <summary>
        /// Message should be removed.
        /// </summary>
        Delete,

        /// <summary>
        /// Nothing should be done.
        /// </summary>
        None
    }
}
