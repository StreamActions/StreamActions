/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using System;
using System.Collections.Generic;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// Represents the enabled status of a plugin.
    /// </summary>
    public class PluginDocument : IDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "plugins";

        /// <summary>
        /// A List of ChannelIds where this plugin is enabled.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<string> EnabledChannelIds { get; } = new List<string>();

        /// <summary>
        /// The Guid of the plugin.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        #endregion Public Properties

        #region Public Methods

        public async void Initialize()
        {
            IMongoCollection<PluginDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PluginDocument>(CollectionName);
            IndexKeysDefinitionBuilder<PluginDocument> indexBuilder = Builders<PluginDocument>.IndexKeys;

            try
            {
                CreateIndexModel<PluginDocument> indexModel = new CreateIndexModel<PluginDocument>(indexBuilder.Ascending(d => d.EnabledChannelIds),
                    new CreateIndexOptions { Name = "PluginDocument_unique_EnabledChannelIds", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }
        }

        #endregion Public Methods
    }
}
