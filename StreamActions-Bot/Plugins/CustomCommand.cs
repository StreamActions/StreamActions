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

using StreamActions.Attributes;
using StreamActions.Database.Documents;
using StreamActions.Plugin;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TwitchLib.Client.Events;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles adding, editing, removing, tag parsing, and output of user-created commands
    /// </summary>
    [Guid("96FBAB20-3348-47F9-8222-525FB0BEEF70")]
    public class CustomCommand : IPlugin
    {
        #region Public Properties

        public bool AlwaysEnabled => false;

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Custom Command plugin for StreamActions";

        public Guid PluginId => typeof(CustomCommand).GUID;

        public string PluginName => "CustomCommand";

        public Uri PluginUri => throw new NotImplementedException();

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        public void Disabled()
        {
            // Unload custom commands.
        }

        public void Enabled()
        {
            // Load custom commands.
        }

        #endregion Public Methods

        #region Private Fields

        private readonly ConcurrentDictionary<string, CommandDocument> _customCommands = new ConcurrentDictionary<string, CommandDocument>();

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Method called when someone adds a new custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "add")]
        private void CustomCommand_OnAddCommand(object sender, OnChatCommandReceivedArgs e)
        {
            // TODO: Implement command adding logic.
        }

        /// <summary>
        /// Method called when someone adds a new command alias.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "alias")]
        private void CustomCommand_OnAliasCommand(object sender, OnChatCommandReceivedArgs e)
        {
            // TODO: Implement command adding logic.
        }

        private void CustomCommand_OnCommand(object sender, OnChatCommandReceivedArgs e)
        {
        }

        /// <summary>
        /// Method called when someone edits a custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "edit")]
        private void CustomCommand_OnEditCommand(object sender, OnChatCommandReceivedArgs e)
        {
            // TODO: Implement command adding logic.
        }

        /// <summary>
        /// Method called when someone changes the permissions of a command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "permission")]
        private void CustomCommand_OnPermissionChangeCommand(object sender, OnChatCommandReceivedArgs e)
        {
            // TODO: Implement command adding logic.
        }

        /// <summary>
        /// Method called when someone removes a custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "remove")]
        private void CustomCommand_OnRemoveCommand(object sender, OnChatCommandReceivedArgs e)
        {
            // TODO: Implement command adding logic.
        }

        /// <summary>
        /// Parses a custom command messages and adds variables where needed.
        /// </summary>
        /// <param name="commandMessage">The custom command message reference.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        private void ParseCommandVariables(ref string commandMessage, OnChatCommandReceivedArgs e)
        {
        }

        #endregion Private Methods
    }
}