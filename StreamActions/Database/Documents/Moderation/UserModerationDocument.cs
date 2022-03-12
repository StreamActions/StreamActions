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

namespace StreamActions.Database.Documents.Moderation
{
    public class UserModerationDocument
    {
        #region Public Properties

        /// <summary>
        /// Id of the channel this belongs too.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// The Guid for referencing this user in the database.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; }

        /// <summary>
        /// If the user has a permit to post links in the chat.
        /// </summary>
        [BsonIgnore]
        public bool IsPermitted => this.PermitExpires <= DateTime.Now;

        /// <summary>
        /// Last time a message was sent. This should only be used for moderation purposes.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime LastMessageSent { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Last time the user got a moderation warning.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime LastModerationWarning { get; set; } = DateTime.MinValue;

        /// <summary>
        /// When the user's permit expires.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime PermitExpires { get; set; } = DateTime.MinValue;

        #endregion Public Properties
    }
}
