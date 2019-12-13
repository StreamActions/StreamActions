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

using System;
using System.Collections.Generic;
using System.Timers;
using TwitchLib.Client.Events;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Loads and manages the state of plugins, as well as running the plugin event handlers.
    /// </summary>
    public class PluginManager
    {
        #region Public Delegates

        /// <summary>
        /// Represents the method that will handle a MessageModeration event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received message.</param>
        /// <returns>The moderation action to take on the message.</returns>
        public delegate ModerationResult MessageModerationEventHandler(object sender, OnMessageReceivedArgs e);

        #endregion Public Delegates

        #region Public Events

        /// <summary>
        /// Fires when a command that passed moderation is received, returns channel, command, ChatMessage, arguments as string, arguments as list.
        /// </summary>
        public event EventHandler<OnChatCommandReceivedArgs> OnChatCommandReceived;

        /// <summary>
        /// Fires when a new chat message arrives and is ready to be processed for moderation, returns ChatMessage.
        /// </summary>
        public event MessageModerationEventHandler OnMessageModeration;

        /// <summary>
        /// Fires when a new chat message arrives and has passed moderation, returns ChatMessage.
        /// </summary>
        public event EventHandler<OnMessageReceivedArgs> OnMessageReceived;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Singleton of PluginManager
        /// </summary>
        public static PluginManager Instance => _instance.Value;

        /// <summary>
        /// Indicates if the bot is in lock down mode. When this is set, only plugins with MessageModeration EventHandlers will be enabled.
        /// </summary>
        public bool InLockdown => this._inLockdown;

        #endregion Public Properties

        #region Private Fields

        /// <summary>
        /// Number of milliseconds to add to the timer that removes entries from _moderatedMessageIds, per EventHandler hooked to OnMessageModeration.
        /// </summary>
        private const int _moderationTimerMultiplier = 250;

        /// <summary>
        /// Field that backs the Instance property.
        /// </summary>
        private static readonly Lazy<PluginManager> _instance = new Lazy<PluginManager>(() => new PluginManager());

        /// <summary>
        /// Stores the MessageId of chat messages that had moderation actions taken, so that OnChatCommandReceived knows to drop them.
        /// </summary>
        private readonly ICollection<string> _moderatedMessageIds = new HashSet<string>();

        /// <summary>
        /// Write lock object for _moderatedMessageIds.
        /// </summary>
        private readonly object _moderatedMessageIdsLock = new object();

        /// <summary>
        /// Field that backs the InLockdown property.
        /// </summary>
        private bool _inLockdown = false;

        #endregion Private Fields

        #region Private Constructors

        private PluginManager()
        {
        }

        #endregion Private Constructors

        #region Private Methods

        private void Twitch_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            if (this._moderatedMessageIds.Contains(e.Command.ChatMessage.Id))
            {
                return;
            }

            OnChatCommandReceived?.Invoke(this, e);
        }

        private void Twitch_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult harshestModeration = new ModerationResult();
            int numModeration = 0;
            if (OnMessageModeration != null)
            {
                foreach (Delegate d in OnMessageModeration.GetInvocationList())
                {
                    ModerationResult rs = (ModerationResult)d.DynamicInvoke(this, e);
                    if (harshestModeration.IsHarsher(rs))
                    {
                        harshestModeration = rs;
                    }
                }

                numModeration = OnMessageModeration.GetInvocationList().Length;
            }

            if (harshestModeration.ShouldModerate)
            {
                lock (this._moderatedMessageIdsLock)
                {
                    this._moderatedMessageIds.Add(e.ChatMessage.Id);
                }

                using Timer timer = new Timer();
                timer.Elapsed += (sender, te) =>
                {
                    lock (this._moderatedMessageIdsLock)
                    {
                        _ = this._moderatedMessageIds.Remove(e.ChatMessage.Id);
                    }
                };
                timer.AutoReset = false;
                timer.Interval = numModeration * _moderationTimerMultiplier;
                timer.Enabled = true;
            }
            else
            {
                OnMessageReceived?.Invoke(this, e);
            }
        }

        #endregion Private Methods
    }
}