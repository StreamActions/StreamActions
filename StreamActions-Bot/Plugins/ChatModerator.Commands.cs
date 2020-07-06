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

            // Each command will return a document if a modification has been made.
            ModerationDocument document = null;

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    document = await this.UpdateLinkFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    document = await this.UpdateLinkFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    document = await this.UpdateLinkFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    document = await this.UpdateLinkFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "whitelist":
                    document = await this.UpdateLinkFilterWhitelist(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
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

            // Update settings.
            if (!(document is null))
            {
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>("chatmoderator");

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == e.Command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d, document);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Method called when we update the link filter exclude via commands.
        /// Command: !bot moderation links exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateLinkFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                return null;
            }

            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.LinkExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = document.LinkExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The link moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the link filter timeout via commands.
        /// Command: !bot moderation links timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateLinkFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.LinkTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = document.LinkTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = document.LinkTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return document;
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

                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = document.LinkTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return document;
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

            return null;
        }

        /// <summary>
        /// Method called when we update the link filter toggle via commands.
        /// Command: !bot moderation links toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateLinkFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                return null;
            }

            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.LinkStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The link moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the link filter warning via commands.
        /// Command: !bot moderation links warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateLinkFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.LinkWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = document.LinkWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = document.LinkWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return document;
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

                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = document.LinkWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The link moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return document;
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

            return null;
        }

        /// <summary>
        /// Method called when we update the link filter whitelist via commands.
        /// Command: !bot moderation links whitelist
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateLinkFilterWhitelist(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.LinkWhitelist.Add(args[5]);

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
                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);

                string finder = document.LinkWhitelist.Find(u => u.Equals(args[5], StringComparison.OrdinalIgnoreCase));

                if (finder.Any())
                {
                    _ = document.LinkWhitelist.Remove(finder);

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
                    return document;
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
                return null;
            }

            // Listing all whitelists.
            if (args[4].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                string whitelists = "";
                int numPages = (int)Math.Ceiling(document.LinkWhitelist.Count / (double)_itemsPerPage);
                int page = Math.Min(numPages, Math.Max(1, int.Parse(args.GetElementAtOrDefault(5, "1"), NumberStyles.Integer, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false))));

                if (document.LinkWhitelist.Count == 0)
                {
                    TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "LinkWhitelistListNone", command.ChatMessage.RoomId,
                        new
                        {
                            User = command.ChatMessage.Username,
                            Sender = command.ChatMessage.Username,
                            command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, There are no whitelisted links.").ConfigureAwait(false));
                    return null;
                }

                foreach (string whitelist in document.LinkWhitelist.Skip((page - 1) * _itemsPerPage).Take(_itemsPerPage))
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

                return null;
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

            return null;
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

            // Each command will return a document if a modification has been made.
            ModerationDocument document = null;

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    document = await this.UpdateCapFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    document = await this.UpdateCapFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    document = await this.UpdateCapFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    document = await this.UpdateCapFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "set":
                    document = await this.UpdateCapFilterOptions(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
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
                        "@{DisplayName}, Manages the bot's link moderation filter. Usage: {CommandPrefix}{BotName} moderation caps [toggle, warning, timeout, exclude, set]").ConfigureAwait(false));
                    break;
            }

            // Update settings.
            if (!(document is null))
            {
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>("chatmoderator");

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == e.Command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d, document);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Method called when we update the cap filter exclude via commands.
        /// Command: !bot moderation caps exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateCapFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                return null;
            }

            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.CapExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = document.CapExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The caps moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the cap filter exclude via commands.
        /// Command: !bot moderation caps set
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateCapFilterOptions(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapMaximumPercentage = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetMaximumPercentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MaximumPercent = document.CapMaximumPercentage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps maximum caps percent allowed in a message has been set to {MaximumPercent}%").ConfigureAwait(false));

                return document;
            }

            // Minimum message length before checking for caps setting.
            // Maximum caps in message setting.
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapMinimumMessageLength = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapSetMinimumLengthUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        MinimumLength = document.CapMinimumMessageLength,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps minimum required length has been set to {MinimumLength} characters.").ConfigureAwait(false));

                return document;
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

            return null;
        }

        /// <summary>
        /// Method called when we update the cap filter timeout via commands.
        /// Command: !bot moderation caps timeout
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateCapFilterTimeout(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapTimeoutTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.CapTimeoutTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutMessage = document.CapTimeoutMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout message has been set to \"{TimeoutMessage}\".").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutReason = document.CapTimeoutReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout reason has been set to \"{TimeoutReason}\".").ConfigureAwait(false));

                return document;
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

                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapTimeoutPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeoutPunishment = document.CapTimeoutPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation timeout punishment has been set to \"{TimeoutPunishment}\".").ConfigureAwait(false));

                return document;
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

            return null;
        }

        /// <summary>
        /// Method called when we update the cap filter toggle via commands.
        /// Command: !bot moderation caps toggle
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateCapFilterToggle(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                return null;
            }

            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.CapStatus = args[4].Equals("on", StringComparison.OrdinalIgnoreCase);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapToggle", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ToggleStatus = args[4].ToLowerInvariant(),
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The caps moderation status has been toggled {ToggleStatus}.").ConfigureAwait(false));

            return document;
        }

        /// <summary>
        /// Method called when we update the cap filter warning via commands.
        /// Command: !bot moderation caps warning
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateCapFilterWarning(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapWarningTimeSeconds = uint.Parse(args[5], NumberStyles.Number, await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningTimeUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        TimeSeconds = document.CapWarningTimeSeconds,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning time has been set to {TimeSeconds} seconds.").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningMessageUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningMessage = document.CapWarningMessage,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning message has been set to \"{WarningMessage}\".").ConfigureAwait(false));

                return document;
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
                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningReasonUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningReason = document.CapWarningReason,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning reason has been set to \"{WarningReason}\".").ConfigureAwait(false));

                return document;
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

                    return null;
                }

                ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                document.CapWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), args[5], true);

                TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "CapWarningPunishmentUpdate", command.ChatMessage.RoomId,
                    new
                    {
                        User = command.ChatMessage.Username,
                        Sender = command.ChatMessage.Username,
                        WarningPunishment = document.CapWarningPunishment,
                        command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The caps moderation warning punishment has been set to \"{WarningPunishment}\".").ConfigureAwait(false));

                return document;
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

            return null;
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

            // Each command will return a document if a modification has been made.
            ModerationDocument document = null;

            switch ((e.Command.ArgumentsAsList.IsNullEmptyOrOutOfRange(3) ? "usage" : e.Command.ArgumentsAsList[3]))
            {
                case "toggle":
                    document = await this.UpdateRepetitionFilterToggle(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "warning":
                    document = await this.UpdateRepetitionFilterWarning(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "timeout":
                    document = await this.UpdateRepetitionFilterTimeout(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "exclude":
                    document = await this.UpdateRepetitionFilterExcludes(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
                    break;

                case "set":
                    document = await this.UpdateRepetitionFilterOptions(e.Command, e.Command.ArgumentsAsList).ConfigureAwait(false);
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

            // Update settings.
            if (!(document is null))
            {
                IMongoCollection<ModerationDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<ModerationDocument>("chatmoderator");

                FilterDefinition<ModerationDocument> filter = Builders<ModerationDocument>.Filter.Where(d => d.ChannelId == e.Command.ChatMessage.RoomId);

                UpdateDefinition<ModerationDocument> update = Builders<ModerationDocument>.Update.Set(d => d, document);

                _ = await collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Method called when we update the repetition filter exclude via commands.
        /// Command: !bot moderation repetition exclude
        /// </summary>
        /// <param name="command">The chat commands used. <see cref="ChatCommand"/></param>
        /// <param name="args">Arguments of the command. <see cref="ChatCommand.ArgumentsAsList"/></param>
        /// <returns>The updated moderation document if an update was made, else it is null.</returns>
        private async Task<ModerationDocument> UpdateRepetitionFilterExcludes(ChatCommand command, List<string> args)
        {
            if (!await Permission.Can(command, false, 4).ConfigureAwait(false))
            {
                return null;
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
                return null;
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
                return null;
            }

            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
            document.RepetitionExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels), string.Join(", ", args.GetRange(4, args.Count - 4)), true);

            TwitchLibClient.Instance.SendMessage(command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "RepetitionExcludeUpdate", command.ChatMessage.RoomId,
                new
                {
                    User = command.ChatMessage.Username,
                    Sender = command.ChatMessage.Username,
                    ExcludedLevel = document.RepetitionExcludedLevels,
                    command.ChatMessage.DisplayName
                },
                "@{DisplayName}, The repetition moderation excluded levels has been set to {ExcludedLevel}.").ConfigureAwait(false));

            return document;
        }

        private async Task<ModerationDocument> UpdateRepetitionFilterOptions(ChatCommand command, List<string> args)
        {
            // TODO: Finish this.
            return null;
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
    }
}