/*
 * Copyright © 2019-2022 StreamActions Team
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
using System.Collections.Generic;

namespace StreamActions.Database.Documents.Users
{
    /// <summary>
    /// A document representing user relationships, such as follows.
    /// </summary>
    public class UserRelationshipDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "userRelationships";

        /// <summary>
        /// The <see cref="UserRelationshipDocument.Id"/> of the channel whose relationships are expressed in this document.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string ChannelId { get; set; }

        /// <summary>
        /// A List of known followers. This List may include users who have unfollowed the channel, unless the bot checks and deletes those entries.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<FollowerDocument> Followers { get; } = new List<FollowerDocument>();

        /// <summary>
        /// A List of known subscribers. This List may include users who have unsubscribed from the channel or allowed their subscription to lapse, unless the bot
        /// checks and deletes those entries.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<SubscriberDocument> Subscribers { get; } = new List<SubscriberDocument>();

        #endregion Public Properties
    }
}
