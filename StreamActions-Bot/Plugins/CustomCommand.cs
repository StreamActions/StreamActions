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
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TwitchLib.Client.Events;
using System.Collections.Generic;
using StreamActions.Enums;

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
        /// Syntax for the command is the following: !command add [permission] [command] [response] - !command add -m !social Follow my Twitter!
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "add")]
        private async void CustomCommand_OnAddCommand(object sender, OnChatCommandReceivedArgs e)
        {
            // List of the arguments said after a command.
            List<string> arguments = e.Command.ArgumentsAsList;
            // Permission to be set on the command.
            UserLevel userLevel = UserLevel.Viewer;
            // The command itself wihtout the prefix.
            string command;
            // The response for this command.
            string response;

            if (arguments.Count >= 3)
            {
                // Remove the prefix from the command.
                if (arguments[1].StartsWith(PluginManager.Instance.ChatCommandIdentifier))
                {
                    arguments[1] = arguments[1].Substring(1);
                }

                command = arguments[1].ToLowerInvariant();

                if (!PluginManager.Instance.DoesCommandExist(command))
                {
                    switch (arguments[0])
                    {
                        case "-b":
                            userLevel = UserLevel.Broadcaster;
                            break;

                        case "-ts":
                            userLevel = UserLevel.TwitchStaff;
                            break;

                        case "-ta":
                            userLevel = UserLevel.TwitchAdmin;
                            break;

                        case "-m":
                            userLevel = UserLevel.Moderator;
                            break;

                        case "-s":
                            userLevel = UserLevel.Subscriber;
                            break;

                        case "-c":
                            // TODO: Register the permission.
                            userLevel = UserLevel.Custom;
                            break;

                        case "-v":
                            userLevel = UserLevel.VIP;
                            break;

                        default:
                            userLevel = UserLevel.Viewer;
                            break;
                    }

                    // Remove the permission and command from the list.
                    arguments.RemoveRange(0, 2);

                    response = string.Join(' ', arguments);

                    IMongoCollection<CommandDocument> commands = Database.Database.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

                    await commands.InsertOneAsync(new CommandDocument
                    {
                        ChannelId = e.Command.ChatMessage.RoomId,
                        Command = command,
                        Response = response,
                        Permission = userLevel,
                        GlobalCooldown = 5
                    }).ConfigureAwait(false);

                    _ = PluginManager.Instance.SubscribeChatCommand(command, (sender, e) => TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, response));
                }
                else
                {
                    // Command exists.
                }
            }
            else
            {
                // Show usage.
            }
        }

        /// <summary>
        /// Method called when someone edits a custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "modify")]
        private void CustomCommand_OnEditCommand(object sender, OnChatCommandReceivedArgs e)
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

        #endregion Private Methods
    }
}