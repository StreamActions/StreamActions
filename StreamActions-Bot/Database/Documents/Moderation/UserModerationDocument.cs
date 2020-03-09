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
using StreamActions.GraphQL.Connections;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StreamActions.Database.Documents.Moderation
{
    public class UserModerationDocument : ICursorable
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
        /// Last time a message was sent.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime LastMessageSent { get; set; }

        /// <summary>
        /// Last time the user got a moderation warning.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime LastModerationWarning { get; set; }

        /// <summary>
        /// List of messages sent by this user to this channel.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        public List<MessageDocument> MessageSent { get; } = new List<MessageDocument>();

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.Id.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods
    }
}