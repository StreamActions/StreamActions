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
using System.Text;
using TwitchLib.Client.Events;

namespace StreamActions.Plugins
{
    public class Moderation : IPlugin
    {
        #region Public Constructors

        /// <summary>
        /// Class constructor.
        /// </summary>
        public Moderation()
        {
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// If the plugin should always be enabled.
        /// </summary>
        public bool AlwaysEnabled => false;

        /// <summary>
        /// The description of the plugin
        /// </summary>
        public string PluginDescription => "Moderation plugin for StreamActions";

        /// <summary>
        /// The name of the plugin.
        /// </summary>
        public string PluginName => "Moderation";

        /// <summary>
        /// The version of the plugin.
        /// </summary>
        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Method called when the plugin gets disabled <see cref="PluginManager.UnloadPlugin(string)"/>.
        /// </summary>
        public void Disabled()
        {
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnCapsCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnColouredMessageCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnEmotesCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnFakePurgeCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnLinksCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnLongMessageCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnOneManSpamCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnRepetitionCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnSymbolsCheck;
            PluginManager.Instance.OnMessageModeration -= this.Moderation_OnZalgoCheck;
        }

        /// <summary>
        /// Method called when the plugin gets activated see <see cref="PluginManager.LoadPlugins(string, bool)"/>.
        /// </summary>
        public void Enabled()
        {
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnCapsCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnColouredMessageCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnEmotesCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnFakePurgeCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnLinksCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnLongMessageCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnOneManSpamCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnRepetitionCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnSymbolsCheck;
            PluginManager.Instance.OnMessageModeration += this.Moderation_OnZalgoCheck;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Method that will check the message for excessive use of caps.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        /// <returns>The result gotten from this check.</returns>
        private ModerationResult Moderation_OnCapsCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnColouredMessageCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnEmotesCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnFakePurgeCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnLinksCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnLongMessageCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnOneManSpamCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnRepetitionCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnSymbolsCheck(object sender, OnMessageReceivedArgs e)
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
        private ModerationResult Moderation_OnZalgoCheck(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult moderationResult = new ModerationResult();

            // TODO: Add check if this is enabled in user settings.
            // TODO: Add filter check.

            return moderationResult;
        }

        #endregion Private Methods
    }
}