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
using System.Globalization;
using StreamActions.Attributes;
using StreamActions.Database.Documents;
using StreamActions.Enums;
using StreamActions.Plugin;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace StreamActions.Plugins
{
    public partial class ChatModerator
    {
        // TO REMOVE: !phantombot moderation links [toggle / whitelist / warning (time / message / reason / punishment) / timeout (time / message / reason / punishment) / exclude]

        #region Private Methods

        /// <summary>
        /// Chat moderator manegement commands.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        [BotnameChatCommand("moderation", UserLevels.Moderator)]
        private async void ChatModerator_OnModerationCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (!await Permission.Can(e.Command, false, 1).ConfigureAwait(false))
            {
                return;
            }

            switch ((e.Command.ArgumentsAsList[2] ?? "usage").ToLowerInvariant())
            {
                case "links":
                    this.UpdateLinkFilter(e.Command);
                    break;

                case "caps":
                    // Handle caps.
                    break;

                case "spam":
                    // Handle spam.
                    break;

                case "symbols":
                    // Handle symbols.
                    break;

                case "emotes":
                    // Handle emotes.
                    break;

                case "zalgo":
                    // Handle zalgo.
                    break;

                case "blacklist":
                    // Handle blacklist
                    break;

                case "fakepurge":
                    // Handle fake purge.
                    break;

                case "lengthymessage":
                    // Handle lengthy messages.
                    break;

                case "onemanspam":
                    // Handle one man spam.
                    break;

                default:
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await I18n.Instance.GetAndFormatWithAsync("ChatModerator", "ModerationUsage", e.Command.ChatMessage.RoomId,
                        new
                        {
                            CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                            BotName = Program.Settings.BotLogin,
                            User = e.Command.ChatMessage.Username,
                            Sender = e.Command.ChatMessage.Username,
                            e.Command.ChatMessage.DisplayName
                        },
                        "@{DisplayName}, Manages the bot's moderation filters. Usage: {CommandPrefix}{BotName} moderation [links, caps, spam, symbols, emotes, zalgo, fakepurge, lengthymessage, onemanspam, blacklist]").ConfigureAwait(false));
                    break;
            }
        }

        /// <summary>
        /// Updates the link filter settings.
        /// </summary>
        /// <param name="command">Command used in chat.</param>
        private async void UpdateLinkFilter(ChatCommand command)
        {
            if (string.IsNullOrEmpty(command.ArgumentsAsList[3]))
            {
                // Usage.
                return;
            }

            ModerationDocument document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);

            if (string.Equals("toggle", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals("on", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase)
                    && !string.Equals("off", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    // Usage.
                    return;
                }

                document.LinkStatus = string.Equals("on", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase);
            }
            else if (string.Equals("warning", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("time", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(command.ArgumentsAsList[5]) || !uint.TryParse(command.ArgumentsAsList[5], out uint time) || time < 1 || time > 1209600)
                    {
                        // Usage.
                        return;
                    }

                    document.LinkWarningTimeSeconds = uint.Parse(command.ArgumentsAsList[5], NumberStyles.Integer,
                        await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));
                    document.LinkWarningPunishment = (document.LinkWarningTimeSeconds > 1 ? ModerationPunishment.Timeout :
                        (document.LinkWarningTimeSeconds < 1 ? ModerationPunishment.Delete : ModerationPunishment.Purge));
                }
                else if (string.Equals("message", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(command.ArgumentsAsList[5]))
                    {
                        // Usage.
                        return;
                    }

                    document.LinkWarningMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));
                }
                else if (string.Equals("reason", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(command.ArgumentsAsList[5]))
                    {
                        // Usage.
                        return;
                    }

                    document.LinkWarningReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));
                }
                else if (string.Equals("punishment", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals("delete", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase)
                        && !string.Equals("purge", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase)
                        && !string.Equals("timeout", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase)
                        && !string.Equals("ban", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase))
                    {
                        // Usage.
                        return;
                    }

                    document.LinkWarningPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), command.ArgumentsAsList[5], true);
                }
                else
                {
                    // Usage.
                }
            }
            else if (string.Equals("timeout", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("time", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(command.ArgumentsAsList[5]) || !uint.TryParse(command.ArgumentsAsList[5], out uint time) || time < 1 || time > 1209600)
                    {
                        // Usage.
                        return;
                    }

                    document.LinkTimeoutTimeSeconds = uint.Parse(command.ArgumentsAsList[5], NumberStyles.Integer,
                        await I18n.Instance.GetCurrentCultureAsync(command.ChatMessage.RoomId).ConfigureAwait(false));
                    document.LinkTimeoutPunishment = (document.LinkTimeoutTimeSeconds > 1 ? ModerationPunishment.Timeout :
                        (document.LinkTimeoutTimeSeconds < 1 ? ModerationPunishment.Delete : ModerationPunishment.Purge));
                }
                else if (string.Equals("message", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(command.ArgumentsAsList[5]))
                    {
                        // Usage.
                        return;
                    }

                    document.LinkTimeoutMessage = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));
                }
                else if (string.Equals("reason", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(command.ArgumentsAsList[5]))
                    {
                        // Usage.
                        return;
                    }

                    document.LinkTimeoutReason = string.Join(" ", command.ArgumentsAsList.GetRange(5, command.ArgumentsAsList.Count - 5));
                }
                else if (string.Equals("punishment", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals("delete", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase)
                        && !string.Equals("purge", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase)
                        && !string.Equals("timeout", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase)
                        && !string.Equals("ban", command.ArgumentsAsList[5], StringComparison.OrdinalIgnoreCase))
                    {
                        // Usage.
                        return;
                    }

                    document.LinkTimeoutPunishment = (ModerationPunishment)Enum.Parse(typeof(ModerationPunishment), command.ArgumentsAsList[5], true);
                }
                else
                {
                    // Usage.
                }
            }
            else if (string.Equals("exclude", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals("subscribers", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase)
                    && !string.Equals("vips", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase)
                    && !string.Equals("subscribers vips",
                    string.Join(" ", command.ArgumentsAsList.GetRange(4, command.ArgumentsAsList.Count - 4)), StringComparison.OrdinalIgnoreCase))
                {
                    // Usage.
                    return;
                }

                document.LinkExcludedLevels = (UserLevels)Enum.Parse(typeof(UserLevels),
                    string.Join(", ", command.ArgumentsAsList.GetRange(4, command.ArgumentsAsList.Count - 4)));
            }
            else if (string.Equals("whitelist", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("add", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("remove", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("list", command.ArgumentsAsList[4], StringComparison.OrdinalIgnoreCase))
                {
                }
                else
                {
                }
            }

            // Update if any changes were made.
            if (!(document is null))
            {
            }
        }

        #endregion Private Methods
    }
}