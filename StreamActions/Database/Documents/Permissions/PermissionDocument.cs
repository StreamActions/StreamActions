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
