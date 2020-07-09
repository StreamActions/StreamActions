/*
 * Copyright © 2019-2020 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using MongoDB.Bson.Serialization.Attributes;
using StreamActions.Enums;
using StreamActions.GraphQL.Connections;
using System;
using System.Globalization;

namespace StreamActions.Database.Documents.Moderation
{
    public class ModerationLogEntryDocument : ICursorable
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

        #region Public Methods

        public string GetCursor() => this.Id.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods
    }
}