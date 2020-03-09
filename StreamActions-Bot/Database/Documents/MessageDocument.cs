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
using System.Globalization;
using TwitchLib.Client.Models;

namespace StreamActions.Database.Documents
{
    public class MessageDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// Id of this message document.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; }

        /// <summary>
        /// Chat message sent <see cref="ChatMessage"/>.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public ChatMessage Message { get; set; }

        /// <summary>
        /// When the message was sent at.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        public DateTime SentAt { get; } = DateTime.Now;

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.Id.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods
    }
}