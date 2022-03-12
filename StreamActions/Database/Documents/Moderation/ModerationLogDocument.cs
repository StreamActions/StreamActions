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
