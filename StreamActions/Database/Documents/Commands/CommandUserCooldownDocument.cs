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
using StreamActions.GraphQL.Connections;
using System;

namespace StreamActions.Database.Documents.Commands
{
    /// <summary>
    /// Contains the cooldown information for a user cooldown, for a particular command. Contained in a <see cref="CommandCooldownDocument"/>.
    /// </summary>
    public class CommandUserCooldownDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// The expiration time of the cooldown.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime CooldownExpires { get; set; }

        /// <summary>
        /// The userId this cooldown applies to.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string UserId { get; set; }

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.UserId;

        #endregion Public Methods
    }
}