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

using StreamActions.Enums;
using System;

namespace StreamActions.Database.Documents.Moderation
{
    public class ModerationLogEntryDocument
    {
        #region Public Properties

        /// <summary>
        /// The Guid for referencing this moderation log entry in the database.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        /// <summary>
        /// The chat message that triggered the action.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string Message { get; set; }

        /// <summary>
        /// The Twitch message Id.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string MessageId { get; set; }

        /// <summary>
        /// The message sent to chat after the action was taken.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ModerationMessage { get; set; }

        /// <summary>
        /// The reason attached to the moderation command.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ModerationReason { get; set; }

        /// <summary>
        /// The type of punishment given to the user.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public ModerationPunishment Punishment { get; set; }

        /// <summary>
        /// If <see cref="Punishment"/> was <see cref="ModerationPunishment.Timeout"/>, the number of seconds the user was timed out for.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(0)]
        public uint TimeoutSeconds { get; set; }

        #endregion Public Properties
    }
}
