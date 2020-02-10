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

        #region Global Moderation Properties

        /// <summary>
        /// The cooldown time in seconds for moderation message to be said in chat. Min: 15s
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(30)]
        public uint ModerationMessageCooldownSeconds { get; set; }

        #endregion Global Moderation Properties

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
        /// If a warning message should be said on a user's second and last offence for links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool LinkTimeoutMessageStatus { get; set; }

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
        /// If a warning message should be said on a user's first offence for links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool LinkWarningMessageStatus { get; set; }

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

        #region Cap Moderation Properties

        /// <summary>
        /// Which roles are excluded from caps moderations, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels CapExcludedLevels { get; set; }

        /// <summary>
        /// The maximum percentage of caps allowed in a message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(50)]
        public uint CapMaximumPercentage { get; set; }

        /// <summary>
        /// How many characters are required before checking a message for caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(15)]
        public uint CapMinimumMessageLength { get; set; }

        /// <summary>
        /// If we should moderate caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool CapStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool CapTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment CapTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint CapTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool CapWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment CapWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint CapWarningTimeSeconds { get; set; }

        #endregion Cap Moderation Properties

        #region Repetition Moderation Properties

        /// <summary>
        /// Maximum number of allowed repeating characters in a message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(10)]
        public uint RepetionMaximumRepeatingCharacters { get; set; }

        /// <summary>
        /// Maximum number of allowed repeating words next to each other.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint RepetionMaximumRepeatingWords { get; set; }

        /// <summary>
        /// Which roles are excluded from repetition moderations, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels RepetitionExcludedLevels { get; set; }

        /// <summary>
        /// Minimum message length before checking for repetition.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(15)]
        public uint RepetitionMinimumMessageLength { get; set; }

        /// <summary>
        /// If we should moderate repetition.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool RepetitionStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for repetition.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool RepetitionTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment RepetitionTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint RepetitionTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for repetition.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool RepetitionWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment RepetitionWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint RepetitionWarningTimeSeconds { get; set; }

        #endregion Repetition Moderation Properties
    }
}