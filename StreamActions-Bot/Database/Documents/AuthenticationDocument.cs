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

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// A document containing authentication information for a Twitch user.
    /// </summary>
    public class AuthenticationDocument
    {
        #region Public Properties

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
    }
}