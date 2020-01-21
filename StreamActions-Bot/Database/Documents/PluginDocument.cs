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

namespace StreamActions.Database.Documents
{
    public class PluginDocument
    {
        #region Public Properties

        /// <summary>
        /// Id of the channel that owns this plugin.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// Id of the plugin.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public Guid Id { get; }

        /// <summary>
        /// Status of the plugin.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonDefaultValue(false)]
        public bool IsEnabled { get; set; }

        #endregion Public Properties
    }
}