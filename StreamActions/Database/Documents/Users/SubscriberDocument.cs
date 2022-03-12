/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

using StreamActions.Enums;
using System;

namespace StreamActions.Database.Documents.Users
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
        [BsonDateTimeOptions(DateOnly = false, Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.DateTime)]
        public DateTime SubscribedAt { get; set; }

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the subscriber.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string SubscriberId { get; set; }

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
