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
using StreamActions.Enums;
using StreamActions.GraphQL.Connections;
using System;
using System.Collections.Generic;

namespace StreamActions.Database.Documents.Users
{
    /// <summary>
    /// A document representing a Twitch user.
    /// </summary>
    public class UserDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// The users <see cref="BotGlobal"/>.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(BotGlobal.None)]
        [BsonIgnoreIfDefault]
        public BotGlobal BotGlobalPermission { get; set; }

        /// <summary>
        /// The users <see cref="TwitchBroadcaster"/>.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(TwitchBroadcaster.None)]
        [BsonIgnoreIfDefault]
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
        /// The users <see cref="TwitchStaff"/>.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(TwitchStaff.None)]
        [BsonIgnoreIfDefault]
        public TwitchStaff StaffType { get; set; }

        /// <summary>
        /// The users <see cref="UserLevels"/>.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(UserLevels.Viewer)]
        [BsonIgnoreIfDefault]
        public UserLevels UserLevels { get; set; }

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.Id;

        #endregion Public Methods
    }
}