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
using StreamActions.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// A document representing a permission group.
    /// </summary>
    public class PermissionGroupDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the channel this permission group belongs to.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// The name of the permission group.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string GroupName { get; set; }

        /// <summary>
        /// The Guid for referencing this group in the database.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        /// <summary>
        /// A List of permissions assigned to this group. Permissions not present in the List inherit from other assigned groups,
        /// and default to implicit deny if no assigned groups allow it.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<PermissionDocument> Permissions { get; } = new List<PermissionDocument>();

        #endregion Public Properties

        #region Public Methods

        public string GetCursor()
        {
            _ = I18n.Instance.CurrentCulture.GetValueOrDefault(this.ChannelId, new WeakReference<CultureInfo>(new CultureInfo("en-US"))).TryGetTarget(out CultureInfo culture);
            return this.Id.ToString("D", culture);
        }

        #endregion Public Methods
    }
}