/*
 * Copyright © 2019-2022 StreamActions Team
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
using MongoDB.Driver;
using StreamActions.Common.Interfaces;
using System.Collections.Generic;

namespace StreamActions.Database.Documents.Moderation
{
    public class ModerationLogDocument : IDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "moderationLogs";

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the channel this moderation log belongs to.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// All of the moderation entries for this channel.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        public List<ModerationLogEntryDocument> Entries { get; } = new List<ModerationLogEntryDocument>();

        #endregion Public Properties

        #region Public Methods

        public async void Initialize()
        {
            IMongoCollection<ModerationLogDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationLogDocument>(CollectionName);
            IndexKeysDefinitionBuilder<ModerationLogDocument> indexBuilder = Builders<ModerationLogDocument>.IndexKeys;

            try
            {
                CreateIndexModel<ModerationLogDocument> indexModel = new CreateIndexModel<ModerationLogDocument>(indexBuilder.Ascending(d => d.ChannelId).Ascending("Entires.Id"),
                    new CreateIndexOptions { Name = "ModerationLogDocument_unique_ChannelId-Id", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }

            try
            {
                CreateIndexModel<ModerationLogDocument> indexModel = new CreateIndexModel<ModerationLogDocument>(indexBuilder.Ascending(d => d.ChannelId).Ascending("Entires.MessageId"),
                    new CreateIndexOptions { Name = "ModerationLogDocument_unique_ChannelId-MessageId", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }
        }

        #endregion Public Methods
    }
}
