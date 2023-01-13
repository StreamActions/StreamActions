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

namespace StreamActions.Database.Documents.Users
{
    /// <summary>
    /// A document containing Twitch Connect token information, stored in a <see cref="AuthenticationDocument"/>.
    /// </summary>
    public class TwitchTokenDocument
    {
        #region Public Properties

        /// <summary>
        /// The refresh token.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string Refresh { get; set; }

        /// <summary>
        /// The authorized scopes.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public AuthScopes Scopes { get; set; }

        /// <summary>
        /// The OAuth token
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string Token { get; set; }

        #endregion Public Properties
    }
}
