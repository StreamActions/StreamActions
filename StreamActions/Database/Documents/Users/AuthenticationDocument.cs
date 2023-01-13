/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Database.Documents.Users
{
    /// <summary>
    /// A document containing authentication information for a Twitch user.
    /// </summary>
    public class AuthenticationDocument : IDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "authentications";

        /// <summary>
        /// A bearer token that authenticates this user on the GraphQL WebSocket server.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string GraphQLBearer { get; set; }

        /// <summary>
        /// A <see cref="TwitchTokenDocument"/> containing the user's Twitch Connect token.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public TwitchTokenDocument TwitchToken { get; set; }

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the user whose authentication information is expressed in this document.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string UserId { get; set; }

        #endregion Public Properties

        #region Public Methods

        public async void Initialize()
        {
            IMongoCollection<AuthenticationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<AuthenticationDocument>(CollectionName);
            IndexKeysDefinitionBuilder<AuthenticationDocument> indexBuilder = Builders<AuthenticationDocument>.IndexKeys;

            try
            {
                CreateIndexModel<AuthenticationDocument> indexModel = new CreateIndexModel<AuthenticationDocument>(indexBuilder.Ascending(d => d.GraphQLBearer),
                    new CreateIndexOptions { Name = "AuthenticationDocument_unique_GraphQLBearer", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }
        }

        #endregion Public Methods
    }
}
