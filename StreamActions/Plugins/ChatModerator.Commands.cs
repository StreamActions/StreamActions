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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using StreamActions.Attributes;
using StreamActions.Database;
using StreamActions.Database.Documents;
using StreamActions.Enums;
using StreamActions.Plugin;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace StreamActions.Plugins
{
    public partial class ChatModerator
    {
        #region Private Fields

        /// <summary>
        /// How may items can show on the whitelist listing.
        /// </summary>
        private const int _itemsPerPage = 10;

        #endregion Private Fields

        #region Moderation Main Methods

        /// <summary>
        /// Chat moderator management commands.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 1).ConfigureAwait(false))
            {
                return;
            }

            TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "Usage", e.Command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = e.Command.ChatMessage.Username,
                    Sender = e.Command.ChatMessage.Username,
                    e.Command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Manages the bot's moderation filters. Usage: {CommandPrefix}{BotName} moderation [links, caps, spam, symbols, emotes, zalgo, fakepurge, lengthymessage, onemanspam, blacklist]").ConfigureAwait(false));
        }

        #endregion Moderation Main Methods

        #region Link Filter Command Methods

        /// <summary>
        /// Moderation commands for links.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "links", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationLinksCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateLinkFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateLinkFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateLinkFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateLinkFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "whitelist":
                    _ = await this.UpdateLinkFilterWhitelist(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's link moderation filter. Usage: {CommandPrefix}{BotName} moderation links [toggle, warning, timeout, exclude, whitelist]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the link filter exclude via commands.
        /// Command: !bot moderation links exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>If the document was updated true is returned, else false is returned.</returns>
        private async Task<bool> UpdateLinkFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the link filter. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation links exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation links exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels userLevel = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkExcludedLevels, userLevel);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = userLevel,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The link moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the link filter timeout via commands.
        /// Command: !bot moderation links timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>If the document was updated true is returned, else false is returned.</returns>
        private async Task<bool> UpdateLinkFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the link moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation links timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation links timeout time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint linkTimeoutTime = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkTimeoutTimeSeconds, linkTimeoutTime);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = linkTimeoutTime,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation links timeout message [message]").ConfigureAwait(false));
                    return false;
                }

                string linkTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkTimeoutMessage, linkTimeoutMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = linkTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation links timeout reason [message]").ConfigureAwait(false));
                    return false;
                }

                string linkTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkTimeoutReason, linkTimeoutReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = linkTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation links timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment linkTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkTimeoutPunishment, linkTimeoutPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = linkTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the link moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation links timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the link filter toggle via commands.
        /// Command: !bot moderation links toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>If the document was updated true is returned, else false is returned.</returns>
        private async Task<bool> UpdateLinkFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the link moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation links toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for link moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation links toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            bool linkStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkStatus, linkStatus);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The link moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the link filter warning via commands.
        /// Command: !bot moderation links warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>If the document was updated true is returned, else false is returned.</returns>
        private async Task<bool> UpdateLinkFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the link moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation links warning [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation links warning time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint linkWarningTime = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkWarningTimeSeconds, linkWarningTime);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = linkWarningTime,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation links warning message [message]").ConfigureAwait(false));
                    return false;
                }

                string linkWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkWarningMessage, linkWarningMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = linkWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation links warning reason [message]").ConfigureAwait(false));
                    return false;
                }

                string linkWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkWarningReason, linkWarningReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = linkWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LinkWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation links warning punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment linkWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LinkWarningPunishment, linkWarningPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = linkWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the link moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation links warning [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the link filter whitelist via commands.
        /// Command: !bot moderation links whitelist
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>If the document was updated true is returned, else false is returned.</returns>
        private async Task<bool> UpdateLinkFilterWhitelist(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "add, remove, list" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded links from the link filter. " +
                    "Usage: {CommandPrefix}{BotName} moderation links whitelist [add, remove, list]").ConfigureAwait(false));
                return false;
            }

            // Adding a whitelist.
            if (args[4].Equals("add", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistAddUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Add an excluded links to the link filter. " +
                        "Usage: {CommandPrefix}{BotName} moderation links whitelist add [link]").ConfigureAwait(false));
                    return false;
                }

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Push(d => d.LinkWhitelist, args[5]);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistAddUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WhitelistedLink = args[5],
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The following link has been added to the link whitelist \"{WhitelistedLink}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Removing a whitelist.
            if (args[4].Equals("remove", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistRemoveUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Removes an excluded links to the link filter. " +
                        "Usage: {CommandPrefix}{BotName} moderation links whitelist remove [link]").ConfigureAwait(false));
                    return false;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);

                string finder = document.LinkWhitelist.Find(u => u.Equals(args[5], StringComparison.OrdinalIgnoreCase));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.PullFilter(d => d.LinkWhitelist, args[5]);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                if (result.IsAcknowledged)
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistRemoveUpdate", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            WhitelistedLink = args[5],
                            command.ChatMessage.DisplayName
                        },
                       "@{DisplayName}, The following link has been removed from the link whitelist \"{WhitelistedLink}\".").ConfigureAwait(false));
                    return result.IsAcknowledged;
                }

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistRemove404Usage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, You can only remove a link from the whitelist that exists. " +
                    "Usage: {CommandPrefix}{BotName} moderation links whitelist remove [link]").ConfigureAwait(false));
                return false;
            }

            // Listing all whitelists.
            if (args[4].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistListNone", command.ChatMessage.RoomId,
                        new
                        {
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, There are no whitelisted links.").ConfigureAwait(false));
                    return true;
                }

                using IAsyncCursor<List<string>> cursor = await collection.FindAsync(filter, new FindOptions<ModerationDocument, List<string>> { Projection = Builders<ModerationDocument>.Projection.Expression(d => d.LinkWhitelist) }).ConfigureAwait(false);

                List<string> whitelist = await cursor.FirstAsync().ConfigureAwait(false);

                string whitelists = "";
                int numPages = (int)Math.Ceiling(whitelist.Count / (double)_itemsPerPage);
                int page = Math.Min(numPages, Math.Max(1, int.Parse(args.GetElementAtOrDefault(5, "1"), NumberStyles.Integer, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false))));

                foreach (string w in whitelist.Skip((page - 1) * _itemsPerPage).Take(_itemsPerPage))
                {
                    if (whitelists.Length > 0)
                    {
                        whitelists += ", ";
                    }

                    whitelists += whitelist;
                }

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistListShow", command.ChatMessage.RoomId,
                    new
                    {
                        Page = page,
                        NumPage = numPages,
                        Whitelists = whitelists,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Whitelist [Page {Page} of {NumPage}]: {Whitelists}").ConfigureAwait(false));

                return true;
            }

            // Usage if none of the above have been specified.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the excluded links from the link filter. " +
                "Usage: {CommandPrefix}{BotName} moderation links whitelist [add, remove, list]").ConfigureAwait(false));

            return false;
        }

        #endregion Link Filter Command Methods

        #region Cap Filter Command Methods

        /// <summary>
        /// Moderation commands for caps.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "caps", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationCapsCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateCapFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateCapFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateCapFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateCapFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "set":
                    _ = await this.UpdateCapFilterOptions(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapsUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's cap moderation filter. Usage: {CommandPrefix}{BotName} moderation caps [toggle, warning, timeout, exclude, set]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the cap filter exclude via commands.
        /// Command: !bot moderation caps exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateCapFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the caps filter. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation caps exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation caps exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels capsExcluded = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapExcludedLevels, capsExcluded);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = capsExcluded,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The caps moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the cap filter exclude via commands.
        /// Command: !bot moderation caps set
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateCapFilterOptions(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "maximumpercent or minimumlength" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the options for the caps filter. " +
                    "Usage: {CommandPrefix}{BotName} moderation caps set [maximumpercent, minimumlength]").ConfigureAwait(false));
                return false;
            }

            // Maximum caps in message setting.
            if (args[4].Equals("maximumpercent", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0 && val <= 100))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetMaximumPercentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapMaximumPercentage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the maximum percentage of caps allowed in a message. " +
                        "Current value: {CurrentValue}%" +
                        "Usage: {CommandPrefix}{BotName} moderation caps set maximumpercent [0 to 100]").ConfigureAwait(false));
                    return false;
                }

                uint capPercent = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapMaximumPercentage, capPercent);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetMaximumPercentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumPercent = capPercent,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps maximum caps percent allowed in a message has been set to {MaximumPercent}%").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Minimum message length before checking for caps setting.
            if (args[4].Equals("minimumlength", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetMinimumLengthUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapMinimumMessageLength,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the minimum characters required in a message before checking for caps. " +
                        "Current value: {CurrentValue}%" +
                        "Usage: {CommandPrefix}{BotName} moderation caps set minimumlength [minimum length]").ConfigureAwait(false));
                    return false;
                }

                uint capMinMsgLength = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapMinimumMessageLength, capMinMsgLength);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetMinimumLengthUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MinimumLength = capMinMsgLength,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps minimum required length has been set to {MinimumLength} characters.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the options for the caps filter. " +
                "Usage: {CommandPrefix}{BotName} moderation caps set [maximumpercent, minimumlength]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the cap filter timeout via commands.
        /// Command: !bot moderation caps timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateCapFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the cap moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation caps timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation caps timeout time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint capTimeoutTime = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapTimeoutTimeSeconds, capTimeoutTime);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = capTimeoutTime,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation caps timeout message [message]").ConfigureAwait(false));
                    return false;
                }

                string capTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapTimeoutMessage, capTimeoutMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = capTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation caps timeout reason [message]").ConfigureAwait(false));
                    return false;
                }

                string capTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapTimeoutReason, capTimeoutReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = capTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation caps timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment capTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapTimeoutPunishment, capTimeoutPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = capTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the cap moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation caps timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the cap filter toggle via commands.
        /// Command: !bot moderation caps toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateCapFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the caps moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation caps toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for caps moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation caps toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            bool capStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapStatus, capStatus);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The caps moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the cap filter warning via commands.
        /// Command: !bot moderation caps warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateCapFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the cap moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation caps warning [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation caps warning time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint capWarning = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapWarningTimeSeconds, capWarning);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = capWarning,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation caps warning message [message]").ConfigureAwait(false));
                    return false;
                }

                string capWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapWarningMessage, capWarningMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = capWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation caps warning reason [message]").ConfigureAwait(false));
                    return false;
                }

                string capWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapWarningReason, capWarningReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = capWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).CapWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation caps warning punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment capWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.CapWarningPunishment, capWarningPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = capWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the cap moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation caps warning [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        #endregion Cap Filter Command Methods

        #region Repetition Filter Command Methods

        /// <summary>
        /// Moderation commands for repetition.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "repetition", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationRepetitionCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateRepetitionFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateRepetitionFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateRepetitionFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateRepetitionFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "set":
                    _ = await this.UpdateRepetitionFilterOptions(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's link moderation filter. Usage: {CommandPrefix}{BotName} moderation repetition [toggle, warning, timeout, exclude, set]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the repetition filter exclude via commands.
        /// Command: !bot moderation repetition exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateRepetitionFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the repetition filter. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation repetition exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation repetition exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels repetitionExcluded = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.RepetitionExcludedLevels, repetitionExcluded);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = repetitionExcluded,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The repetition moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the repetition filter options.
        /// Command: !bot moderation repetition set
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateRepetitionFilterOptions(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "maximumrepeatingcharaters or maximumrepeatingwords or minimumlength" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionSetUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the options for the repetition filter. " +
                    "Usage: {CommandPrefix}{BotName} moderation repetition set [maximumrepeatingcharaters, maximumrepeatingwords, minimumlength]").ConfigureAwait(false));
                return false;
            }

            // Maximum repeating characters setting.
            if (args[4].Equals("maximumrepeatingcharaters", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionMaximumCharactersUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetionMaximumRepeatingCharacters,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the maximum repeating characters allowed in a message. " +
                        "Current value: {CurrentValue} characters. " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition set maximumrepeatingcharaters [amount]").ConfigureAwait(false));
                    return false;
                }

                uint repetitionMaxRepeatingChars = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.RepetionMaximumRepeatingCharacters, repetitionMaxRepeatingChars);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionMaximumCharactersUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumAmount = repetitionMaxRepeatingChars,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The maximum repeating characters allowed in a message has been set to {MaximumAmount} characters.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Maximum repeating words setting.
            if (args[4].Equals("maximumrepeatingwords", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionMaximumWordsUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetionMaximumRepeatingWords,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the maximum repeating words allowed in a message. " +
                        "Current value: {CurrentValue} words. " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition set maximumrepeatingwords [amount]").ConfigureAwait(false));
                    return false;
                }

                uint repetitionMaxWords = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.RepetionMaximumRepeatingWords, repetitionMaxWords);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionMaximumWordsUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumAmount = repetitionMaxWords,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The maximum repeating words allowed in a message has been set to {MaximumAmount} words.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Minimum length to check for repetition.
            if (args[4].Equals("minimumlength", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionMinimumLengthUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionMinimumMessageLength,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the minimum characters needed to check for repetition. " +
                        "Current value: {CurrentValue} characters. " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition set minimumlength [amount]").ConfigureAwait(false));
                    return false;
                }

                uint repetitionMinMsgLength = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.RepetitionMinimumMessageLength, repetitionMinMsgLength);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionMinimumWordsUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MinimumAmount = repetitionMinMsgLength,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The minimum characters needed to check for repetition has been to to {MinimumAmount} characters.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionSetUsage", command.ChatMessage.RoomId,
                 new
                 {
                     CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                     BotName = Program.Settings.BotLogin,
                     User = command.ChatMessage.Username,
                     Sender = command.ChatMessage.Username,
                     command.ChatMessage.DisplayName
                 },
                 "@{DisplayName}, Sets the options for the repetition filter. " +
                 "Usage: {CommandPrefix}{BotName} moderation repetition set [maximumrepeatingcharaters, maximumrepeatingwords, minimumlength]").ConfigureAwait(false));
            return false;
        }

        /// <summary>
        /// Method called when we update the repetition filter timeout via commands.
        /// Command: !bot moderation repetition timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateRepetitionFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the repetition moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation repetition timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return null;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition timeout time [1 to 1209600]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.RepetitionTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition timeout message [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = document.RepetitionTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return document;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition timeout reason [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = document.RepetitionTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return document;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = document.RepetitionTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return document;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the repetition moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation repetition timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return null;
        }

        /// <summary>
        /// Method called when we update the repetition filter toggle via commands.
        /// Command: !bot moderation repetition toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateRepetitionFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the repetition moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation repetition toggle [on, off]").ConfigureAwait(false));
                return null;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for repetition moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation repetition toggle [on, off]").ConfigureAwait(false));
                return null;
            }

            //TODO: Refactor Mongo
            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.RepetitionStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The repetition moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the repetition filter warning via commands.
        /// Command: !bot moderation repetition warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateRepetitionFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the repetition moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation repetition warning [time, message, reason, punishment]").ConfigureAwait(false));
                return null;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition warning time [1 to 1209600]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.RepetitionWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition warning message [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = document.RepetitionWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return document;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition warning reason [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = document.RepetitionWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return document;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).RepetitionWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation repetition warning punishment [{Punishments}]").ConfigureAwait(false));

                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.RepetitionWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = document.RepetitionWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The repetition moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return document;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the repetition moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation repetition warning [time, message, reason, punishment]").ConfigureAwait(false));

            return null;
        }

        #endregion Repetition Filter Command Methods

        #region Symbol Filter Command Methods

        /// <summary>
        /// Moderation commands for symbols.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "symbols", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationSymbolsCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateSymbolFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateSymbolFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateSymbolFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateSymbolFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "set":
                    _ = await this.UpdateSymbolFilterOptions(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's link moderation filter. Usage: {CommandPrefix}{BotName} moderation symbols [toggle, warning, timeout, exclude]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the symbol filter exclude via commands.
        /// Command: !bot moderation symbols exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateSymbolFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the symbol filter. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation symbols exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation symbols exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels symbolExcludeLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolExcludedLevels, symbolExcludeLevels);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = symbolExcludeLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The symbol moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when updating the symbol filter.
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateSymbolFilterOptions(ChatCommand command, List<string> args)
        {
            // TODO: finish this in 5 years.
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "maximumpercent or maximumgrouped or minimumlength" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsSetUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the options for the symbols filter. " +
                    "Usage: {CommandPrefix}{BotName} moderation symbols set [maximumpercent, maximumgrouped, minimumlength]").ConfigureAwait(false));
                return false;
            }

            // Maximum percent of symbols allowed in a message.
            if (args[4].Equals("maximumpercent", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0 && val <= 100))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsMaximumPercentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolMaximumPercent,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the maximum percent of symbols allowed in a message. " +
                        "Current value: {CurrentValue}% " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols set maximumpercent [0 - 100]").ConfigureAwait(false));
                    return false;
                }

                uint symbolMaximumPercent = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolMaximumPercent, symbolMaximumPercent);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsMaximumPercentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumAmount = symbolMaximumPercent,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The maximum percent of symbols allowed in a message has been set to {MaximumAmount}%").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Maximum grouped symbols allowed in a message.
            if (args[4].Equals("maximumgrouped", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsMaximumGroupedUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolMaximumGrouped,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the maximum amount of groupd symbols allowed in a message. " +
                        "Current value: {CurrentValue} symbols " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols set maximumgrouped [amount]").ConfigureAwait(false));
                    return false;
                }

                uint symbolMaximumGrouped = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolMaximumGrouped, symbolMaximumGrouped);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsMaximumGroupedUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumAmount = symbolMaximumGrouped,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The maximum amount of grouped symbols allowed in a message has been set to {MaximumAmount} symbols").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Minimum length to check for symbols.
            if (args[4].Equals("minimumlength", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsMinimumLengthUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolMinimumMessageLength,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the minimum characters needed to check for symbols. " +
                        "Current value: {CurrentValue} characters. " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols set minimumlength [amount]").ConfigureAwait(false));
                    return false;
                }

                uint symbolMinimumMessageLength = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolMinimumMessageLength, symbolMinimumMessageLength);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsMinimumWordsUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MinimumAmount = symbolMinimumMessageLength,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The minimum characters needed to check for symbols has been to to {MinimumAmount} characters.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolsSetUsage", command.ChatMessage.RoomId,
               new
               {
                   CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                   BotName = Program.Settings.BotLogin,
                   User = command.ChatMessage.Username,
                   Sender = command.ChatMessage.Username,
                   command.ChatMessage.DisplayName
               },
               "@{DisplayName}, Sets the options for the symbols filter. " +
               "Usage: {CommandPrefix}{BotName} moderation symbols set [maximumpercent, maximumgrouped, minimumlength]").ConfigureAwait(false));
            return false;
        }

        /// <summary>
        /// Method called when we update the symbol filter timeout via commands.
        /// Command: !bot moderation symbols timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateSymbolFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the symbol moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation symbols timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols timeout time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint symbolTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolTimeoutTimeSeconds, symbolTimeoutTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = symbolTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols timeout message [message]").ConfigureAwait(false));
                    return false;
                }

                string symbolTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolTimeoutMessage, symbolTimeoutMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = symbolTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols timeout reason [message]").ConfigureAwait(false));
                    return false;
                }

                string symbolTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolTimeoutReason, symbolTimeoutReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = symbolTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment symbolTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolTimeoutPunishment, symbolTimeoutPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = symbolTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the symbol moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation symbols timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the symbol filter toggle via commands.
        /// Command: !bot moderation symbols toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateSymbolFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the symbol moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation symbols toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for link moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation symbols toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            bool symbolStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolStatus, symbolStatus);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The symbol moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the symbol filter warning via commands.
        /// Command: !bot moderation symbols warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateSymbolFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the symbol moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation symbols warning [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols warning time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint symbolWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolWarningTimeSeconds, symbolWarningTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = symbolWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols warning message [message]").ConfigureAwait(false));
                    return false;
                }

                string symbolWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolWarningMessage, symbolWarningMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = symbolWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols warning reason [message]").ConfigureAwait(false));
                    return false;
                }

                string symbolWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolWarningReason, symbolWarningReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = symbolWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).SymbolWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation symbols warning punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment symbolWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.SymbolWarningPunishment, symbolWarningPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = symbolWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The symbol moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "SymbolWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the symbol moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation symbols warning [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        #endregion Symbol Filter Command Methods

        #region Lengthy Message Filter Command Methods

        /// <summary>
        /// Moderation commands for lengthy messages.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "lengthy", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationLongMessageCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateLengthyFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateLengthyFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateLengthyFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateLengthyFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "set":
                    _ = await this.UpdateLengthyMessageFilterOptions(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's lengthy message moderation filter. Usage: {CommandPrefix}{BotName} moderation lengthy [toggle, warning, timeout, exclude, set]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the lengthy message filter exclude via commands.
        /// Command: !bot moderation lengthy exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateLengthyFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the lengthy message filter. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation lengthy exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation lengthy exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels lengthyMessageExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageExcludedLevels, lengthyMessageExcludedLevels);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = lengthyMessageExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The lengthy message moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the lengthy message filter timeout via commands.
        /// Command: !bot moderation lengthy timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateLengthyFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the lengthy message moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation lengthy timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy timeout time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint lengthyMessageTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageTimeoutTimeSeconds, lengthyMessageTimeoutTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = lengthyMessageTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy timeout message [message]").ConfigureAwait(false));
                    return false;
                }

                string lengthyMessageTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageTimeoutMessage, lengthyMessageTimeoutMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = lengthyMessageTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy timeout reason [message]").ConfigureAwait(false));
                    return false;
                }

                string lengthyMessageTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageTimeoutReason, lengthyMessageTimeoutReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = lengthyMessageTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment lengthyMessageTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageTimeoutPunishment, lengthyMessageTimeoutPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = lengthyMessageTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the lengthy message moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation lengthy timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the lengthy message filter toggle via commands.
        /// Command: !bot moderation lengthy toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateLengthyFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the lengthy message moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation lengthy toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for lengthy messages moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation lengthy toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            bool lengthyMessageStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageStatus, lengthyMessageStatus);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The lengthy message moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the lengthy message filter warning via commands.
        /// Command: !bot moderation lengthy warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateLengthyFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the lengthy message moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation lengthy warning [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy warning time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint lengthyMessageWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageWarningTimeSeconds, lengthyMessageWarningTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = lengthyMessageWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy warning message [message]").ConfigureAwait(false));
                    return false;
                }

                string lengthyMessageWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageWarningMessage, lengthyMessageWarningMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = lengthyMessageWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy warning reason [message]").ConfigureAwait(false));
                    return false;
                }

                string lengthyMessageWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageWarningReason, lengthyMessageWarningReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = lengthyMessageWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy warning punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment lengthyMessageWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageWarningPunishment, lengthyMessageWarningPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = lengthyMessageWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The lengthy message moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthyWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the lengthy message moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation lengthy warning [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the lengthy message filter exclude via commands.
        /// Command: !bot moderation lengthy set
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateLengthyMessageFilterOptions(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "maximumlength" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthySetUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the options for the lengthy message filter. " +
                    "Usage: {CommandPrefix}{BotName} moderation lengthy set [maximumlength]").ConfigureAwait(false));
                return false;
            }

            // Maximum message length.
            if (args[4].Equals("maximumlength", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthySetMinimumLengthUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageMaximumLength,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the minimum characters required in a message before checking for lengthy messages. " +
                        "Current value: {CurrentValue} characters" +
                        "Usage: {CommandPrefix}{BotName} moderation lengthy set maximumlength [maximum length]").ConfigureAwait(false));
                    return false;
                }

                uint lengthyMessageMaximumLength = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.LengthyMessageMaximumLength, lengthyMessageMaximumLength);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthySetMinimumLengthUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumLength = lengthyMessageMaximumLength,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The maximum message length has been set to {MaximumLength} characters.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LengthySetUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).LengthyMessageExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the options for the lengthy message filter. " +
                "Usage: {CommandPrefix}{BotName} moderation lengthy set [maximumlength]").ConfigureAwait(false));

            return false;
        }

        #endregion Lengthy Message Filter Command Methods

        #region Action Message Filter Command Methods

        /// <summary>
        /// Moderation commands for action messages.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "action", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationActionMessageCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateActionMessageFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateActionMessageFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateActionMessageFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateActionMessageFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's action message moderation filter. Usage: {CommandPrefix}{BotName} moderation action [toggle, warning, timeout, exclude]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the action message exclude via commands.
        /// Command: !bot moderation action exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateActionMessageFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the action message. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation action exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation action exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels actionMessageExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageExcludedLevels, actionMessageExcludedLevels);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = actionMessageExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The action message moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the action message timeout via commands.
        /// Command: !bot moderation action timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateActionMessageFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the action message moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation action timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation action timeout time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint actionMessageTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageTimeoutTimeSeconds, actionMessageTimeoutTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = actionMessageTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation action timeout message [message]").ConfigureAwait(false));
                    return false;
                }

                string actionMessageTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageTimeoutMessage, actionMessageTimeoutMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = actionMessageTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation action timeout reason [message]").ConfigureAwait(false));
                    return false;
                }

                string actionMessageTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageTimeoutReason, actionMessageTimeoutReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = actionMessageTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation action timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment actionMessageTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageTimeoutPunishment, actionMessageTimeoutPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = actionMessageTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the action message moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation action timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the action message toggle via commands.
        /// Command: !bot moderation action toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateActionMessageFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the action message moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation action toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for action message moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation action toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            bool actionMessageStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageStatus, actionMessageStatus);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The action message moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the action message warning via commands.
        /// Command: !bot moderation action warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateActionMessageFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the action message moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation action warning [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation action warning time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint actionMessageWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageWarningTimeSeconds, actionMessageWarningTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = actionMessageWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation action warning message [message]").ConfigureAwait(false));
                    return false;
                }

                string actionMessageWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageWarningMessage, actionMessageWarningMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = actionMessageWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation action warning reason [message]").ConfigureAwait(false));
                    return false;
                }

                string actionMessageWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageWarningReason, actionMessageWarningReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = actionMessageWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ActionMessageWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation action warning punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment actionMessageWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.ActionMessageWarningPunishment, actionMessageWarningPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = actionMessageWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The action message moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ActionMessageWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the action message moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation action warning [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        #endregion Action Message Filter Command Methods

        #region Emote Filter Command Methods

        /// <summary>
        /// Moderation commands for emotes.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "emotes", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationEmotesCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateEmoteFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateEmoteFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateEmoteFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateEmoteFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "set":
                    _ = await this.UpdateEmoteFilterOptions(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's emote moderation filter. Usage: {CommandPrefix}{BotName} moderation emotes [toggle, warning, timeout, exclude, set]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the emote filter exclude via commands.
        /// Command: !bot moderation emotes exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateEmoteFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the emotes filter. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation emotes exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation emotes exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels emoteExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteExcludedLevels, emoteExcludedLevels);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = emoteExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The emotes moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the emote filter exclude via commands.
        /// Command: !bot moderation emotes set
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateEmoteFilterOptions(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "maximumpercent or minimumlength" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteSetUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the options for the emotes filter. " +
                    "Usage: {CommandPrefix}{BotName} moderation emotes set [maximum, removeemotesonly]").ConfigureAwait(false));
                return false;
            }

            // Maximum emotes in message setting.
            if (args[4].Equals("maximum", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteSetMaximumUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteMaximumAllowed,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the maximum percentage of emotes allowed in a message. " +
                        "Current value: {CurrentValue} emotes" +
                        "Usage: {CommandPrefix}{BotName} moderation emotes set maximumpercent [amount]").ConfigureAwait(false));
                    return false;
                }

                uint emoteMaximumAllowed = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteMaximumAllowed, emoteMaximumAllowed);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteSetMaximumUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumEmotes = emoteMaximumAllowed,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes maximum emotes allowed in a message has been set to {MaximumEmotes} emotes.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Remove messages only containing emotes.
            if (args[4].Equals("removeemotesonly", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !bool.TryParse(args[5], out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteSetRemoveAllUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteRemoveOnlyEmotes,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets if messages only containing emotes should be deleted. " +
                        "Current value: {CurrentValue}" +
                        "Usage: {CommandPrefix}{BotName} moderation emotes set removeemotesonly [true or false]").ConfigureAwait(false));
                    return false;
                }

                bool emoteRemoveOnlyEmotes = bool.Parse(args[5]);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteRemoveOnlyEmotes, emoteRemoveOnlyEmotes);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteSetRemoveAllUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        RemoveOnly = emoteRemoveOnlyEmotes,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes remove only emotes filter has been set to {RemoveOnly}.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteSetUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the options for the emotes filter. " +
                "Usage: {CommandPrefix}{BotName} moderation emotes set [maximum, removeemotesonly]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the emote filter timeout via commands.
        /// Command: !bot moderation emotes timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateEmoteFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the emote moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation emotes timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes timeout time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint emoteTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteTimeoutTimeSeconds, emoteTimeoutTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = emoteTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes timeout message [message]").ConfigureAwait(false));
                    return false;
                }

                string emoteTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteTimeoutMessage, emoteTimeoutMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = emoteTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes timeout reason [message]").ConfigureAwait(false));
                    return false;
                }

                string emoteTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteTimeoutReason, emoteTimeoutReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = emoteTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment emoteTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteTimeoutPunishment, emoteTimeoutPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = emoteTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the emote moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation emotes timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the emote filter toggle via commands.
        /// Command: !bot moderation emotes toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateEmoteFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the emotes moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation emotes toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for emotes moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation emotes toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            bool emoteStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteStatus, emoteStatus);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The emotes moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the emote filter warning via commands.
        /// Command: !bot moderation emotes warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateEmoteFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the emote moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation emotes warning [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes warning time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint emoteWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteWarningTimeSeconds, emoteWarningTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = emoteWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes warning message [message]").ConfigureAwait(false));
                    return false;
                }

                string emoteWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteWarningMessage, emoteWarningMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = emoteWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes warning reason [message]").ConfigureAwait(false));
                    return false;
                }

                string emoteWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteWarningReason, emoteWarningReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = emoteWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).EmoteWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation emotes warning punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment emoteWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.EmoteWarningPunishment, emoteWarningPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = emoteWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The emotes moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "EmoteWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the emote moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation emotes warning [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        #endregion Emote Filter Command Methods

        #region Fake Purge Command Methods

        /// <summary>
        /// Moderation commands for fake purge.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "fakepurge", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationFakePurgeCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    _ = await this.UpdateFakePurgeFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    _ = await this.UpdateFakePurgeFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    _ = await this.UpdateFakePurgeFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    _ = await this.UpdateFakePurgeFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's fake purge moderation filter. Usage: {CommandPrefix}{BotName} moderation fakepurge [toggle, warning, timeout, exclude]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Method called when we update the fake purge exclude via commands.
        /// Command: !bot moderation fakepurge exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateFakePurgeFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the fake purge. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation fakepurge exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation fakepurge exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return false;
            }

            UserLevels fakePurgeExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeExcludedLevels, fakePurgeExcludedLevels);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = fakePurgeExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The fake purge moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the fake purge timeout via commands.
        /// Command: !bot moderation fakepurge timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateFakePurgeFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the fake purge moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation fakepurge timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge timeout time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint fakePurgeTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeTimeoutTimeSeconds, fakePurgeTimeoutTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = fakePurgeTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge timeout message [message]").ConfigureAwait(false));
                    return false;
                }

                string fakePurgeTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeTimeoutMessage, fakePurgeTimeoutMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = fakePurgeTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge timeout reason [message]").ConfigureAwait(false));
                    return false;
                }

                string fakePurgeTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeTimeoutReason, fakePurgeTimeoutReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = fakePurgeTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment fakePurgeTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeTimeoutPunishment, fakePurgeTimeoutPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = fakePurgeTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the fake purge moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation fakepurge timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        /// <summary>
        /// Method called when we update the fake purge toggle via commands.
        /// Command: !bot moderation fakepurge toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateFakePurgeFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the fake purge moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation fakepurge toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for fake purge moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation fakepurge toggle [on, off]").ConfigureAwait(false));
                return false;
            }

            bool fakePurgeStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            // Update database.
            IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

            FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

            UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeStatus, fakePurgeStatus);

            UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The fake purge moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return result.IsAcknowledged;
        }

        /// <summary>
        /// Method called when we update the fake purge warning via commands.
        /// Command: !bot moderation fakepurge warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<bool> UpdateFakePurgeFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return false;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the fake purge moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation fakepurge warning [time, message, reason, punishment]").ConfigureAwait(false));
                return false;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge warning time [1 to 1209600]").ConfigureAwait(false));
                    return false;
                }

                uint fakePurgeWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeWarningTimeSeconds, fakePurgeWarningTimeSeconds);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = fakePurgeWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge warning message [message]").ConfigureAwait(false));
                    return false;
                }

                string fakePurgeWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeWarningMessage, fakePurgeWarningMessage);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = fakePurgeWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge warning reason [message]").ConfigureAwait(false));
                    return false;
                }

                string fakePurgeWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeWarningReason, fakePurgeWarningReason);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = fakePurgeWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).FakePurgeWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation fakepurge warning punishment [{Punishments}]").ConfigureAwait(false));

                    return false;
                }

                ModerationPunishment fakePurgeWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                // Update database.
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d.FakePurgeWarningPunishment, fakePurgeWarningPunishment);

                UpdateResult result = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = fakePurgeWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The fake purge moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return result.IsAcknowledged;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "FakePurgeWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the fake purge moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation fakepurge warning [time, message, reason, punishment]").ConfigureAwait(false));

            return false;
        }

        #endregion Fake Purge Command Methods

        #region Zalgo Filter Command Methods

        /// <summary>
        /// Moderation commands for zalgo.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "zalgo", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationZalgoCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            // Each command will return a document if a modification has been made.
            ModerationDocument document = null;

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    document = await this.UpdateZalgoFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    document = await this.UpdateZalgoFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    document = await this.UpdateZalgoFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    document = await this.UpdateZalgoFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's zalgo moderation filter. Usage: {CommandPrefix}{BotName} moderation zalgo [toggle, warning, timeout, exclude]").ConfigureAwait(false));
                    break;
            }

            // Update settings.
            if (!(document is null))
            {
                //TODO: Refactor Mongo
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == e.Command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d, document);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Method called when we update the zalgo exclude via commands.
        /// Command: !bot moderation zalgo exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateZalgoFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the zalgo. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation zalgo exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return null;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation zalgo exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return null;
            }

            //TODO: Refactor Mongo
            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.ZalgoExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = document.ZalgoExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The zalgo moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the zalgo timeout via commands.
        /// Command: !bot moderation zalgo timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateZalgoFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the zalgo moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation zalgo timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return null;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo timeout time [1 to 1209600]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.ZalgoTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo timeout message [message]").ConfigureAwait(false));
                    return null;
                }
                //TODO: Refactor Mongo

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = document.ZalgoTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return document;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo timeout reason [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = document.ZalgoTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return document;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = document.ZalgoTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return document;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the zalgo moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation zalgo timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return null;
        }

        /// <summary>
        /// Method called when we update the zalgo toggle via commands.
        /// Command: !bot moderation zalgo toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateZalgoFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the zalgo moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation zalgo toggle [on, off]").ConfigureAwait(false));
                return null;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for zalgo moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation zalgo toggle [on, off]").ConfigureAwait(false));
                return null;
            }

            //TODO: Refactor Mongo
            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.ZalgoStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The zalgo moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the zalgo warning via commands.
        /// Command: !bot moderation zalgo warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateZalgoFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the zalgo moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation zalgo warning [time, message, reason, punishment]").ConfigureAwait(false));
                return null;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo warning time [1 to 1209600]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.ZalgoWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo warning message [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = document.ZalgoWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return document;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo warning reason [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = document.ZalgoWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return document;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).ZalgoWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation zalgo warning punishment [{Punishments}]").ConfigureAwait(false));

                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.ZalgoWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = document.ZalgoWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The zalgo moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return document;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ZalgoWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the zalgo moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation zalgo warning [time, message, reason, punishment]").ConfigureAwait(false));

            return null;
        }

        #endregion Zalgo Filter Command Methods

        #region One Man spam Filter Command Methods

        /// <summary>
        /// Moderation commands for one man spam.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event arguments from the Twitch Lib <see cref="OnChatCommandReceivedArgs">/></param>
        [BotnameChatCommand("moderation", "onemanspam", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationOneManSpamCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 2).ConfigureAwait(false))
            {
                return;
            }

            // Each command will return a document if a modification has been made.
            ModerationDocument document = null;

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    document = await this.UpdateOneManSpamFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    document = await this.UpdateOneManSpamFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    document = await this.UpdateOneManSpamFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    document = await this.UpdateOneManSpamFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's one man spam moderation filter. Usage: {CommandPrefix}{BotName} moderation onemanspam [toggle, warning, timeout, exclude]").ConfigureAwait(false));
                    break;
            }

            // Update settings.
            if (!(document is null))
            {
                //TODO: Refactor Mongo
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>(ModerationDocument.CollectionName);

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == e.Command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d, document);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Method called when we update the one man spam exclude via commands.
        /// Command: !bot moderation onemanspam exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateOneManSpamFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "subscribers, vips, subscribers vips" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamExcludeUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the excluded levels for the one man spam. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation onemanspam exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return null;
            }

            // Make sure it is valid.
            if (!args[4].Equals("subscribers", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("vips", StringComparison.OrdinalIgnoreCase) && !string.Equals("subscribers vips", string.Join(" ", args.GetRange(4, args.Count - 4)), StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamExcludeOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamExcludedLevels,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid level must be specified for the excluded levels. " +
                    "Current value: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation onemanspam exclude [subscribers, vips, subscribers vips]").ConfigureAwait(false));
                return null;
            }

            //TODO: Refactor Mongo
            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.OneManSpamExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = document.OneManSpamExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The one man spam moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the one man spam filter exclude via commands.
        /// Command: !bot moderation onemanspam set
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateOneManSpamFilterOptions(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "maximummessages or timespan" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamSetUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the options for the one man spam filter. " +
                    "Usage: {CommandPrefix}{BotName} moderation onemanspam set [maximummessages, timespan]").ConfigureAwait(false));
                return null;
            }

            // Maximum messages allowed in a time span.
            if (args[4].Equals("maximummessages", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamSetMaximumMsgUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamMaximumMessages,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the maximum messages allowed in a time span. " +
                        "Current value: {CurrentValue} messages. " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam set maximummessages [amount]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamMaximumMessages = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpanSetMaximumMsgUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumMessages = document.OneManSpamMaximumMessages,
                        TimeSpan = document.OneManSpamResetTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The maximum messages allowed in {TimeSpan} seconds has been set to {MaximumMessages} messages.").ConfigureAwait(false));

                return document;
            }

            // Time span for messages
            if (args[4].Equals("timespan", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint val) && val >= 0))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamSetTimeSpanUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamResetTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the time span for the maximum messaged allowed to be sent. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam set timespan [amount]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamResetTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamSetTimeSpanUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSpan = document.OneManSpamResetTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The time span for the amount of messages allowed has been set to {TimeSpan} messages.").ConfigureAwait(false));

                return document;
            }

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamSetUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the options for the one man spam filter. " +
                "Usage: {CommandPrefix}{BotName} moderation onemanspam set [maximummessages, timespan]").ConfigureAwait(false));

            return null;
        }

        /// <summary>
        /// Method called when we update the one man spam timeout via commands.
        /// Command: !bot moderation onemanspam timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateOneManSpamFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the timeout offence of the one man spam moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation onemanspam timeout [time, message, reason, punishment]").ConfigureAwait(false));
                return null;
            }

            // Timeout time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamTimeoutTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam timeout time [1 to 1209600]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.OneManSpamTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
            }

            // Timeout message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamTimeoutMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam timeout message [message]").ConfigureAwait(false));
                    return null;
                }
                //TODO: Refactor Mongo

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = document.OneManSpamTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return document;
            }

            // Timeout reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamTimeoutReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam timeout reason [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = document.OneManSpamTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return document;
            }

            // Timeout punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamTimeoutPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam timeout punishment [{Punishments}]").ConfigureAwait(false));

                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = document.OneManSpamTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return document;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamTimeoutUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the timeout offence of the one man spam moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation onemanspam timeout [time, message, reason, punishment]").ConfigureAwait(false));

            return null;
        }

        /// <summary>
        /// Method called when we update the one man spam toggle via commands.
        /// Command: !bot moderation onemanspam toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateOneManSpamFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "on" or "off" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamToggleUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Toggles the one man spam moderation. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation onemanspam toggle [on, off]").ConfigureAwait(false));
                return null;
            }

            // If the forth argument isn't "on" or "off" at all.
            if (!args[4].Equals("on", StringComparison.OrdinalIgnoreCase) && !args[4].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamToggleOptions", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamStatus,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, A valid toggle for one man spam moderation must be specified. " +
                    "Current status: {CurrentValue}. " +
                    "Usage: {CommandPrefix}{BotName} moderation onemanspam toggle [on, off]").ConfigureAwait(false));
                return null;
            }

            //TODO: Refactor Mongo
            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.OneManSpamStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The one man spam moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the one man spam warning via commands.
        /// Command: !bot moderation onemanspam warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateOneManSpamFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
            }

            // If "time, message, reason, punishment" hasn't been specified in the arguments.
            if (args.IsNullEmptyOrOutOfRange(4))
            {
                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningUsage", command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        BotName = Program.Settings.BotLogin,
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, Sets the values for the warning offence of the one man spam moderation. " +
                    "Usage: {CommandPrefix}{BotName} moderation onemanspam warning [time, message, reason, punishment]").ConfigureAwait(false));
                return null;
            }

            // Warning time setting.
            if (args[4].Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !(uint.TryParse(args[5], out uint value) && value > 0 && value <= 1209600))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningTimeUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamWarningTimeSeconds,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets how long a user will get timed-out for on their first offence in seconds. " +
                        "Current value: {CurrentValue} seconds. " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam warning time [1 to 1209600]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.OneManSpamWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
            }

            // Warning message setting.
            if (args[4].Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningMessageUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamWarningMessage,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said in chat when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam warning message [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = document.OneManSpamWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return document;
            }

            // Warning reason setting.
            if (args[4].Equals("reason", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningReasonUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamWarningReason,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam warning reason [message]").ConfigureAwait(false));
                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = document.OneManSpamWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return document;
            }

            // Warning punishment setting.
            if (args[4].Equals("punishment", StringComparison.OrdinalIgnoreCase))
            {
                if (args.IsNullEmptyOrOutOfRange(5) || !Enum.TryParse(typeof(ModerationPunishment), args[5], true, out _))
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningPunishmentUsage", command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            CurrentValue = (await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false)).OneManSpamWarningPunishment,
                            Punishments = string.Join(", ", Enum.GetNames(typeof(ModerationPunishment))),
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Sets the message said to moderators when a user get timed-out on their first offence. " +
                        "Current value: \"{CurrentValue}\". " +
                        "Usage: {CommandPrefix}{BotName} moderation onemanspam warning punishment [{Punishments}]").ConfigureAwait(false));

                    return null;
                }

                //TODO: Refactor Mongo
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.OneManSpamWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = document.OneManSpamWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The one man spam moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return document;
            }

            // Usage if none of the above match.
            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "OneManSpamWarningUsage", command.ChatMessage.RoomId,
                new
                {
                    CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                    BotName = Program.Settings.BotLogin,
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, Sets the values for the warning offence of the one man spam moderation. " +
                "Usage: {CommandPrefix}{BotName} moderation onemanspam warning [time, message, reason, punishment]").ConfigureAwait(false));

            return null;
        }

        #endregion One Man spam Filter Command Methods
    }
}