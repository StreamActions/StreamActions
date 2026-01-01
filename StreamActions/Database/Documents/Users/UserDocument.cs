/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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
using StreamActions.Database.Documents.Moderation;
using StreamActions.Enums;
using System;
using System.Collections.Generic;

namespace StreamActions.Database.Documents.Users
{
    /// <summary>
    /// A document representing a Twitch user.
    /// </summary>
    public class UserDocument : IDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "users";

        /// <summary>
        /// The users <see cref="BotGlobal"/>.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(BotGlobal.None)]
        public BotGlobal BotGlobalPermission { get; set; }

        /// <summary>
        /// The users <see cref="TwitchBroadcaster"/>.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(TwitchBroadcaster.None)]
        public TwitchBroadcaster BroadcasterType { get; set; }

        /// <summary>
        /// The users current culture.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CurrentCulture { get; set; }

        /// <summary>
        /// The users Display Name on Twitch, if set.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string DisplayName { get; set; }

        /// <summary>
        /// The users Email address, if available.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string Email { get; set; }

        /// <summary>
        /// The users Twitch user Id.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string Id { get; set; }

        /// <summary>
        /// The users login name on Twitch.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string Login { get; set; }

        /// <summary>
        /// A List of permission group memberships.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        public List<Guid> PermissionGroupMembership { get; } = new List<Guid>();

        /// <summary>
        /// Indicates whether the bot should join and run in this channel.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        public bool ShouldJoin { get; set; }

        /// <summary>
        /// The users <see cref="TwitchStaff"/>.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(TwitchStaff.None)]
        public TwitchStaff StaffType { get; set; }

        /// <summary>
        /// The users <see cref="UserLevels"/> per channel, "string" being the channel Id.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        public Dictionary<string, UserLevels> UserLevel { get; } = new Dictionary<string, UserLevels>();

        /// <summary>
        /// All moderation action related to this user based on channel Id.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        public List<UserModerationDocument> UserModeration { get; } = new List<UserModerationDocument>();

        #endregion Public Properties

        #region Public Methods

        public async void Initialize()
        {
            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>(CollectionName);
            IndexKeysDefinitionBuilder<UserDocument> indexBuilder = Builders<UserDocument>.IndexKeys;

            try
            {
                CreateIndexModel<UserDocument> indexModel = new CreateIndexModel<UserDocument>(indexBuilder.Ascending(d => d.Login),
                    new CreateIndexOptions { Name = "UserDocument_unique_Login", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }

            try
            {
                CreateIndexModel<UserDocument> indexModel = new CreateIndexModel<UserDocument>(indexBuilder.Ascending(d => d.Id),
                    new CreateIndexOptions { Name = "UserDocument_unique_Id", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }

            try
            {
                CreateIndexModel<UserDocument> indexModel = new CreateIndexModel<UserDocument>(indexBuilder.Combine(indexBuilder.Ascending(d => d.Id), indexBuilder.Ascending(d => d.PermissionGroupMembership)),
                    new CreateIndexOptions { Name = "UserDocument_unique_PermissionGroupMembership", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }
        }

        #endregion Public Methods
    }
}
