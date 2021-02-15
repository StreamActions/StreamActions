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
using MongoDB.Driver;
using StreamActions.Database.Documents.Users;
using StreamActions.GraphQL.Connections;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StreamActions.Database.Documents.Permissions
{
    /// <summary>
    /// A document representing a permission group.
    /// </summary>
    public class PermissionGroupDocument : ICursorable, IDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "permissionGroups";

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the channel this permission group belongs to.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// The name of the permission group.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string GroupName { get; set; }

        /// <summary>
        /// The Guid for referencing this group in the database.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        /// <summary>
        /// A List of permissions assigned to this group. Permissions not present in the List inherit from other assigned groups,
        /// and default to implicit deny if no assigned groups allow it.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<PermissionDocument> Permissions { get; } = new List<PermissionDocument>();

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.Id.ToString("D", CultureInfo.InvariantCulture);

        public async void Initialize()
        {
            IMongoCollection<PermissionGroupDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<PermissionGroupDocument>(CollectionName);
            IndexKeysDefinitionBuilder<PermissionGroupDocument> indexBuilder = Builders<PermissionGroupDocument>.IndexKeys;

            try
            {
                CreateIndexModel<PermissionGroupDocument> indexModel = new CreateIndexModel<PermissionGroupDocument>(indexBuilder.Ascending(d => d.ChannelId).Ascending(d => d.GroupName),
                    new CreateIndexOptions { Name = "PermissionGroupDocument_unique_ChannelId-GroupName", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }
        }

        #endregion Public Methods
    }
}