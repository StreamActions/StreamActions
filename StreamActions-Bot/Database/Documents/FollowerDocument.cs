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
using System;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// A Document representing follower information, stored in <see cref="UserRelationshipDocument"/>.
    /// </summary>
    public class FollowerDocument : ICursorable
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

        #region Public Methods

        public string GetCursor() => this.FollowerId;

        #endregion Public Methods
    }
}