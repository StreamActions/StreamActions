﻿/*
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

using StreamActions.Plugin;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using System.Linq;
using System.Runtime.InteropServices;
using System.Globalization;
using StreamActions.Database.Documents;
using MongoDB.Driver;
using StreamActions.Database;
using System.Threading.Tasks;
using StreamActions.Enums;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles various moderation actions against chat messages, such as links protection.
    /// </summary>
    [Guid("723C3D97-7FB3-40C9-AFC5-904D38170384")]
    public class ChatModerator : IPlugin
    {
        #region Public Constructors

        /// <summary>
        /// Class constructor.
        /// </summary>
        public ChatModerator()
        {
        }

        #endregion Public Constructors

        #region Public Properties

        public bool AlwaysEnabled => true;

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Chat Moderation plugin for StreamActions";

        public Guid PluginId => typeof(ChatModerator).GUID;

        public string PluginName => "ChatModerator";

        public Uri PluginUri => new Uri("https://github.com/StreamActions/StreamActions-Bot");

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        public void Disabled()
        {
        }

        public void Enabled()
        {
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnCapsCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnColouredMessageCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnEmotesCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnFakePurgeCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnLinksCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnLongMessageCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnOneManSpamCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnRepetitionCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnSymbolsCheck;
            PluginManager.Instance.OnMessageModeration += this.ChatModerator_OnZalgoCheck;
        }

        public string GetCursor() => this.PluginId.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods

        #region Private filter regex

        // Ignore Spelling: cdefgilmnoqrstuwxz, abdefghijmnorstvwyz, acdfghiklmnoruvxyz, ejkmoz, cegrstu, fyi, ijkmor, abdefghilmnpqrstuwy, kmnrtu, delmnoqrst, emop, eghimnrwyz
        // Ignore Spelling: eghimnrwyz, eghimnrwyz, eghimnrwyz, abcikrstuvy, mobi, moe, acdeghklmnopqrstuvwxyz, acefgilopruz, aefghklmnrstwy, qa, eouw, abcdeghijklmnortuvyz
        // Ignore Spelling: cdfghjklmnoprtvwz, agkmsyz, ceginu, xxx, fs, etu, amw

        /// <summary>
        /// Regular expression that is used for getting the number of capital letters in a string.
        /// Example: I AM YELLING!
        /// </summary>
        private readonly Regex _capsRegex = new Regex(@"[A-Z]", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used to getting the number of repeating characters in a string.
        /// Example: aaaaaaaaaaaaaaaaaaaaaaaa
        /// </summary>
        private readonly Regex _characterRepetitionRegex = new Regex(@"(\S)\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the amount of grouped symbols in a string.
        /// Example: #$#$#$#$#$#$#$#$#$#$$#$#$$##### how are you ???????????????
        /// </summary>
        private readonly Regex _groupedSymbolsRegex = new Regex(@"([-!$%#^&*()_+|~=`{}\[\]:'<>?,.\/\\;""])\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is for finding URLs in a string.
        /// Example: google.com
        /// </summary>
        private readonly Regex _linkRegex = new Regex(@"((?:(http|https|rtsp):\/\/(?:(?:[a-z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,64}(?:\:(?:[a-z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,25})?\@)?)?((?:(?:[a-z0-9][a-z0-9\-]{0,64}\.)+(?:(?:aero|a[cdefgilmnoqrstuwxz])|(?:biz|bike|bot|b[abdefghijmnorstvwyz])|(?:com|c[acdfghiklmnoruvxyz])|d[ejkmoz]|(?:edu|e[cegrstu])|(?:fyi|f[ijkmor])|(?:gov|g[abdefghilmnpqrstuwy])|(?:how|h[kmnrtu])|(?:info|i[delmnoqrst])|(?:jobs|j[emop])|k[eghimnrwyz]|l[abcikrstuvy]|(?:mil|mobi|moe|m[acdeghklmnopqrstuvwxyz])|(?:name|net|n[acefgilopruz])|(?:org|om)|(?:pro|p[aefghklmnrstwy])|qa|(?:r[eouw])|(?:s[abcdeghijklmnortuvyz])|(?:t[cdfghjklmnoprtvwz])|u[agkmsyz]|(?:vote|v[ceginu])|(?:xxx)|(?:watch|w[fs])|y[etu]|z[amw]))|(?:(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[1-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[1-9]|0)\\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[1-9]|0)\\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])))(?:\:\d{1,5})?)(\/(?:(?:[a-z0-9\;\/\?\:\@\&\=\#\~\-\.\+\!\*\\\'\(\)\,\_])|(?:\%[a-fA-F0-9]{2}))*)?(?:\b|$)|(\.[a-z]+\/|magnet:\/\/|mailto:\/\/|ed2k:\/\/|irc:\/\/|ircs:\/\/|skype:\/\/|ymsgr:\/\/|xfire:\/\/|steam:\/\/|aim:\/\/|spotify:\/\/)", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the number of symbols in a string.
        /// Example: ^^^^#%#%#^#^##*#
        /// </summary>
        private readonly Regex _symbolsRegex = new Regex(@"[-!$%#^&*()_+|~=`{}\[\]:'<>?,.\/\\;""]", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used to getting the number of repeating words in a string.
        /// Example: hi hi hi hi hi hi hi
        /// </summary>
        private readonly Regex _wordRepetitionRegex = new Regex(@"(\S+\s)\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the number of zalgo/boxed/disruptive symbols in a string.
        /// </summary>
        private readonly Regex _zalgoRegex = new Regex(@"[^\uD83C-\uDBFF\uDC00-\uDFFF\u0401\u0451\u0410-\u044f\u0009-\u02b7\u2000-\u20bf\u2122\u0308]", RegexOptions.Compiled);

        #endregion Private filter regex

        #region Filter check methods

        /// <summary>
        /// Method that will return the longest sequence amount of repeating characters in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating characters in the message.</returns>
        private int GetLongestSequenceOfRepeatingCharacters(string message) => this._characterRepetitionRegex.Matches(message).DefaultIfEmpty().Max(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that will return the longest sequence amount of repeating symbol in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating symbols in the message.</returns>
        private int GetLongestSequenceOfRepeatingSymbols(string message) => this._groupedSymbolsRegex.Matches(message).DefaultIfEmpty().Max(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that will return the longest sequence amount of repeating words in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating words in the message.</returns>
        private int GetLongestSequenceOfRepeatingWords(string message) => this._wordRepetitionRegex.Matches(message).DefaultIfEmpty().Max(m => (m is null ? 0 : m.Value.Split(' ').Length));

        /// <summary>
        /// Method that gets the length of a message
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>The length of the message.</returns>
        private int GetMessageLength(string message) => message.Length;

        /// <summary>
        /// Method that gets the number of caps in a message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Number of caps in the message.</returns>
        private int GetNumberOfCaps(string message) => this._capsRegex.Matches(message).DefaultIfEmpty().Sum(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that gets the number of emotes in a message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Number of emotes in the message.</returns>
        private int GetNumberOfEmotes(EmoteSet emoteSet) => emoteSet.Emotes.Count;

        /// <summary>
        /// Method that gets the number of symbols in a message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Number of symbols in the message.</returns>
        private int GetNumberOfSymbols(string message) => this._symbolsRegex.Matches(message).DefaultIfEmpty().Sum(m => (m is null ? 0 : m.Length));

        /// <summary>
        /// Method that checks if the message has a fake purge.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>If the message has a fake purge.</returns>
        private bool HasFakePurge(string message) =>
            message.Equals("<message deleted>", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("<deleted message>", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("message deleted by a moderator.", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("message removed by a moderator.", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Method that is used to check if a message is coloured, meaning it starts with the command /me on Twitch.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>True if the message starts with /me.</returns>
        private bool HasTwitchAction(string message) => message.StartsWith("/me", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Method that is used to check if the message contains a URL.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>True if the message has a URL.</returns>
        private bool HasUrl(string message) => this._linkRegex.IsMatch(message);

        /// <summary>
        /// Method that is used to check if a message contains zalgo characters.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>True if the message has zalgo characters.</returns>
        private bool HasZalgo(string message) => this._zalgoRegex.IsMatch(message);

        /// <summary>
        /// Method that removes Twitch emotes from a string.
        /// </summary>
        /// <param name="message">Main message.</param>
        /// <param name="emoteSet">Emotes in the message.</param>
        /// <returns>The message without any emotes.</returns>
        private string RemoveEmotesFromMessage(string message, EmoteSet emoteSet)
        {
            List<Emote> emotes = emoteSet.Emotes;

            for (int i = emotes.Count - 1; i >= 0; i--)
            {
                message = message.Remove(emotes[i].StartIndex, (emotes[i].EndIndex - emotes[i].StartIndex));
            }

            return message;
        }

        #endregion Filter check methods

        #region Private Methods

        /// <summary>
        /// Returns the filter settings for a channel.
        /// </summary>
        /// <param name="channelId">Id of the channel.</param>
        /// <returns>The document settings.</returns>
        public static async Task<ModerationDocument> GetFilterDocumentForChannel(string channelId)
        {
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>("moderation");

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Eq(m => m.ChannelId, channelId);

            using IAsyncCursor<ModerationDocument> cursor = (await collection.FindAsync(filter).ConfigureAwait(false));

            return (await cursor.FirstAsync().ConfigureAwait(false));
        }

        /// <summary>
        /// Method that will check the message for excessive use of caps.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnCapsCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;
            string message = this.RemoveEmotesFromMessage(e.ChatMessage.Message, e.ChatMessage.EmoteSet);

            // Check if the caps filter is enabled.
            if (document.CapStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage.UserId, UserLevels.Broadcaster | UserLevels.Moderator | document.CapExcludedLevels).ConfigureAwait(false))
                {
                    if (this.GetMessageLength(message) >= document.CapMinimumMessageLength)
                    {
                        if (((this.GetNumberOfCaps(message) / this.GetMessageLength(message)) * 100.0) >= document.CapMaximumPercentage)
                        {
                            if (this.UserHasWarning(document, e.ChatMessage.UserId))
                            {
                                moderationResult.ShouldTimeout = document.CapTimeoutPunishment.Equals(ModerationPunishment.Timeout);
                                moderationResult.ShouldDelete = document.CapTimeoutPunishment.Equals(ModerationPunishment.Delete);
                                moderationResult.ShouldPurge = document.CapTimeoutPunishment.Equals(ModerationPunishment.Purge);
                                moderationResult.TimeoutSeconds = document.CapTimeoutTimeSeconds;
                                moderationResult.ModerationReason = document.CapTimeoutReason;
                                moderationResult.ModerationMessage = document.CapTimeoutMessage;
                            }
                            else
                            {
                                moderationResult.ShouldTimeout = document.CapWarningPunishment.Equals(ModerationPunishment.Timeout);
                                moderationResult.ShouldDelete = document.CapWarningPunishment.Equals(ModerationPunishment.Delete);
                                moderationResult.ShouldPurge = document.CapWarningPunishment.Equals(ModerationPunishment.Purge);
                                moderationResult.TimeoutSeconds = document.CapWarningTimeSeconds;
                                moderationResult.ModerationReason = document.CapWarningReason;
                                moderationResult.ModerationMessage = document.CapWarningMessage;
                            }
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for the use of the /me command on Twitch.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnColouredMessageCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            // Check if the caps filter is enabled.
            if (document.ActionMessageStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage.UserId, UserLevels.Broadcaster | UserLevels.Moderator | document.ActionMessageExcludedLevels).ConfigureAwait(false))
                {
                    if (this.HasTwitchAction(e.ChatMessage.Message))
                    {
                        if (this.UserHasWarning(document, e.ChatMessage.UserId))
                        {
                            moderationResult.ShouldTimeout = document.ActionMessageTimeoutPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.ActionMessageTimeoutPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.ActionMessageTimeoutPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.ActionMessageTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.ActionMessageTimeoutReason;
                            moderationResult.ModerationMessage = document.ActionMessageTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.ShouldTimeout = document.ActionMessageWarningPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.ActionMessageWarningPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.ActionMessageWarningPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.ActionMessageWarningTimeSeconds;
                            moderationResult.ModerationReason = document.ActionMessageWarningReason;
                            moderationResult.ModerationMessage = document.ActionMessageWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of emotes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnEmotesCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            // Check if the caps filter is enabled.
            if (document.EmoteStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage.UserId, UserLevels.Broadcaster | UserLevels.Moderator | document.EmoteExcludedLevels).ConfigureAwait(false))
                {
                    if ((this.GetNumberOfEmotes(e.ChatMessage.EmoteSet) >= document.EmoteMaximumAllowed) || (document.EmoteRemoveOnlyEmotes && this.RemoveEmotesFromMessage(e.ChatMessage.Message, e.ChatMessage.EmoteSet).Trim().Length == 0))
                    {
                        if (this.UserHasWarning(document, e.ChatMessage.UserId))
                        {
                            moderationResult.ShouldTimeout = document.EmoteTimeoutPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.EmoteTimeoutPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.EmoteTimeoutPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.EmoteTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.EmoteTimeoutReason;
                            moderationResult.ModerationMessage = document.EmoteTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.ShouldTimeout = document.EmoteWarningPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.EmoteWarningPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.EmoteWarningPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.EmoteWarningTimeSeconds;
                            moderationResult.ModerationReason = document.EmoteWarningReason;
                            moderationResult.ModerationMessage = document.EmoteWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for the use of a fake purge (<message-deleted>) variations.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnFakePurgeCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            // Check if the caps filter is enabled.
            if (document.FakePurgeStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage.UserId, UserLevels.Broadcaster | UserLevels.Moderator | document.FakePurgeExcludedLevels).ConfigureAwait(false))
                {
                    if (this.HasFakePurge(e.ChatMessage.Message))
                    {
                        if (this.UserHasWarning(document, e.ChatMessage.UserId))
                        {
                            moderationResult.ShouldTimeout = document.FakePurgeTimeoutPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.FakePurgeTimeoutPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.FakePurgeTimeoutPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.FakePurgeTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.FakePurgeTimeoutReason;
                            moderationResult.ModerationMessage = document.FakePurgeTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.ShouldTimeout = document.FakePurgeWarningPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.FakePurgeWarningPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.FakePurgeWarningPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.FakePurgeWarningTimeSeconds;
                            moderationResult.ModerationReason = document.FakePurgeWarningReason;
                            moderationResult.ModerationMessage = document.FakePurgeWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for use of links.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnLinksCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            // Check if the caps filter is enabled.
            if (document.LinkStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage.UserId, UserLevels.Broadcaster | UserLevels.Moderator | document.LinkExcludedLevels).ConfigureAwait(false))
                {
                    // TODO: Check whitelist
                    // TODO: Check to make sure the URL isn't for clips.
                    if (this.HasUrl(e.ChatMessage.Message))
                    {
                        if (this.UserHasWarning(document, e.ChatMessage.UserId))
                        {
                            moderationResult.ShouldTimeout = document.LinkTimeoutPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.LinkTimeoutPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.LinkTimeoutPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.LinkTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.LinkTimeoutReason;
                            moderationResult.ModerationMessage = document.LinkTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.ShouldTimeout = document.LinkWarningPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.LinkWarningPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.LinkWarningPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.LinkWarningTimeSeconds;
                            moderationResult.ModerationReason = document.LinkWarningReason;
                            moderationResult.ModerationMessage = document.LinkWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for lengthy messages.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnLongMessageCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            // Check if the lengthy message filter is enabled.
            if (document.LenghtyMessageStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage.UserId, UserLevels.Broadcaster | UserLevels.Moderator | document.LenghtyMessageExcludedLevels).ConfigureAwait(false))
                {
                    if (this.GetMessageLength(e.ChatMessage.Message) > document.LenghtyMessageMaximumLength)
                    {
                        if (this.UserHasWarning(document, e.ChatMessage.UserId))
                        {
                            moderationResult.ShouldTimeout = document.LenghtyMessageTimeoutPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.LenghtyMessageTimeoutPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.LenghtyMessageTimeoutPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.LenghtyMessageTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.LenghtyMessageTimeoutReason;
                            moderationResult.ModerationMessage = document.LenghtyMessageTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.ShouldTimeout = document.LenghtyMessageWarningPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.LenghtyMessageWarningPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.LenghtyMessageWarningPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.LenghtyMessageWarningTimeSeconds;
                            moderationResult.ModerationReason = document.LenghtyMessageWarningReason;
                            moderationResult.ModerationMessage = document.LenghtyMessageWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message someone sending too many messages at once or the same message over and over.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnOneManSpamCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            if (document.OneManSpamStatus)
            {
                // TODO: Add check if this is enabled in user settings.
                // TODO: Add filter check.
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of repeating characters in a message.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnRepetitionCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            if (document.RepetitionStatus)
            {
                // TODO: Add check if this is enabled in user settings.
                // TODO: Add filter check.
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of symbols.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnSymbolsCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            if (document.SymbolStatus)
            {
                // TODO: Add check if this is enabled in user settings.
                // TODO: Add filter check.
            }

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for disruptive characters.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private async Task<ModerationResult> ChatModerator_OnZalgoCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            ModerationDocument document = GetFilterDocumentForChannel(e.ChatMessage.RoomId).Result;

            // Check if the zalgo filter is enabled.
            if (document.ZalgoStatus)
            {
                // User is not a Broadcaster, Moderator, or in the excluded levels.
                if (!await Permission.Can(e.ChatMessage.UserId, UserLevels.Broadcaster | UserLevels.Moderator | document.ZalgoExcludedLevels).ConfigureAwait(false))
                {
                    if (this.HasZalgo(e.ChatMessage.Message))
                    {
                        if (this.UserHasWarning(document, e.ChatMessage.UserId))
                        {
                            moderationResult.ShouldTimeout = document.ZalgoTimeoutPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.ZalgoTimeoutPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.ZalgoTimeoutPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.ZalgoTimeoutTimeSeconds;
                            moderationResult.ModerationReason = document.ZalgoTimeoutReason;
                            moderationResult.ModerationMessage = document.ZalgoTimeoutMessage;
                        }
                        else
                        {
                            moderationResult.ShouldTimeout = document.ZalgoWarningPunishment.Equals(ModerationPunishment.Timeout);
                            moderationResult.ShouldDelete = document.ZalgoWarningPunishment.Equals(ModerationPunishment.Delete);
                            moderationResult.ShouldPurge = document.ZalgoWarningPunishment.Equals(ModerationPunishment.Purge);
                            moderationResult.TimeoutSeconds = document.ZalgoWarningTimeSeconds;
                            moderationResult.ModerationReason = document.ZalgoWarningReason;
                            moderationResult.ModerationMessage = document.ZalgoWarningMessage;
                        }
                    }
                }
            }

            return moderationResult;
        }

        /// <summary>
        /// If a user currently has a warning.
        /// </summary>
        /// <param name="document">Moderation document.</param>
        /// <param name="userId">User id to check for warnings</param>
        /// <returns>True if the user has a warning.</returns>
        private bool UserHasWarning(ModerationDocument document, string userId) => document.ModerationLastUserTimeouts.ContainsKey(userId) && document.ModerationLastUserTimeouts[userId].AddSeconds(document.ModerationWarningTimeSeconds) > DateTime.Now;

        #endregion Private Methods
    }
}