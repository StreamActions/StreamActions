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

namespace StreamActions.Database.Documents.Permissions
{
    /// <summary>
    /// Represents a permission assigned to a <see cref="PermissionGroupDocument"/>.
    /// </summary>
    public class PermissionDocument
    {
        #region Public Properties

        /// <summary>
        /// If <c>true</c>, permission is explicitly denied; otherwise, permission is allowed.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonDefaultValue(false)]
        public bool IsDenied { get; set; }

        /// <summary>
        /// The unique permission name that is being represented.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string PermissionName { get; set; }

        #endregion Public Properties
    }
}
