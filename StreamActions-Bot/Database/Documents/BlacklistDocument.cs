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
using StreamActions.GraphQL.Connections;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// Used for moderation blacklists.
    /// </summary>
    public class BlacklistDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// Id of this blacklist.
        /// </summary>
        [BsonId]
        [BsonElement]
        [BsonRequired]
        public Guid Id { get; set; }

        /// <summary>
        /// If this blacklist uses regex.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool IsRegex { get; set; }

        /// <summary>
        /// Phrase that is blacklisted, is null when IsRegex is true.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string Phrase { get; set; }

        /// <summary>
        /// Type of punishment given when using this phrase.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public ModerationPunishment Punishment { get; set; }

        /// <summary>
        /// Regex phrase that is blacklisted, is null when IsRegex is false.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public Regex RegexPhrase { get; set; }

        /// <summary>
        /// How long the user should be timed-out for when using this phrase, this is null when the punishment is <see cref="ModerationPunishment.Delete"/> or <see cref="ModerationPunishment.Ban"/>.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(600)]
        [BsonIgnoreIfDefault]
        public uint TimeoutTimeSeconds { get; set; }

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.Id.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods
    }
}