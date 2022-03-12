/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using StreamActions.Attributes;
using StreamActions.Database;
using StreamActions.Database.Documents.Commands;
using StreamActions.Enums;
using StreamActions.Plugin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles adding, editing, removing, tag parsing, and output of user-created commands
    /// </summary>
    [Guid("96FBAB20-3348-47F9-8222-525FB0BEEF70")]
    public class CustomCommand : IPlugin
    {
        #region Public Constructors

        /// <summary>
        /// Class constructor.
        /// </summary>
        public CustomCommand()
        {
        }

        #endregion Public Constructors

        #region Public Properties

        public bool AlwaysEnabled => true;

        public IReadOnlyCollection<Guid> Dependencies => ImmutableArray<Guid>.Empty;
        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Custom Command plugin for StreamActions";

        public Guid PluginId => typeof(CustomCommand).GUID;
        public string PluginName => "CustomCommand";
        public Uri PluginUri => new Uri("https://github.com/StreamActions/StreamActions");
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
            IMongoCollection<CommandDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>(CommandDocument.CollectionName);

            FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == channelId && !c.IsWhisperCommand && c.Command == command.ToLowerInvariant());

            if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                return null;
            }

            using IAsyncCursor<CommandDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            return await cursor.FirstAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Looks up the <see cref="CommandDocument"/> for all commands registered to a given channel.
        /// </summary>
        /// <param name="channelId">The channelId the commands are registered to.</param>
        /// <returns>A List of <see cref="CommandDocument"/>.</returns>
        public static async Task<List<CommandDocument>> ListCustomCommandsAsync(string channelId)
        {
            IMongoCollection<CommandDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>(CommandDocument.CollectionName);

            FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == channelId && !c.IsWhisperCommand);
            using IAsyncCursor<CommandDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

            return await cursor.ToListAsync().ConfigureAwait(false);
        }

        public void Disabled()
        {
        }

        public void Enabled() => PluginManager.Instance.OnChatCommandReceived += this.CustomCommand_OnCustomCommand;

        public string GetCursor() => this.PluginId.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Regular expression used to detect the command format for when adding or editing commands.
        /// </summary>
        private readonly Regex _commandAddEditRegex = new Regex("(?<prefix>" + PluginManager.Instance.ChatCommandIdentifier + ")(?<command>\\S{1,})\\s((?<userlevel>(-b|-m|-ts|-ta|-s|-v|-c))\\s)?(?<response>[\\w\\W]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Regular expression used to detect the command format for when removing a command.
        /// </summary>
        private readonly Regex _commandRemoveRegex = new Regex("(?<prefix>" + PluginManager.Instance.ChatCommandIdentifier + ")(?<command>\\S{1,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Gets the user level from the argument in the command.
        /// </summary>
        /// <param name="tag">One of the user level tags below.</param>
        /// <returns>The user level.</returns>
        private static UserLevels GetUserLevelFromTag(string tag) => tag switch
        {
            "-b" => UserLevels.Broadcaster,
            "-c" => UserLevels.Broadcaster,
            "-tf" => UserLevels.TwitchStaff,
            "-ta" => UserLevels.TwitchAdmin,
            "-m" => UserLevels.Moderator,
            "-s" => UserLevels.Subscriber,
            "-v" => UserLevels.VIP,
            _ => UserLevels.Viewer
        };

        /// <summary>
        /// Method called when someone adds a new custom command.
        /// Syntax for the command is the following: !command add [command] [permission] [response] - !command add !social -m Follow my Twitter!
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "add", UserLevels.Moderator)]
        private async void CustomCommand_OnAddCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            Match commandMatch = this._commandAddEditRegex.Match(e.Command.ArgumentsAsString);

            if (commandMatch.Success)
            {
                string command = commandMatch.Groups["command"].Value.ToLowerInvariant();

                if (!await PluginManager.DoesCustomChatCommandExistAsync(e.Command.ChatMessage.RoomId, command).ConfigureAwait(false))
                {
                    UserLevels userLevel = GetUserLevelFromTag(commandMatch.Groups["userlevel"].Value);
                    string response = commandMatch.Groups["response"].Value;

                    // TODO: Register the custom role

                    IMongoCollection<CommandDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>(CommandDocument.CollectionName);

                    CommandDocument commandDocument = new CommandDocument
                    {
                        ChannelId = e.Command.ChatMessage.RoomId,
                        Command = command,
                        Response = response,
                        UserLevel = userLevel
                    };

                    await collection.InsertOneAsync(commandDocument).ConfigureAwait(false);

                    // Command added.
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                        await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "AddSuccess", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            Command = command,
                            Response = response,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, the command {CommandPrefix}{Command} has been added with the response: {Response}").ConfigureAwait(false));
                }
                else
                {
                    // Command exists.
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                        await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "AddAlreadyExists", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            Command = command,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, the command {CommandPrefix}{Command} cannot be added as it already exists.").ConfigureAwait(false));
                }
            }
            else
            {
                // Wrong syntax.
                TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                    await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "AddUsage", e.Command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        User = e.Command.ChatMessage.Username,
                        Sender = e.Command.ChatMessage.Username,
                        e.Command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Adds a custom command. Usage: {CommandPrefix}command add {CommandPrefix}[command] [permission (optional)] [response]").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Method called when someone executes a custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChatCommandReceivedArgs"/> object.</param>
        private async void CustomCommand_OnCustomCommand(object sender, OnChatCommandReceivedArgs e)
        {
            //TODO: Refactor Mongo
            CommandDocument commandDocument = await GetCustomCommandAsync(e.Command.ChatMessage.RoomId, e.Command.CommandText).ConfigureAwait(false);

            if (!(commandDocument is null))
            {
                if (!await Permission.Can(e.Command, false, 0).ConfigureAwait(false))
                {
                    return;
                }

                TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.RoomId, commandDocument.Response);
            }
        }

        /// <summary>
        /// Method called when someone edits a custom command.
        /// Syntax for the command is the following: !command modify [command] [permission] [response] - !command modify !social -m Follow my Twitter!
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "modify", UserLevels.Moderator)]
        private async void CustomCommand_OnEditCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            Match commandMatch = this._commandAddEditRegex.Match(e.Command.ArgumentsAsString);

            if (commandMatch.Success)
            {
                string command = commandMatch.Groups["command"].Value.ToLowerInvariant();

                if (await PluginManager.DoesCustomChatCommandExistAsync(e.Command.ChatMessage.RoomId, command).ConfigureAwait(false))
                {
                    UserLevels userLevel = GetUserLevelFromTag(commandMatch.Groups["userlevel"].Value);
                    string response = commandMatch.Groups["response"].Value;

                    IMongoCollection<CommandDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>(CommandDocument.CollectionName);

                    // Update the document.
                    UpdateDefinition<CommandDocument> update = Builders<CommandDocument>.Update.Set(c => c.Response, response);
                    FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == e.Command.ChatMessage.RoomId && !c.IsWhisperCommand && c.Command == command);

                    // Update permission if the user specified it.
                    if (!string.IsNullOrEmpty(commandMatch.Groups["userlevel"].Value))
                    {
                        _ = update.Set(c => c.UserLevel, userLevel);
                    }

                    _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                    // Command modified.
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                        await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "ModifySuccess", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            Command = command,
                            Response = response,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, the command {CommandPrefix}{Command} has been modified with the response: {Response}").ConfigureAwait(false));
                }
                else
                {
                    // Command does not exist.
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                        await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "ModifyNotExists", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            Command = command,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, the command {CommandPrefix}{Command} cannot be modified as it doesn't exist.").ConfigureAwait(false));
                }
            }
            else
            {
                // Wrong syntax.
                TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                    await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "ModifyUsage", e.Command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        User = e.Command.ChatMessage.Username,
                        Sender = e.Command.ChatMessage.Username,
                        e.Command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Modifies a command. Usage: {CommandPrefix}command modify {CommandPrefix}[command] [permission (optional)] [response]").ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Method called when someone removes a custom command.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChatCommandReceivedArgs"/> object.</param>
        [ChatCommand("command", "remove", UserLevels.Moderator)]
        private async void CustomCommand_OnRemoveCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            Match commandMatch = this._commandRemoveRegex.Match(e.Command.ArgumentsAsString);

            if (commandMatch.Success)
            {
                string command = commandMatch.Groups["command"].Value.ToLowerInvariant();

                if (await PluginManager.DoesCustomChatCommandExistAsync(e.Command.ChatMessage.RoomId, command).ConfigureAwait(false))
                {
                    IMongoCollection<CommandDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>(CommandDocument.CollectionName);

                    FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == e.Command.ChatMessage.RoomId && !c.IsWhisperCommand && c.Command == command);

                    _ = await collection.DeleteOneAsync(filter).ConfigureAwait(false);
                    //TODO: Unregister custom permission

                    // Command removed.
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                        await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "RemoveSuccess", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            Command = command,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, the command {CommandPrefix}{Command} has been removed.").ConfigureAwait(false));
                }
                else
                {
                    // Command doesn't exist.
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                        await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "RemoveNotExists", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            Command = command,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, the command {CommandPrefix}{Command} cannot be removed as it doesn't exist.").ConfigureAwait(false));
                }
            }
            else
            {
                // Wrong syntax.
                TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel,
                    await I18n.Instance.GetAndFormatWithAsync("CustomCommand", "RemoveUsage", e.Command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        User = e.Command.ChatMessage.Username,
                        Sender = e.Command.ChatMessage.Username,
                        e.Command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Removes a custom command. Usage: {CommandPrefix}command remove {CommandPrefix}[command]").ConfigureAwait(false));
            }
        }

        #endregion Private Methods
    }
}
