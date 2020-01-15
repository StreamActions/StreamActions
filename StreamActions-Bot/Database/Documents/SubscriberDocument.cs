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
using System;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// A document representing subscription information, stored in a <see cref="UserRelationshipDocument"/>.
    /// </summary>
    public class SubscriberDocument
    {
        #region Public Properties

        /// <summary>
        /// The number of continuous months the user has been subscribed for.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(0)]
        [BsonIgnoreIfDefault]
        public int ContinuousMonths { get; set; }

        /// <summary>
        /// Whether the current month is a gift sub.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(false)]
        [BsonIgnoreIfDefault]
        public bool IsGift { get; set; }

        /// <summary>
        /// The timestamp of the last recorded renewal. This may be out of date or set to the time the subscription was shared with chat if the bot does not request this information from the API.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public DateTime SubscribedAt { get; set; }

        /// <summary>
        /// The current month's subscription tier.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public SubscriptionPlan Tier { get; set; }

        /// <summary>
        /// The total number of months the suer has been subscribed for.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(0)]
        [BsonIgnoreIfDefault]
        public int TotalMonths { get; set; }

        #endregion Public Properties
    }
}