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

using System;

namespace StreamActions.Database.Documents.Users
{
    /// <summary>
    /// A Document representing follower information, stored in <see cref="UserRelationshipDocument"/>.
    /// </summary>
    public class FollowerDocument
    {
        #region Public Properties

        /// <summary>
        /// The DateTime when the viewer followed.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonDateTimeOptions(DateOnly = false, Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.DateTime)]
        public DateTime FollowedAt { get; set; }

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the follower.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string FollowerId { get; set; }

        #endregion Public Properties
    }
}
