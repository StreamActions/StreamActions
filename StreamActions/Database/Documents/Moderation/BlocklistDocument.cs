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

using StreamActions.Enums;
using System;
using System.Text.RegularExpressions;

namespace StreamActions.Database.Documents.Moderation
{
    /// <summary>
    /// Used for moderation blocklists.
    /// </summary>
    public class BlocklistDocument
    {
        #region Public Properties

        /// <summary>
        /// Message said in chat when the user uses the blocklisted phrase.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChatMessage { get; set; }

        /// <summary>
        /// Id of this blocklist.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        /// <summary>
        /// If this blocklist uses regex.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool IsRegex { get; set; }

        /// <summary>
        /// What the blocklist should match on.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(BlocklistMatchTypes.Message)]
        public BlocklistMatchTypes MatchOn { get; set; }

        /// <summary>
        /// Name for this blocklist.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string Name { get; set; }

        /// <summary>
        /// Phrase that is blocklisted, is null when IsRegex is true.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string Phrase { get; set; }

        /// <summary>
        /// Type of punishment given when using this phrase.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment Punishment { get; set; }

        /// <summary>
        /// Regex phrase that is blocklisted, is null when IsRegex is false.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public Regex RegexPhrase { get; set; }

        /// <summary>
        /// If a message should be said in chat when someone uses the blocklist.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool ShouldSendChatMessage { get; set; }

        /// <summary>
        /// Message said next to the timeout.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string TimeoutReason { get; set; }

        /// <summary>
        /// How long the user should be timed-out for when using this phrase, if the punishment is set to <see cref="ModerationPunishment.Timeout"/>.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint TimeoutTimeSeconds { get; set; }

        #endregion Public Properties
    }
}
