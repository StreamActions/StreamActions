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
using System.Text;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

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
        /// Method that checks if the message has a fake purge.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <returns>If the message has a fake purge.</returns>
        private bool HasFakePurge(string message) =>
            message.Equals("<message deleted>", StringComparison.OrdinalIgnoreCase) ||
            message.Equals("message deleted by a moderator.", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Checks if the message has too many caps.
        /// </summary>
        /// <param name="message">The string message without any Twitch emotes.</param>
        /// <param name="maximum">Maximum amount of caps allowed in a message.</param>
        /// <returns>if we hit the caps limit.</returns>
        private bool HasMaximumCaps(string message, int maximum)
        {
            bool hasMaximumCaps = false;
            int count = 0;

            for (int i = 0; i < message.Length; i++)
            {
                if (char.IsUpper(message[i]))
                {
                    count++;
                    // Check if we hit the maximum allowed, no point in keeping going once we hit the maximum.
                    if (count >= maximum)
                    {
                        hasMaximumCaps = true;
                        break;
                    }
                }
            }

            return hasMaximumCaps;
        }

        /// <summary>
        /// Method that checks if the message has too many emotes.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <param name="maximum">Maximum number of emotes allowed in a message.</param>
        /// <returns>If the message has too many emotes.</returns>
        private bool HasMaximumEmotes(EmoteSet emoteSet, int maximum) => emoteSet.Emotes.Count >= maximum;

        /// <summary>
        /// Method that checks if the message is lengthy.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <param name="maximum">Maximum length of message allowed.</param>
        /// <returns>if the message is lenghty.</returns>
        private bool HasMaximumMessageLength(string message, int maximum) => message.Length >= maximum;

        /// <summary>
        /// Method that checks if the message has too many symbols.
        /// </summary>
        /// <param name="message">Message to be checked.</param>
        /// <param name="maximum">Maximum allowed of non letters and digits.</param>
        /// <returns>If we git the maximum symbols.</returns>
        private bool HasMaximumSymbols(string message, int maximum)
        {
            bool hasMaximumSymbols = false;
            int count = 0;

            for (int i = 0; i < message.Length; i++)
            {
                if (!char.IsWhiteSpace(message[i]) && !char.IsLetterOrDigit(message[i]))
                {
                    count++;
                    // Check if we hit the maximum allowed, no point in keeping going once we hit the maximum.
                    if (count >= maximum)
                    {
                        hasMaximumSymbols = true;
                        break;
                    }
                }
            }

            return hasMaximumSymbols;
        }

        /// <summary>
        /// Method that removes Twitch emotes from a string.
        /// </summary>
        /// <param name="message">Main message.</param>
        /// <param name="emoteSet">Emotes in the message.</param>
        /// <returns>The message without any emotes.</returns>
        private string RemoveEmotesFromMessage(string message, EmoteSet emoteSet)
        {
            StringBuilder sb = new StringBuilder();

            // TODO: Implement logic to remove the emotes at specific indexes.

            return sb.ToString();
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