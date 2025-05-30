﻿/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using StreamActions.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StreamActions.Plugin
{
    public partial class PluginManager
    {
        #region Public Methods

        /// <summary>
        /// Indicates if the provided custom chat command is already registered.
        /// </summary>
        /// <param name="channelId">The channelId to test.</param>
        /// <param name="command">The command name to test.</param>
        /// <returns><c>true</c> if that command already exists.</returns>
        public static async Task<bool> DoesCustomChatCommandExistAsync(string channelId, string command) => !(await CustomCommand.GetCustomCommandAsync(channelId, command).ConfigureAwait(false) is null);

        /// <summary>
        /// A list of all currently registered custom chat commands for channelId.
        /// </summary>
        /// <param name="channelId">The channelId to check.</param>
        /// <returns>A list of registered custom chat commands.</returns>
        public static async Task<IEnumerable<string>> GetRegisteredCustomChatCommandsAsync(string channelId) => (await CustomCommand.ListCustomCommandsAsync(channelId).ConfigureAwait(false)).Select(d => d.Command);

        /// <summary>
        /// Indicates if the <c>!botname</c> tree already has the provided chat command registered.
        /// </summary>
        /// <param name="command">The command name to test.</param>
        /// <returns><c>true</c> if that command already exists under the <c>!botname</c> tree.</returns>
        public bool DoesBotnameChatCommandExist(string command) => this._registeredBotnameChatCommands.Any(l => l.Value.Any(c => string.Equals(c, command, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Indicates if the <c>!botname</c> tree already has the provided whisper command registered.
        /// </summary>
        /// <param name="command">The command name to test.</param>
        /// <returns><c>true</c> if that command already exists under the <c>!botname</c> tree.</returns>
        public bool DoesBotnameWhisperCommandExist(string command) => this._registeredBotnameWhisperCommands.Any(l => l.Value.Any(c => string.Equals(c, command, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Indicates if the provided chat command is already registered.
        /// </summary>
        /// <param name="command">The command name to test.</param>
        /// <returns><c>true</c> if that command already exists.</returns>
        public bool DoesChatCommandExist(string command) => this._registeredChatCommands.Any(l => l.Value.Any(c => string.Equals(c, command, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Indicates if the provided whisper command is already registered.
        /// </summary>
        /// <param name="command">The command name to test.</param>
        /// <returns><c>true</c> if that command already exists.</returns>
        public bool DoesWhisperCommandExist(string command) => this._registeredWhisperCommands.Any(l => l.Value.Any(c => string.Equals(c, command, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Registers a <c>!botname</c> chat command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the command.</param>
        /// <param name="command">The command to register.</param>
        /// <returns><c>true</c> on success; <c>false</c> if command is already registered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public bool RegisterBotnameChatCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (!this.DoesBotnameChatCommandExist(command))
            {
                if (!this._registeredBotnameChatCommands.ContainsKey(typeName))
                {
                    _ = this._registeredBotnameChatCommands.TryAdd(typeName, new List<string>());
                }

                if (string.IsNullOrWhiteSpace(command))
                {
                    throw new ArgumentNullException(nameof(command));
                }

                this._registeredBotnameChatCommands[typeName].Add(command.ToLowerInvariant());

                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers <c>!botname</c> chat commands.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the commands.</param>
        /// <param name="commands">The commands to register.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="commands"/> is null or empty.</exception>
        public void RegisterBotnameChatCommands(string typeName, string[] commands)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (commands is null || commands.Length == 0)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            foreach (string command in commands)
            {
                _ = this.RegisterBotnameChatCommand(typeName, command);
            }
        }

        /// <summary>
        /// Registers a <c>!botname</c> whisper command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the command.</param>
        /// <param name="command">The command to register.</param>
        /// <returns><c>true</c> on success; <c>false</c> if command is already registered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public bool RegisterBotnameWhisperCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (!this.DoesBotnameWhisperCommandExist(command))
            {
                if (!this._registeredBotnameWhisperCommands.ContainsKey(typeName))
                {
                    _ = this._registeredBotnameWhisperCommands.TryAdd(typeName, new List<string>());
                }

                if (string.IsNullOrWhiteSpace(command))
                {
                    throw new ArgumentNullException(nameof(command));
                }

                this._registeredBotnameWhisperCommands[typeName].Add(command.ToLowerInvariant());

                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers <c>!botname</c> whisper commands.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the commands.</param>
        /// <param name="commands">The commands to register.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="commands"/> is null or empty.</exception>
        public void RegisterBotnameWhisperCommands(string typeName, string[] commands)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (commands is null || commands.Length == 0)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            foreach (string command in commands)
            {
                _ = this.RegisterBotnameWhisperCommand(typeName, command);
            }
        }

        /// <summary>
        /// Registers a chat command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the command.</param>
        /// <param name="command">The command to register.</param>
        /// <returns><c>true</c> on success; <c>false</c> if command is already registered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public bool RegisterChatCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (!this.DoesChatCommandExist(command))
            {
                if (!this._registeredChatCommands.ContainsKey(typeName))
                {
                    _ = this._registeredChatCommands.TryAdd(typeName, new List<string>());
                }

                if (string.IsNullOrWhiteSpace(command))
                {
                    throw new ArgumentNullException(nameof(command));
                }

                this._registeredChatCommands[typeName].Add(command.ToLowerInvariant());

                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers chat commands.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the commands.</param>
        /// <param name="commands">The commands to register.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="commands"/> is null or empty.</exception>
        public void RegisterChatCommands(string typeName, string[] commands)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (commands is null || commands.Length == 0)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            foreach (string command in commands)
            {
                _ = this.RegisterChatCommand(typeName, command);
            }
        }

        /// <summary>
        /// Registers a whisper command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the command.</param>
        /// <param name="command">The command to register.</param>
        /// <returns><c>true</c> on success; <c>false</c> if command is already registered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public bool RegisterWhisperCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (!this.DoesWhisperCommandExist(command))
            {
                if (!this._registeredWhisperCommands.ContainsKey(typeName))
                {
                    _ = this._registeredWhisperCommands.TryAdd(typeName, new List<string>());
                }

                if (string.IsNullOrWhiteSpace(command))
                {
                    throw new ArgumentNullException(nameof(command));
                }

                this._registeredWhisperCommands[typeName].Add(command.ToLowerInvariant());

                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers whisper commands.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the commands.</param>
        /// <param name="commands">The commands to register.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="commands"/> is null or empty.</exception>
        public void RegisterWhisperCommands(string typeName, string[] commands)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (commands is null || commands.Length == 0)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            foreach (string command in commands)
            {
                _ = this.RegisterWhisperCommand(typeName, command);
            }
        }

        /// <summary>
        /// Attempts to subscribe the provided Delegate to the designated chat <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="ChatCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeBotnameChatCommand(string command, ChatCommandReceivedEventHandler handler)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._botnameChatCommandEventHandlers.TryAdd(command.ToLowerInvariant(), handler);
        }

        /// <summary>
        /// Attempts to subscribe the provided Delegate to the designated whisper <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="WhisperCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeBotnameWhisperCommand(string command, WhisperCommandReceivedEventHandler handler)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._botnameWhisperCommandEventHandlers.TryAdd(command.ToLowerInvariant(), handler);
        }

        /// <summary>
        /// Attempts to subscribe the provided Delegate to the designated chat <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="ChatCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeChatCommand(string command, ChatCommandReceivedEventHandler handler)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._chatCommandEventHandlers.TryAdd(command.ToLowerInvariant(), handler);
        }

        /// <summary>
        /// Attempts to subscribe the provided Delegate to the designated whisper <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="WhisperCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeWhisperCommand(string command, WhisperCommandReceivedEventHandler handler)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._whisperCommandEventHandlers.TryAdd(command.ToLowerInvariant(), handler);
        }

        /// <summary>
        /// Unregisters all <c>!botname</c> chat commands from a Type.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the commands.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
        public void UnregisterAllBotnameChatCommands(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            _ = this._registeredBotnameChatCommands.TryRemove(typeName, out _);
        }

        /// <summary>
        /// Unregisters all <c>!botname</c> whisper commands from a Type.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the commands.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
        public void UnregisterAllBotnameWhisperCommands(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            _ = this._registeredBotnameWhisperCommands.TryRemove(typeName, out _);
        }

        /// <summary>
        /// Unregisters all chat commands from a Type.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the commands.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
        public void UnregisterAllChatCommands(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            _ = this._registeredChatCommands.TryRemove(typeName, out _);
        }

        /// <summary>
        /// Unregisters all whisper commands from a Type.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the commands.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
        public void UnregisterAllWhisperCommands(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            _ = this._registeredWhisperCommands.TryRemove(typeName, out _);
        }

        /// <summary>
        /// Unregisters a <c>!botname</c> chat command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the command.</param>
        /// <param name="command">The command to unregister.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public void UnregisterBotnameChatCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (this._registeredBotnameChatCommands.ContainsKey(typeName))
            {
                _ = this._registeredBotnameChatCommands[typeName].Remove(command.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Unregisters a <c>!botname</c> whisper command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the command.</param>
        /// <param name="command">The command to unregister.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public void UnregisterBotnameWhisperCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (this._registeredBotnameWhisperCommands.ContainsKey(typeName))
            {
                _ = this._registeredBotnameWhisperCommands[typeName].Remove(command.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Unregisters a chat command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the command.</param>
        /// <param name="command">The command to unregister.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public void UnregisterChatCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (this._registeredChatCommands.ContainsKey(typeName))
            {
                _ = this._registeredChatCommands[typeName].Remove(command.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Unregisters a whisper command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the command.</param>
        /// <param name="command">The command to unregister.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public void UnregisterWhisperCommand(string typeName, string command)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (this._registeredWhisperCommands.ContainsKey(typeName))
            {
                _ = this._registeredWhisperCommands[typeName].Remove(command.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Attempts to unsubscribe the designated chat <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeBotnameChatCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._botnameChatCommandEventHandlers.TryRemove(command.ToLowerInvariant(), out _);
        }

        /// <summary>
        /// Attempts to unsubscribe the designated whisper <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeBotnameWhisperCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._botnameWhisperCommandEventHandlers.TryRemove(command.ToLowerInvariant(), out _);
        }

        /// <summary>
        /// Attempts to unsubscribe the designated chat <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeChatCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._chatCommandEventHandlers.TryRemove(command.ToLowerInvariant(), out _);
        }

        /// <summary>
        /// Attempts to unsubscribe the designated whisper <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeWhisperCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentNullException(nameof(command));
            }

            return this._whisperCommandEventHandlers.TryRemove(command.ToLowerInvariant(), out _);
        }

        /// <summary>
        /// Updates the subscribed chat <c>!botname command</c> names.
        /// </summary>
        /// <param name="commandMap">A Dictionary of commands to update. Key is the original command name; value is the new command name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="commandMap"/> is null.</exception>
        public void UpdateBotnameChatCommands(Dictionary<string, string> commandMap)
        {
            if (commandMap is null)
            {
                throw new ArgumentNullException(nameof(commandMap));
            }

            foreach (KeyValuePair<string, string> kvp in commandMap)
            {
                if (this._botnameChatCommandEventHandlers.TryRemove(kvp.Key.ToLowerInvariant(), out ChatCommandReceivedEventHandler handler))
                {
                    _ = this._botnameChatCommandEventHandlers.TryAdd(kvp.Value.ToLowerInvariant(), handler);
                }
            }
        }

        /// <summary>
        /// Updates the subscribed whisper <c>!botname command</c> names.
        /// </summary>
        /// <param name="commandMap">A Dictionary of commands to update. Key is the original command name; value is the new command name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="commandMap"/> is null.</exception>
        public void UpdateBotnameWhisperCommands(Dictionary<string, string> commandMap)
        {
            if (commandMap is null)
            {
                throw new ArgumentNullException(nameof(commandMap));
            }

            foreach (KeyValuePair<string, string> kvp in commandMap)
            {
                if (this._botnameWhisperCommandEventHandlers.TryRemove(kvp.Key.ToLowerInvariant(), out WhisperCommandReceivedEventHandler handler))
                {
                    _ = this._botnameWhisperCommandEventHandlers.TryAdd(kvp.Value.ToLowerInvariant(), handler);
                }
            }
        }

        /// <summary>
        /// Updates the subscribed chat <c>!command</c> names.
        /// </summary>
        /// <param name="commandMap">A Dictionary of commands to update. Key is the original command name; value is the new command name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="commandMap"/> is null.</exception>
        public void UpdateChatCommands(Dictionary<string, string> commandMap)
        {
            if (commandMap is null)
            {
                throw new ArgumentNullException(nameof(commandMap));
            }

            foreach (KeyValuePair<string, string> kvp in commandMap)
            {
                if (this._chatCommandEventHandlers.TryRemove(kvp.Key.ToLowerInvariant(), out ChatCommandReceivedEventHandler handler))
                {
                    _ = this._chatCommandEventHandlers.TryAdd(kvp.Value.ToLowerInvariant(), handler);
                }
            }
        }

        /// <summary>
        /// Updates the subscribed whisper <c>!command</c> names.
        /// </summary>
        /// <param name="commandMap">A Dictionary of commands to update. Key is the original command name; value is the new command name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="commandMap"/> is null.</exception>
        public void UpdateWhisperCommands(Dictionary<string, string> commandMap)
        {
            if (commandMap is null)
            {
                throw new ArgumentNullException(nameof(commandMap));
            }

            foreach (KeyValuePair<string, string> kvp in commandMap)
            {
                if (this._whisperCommandEventHandlers.TryRemove(kvp.Key.ToLowerInvariant(), out WhisperCommandReceivedEventHandler handler))
                {
                    _ = this._whisperCommandEventHandlers.TryAdd(kvp.Value.ToLowerInvariant(), handler);
                }
            }
        }

        #endregion Public Methods
    }
}
