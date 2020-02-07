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
using StreamActions.Database.Documents.Users;
using StreamActions.GraphQL.Connections;
using System;
using System.Globalization;

namespace StreamActions.Database.Documents.Permissions
{
    /// <summary>
    /// Represents a users membership in a permission group.
    /// </summary>
    public class PermissionGroupMembershipDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the channel where this group applies.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// The <see cref="PermissionGroupDocument.Id"/> of the group this user is a member of.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid PermissionGroupId { get; set; }

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.PermissionGroupId.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods
    }
}