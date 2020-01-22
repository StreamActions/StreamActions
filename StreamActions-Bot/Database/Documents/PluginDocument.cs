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
using System;
using System.Collections.Generic;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// Represents the enabled status of a plugin.
    /// </summary>
    public class PluginDocument
    {
        #region Public Properties

        /// <summary>
        /// A List of ChannelIds where this plugin is enabled.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<string> EnabledChannelIds { get; } = new List<string>();

        /// <summary>
        /// The Guid of the plugin.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        #endregion Public Properties
    }
}