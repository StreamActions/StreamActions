/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Database.Documents.Commands
{
    /// <summary>
    /// Contains the cooldown information for a user cooldown, for a particular command. Contained in a <see cref="CommandCooldownDocument"/>.
    /// </summary>
    public class CommandUserCooldownDocument
    {
        #region Public Properties

        /// <summary>
        /// The expiration time of the cooldown.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime CooldownExpires { get; set; }

        /// <summary>
        /// The userId this cooldown applies to.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string UserId { get; set; }

        #endregion Public Properties
    }
}
