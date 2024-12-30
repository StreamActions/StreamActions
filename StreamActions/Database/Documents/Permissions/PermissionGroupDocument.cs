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
using StreamActions.Database.Documents.Users;
using System;
using System.Collections.Generic;

namespace StreamActions.Database.Documents.Permissions
{
    /// <summary>
    /// A document representing a permission group.
    /// </summary>
    public class PermissionGroupDocument : IDocument
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
