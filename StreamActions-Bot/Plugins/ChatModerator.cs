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

using StreamActions.Plugin;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using System.Linq;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles various moderation actions against chat messages, such as links protection.
    /// </summary>
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

        #region Private filter regex

        /// <summary>
        /// Regular expression that is used for getting the number of capital letters in a string.
        /// </summary>
        private readonly Regex _capsRegex = new Regex(@"[A-Z]", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the amount of grouped symbols in a string.
        /// </summary>
        private readonly Regex _groupedSymbolsRegex = new Regex(@"([-!$%#^&*()_+|~=`{}\[\]:'<>?,.\/\\;""])\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is for finding URLs in a string.
        /// </summary>
        private readonly Regex _linkRegex = new Regex(@"((?:(http|https|rtsp):\/\/(?:(?:[a-z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,64}(?:\:(?:[a-z0-9\$\-_\.\+\!\*\\\'\(\)\,\;\?\&\=]|(?:\%[a-fA-F0-9]{2})){1,25})?\@)?)?((?:(?:[a-z0-9][a-z0-9\-]{0,64}\.)+(?:(?:aero|a[cdefgilmnoqrstuwxz])|(?:biz|bike|bot|b[abdefghijmnorstvwyz])|(?:com|c[acdfghiklmnoruvxyz])|d[ejkmoz]|(?:edu|e[cegrstu])|(?:fyi|f[ijkmor])|(?:gov|g[abdefghilmnpqrstuwy])|(?:how|h[kmnrtu])|(?:info|i[delmnoqrst])|(?:jobs|j[emop])|k[eghimnrwyz]|l[abcikrstuvy]|(?:mil|mobi|moe|m[acdeghklmnopqrstuvwxyz])|(?:name|net|n[acefgilopruz])|(?:org|om)|(?:pro|p[aefghklmnrstwy])|qa|(?:r[eouw])|(?:s[abcdeghijklmnortuvyz])|(?:t[cdfghjklmnoprtvwz])|u[agkmsyz]|(?:vote|v[ceginu])|(?:xxx)|(?:watch|w[fs])|y[etu]|z[amw]))|(?:(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[1-9])\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[1-9]|0)\\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[1-9]|0)\\.(?:25[0-5]|2[0-4][0-9]|[0-1][0-9]{2}|[1-9][0-9]|[0-9])))(?:\:\d{1,5})?)(\/(?:(?:[a-z0-9\;\/\?\:\@\&\=\#\~\-\.\+\!\*\\\'\(\)\,\_])|(?:\%[a-fA-F0-9]{2}))*)?(?:\b|$)|(\.[a-z]+\/|magnet:\/\/|mailto:\/\/|ed2k:\/\/|irc:\/\/|ircs:\/\/|skype:\/\/|ymsgr:\/\/|xfire:\/\/|steam:\/\/|aim:\/\/|spotify:\/\/)", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used to getting the number of repeating characters or words in a string.
        /// </summary>
        private readonly Regex _repetitionRegex = new Regex(@"(\S+\s*)\1+", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the number of symbols in a string.
        /// </summary>
        private readonly Regex _symbolsRegex = new Regex(@"[-!$%#^&*()_+|~=`{}\[\]:'<>?,.\/\\;""]", RegexOptions.Compiled);

        /// <summary>
        /// Regular expression that is used for getting the number of zalgo/boxed/disruptive symbols in a string.
        /// </summary>
        private readonly Regex _zalgoRegex = new Regex(@"[^\uD83C-\uDBFF\uDC00-\uDFFF\u0401\u0451\u0410-\u044f\u0009-\u02b7\u2000-\u20bf\u2122\u0308]", RegexOptions.Compiled);

        #endregion Private filter regex

        #region Public Properties

        public bool AlwaysEnabled => false;

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Chat Moderation plugin for StreamActions";

        public string PluginName => "ChatModerator";

        public Uri PluginUri => throw new NotImplementedException();

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        public void Disabled()
        {
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnCapsCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnColouredMessageCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnEmotesCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnFakePurgeCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnLinksCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnLongMessageCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnOneManSpamCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnRepetitionCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnSymbolsCheck;
            PluginManager.Instance.OnMessageModeration -= this.ChatModerator_OnZalgoCheck;
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

        #endregion Public Methods

        #region Filter check methods

        /// <summary>
        /// Method that will return the longest sequence amount of repeating characters or words in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating characters and words in the message.</returns>
        private int GetLongestSequenceOfRepeatingCharacters(string message) => this._repetitionRegex.Matches(message).Max(m => m.Length);

        /// <summary>
        /// Method that will return the longest sequence amount of repeating symbol in the message.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>Length of longest sequence repeating symbols in the message.</returns>
        private int GetLongestSequenceOfRepeatingSymbols(string message) => this._groupedSymbolsRegex.Matches(message).Max(m => m.Length);

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
        private int GetNumberOfCaps(string message) => this._capsRegex.Matches(message).Sum(m => m.Length);

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
        private int GetNumberOfSymbols(string message) => this._symbolsRegex.Matches(message).Sum(m => m.Length);

        /// <summary>
        /// Method that checks if the message has a fake purge.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>If the message has a fake purge.</returns>
        private bool HasFakePurge(string message) =>
            message.Equals("<message deleted>", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("<deleted message>", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("message deleted by a moderator.", StringComparison.OrdinalIgnoreCase);

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
        /// Method that will check the message for excessive use of caps.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnCapsCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();
            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for the use of the /me command on Twitch.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnColouredMessageCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of emotes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnEmotesCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for the use of a fake purge (<message-deleted>) variations.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnFakePurgeCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for use of links.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnLinksCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for lenghty messages.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnLongMessageCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message someone sending too many messages at once or the same message over and over.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnOneManSpamCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of repeating characters in a message.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnRepetitionCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for excessive use of symbols.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnSymbolsCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        /// <summary>
        /// Method that will check the message for disruptive characters.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult ChatModerator_OnZalgoCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        #endregion Private Methods
    }
}