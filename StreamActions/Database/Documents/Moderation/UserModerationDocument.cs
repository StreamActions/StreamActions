/*
 * Copyright © 2019-2021 StreamActions Team
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
