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
using System.Collections.Generic;

namespace StreamActions.Database.Documents
{
    public class ModerationDocument
    {
        #region Main document Properties

        /// <summary>
        /// Id of the channel these settings belong too.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// Id for these settings.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        #endregion Main document Properties

        #region Blacklist Moderation Properties

        [BsonElement]
        [BsonIgnoreIfNull]
        /// <summary>
        /// Where all channel blacklists are stored.
        /// </summary>
        public List<BlacklistDocument> Blacklist { get; set; }

        #endregion Blacklist Moderation Properties

        #region Link Moderation Properties

        /// <summary>
        /// If users should be able to post clips for this channel even when link moderation is enabled.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool LinkAllowChannelClips { get; set; }

        /// <summary>
        /// Which roles are excluded from link moderations, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels LinkExcludedLevels { get; set; }

        /// <summary>
        /// How long a user will be permited to post a link.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(30)]
        public uint LinkPermitTimeSeconds { get; set; }

        /// <summary>
        /// If we should moderate links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool LinkStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkTimeoutMessage { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment LinkTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint LinkTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkWarningMessage { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment LinkWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint LinkWarningTimeSeconds { get; set; }

        /// <summary>
        /// List to store all of the whitelists.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<string> LinkWhitelist { get; set; }

        #endregion Link Moderation Properties
    }
}