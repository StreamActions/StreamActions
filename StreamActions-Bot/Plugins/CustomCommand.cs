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
using System.Runtime.InteropServices;
using TwitchLib.Client.Events;
using StreamActions.Enums;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles adding, editing, removing, tag parsing, and output of user-created commands
    /// </summary>
    [Guid("96FBAB20-3348-47F9-8222-525FB0BEEF70")]
    public class CustomCommand : IPlugin
    {
        #region Public Properties

        public bool AlwaysEnabled => true;

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Custom Command plugin for StreamActions";

        public Guid PluginId => typeof(CustomCommand).GUID;

        public string PluginName => "CustomCommand";

        public Uri PluginUri => new Uri("https://github.com/StreamActions/StreamActions-Bot");

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Looks up the <see cref="CommandDocument"/> for the given command.
        /// </summary>
        /// <param name="channelId">The channelId the command is registered to.</param>
        /// <param name="command">The command to lookup.</param>
        /// <returns>The <see cref="CommandDocument"/> for the command, if it exists; <c>null</c> otherwise.</returns>
        public static async Task<CommandDocument> GetCustomCommandAsync(string channelId, string command)
        {
            IMongoCollection<CommandDocument> commands = Database.Database.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

            FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == channelId && c.Command == command.ToLowerInvariant());
            using IAsyncCursor<CommandDocument> cursor = await commands.FindAsync(filter).ConfigureAwait(false);

            return await cursor.FirstAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Looks up the <see cref="CommandDocument"/> for all commands registered to a given channel.
        /// </summary>
        /// <param name="channelId">The channelId the commands are registered to.</param>
        /// <returns>A List of <see cref="CommandDocument"/>.</returns>
        public static async Task<List<CommandDocument>> ListCustomCommandsAsync(string channelId)
        {
            IMongoCollection<CommandDocument> commands = Database.Database.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

            FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == channelId);
            using IAsyncCursor<CommandDocument> cursor = await commands.FindAsync(filter).ConfigureAwait(false);

            return await cursor.ToListAsync().ConfigureAwait(false);
        }

        public void Disabled()
        {
        }

        public void Enabled() => PluginManager.Instance.OnChatCommandReceived += this.CustomCommand_OnCustomCommand;

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Regular expression used to detect the command format for when adding or editing commands.
        /// </summary>
        private readonly Regex _commandAddEditRegex = new Regex("(?<command>" + PluginManager.Instance.ChatCommandIdentifier + "\\S{1,})\\s((?<userlevel>(-b|-m|ts|ta|-s|-v|-c))\\s)?(?<response>[\\w\\W]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Regular expression used to detect the command format for when removing a command.
        /// </summary>
        private readonly Regex _commandRemoveRegex = new Regex("(?<command>" + PluginManager.Instance.ChatCommandIdentifier + "\\S{1,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Method called when someone adds a new custom command.
        /// Syntax for the command is the following: !command add [command] [permission] [response] - !command add !social -m Follow my Twitter!
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "add")]
        private async void CustomCommand_OnAddCommand(object sender, OnChatCommandReceivedArgs e)
        {
            Match commandMatch = this._commandAddEditRegex.Match(e.Command.ArgumentsAsString);

            if (commandMatch.Success)
            {
                if (!await PluginManager.DoesCustomChatCommandExistAsync(e.Command.ChatMessage.RoomId, commandMatch.Groups["command"].Value).ConfigureAwait(false))
                {
                    UserLevel userLevel;
                    string command = commandMatch.Groups["command"].Value.ToLowerInvariant();
                    string response = commandMatch.Groups["response"].Value;

                    switch (commandMatch.Groups["userlevel"].Value)
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

                    IMongoCollection<CommandDocument> commands = Database.Database.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

                    CommandDocument commandDocument = new CommandDocument
                    {
                        ChannelId = e.Command.ChatMessage.RoomId,
                        Command = command,
                        Response = response,
                        UserLevel = userLevel,
                        GlobalCooldown = 5
                    };

                    if (!await PluginManager.DoesCustomChatCommandExistAsync(e.Command.ChatMessage.RoomId, command).ConfigureAwait(false))
                    {
                        await commands.InsertOneAsync(commandDocument).ConfigureAwait(false);
                    }

                    // Command added.
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
        /// Method called when someone executes a custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        private async void CustomCommand_OnCustomCommand(object sender, OnChatCommandReceivedArgs e)
        {
            CommandDocument commandDocument = await GetCustomCommandAsync(e.Command.ChatMessage.RoomId, e.Command.CommandText).ConfigureAwait(false);

            if (!(commandDocument is null))
            {
                TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.RoomId, commandDocument.Response);
            }
        }

        /// <summary>
        /// Method called when someone edits a custom command.
        /// Syntax for the command is the following: !command modify [command] [permission] [response] - !command modify !social -m Follow my Twitter!
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "modify")]
        private async void CustomCommand_OnEditCommand(object sender, OnChatCommandReceivedArgs e)
        {
            Match commandMatch = this._commandAddEditRegex.Match(e.Command.ArgumentsAsString);

            if (commandMatch.Success)
            {
                if (await PluginManager.DoesCustomChatCommandExistAsync(e.Command.ChatMessage.RoomId, commandMatch.Groups["command"].Value).ConfigureAwait(false))
                {
                    UserLevel userLevel = UserLevel.Viewer;
                    string command = commandMatch.Groups["command"].Value.ToLowerInvariant();
                    string response = commandMatch.Groups["response"].Value;

                    switch (commandMatch.Groups["userlevel"].Value)
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

                    IMongoCollection<CommandDocument> commands = Database.Database.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

                    // Update the document.
                    UpdateDefinition<CommandDocument> commandUpdate = Builders<CommandDocument>.Update.Set(c => c.Response, response);
                    FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == e.Command.ChatMessage.RoomId && c.Command == command);

                    // Update permission if the user specified it.
                    if (!string.IsNullOrEmpty(commandMatch.Groups["userlevel"].Value))
                    {
                        _ = commandUpdate.Set(c => c.UserLevel, userLevel);
                    }

                    _ = await commands.UpdateOneAsync(filter, commandUpdate).ConfigureAwait(false);

                    // Command modified.
                }
                else
                {
                    // Command does not exist.
                }
            }
            else
            {
                // Show usage + current permission and response.
            }
        }

        /// <summary>
        /// Method called when someone removes a custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "remove")]
        private async void CustomCommand_OnRemoveCommand(object sender, OnChatCommandReceivedArgs e)
        {
            Match commandMatch = this._commandRemoveRegex.Match(e.Command.ArgumentsAsString);

            if (commandMatch.Success)
            {
                if (await PluginManager.DoesCustomChatCommandExistAsync(e.Command.ChatMessage.RoomId, commandMatch.Groups["command"].Value).ConfigureAwait(false))
                {
                    string command = commandMatch.Groups["command"].Value.ToLowerInvariant();

                    IMongoCollection<CommandDocument> commands = Database.Database.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

                    FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == e.Command.ChatMessage.RoomId && c.Command == command);

                    _ = await commands.DeleteOneAsync(filter).ConfigureAwait(false);
                    //TODO: Unregister custom permission

                    // Command removed.
                }
                else
                {
                    // Command doesn't exist.
                }
            }
            else
            {
                // Show usage.
            }
        }

        #endregion Private Methods
    }
}