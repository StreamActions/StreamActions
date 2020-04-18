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

            switch ((e.Command.ArgumentsAsList[1] ?? "usage").ToLowerInvariant())
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Just parsing a normal number.")]
        private async void UpdateLinkFilter(ChatCommand command)
        {
            ModerationDocument document = null;

            if (string.IsNullOrEmpty(command.ArgumentsAsList[2]))
            {
                // Usage.
                return;
            }

            if (string.Equals("toggle", command.ArgumentsAsList[2], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("on", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                    document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                    document.LinkStatus = true;
                }
                else if (string.Equals("off", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                    document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                    document.LinkStatus = false;
                }
                else
                {
                    // Usage.
                }
            }
            else if (string.Equals("warning", command.ArgumentsAsList[2], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("time", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(command.ArgumentsAsList[4]) && uint.TryParse(command.ArgumentsAsList[4], out _))
                    {
                        document = await GetFilterDocumentForChannel(command.ChatMessage.RoomId).ConfigureAwait(false);
                        document.LinkWarningTimeSeconds = uint.Parse(command.ArgumentsAsList[4]);
                        document.LinkWarningPunishment = (document.LinkWarningTimeSeconds > 1 ? ModerationPunishment.Timeout :
                            (document.LinkTimeoutTimeSeconds < 1 ? ModerationPunishment.Delete : ModerationPunishment.Purge));
                    }
                    else
                    {
                        // Usage.
                    }
                }
                else if (string.Equals("message", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("reason", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("punishment", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else
                {
                    // Usage.
                }
            }
            else if (string.Equals("timeout", command.ArgumentsAsList[2], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("time", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("message", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("reason", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("punishment", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else
                {
                    // Usage.
                }
            }
            else if (string.Equals("exclude", command.ArgumentsAsList[2], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("add", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("remove", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else
                {
                }
            }
            else if (string.Equals("whitelist", command.ArgumentsAsList[2], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals("add", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("remove", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (string.Equals("list", command.ArgumentsAsList[3], StringComparison.OrdinalIgnoreCase))
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