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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using StreamActions.Attributes;
using System.Threading.Tasks;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Loads and manages the state of plugins, as well as running the plugin event handlers.
    /// </summary>
    public class PluginManager
    {
        #region Public Delegates

        /// <summary>
        /// Represents the method that will handle a ChatCommandReceived event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received command.</param>
        public delegate void ChatCommandReceivedEventHandler(object sender, OnChatCommandReceivedArgs e);

        /// <summary>
        /// Represents the method that will handle a <see cref="OnMessageModeration"/> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received message.</param>
        /// <returns>A <see cref="ModerationResult"/> representing any moderation action to take on the message.</returns>
        public delegate ModerationResult MessageModerationEventHandler(object sender, OnMessageReceivedArgs e);

        /// <summary>
        /// Represents the method that will handle a WhisperCommandReceived event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received command.</param>
        public delegate void WhisperCommandReceivedEventHandler(object sender, OnWhisperCommandReceivedArgs e);

        #endregion Public Delegates

        #region Public Events

        /// <summary>
        /// Fires when a new chat message arrives and is ready to be processed for moderation, returns <see cref="ChatMessage"/>.
        /// </summary>
        public event MessageModerationEventHandler OnMessageModeration;

        /// <summary>
        /// Fires when a new chat message arrives and has passed moderation, returns <see cref="ChatMessage"/>.
        /// </summary>
        public event EventHandler<OnMessageReceivedArgs> OnMessageReceived;

        /// <summary>
        /// Fires when a new whisper arrives, returns <see cref="WhisperMessage"/>.
        /// </summary>
        public event EventHandler<OnWhisperReceivedArgs> OnWhisperReceived;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Singleton of <see cref="PluginManager"/>.
        /// </summary>
        public static PluginManager Instance => _instance.Value;

        /// <summary>
        /// The character that is used as the prefix to identify a chat command.
        /// </summary>
        public char ChatCommandIdentifier { get; set; } = '!';

        /// <summary>
        /// Indicates if the bot is in lock down mode. When this is set, only plugins with <see cref="OnMessageModeration"/> EventHandlers or the <see cref="IPlugin.AlwaysEnabled"/>
        /// property will be enabled.
        /// </summary>
        public bool InLockdown => this._inLockdown;

        /// <summary>
        /// Provides a copy of the currently loaded plugins as WeakReferences, by FullName.
        /// </summary>
        public Dictionary<string, WeakReference<IPlugin>> Plugins
        {
            get
            {
                Dictionary<string, WeakReference<IPlugin>> value = new Dictionary<string, WeakReference<IPlugin>>();

                foreach (KeyValuePair<string, IPlugin> kvp in this._plugins)
                {
                    value.Add(kvp.Key, new WeakReference<IPlugin>(kvp.Value));
                }

                return value;
            }
        }

        /// <summary>
        /// A list of all currently registered <c>!botname</c> chat commands.
        /// </summary>
        public IEnumerable<string> RegisteredBotnameChatCommands => this._registeredBotnameChatCommands.SelectMany(l => l.Value.Select(x => x));

        /// <summary>
        /// A list of all currently registered chat commands.
        /// </summary>
        public IEnumerable<string> RegisteredChatCommands => this._registeredChatCommands.SelectMany(l => l.Value.Select(x => x));

        /// <summary>
        /// The character that is used as the prefix to identify a whisper command.
        /// </summary>
        public char WhisperCommandIdentifier { get; set; } = '!';

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Indicates if the <c>!botname</c> tree already has the provided command registered.
        /// </summary>
        /// <param name="command">The command name to test.</param>
        /// <returns><c>true</c> if that command already exists under the <c>!botname</c> tree.</returns>
        public bool DoesBotnameCommandExist(string command) => this._registeredBotnameChatCommands.Any(l => l.Value.Any<string>(c => string.Equals(c, command, StringComparison.InvariantCultureIgnoreCase)));

        /// <summary>
        /// Indicates if the provided command is already registered.
        /// </summary>
        /// <param name="command">The command name to test.</param>
        /// <returns><c>true</c> if that command already exists.</returns>
        public bool DoesCommandExist(string command) => this._registeredChatCommands.Any(l => l.Value.Any<string>(c => string.Equals(c, command, StringComparison.InvariantCultureIgnoreCase)));

        /// <summary>
        /// Registers a <c>!botname</c> chat command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the command.</param>
        /// <param name="command">The command to register.</param>
        /// <returns><c>true</c> on success; <c>false</c> if command is already registered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public bool RegisterBotnameChatCommand(string typeName, string command)
        {
            if (typeName is null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (!this.DoesBotnameCommandExist(command))
            {
                if (!this._registeredBotnameChatCommands.ContainsKey(typeName))
                {
                    _ = this._registeredBotnameChatCommands.TryAdd(typeName, new List<string>());
                }

                this._registeredBotnameChatCommands[typeName].Add(command);

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
            if (typeName is null)
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
        /// Registers a chat command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is registering the command.</param>
        /// <param name="command">The command to register.</param>
        /// <returns><c>true</c> on success; <c>false</c> if command is already registered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public bool RegisterChatCommand(string typeName, string command)
        {
            if (typeName is null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (!this.DoesCommandExist(command))
            {
                if (!this._registeredChatCommands.ContainsKey(typeName))
                {
                    _ = this._registeredChatCommands.TryAdd(typeName, new List<string>());
                }

                this._registeredChatCommands[typeName].Add(command);

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
            if (typeName is null)
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
        /// Attempts to subscribe the provided Delegate to the designated chat <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="ChatCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeBotnameChatCommand(string command, ChatCommandReceivedEventHandler handler) => this._botnameChatCommandEventHandlers.TryAdd(command, handler);

        /// <summary>
        /// Attempts to subscribe the provided Delegate to the designated whisper <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="WhisperCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeBotnameWhisperCommand(string command, WhisperCommandReceivedEventHandler handler) => this._botnameWhisperCommandEventHandlers.TryAdd(command, handler);

        /// <summary>
        /// Attempts to subscribe the provided Delegate to the designated chat <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="ChatCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeChatCommand(string command, ChatCommandReceivedEventHandler handler) => this._chatCommandEventHandlers.TryAdd(command, handler);

        /// <summary>
        /// Attempts to subscribe the provided Delegate to the designated whisper <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to subscribe to, without the <c>!</c>.</param>
        /// <param name="handler">The <see cref="WhisperCommandReceivedEventHandler"/> to subscribe.</param>
        /// <returns><c>true</c> if the command was subscribed successfully; <c>false</c> if the command already exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool SubscribeWhisperCommand(string command, WhisperCommandReceivedEventHandler handler) => this._whisperCommandEventHandlers.TryAdd(command, handler);

        /// <summary>
        /// Unregisters all <c>!botname</c> chat commands from a Type.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the commands.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
        public void UnregisterAllBotnameChatCommands(string typeName)
        {
            if (typeName is null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            _ = this._registeredBotnameChatCommands.TryRemove(typeName, out _);
        }

        /// <summary>
        /// Unregisters all chat commands from a Type.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the commands.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null.</exception>
        public void UnregisterAllChatCommands(string typeName)
        {
            if (typeName is null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            _ = this._registeredChatCommands.TryRemove(typeName, out _);
        }

        /// <summary>
        /// Unregisters a <c>!botname</c> chat command.
        /// </summary>
        /// <param name="typeName">The Type.FullName that is unregistering the command.</param>
        /// <param name="command">The command to unregister.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeName"/> is null; <paramref name="command"/> is null or empty.</exception>
        public void UnregisterBotnameChatCommand(string typeName, string command)
        {
            if (typeName is null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (this._registeredBotnameChatCommands.ContainsKey(typeName))
            {
                _ = this._registeredBotnameChatCommands[typeName].Remove(command);
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
            if (typeName is null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (command is null || command.Length == 0)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (this._registeredChatCommands.ContainsKey(typeName))
            {
                _ = this._registeredChatCommands[typeName].Remove(command);
            }
        }

        /// <summary>
        /// Attempts to unsubscribe the designated chat <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeBotnameChatCommand(string command) => this._botnameChatCommandEventHandlers.TryRemove(command, out _);

        /// <summary>
        /// Attempts to unsubscribe the designated whisper <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeBotnameWhisperCommand(string command) => this._botnameWhisperCommandEventHandlers.TryRemove(command, out _);

        /// <summary>
        /// Attempts to unsubscribe the designated chat <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeChatCommand(string command) => this._chatCommandEventHandlers.TryRemove(command, out _);

        /// <summary>
        /// Attempts to unsubscribe the designated whisper <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to unsubscribe from, without the <c>!</c>.</param>
        /// <returns><c>true</c> if the command was unsubscribed successfully; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <c>null</c>.
        public bool UnsubscribeWhisperCommand(string command) => this._whisperCommandEventHandlers.TryRemove(command, out _);

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
                if (this._botnameChatCommandEventHandlers.TryRemove(kvp.Key, out ChatCommandReceivedEventHandler handler))
                {
                    _ = this._botnameChatCommandEventHandlers.TryAdd(kvp.Value, handler);
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
                if (this._botnameWhisperCommandEventHandlers.TryRemove(kvp.Key, out WhisperCommandReceivedEventHandler handler))
                {
                    _ = this._botnameWhisperCommandEventHandlers.TryAdd(kvp.Value, handler);
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
                if (this._chatCommandEventHandlers.TryRemove(kvp.Key, out ChatCommandReceivedEventHandler handler))
                {
                    _ = this._chatCommandEventHandlers.TryAdd(kvp.Value, handler);
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
                if (this._whisperCommandEventHandlers.TryRemove(kvp.Key, out WhisperCommandReceivedEventHandler handler))
                {
                    _ = this._whisperCommandEventHandlers.TryAdd(kvp.Value, handler);
                }
            }
        }

        #endregion Public Methods

        #region Internal Methods

        /// <summary>
        /// Calls <see cref="IPlugin.Disabled"/> and unregisters all <see cref="ChatCommandReceivedEventHandler"/> for the type.
        /// </summary>
        /// <param name="typeName">The full name of the plugin to disable, as would be provided by Type.FullName.</param>
        internal void DisablePlugin(string typeName)
        {
            this._plugins[typeName].Disabled();

            foreach (MethodInfo mInfo in this._plugins[typeName].GetType().GetMethods())
            {
                foreach (ChatCommandAttribute attr in Attribute.GetCustomAttributes(mInfo, typeof(ChatCommandAttribute)))
                {
                    if (attr is BotnameChatCommandAttribute bchatAttr)
                    {
                        _ = this._botnameChatCommandEventHandlers.TryRemove(bchatAttr.Command + (bchatAttr.SubCommand ?? " " + bchatAttr.SubCommand), out _);
                    }
                    else if (attr is ChatCommandAttribute chatAttr)
                    {
                        _ = this._chatCommandEventHandlers.TryRemove(chatAttr.Command + (chatAttr.SubCommand ?? " " + chatAttr.SubCommand), out _);
                    }
                }
            }
        }

        /// <summary>
        /// Calls <see cref="IPlugin.Enabled"/> and registers all <see cref="ChatCommandReceivedEventHandler"/> for the type.
        /// </summary>
        /// <param name="typeName">The full name of the plugin to enable, as would be provided by Type.FullName.</param>
        internal void EnablePlugin(string typeName)
        {
            //TODO: check if plugin is currently enabled in user settings
            this._plugins[typeName].Enabled();

            foreach (MethodInfo mInfo in this._plugins[typeName].GetType().GetMethods())
            {
                foreach (ChatCommandAttribute attr in Attribute.GetCustomAttributes(mInfo, typeof(ChatCommandAttribute)))
                {
                    if (attr is BotnameChatCommandAttribute bchatAttr)
                    {
                        _ = this._botnameChatCommandEventHandlers.TryAdd(bchatAttr.Command + (bchatAttr.SubCommand ?? " " + bchatAttr.SubCommand), (ChatCommandReceivedEventHandler)Delegate.CreateDelegate(typeof(ChatCommandReceivedEventHandler), mInfo));
                        _ = this.RegisterBotnameChatCommand(typeName, bchatAttr.Command);
                    }
                    else if (attr is ChatCommandAttribute chatAttr)
                    {
                        _ = this._chatCommandEventHandlers.TryAdd(chatAttr.Command + (chatAttr.SubCommand ?? " " + chatAttr.SubCommand), (ChatCommandReceivedEventHandler)Delegate.CreateDelegate(typeof(ChatCommandReceivedEventHandler), mInfo));
                        _ = this.RegisterChatCommand(typeName, chatAttr.Command);
                    }
                }
            }
        }

        /// <summary>
        /// Switches the bot out of lock down mode. Plugins are re-enabled if user settings permit.
        /// </summary>
        internal void EndLockdown()
        {
            this._inLockdown = false;

            foreach (KeyValuePair<string, IPlugin> kvp in this._plugins)
            {
                this.EnablePlugin(kvp.Key);
            }
        }

        /// <summary>
        /// Switches the bot into lock down mode. All plugins that do not have a <see cref="OnMessageModeration"/> EventHandler or are <see cref="IPlugin.AlwaysEnabled"/> will be disabled.
        /// </summary>
        internal void InitiateLockdown()
        {
            this._inLockdown = true;

            List<string> lockdownPlugins = new List<string>();

            if (OnMessageModeration != null)
            {
                foreach (Delegate d in OnMessageModeration.GetInvocationList())
                {
                    lockdownPlugins.Add(d.Method.DeclaringType.FullName);
                }
            }

            foreach (KeyValuePair<string, IPlugin> kvp in this._plugins)
            {
                if (!lockdownPlugins.Contains(kvp.Key))
                {
                    this.DisablePlugin(kvp.Key);
                }
            }
        }

        /// <summary>
        /// Loads all plugins from the specified assembly.
        /// </summary>
        /// <param name="relativePath">The path to the assembly file, relative to the plugins directory. Set to null or empty string to load from the current assembly.
        /// Paths must always use <c>\</c> as the directory separator character unless they come from a member that has already detected the OS directory separator character.</param>
        /// <param name="isReloading">Indicates if the assembly is being reloaded, and should therefore disable existing instances first for GC.</param>
        internal void LoadPlugins(string relativePath, bool isReloading = false)
        {
            Assembly assembly;

            if (relativePath is null || relativePath.Length == 0)
            {
                assembly = Assembly.GetExecutingAssembly();
            }
            else
            {
                string pluginLocation = Path.GetFullPath(Path.Combine(typeof(Program).Assembly.Location, "plugins", relativePath.Replace('\\', Path.DirectorySeparatorChar)));
                PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
                assembly = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
            }

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                {
                    if (Activator.CreateInstance(type) is IPlugin result)
                    {
                        if (isReloading)
                        {
                            this.UnloadPlugin(type.FullName);
                        }

                        _ = this._plugins.TryAdd(type.FullName, result);
                        this.EnablePlugin(type.FullName);
                    }
                }
            }
        }

        /// <summary>
        /// Disables and removes the reference to a plugin, enabling GC to occur.
        /// </summary>
        /// <param name="fullName">The full name of the plugin to disable, as would be provided by Type.FullName.</param>
        internal void UnloadPlugin(string fullName)
        {
            this.DisablePlugin(fullName);
            this.UnregisterAllChatCommands(fullName);
            this.UnregisterAllBotnameChatCommands(fullName);
            _ = this._plugins.TryRemove(fullName, out _);
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<PluginManager> _instance = new Lazy<PluginManager>(() => new PluginManager());

        /// <summary>
        /// Stores the enabled <see cref="ChatCommandReceivedEventHandler"/> delegates that handle ChatCommandReceived events for the <c>!botname</c> tree.
        /// </summary>
        private readonly ConcurrentDictionary<string, ChatCommandReceivedEventHandler> _botnameChatCommandEventHandlers = new ConcurrentDictionary<string, ChatCommandReceivedEventHandler>();

        /// <summary>
        /// Stores the enabled <see cref="WhisperCommandReceivedEventHandler"/> delegates that handle WhisperCommandReceived events for the <c>!botname</c> tree.
        /// </summary>
        private readonly ConcurrentDictionary<string, WhisperCommandReceivedEventHandler> _botnameWhisperCommandEventHandlers = new ConcurrentDictionary<string, WhisperCommandReceivedEventHandler>();

        /// <summary>
        /// Stores the enabled <see cref="ChatCommandReceivedEventHandler"/> delegates that handle ChatCommandReceived events.
        /// </summary>
        private readonly ConcurrentDictionary<string, ChatCommandReceivedEventHandler> _chatCommandEventHandlers = new ConcurrentDictionary<string, ChatCommandReceivedEventHandler>();

        /// <summary>
        /// ConcurrentDictionary of currently loaded plugins.
        /// </summary>
        private readonly ConcurrentDictionary<string, IPlugin> _plugins = new ConcurrentDictionary<string, IPlugin>();

        /// <summary>
        /// ConcurrentDictionary of registered <c>!botname</c> chat commands.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _registeredBotnameChatCommands = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// ConcurrentDictionary of registered chat commands.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _registeredChatCommands = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// Stores the enabled <see cref="WhisperCommandReceivedEventHandler"/> delegates that handle WhisperCommandReceived events.
        /// </summary>
        private readonly ConcurrentDictionary<string, WhisperCommandReceivedEventHandler> _whisperCommandEventHandlers = new ConcurrentDictionary<string, WhisperCommandReceivedEventHandler>();

        /// <summary>
        /// Field that backs the <see cref="InLockdown"/> property.
        /// </summary>
        private bool _inLockdown = false;

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        private PluginManager()
        {
            //TODO: Check the settings file for an alternate ChatCommandIdentifier
            TwitchLibClient.Instance.TwitchClient.OnMessageReceived += this.Twitch_OnMessageReceived;
            TwitchLibClient.Instance.TwitchClient.OnWhisperReceived += this.Twitch_OnWhisperReceived;
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Passes <see cref="OnMessageReceivedArgs"/> on to subscribers of <see cref="OnMessageModeration"/> to determine if a moderation action should be taken.
        /// If the message passes moderation, passes it on to subscribers of <see cref="OnMessageReceived"/> and <see cref="ChatCommandReceivedEventHandler"/> handlers, otherwise,
        /// performs the harshest indicated moderation action.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        private async void Twitch_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult harshestModeration = new ModerationResult();

            await Task.Run(() =>
             {
                 int numModeration = 0;
                 if (OnMessageModeration != null)
                 {
                     foreach (MessageModerationEventHandler d in OnMessageModeration.GetInvocationList())
                     {
                         ModerationResult rs = (ModerationResult)d.Invoke(this, e);
                         if (harshestModeration.IsHarsher(rs))
                         {
                             harshestModeration = rs;
                         }
                     }

                     numModeration = OnMessageModeration.GetInvocationList().Length;
                 }
             }).ConfigureAwait(false);

            await Task.Run(() =>
            {
                if (harshestModeration.ShouldModerate)
                {
                    //TODO: Take the moderation action and send the ModerationMessage to chat if not InLockdown
                }
                else
                {
                    OnMessageReceived?.Invoke(this, e);

                    if (Equals(e.ChatMessage.Message[0], this.ChatCommandIdentifier))
                    {
                        ChatCommand chatCommand = new ChatCommand(e.ChatMessage);
                        ChatCommandReceivedEventHandler eventHandler;

                        //TODO: Replace string "botname" with the botname var
                        if (string.Equals(chatCommand.CommandText, "botname", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (this._botnameChatCommandEventHandlers.TryGetValue(chatCommand.ArgumentsAsList[0] + " " + chatCommand.ArgumentsAsList[1], out eventHandler))
                            {
                                eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                            }
                            else if (this._botnameChatCommandEventHandlers.TryGetValue(chatCommand.ArgumentsAsList[0], out eventHandler))
                            {
                                eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                            }
                        }
                        else
                        {
                            if (this._chatCommandEventHandlers.TryGetValue(chatCommand.CommandText + " " + chatCommand.ArgumentsAsList[0], out eventHandler))
                            {
                                eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                            }
                            else if (this._chatCommandEventHandlers.TryGetValue(chatCommand.CommandText, out eventHandler))
                            {
                                eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                            }
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Passes <see cref="OnWhisperReceivedArgs"/> on to subscribers of <see cref="OnWhisperReceived"/> and <see cref="WhisperCommandReceivedEventHandler"/> handlers.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnWhisperReceivedArgs"/> object.</param>
        private async void Twitch_OnWhisperReceived(object sender, OnWhisperReceivedArgs e) => await Task.Run(() =>
                                                                                             {
                                                                                                 OnWhisperReceived?.Invoke(this, e);

                                                                                                 if (Equals(e.WhisperMessage.Message[0], this.WhisperCommandIdentifier))
                                                                                                 {
                                                                                                     WhisperCommand whisperCommand = new WhisperCommand(e.WhisperMessage);
                                                                                                     WhisperCommandReceivedEventHandler eventHandler;

                                                                                                     //TODO: Replace string "botname" with the botname var
                                                                                                     if (string.Equals(whisperCommand.CommandText, "botname", StringComparison.InvariantCultureIgnoreCase))
                                                                                                     {
                                                                                                         if (this._botnameWhisperCommandEventHandlers.TryGetValue(whisperCommand.ArgumentsAsList[0] + " " + whisperCommand.ArgumentsAsList[1], out eventHandler))
                                                                                                         {
                                                                                                             eventHandler.Invoke(this, new OnWhisperCommandReceivedArgs { Command = whisperCommand });
                                                                                                         }
                                                                                                         else if (this._botnameWhisperCommandEventHandlers.TryGetValue(whisperCommand.ArgumentsAsList[0], out eventHandler))
                                                                                                         {
                                                                                                             eventHandler.Invoke(this, new OnWhisperCommandReceivedArgs { Command = whisperCommand });
                                                                                                         }
                                                                                                     }
                                                                                                     else
                                                                                                     {
                                                                                                         if (this._whisperCommandEventHandlers.TryGetValue(whisperCommand.CommandText + " " + whisperCommand.ArgumentsAsList[0], out eventHandler))
                                                                                                         {
                                                                                                             eventHandler.Invoke(this, new OnWhisperCommandReceivedArgs { Command = whisperCommand });
                                                                                                         }
                                                                                                         else if (this._whisperCommandEventHandlers.TryGetValue(whisperCommand.CommandText, out eventHandler))
                                                                                                         {
                                                                                                             eventHandler.Invoke(this, new OnWhisperCommandReceivedArgs { Command = whisperCommand });
                                                                                                         }
                                                                                                     }
                                                                                                 }
                                                                                             }).ConfigureAwait(false);

        #endregion Private Methods
    }
}