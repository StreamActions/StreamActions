using StreamActions.Attributes;
using StreamActions.Plugin;
using System;
using TwitchLib.Client.Events;

namespace StreamActions.Plugins
{
    public class CustomCommand : IPlugin
    {
        #region Public Properties

        public bool AlwaysEnabled => false;

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Custom Command plugin for StreamActions";

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
        /// Method called when someone adds a new command alias
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "alias")]
        private void CustomCommand_OnAliasCommand(object sender, OnChatCommandReceivedArgs e)
        {
            // TODO: Implement command adding logic.
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
        /// Method called when someone changes the permisions of a command.
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