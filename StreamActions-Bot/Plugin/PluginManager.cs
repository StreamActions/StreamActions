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
using System.Timers;
using TwitchLib.Client.Events;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Loads and manages the state of plugins, as well as running the plugin event handlers.
    /// </summary>
    public class PluginManager
    {
        #region Public Delegates

        /// <summary>
        /// Represents the method that will handle a <see cref="PluginManager.OnMessageModeration"/> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received message.</param>
        /// <returns>A <see cref="ModerationResult"/> representing any moderation action to take on the message.</returns>
        public delegate ModerationResult MessageModerationEventHandler(object sender, OnMessageReceivedArgs e);

        #endregion Public Delegates

        #region Public Events

        /// <summary>
        /// Fires when a command that passed moderation is received, returns <see cref="TwitchLib.Client.Models.ChatCommand"/>.
        /// </summary>
        public event EventHandler<OnChatCommandReceivedArgs> OnChatCommandReceived;

        /// <summary>
        /// Fires when a new chat message arrives and is ready to be processed for moderation, returns <see cref="TwitchLib.Client.Models.ChatMessage"/>.
        /// </summary>
        public event MessageModerationEventHandler OnMessageModeration;

        /// <summary>
        /// Fires when a new chat message arrives and has passed moderation, returns <see cref="TwitchLib.Client.Models.ChatMessage"/>.
        /// </summary>
        public event EventHandler<OnMessageReceivedArgs> OnMessageReceived;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Singleton of <see cref="PluginManager"/>.
        /// </summary>
        public static PluginManager Instance => _instance.Value;

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
        /// Switches the bot out of lock down mode. Plugins are re-enabled if user settings permit.
        /// </summary>
        internal void EndLockdown()
        {
            this._inLockdown = false;

            foreach (KeyValuePair<string, IPlugin> kvp in this._plugins)
            {
                //TODO: Call IPlugin.Enabled() if plugin is currently enabled in user settings
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
                    kvp.Value.Disabled();
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
            System.Reflection.Assembly assembly;

            if (relativePath == null || relativePath.Length == 0)
            {
                assembly = System.Reflection.Assembly.GetExecutingAssembly();
            }
            else
            {
                string pluginLocation = Path.GetFullPath(Path.Combine(typeof(Program).Assembly.Location, "plugins", relativePath.Replace('\\', Path.DirectorySeparatorChar)));
                PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
                assembly = loadContext.LoadFromAssemblyName(new System.Reflection.AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
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
                        //TODO: Call IPlugin.Enabled() if plugin is currently enabled in user settings
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
            if (this._plugins.TryRemove(fullName, out IPlugin plugin))
            {
                plugin.Disabled();
            }
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Number of milliseconds to add to the timer that removes entries from <see cref="_moderatedMessageIds"/>, per EventHandler hooked to <see cref="OnMessageModeration"/>.
        /// </summary>
        private const int _moderationTimerMultiplier = 250;

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<PluginManager> _instance = new Lazy<PluginManager>(() => new PluginManager());

        /// <summary>
        /// Stores the <see cref="TwitchLib.Client.Models.ChatMessage.Id"/> of chat messages that had moderation actions taken, so that <see cref="OnChatCommandReceived"/> knows to drop them.
        /// </summary>
        private readonly ICollection<string> _moderatedMessageIds = new HashSet<string>();

        /// <summary>
        /// Write lock object for <see cref="_moderatedMessageIds"/>.
        /// </summary>
        private readonly object _moderatedMessageIdsLock = new object();

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

        private PluginManager()
        {
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Passes <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> on to subscribers of <see cref="OnChatCommandReceived"/> if the underlying message has not been marked for moderation.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnChatCommandReceivedArgs"/> object.</param>
        private void Twitch_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            if (this._moderatedMessageIds.Contains(e.Command.ChatMessage.Id))
            {
                return;
            }

            OnChatCommandReceived?.Invoke(this, e);
        }

        /// <summary>
        /// Passes <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> on to subscribers of <see cref="OnMessageModeration"/> to determine if a moderation action should be taken.
        /// If the message passes moderation, passes it on to subscribers of <see cref="OnMessageReceived"/>, otherwise, performs the harshest indicated moderation action and marks it to
        /// be ignored by <see cref="OnChatCommandReceived"/>.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="TwitchLib.Client.Events.OnMessageReceivedArgs"/> object.</param>
        private void Twitch_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            ModerationResult harshestModeration = new ModerationResult();
            int numModeration = 0;
            if (OnMessageModeration != null)
            {
                foreach (Delegate d in OnMessageModeration.GetInvocationList())
                {
                    ModerationResult rs = (ModerationResult)d.DynamicInvoke(this, e);
                    if (harshestModeration.IsHarsher(rs))
                    {
                        harshestModeration = rs;
                    }
                }

                numModeration = OnMessageModeration.GetInvocationList().Length;
            }

            if (harshestModeration.ShouldModerate)
            {
                lock (this._moderatedMessageIdsLock)
                {
                    this._moderatedMessageIds.Add(e.ChatMessage.Id);
                }

                using Timer timer = new Timer();
                timer.Elapsed += (sender, te) =>
                {
                    lock (this._moderatedMessageIdsLock)
                    {
                        _ = this._moderatedMessageIds.Remove(e.ChatMessage.Id);
                    }
                };
                timer.AutoReset = false;
                timer.Interval = numModeration * _moderationTimerMultiplier;
                timer.Enabled = true;

                //TODO: Take the moderation action and send the ModerationMessage to chat if not InLockdown
            }
            else
            {
                OnMessageReceived?.Invoke(this, e);
            }
        }

        #endregion Private Methods
    }
}