/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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
