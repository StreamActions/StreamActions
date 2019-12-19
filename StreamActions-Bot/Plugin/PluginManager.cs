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
using System.Reflection;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using StreamActions.Attributes;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Loads and manages the state of plugins, as well as running the plugin event handlers.
    /// </summary>
    public class PluginManager
    {
        #region Public Delegates

        /// <summary>
        /// Represents the method that will handle a <see cref="TwitchLib.Client.TwitchClient.OnChatCommandReceived"/> event.
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
        /// Provides a copy of the currently loaded plugins as WeakReferences.
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

        #endregion Public Properties

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
                    }
                    else if (attr is ChatCommandAttribute chatAttr)
                    {
                        _ = this._chatCommandEventHandlers.TryAdd(chatAttr.Command + (chatAttr.SubCommand ?? " " + chatAttr.SubCommand), (ChatCommandReceivedEventHandler)Delegate.CreateDelegate(typeof(ChatCommandReceivedEventHandler), mInfo));
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

            if (relativePath == null || relativePath.Length == 0)
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
            _ = this._plugins.TryRemove(fullName, out _);
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<PluginManager> _instance = new Lazy<PluginManager>(() => new PluginManager());

        /// <summary>
        /// Stores the enabled <see cref="ChatCommandReceivedEventHandler"/> delegates that handle <see cref="TwitchLib.Client.TwitchClient.OnChatCommandReceived"/> events for the <c>!botname</c> tree.
        /// </summary>
        private readonly ConcurrentDictionary<string, ChatCommandReceivedEventHandler> _botnameChatCommandEventHandlers = new ConcurrentDictionary<string, ChatCommandReceivedEventHandler>();

        /// <summary>
        /// Stores the enabled <see cref="ChatCommandReceivedEventHandler"/> delegates that handle <see cref="TwitchLib.Client.TwitchClient.OnChatCommandReceived"/> events.
        /// </summary>
        private readonly ConcurrentDictionary<string, ChatCommandReceivedEventHandler> _chatCommandEventHandlers = new ConcurrentDictionary<string, ChatCommandReceivedEventHandler>();

        /// <summary>
        /// ConcurrentDictionary of currently loaded plugins.
        /// </summary>
        private readonly ConcurrentDictionary<string, IPlugin> _plugins = new ConcurrentDictionary<string, IPlugin>();

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
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Passes <see cref="OnMessageReceivedArgs"/> on to subscribers of <see cref="OnMessageModeration"/> to determine if a moderation action should be taken.
        /// If the message passes moderation, passes it on to subscribers of <see cref="OnMessageReceived"/> and <see cref="ChatCommandReceivedEventHandler"/>, otherwise,
        /// performs the harshest indicated moderation action.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageReceivedArgs"/> object.</param>
        private void Twitch_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult harshestModeration = new ModerationResult();
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
        }

        #endregion Private Methods
    }
}