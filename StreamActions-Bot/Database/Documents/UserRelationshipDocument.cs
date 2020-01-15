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
    /// A document representing user relationships, such as follows.
    /// </summary>
    public class UserRelationshipDocument
    {
        #region Public Properties

        /// <summary>
        /// A Dictionary of known followers. Key is the <see cref="UserDocument.Id"/> of the follower; value is the timestamp denoting when the user followed the channel.
        /// This Dictionary may include users who have unfollowed the channel, unless the bot checks and deletes those entries.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "BsonDocument")]
        public Dictionary<int, DateTime> Followers { get; set; }

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the channel whose relationships are expressed in this document.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string Id { get; set; }

        /// <summary>
        /// A Dictionary of known subscribers. Key is the <see cref="UserDocument.Id"/> of the subscriber; value is a <see cref="SubscriberDocument"/> denoting the known
        /// information about the subscription. This Dictionary may include users who have unsubscribed from the channel or allowed their subscription to lapse, unless the bot
        /// checks and deletes those entries.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "BsonDocument")]
        public Dictionary<int, SubscriberDocument> Subscribers { get; set; }

        #endregion Public Properties
    }
}