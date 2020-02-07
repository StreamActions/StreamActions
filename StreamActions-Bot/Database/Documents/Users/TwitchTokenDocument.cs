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
using TwitchLib.Api.Core.Enums;

namespace StreamActions.Database.Documents.Users
{
    /// <summary>
    /// A document containing Twitch Connect token information, stored in a <see cref="AuthenticationDocument"/>.
    /// </summary>
    public class TwitchTokenDocument
    {
        #region Public Properties

        [BsonElement]
        [BsonRequired]
        public string Refresh { get; set; }

        [BsonElement]
        [BsonRequired]
        public AuthScopes Scopes { get; set; }

        [BsonElement]
        [BsonRequired]
        public string Token { get; set; }

        #endregion Public Properties
    }
}