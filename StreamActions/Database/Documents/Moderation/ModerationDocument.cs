/*
 * Copyright © 2019-2021 StreamActions Team
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
using MongoDB.Driver;
using StreamActions.Enums;
using StreamActions.Interfaces.Database;
using System;
using System.Collections.Generic;

namespace StreamActions.Database.Documents.Moderation
{
    public class ModerationDocument : IDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "moderations";

        /// <summary>
        /// Which roles are excluded from not using an action message (/me) moderations, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels ActionMessageExcludedLevels { get; set; }

        /// <summary>
        /// If we should moderate action (/me) messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool ActionMessageStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ActionMessageTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool ActionMessageTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment ActionMessageTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ActionMessageTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint ActionMessageTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ActionMessageWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool ActionMessageWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment ActionMessageWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ActionMessageWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for using an action message (/me).
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint ActionMessageWarningTimeSeconds { get; set; }

        [BsonElement]
        [BsonIgnoreIfNull]
        public List<BlocklistDocument> Blocklist { get; } = new List<BlocklistDocument>();

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
        /// Message said in chat on a user's second and last offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offense for caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool CapTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment CapTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint CapTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offense for caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool CapWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment CapWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string CapWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offense for using caps.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint CapWarningTimeSeconds { get; set; }

        /// <summary>
        /// Id of the channel these settings belong too.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// Which roles are excluded from excessive emote use moderation, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels EmoteExcludedLevels { get; set; }

        /// <summary>
        /// How many Twitch emotes can be in a message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(10)]
        public uint EmoteMaximumAllowed { get; set; }

        /// <summary>
        /// If a message only containing emotes should be removed.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(10)]
        public bool EmoteRemoveOnlyEmotes { get; set; }

        /// <summary>
        /// If we should moderate emotes.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool EmoteStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string EmoteTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool EmoteTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment EmoteTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string EmoteTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint EmoteTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string EmoteWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool EmoteWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment EmoteWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string EmoteWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for excessive emote use.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint EmoteWarningTimeSeconds { get; set; }

        /// <summary>
        /// Which roles are excluded from using fake purges moderation, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels FakePurgeExcludedLevels { get; set; }

        /// <summary>
        /// If we should moderate fake purge messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool FakePurgeStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string FakePurgeTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool FakePurgeTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment FakePurgeTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string FakePurgeTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint FakePurgeTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string FakePurgeWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool FakePurgeWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment FakePurgeWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string FakePurgeWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for using a fake purge.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint FakePurgeWarningTimeSeconds { get; set; }

        /// <summary>
        /// Id for these settings.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        /// <summary>
        /// Last time a warning or timeout message was sent
        /// </summary>
        [BsonIgnore]
        public DateTime LastModerationMessageSent { get; set; } = DateTime.Now;

        /// <summary>
        /// Which roles are excluded from Lengthy message moderations, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels LengthyMessageExcludedLevels { get; set; }

        /// <summary>
        /// Maximum length for a message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(300)]
        public uint LengthyMessageMaximumLength { get; set; }

        /// <summary>
        /// If we should moderate lengthy messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool LengthyMessageStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LengthyMessageTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for a Lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool LengthyMessageTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment LengthyMessageTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LengthyMessageTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint LengthyMessageTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LengthyMessageWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for a Lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool LengthyMessageWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment LengthyMessageWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LengthyMessageWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for a lengthy message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint LengthyMessageWarningTimeSeconds { get; set; }

        /// <summary>
        /// List to store all of the allowlists.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public List<string> LinkAllowlist { get; } = new List<string>();

        /// <summary>
        /// Which roles are excluded from link moderations, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels LinkExcludedLevels { get; set; }

        /// <summary>
        /// How long a user will be permitted to post a link.
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
        /// Message said in chat on a user's second and last offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offense for links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool LinkTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment LinkTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint LinkTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// If we should use aggressive URL detection in the channel.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool LinkUseAggressiveDetection { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offense for links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool LinkWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment LinkWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string LinkWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offense for using links.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint LinkWarningTimeSeconds { get; set; }

        /// <summary>
        /// The cooldown time in seconds for moderation message to be said in chat. Minimum: 15s
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(30)]
        public uint ModerationMessageCooldownSeconds { get; set; }

        /// <summary>
        /// How long a warning lasts on a single user in seconds.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(86400)]
        public uint ModerationWarningTimeSeconds { get; set; }

        /// <summary>
        /// Which roles are excluded from one man spam moderation, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels OneManSpamExcludedLevels { get; set; }

        /// <summary>
        /// How many messages can a user send in a time periode.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(20)]
        public uint OneManSpamMaximumMessages { get; set; }

        /// <summary>
        /// How many seconds is the time periode for sending messages
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(20)]
        public uint OneManSpamResetTimeSeconds { get; set; }

        /// <summary>
        /// If we should moderate one man spam
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool OneManSpamStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string OneManSpamTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool OneManSpamTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment OneManSpamTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string OneManSpamTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint OneManSpamTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string OneManSpamWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool OneManSpamWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment OneManSpamWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string OneManSpamWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for spamming chat with messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint OneManSpamWarningTimeSeconds { get; set; }

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
        /// Message said in chat on a user's second and last offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offense for repetition.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool RepetitionTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment RepetitionTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint RepetitionTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offense for repetition.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool RepetitionWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment RepetitionWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string RepetitionWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offense for repetition in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint RepetitionWarningTimeSeconds { get; set; }

        /// <summary>
        /// Which roles are excluded from symbols moderations, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels SymbolExcludedLevels { get; set; }

        /// <summary>
        /// How many symbols can get grouped together in a message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(10)]
        public uint SymbolMaximumGrouped { get; set; }

        /// <summary>
        /// How many symbols are allowed in a message based on percent.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(30)]
        public uint SymbolMaximumPercent { get; set; }

        /// <summary>
        /// How many characters required in a message before checking for symbols.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(10)]
        public uint SymbolMinimumMessageLength { get; set; }

        /// <summary>
        /// If we should moderate Symbol.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool SymbolStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string SymbolTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for symbols.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool SymbolTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment SymbolTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string SymbolTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint SymbolTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string SymbolWarningMessage { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment SymbolWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string SymbolWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for symbols in the message.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint SymbolWarningTimeSeconds { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for symbols.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool WarningMessageStatus { get; set; }

        /// <summary>
        /// Which roles are excluded from using characters or similar moderation, note that moderators and above are always excluded.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public UserLevels ZalgoExcludedLevels { get; set; }

        /// <summary>
        /// If we should moderate zalgo characters or similar messages.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool ZalgoStatus { get; set; }

        /// <summary>
        /// Message said in chat on a user's second and last offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ZalgoTimeoutMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's second and last offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool ZalgoTimeoutMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user second and last offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment ZalgoTimeoutPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's second and last offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ZalgoTimeoutReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their second and last offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(600)]
        public uint ZalgoTimeoutTimeSeconds { get; set; }

        /// <summary>
        /// Message said in chat on a user's first offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ZalgoWarningMessage { get; set; }

        /// <summary>
        /// If a warning message should be said on a user's first offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool ZalgoWarningMessageStatus { get; set; }

        /// <summary>
        /// Type of punishment given for a user's first offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(ModerationPunishment.Timeout)]
        public ModerationPunishment ZalgoWarningPunishment { get; set; }

        /// <summary>
        /// Message said next to the timeout message on a user's first offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string ZalgoWarningReason { get; set; }

        /// <summary>
        /// How long a user will get timed-out on their first offence for using zalgo characters or similar.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(5)]
        public uint ZalgoWarningTimeSeconds { get; set; }

        #endregion Public Properties

        #region Public Methods

        public async void Initialize()
        {
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(CollectionName);
            IndexKeysDefinitionBuilder<ModerationDocument> indexBuilder = Builders<ModerationDocument>.IndexKeys;

            try
            {
                CreateIndexModel<ModerationDocument> indexModel = new CreateIndexModel<ModerationDocument>(indexBuilder.Ascending(d => d.ChannelId),
                    new CreateIndexOptions { Name = "ModerationDocument_unique_ChannelId", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }
        }

        #endregion Public Methods
    }
}
