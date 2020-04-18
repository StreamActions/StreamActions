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
using StreamActions.Database.Documents;
using MongoDB.Driver;
using StreamActions.Enums;
using StreamActions.Database;
using StreamActions.Plugins;
using StreamActions.Database.Documents.Commands;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Loads and manages the state of plugins, as well as running the plugin event handlers.
    /// </summary>
    public partial class PluginManager
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
        public delegate Task<ModerationResult> MessageModerationEventHandler(object sender, OnMessageReceivedArgs e);

        /// <summary>
        /// Represents the method that will handle a <see cref="OnMessagePreModeration"/> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received message.</param>
        /// <returns>Nothing.</returns>
        public delegate Task MessagePreModerationEventHandler(object sender, OnMessageReceivedArgs e);

        /// <summary>
        /// Represents the method that will handle a WhisperCommandReceived event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received command.</param>
        public delegate void WhisperCommandReceivedEventHandler(object sender, OnWhisperCommandReceivedArgs e);

        #endregion Public Delegates

        #region Public Events

        /// <summary>
        /// Fires when a command has been detected that is not already registered, returns <see cref="ChatCommand"/>. This should be used sparingly.
        /// </summary>
        public event ChatCommandReceivedEventHandler OnChatCommandReceived;

        /// <summary>
        /// Fires when a new chat message arrives and is ready to be processed for moderation, returns <see cref="ChatMessage"/>.
        /// </summary>
        public event MessageModerationEventHandler OnMessageModeration;

        /// <summary>
        /// Fires when a new chat message arrives, just before moderation, returns <see cref="ChatMessage"/>.
        /// </summary>
        public event MessagePreModerationEventHandler OnMessagePreModeration;

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
        /// The default command identifier.
        /// </summary>
        public static readonly char DefaultCommandIdentifier = '!';

        /// <summary>
        /// Singleton of <see cref="PluginManager"/>.
        /// </summary>
        public static PluginManager Instance => _instance.Value;

        /// <summary>
        /// The character that is used as the prefix to identify a chat command.
        /// </summary>
        public char ChatCommandIdentifier { get; set; } = DefaultCommandIdentifier;

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
        public char WhisperCommandIdentifier { get; set; } = DefaultCommandIdentifier;

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Loads the <see cref="CommandDocument"/> associated with the specified command, or creates one if it doesn't exist.
        /// </summary>
        /// <param name="channelId">The channel Id the command belongs to.</param>
        /// <param name="command">The command to lookup.</param>
        /// <returns>A <see cref="CommandDocument"/>.</returns>
        public async Task<CommandDocument> LoadCommandAsync(string channelId, string command)
        {
            IMongoCollection<CommandDocument> commands = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");

            FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.ChannelId == channelId && c.Command == command);

            if (await commands.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
            {
                await commands.InsertOneAsync(new CommandDocument
                {
                    ChannelId = channelId,
                    Command = command,
                    Cooldowns = new CommandCooldownDocument(),
                    UserLevel = this._defaultCommandPermissions[command]
                }).ConfigureAwait(false);
            }

            using IAsyncCursor<CommandDocument> cursor = await commands.FindAsync(filter).ConfigureAwait(false);
            CommandDocument commandDocument = await cursor.SingleAsync().ConfigureAwait(false);
            commandDocument.Cooldowns.Id = commandDocument.Id;

            return commandDocument;
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
                foreach (CommandAttribute attr in Attribute.GetCustomAttributes(mInfo, typeof(CommandAttribute)))
                {
                    if (attr is BotnameChatCommandAttribute bchatAttr)
                    {
                        _ = this._botnameChatCommandEventHandlers.TryRemove((bchatAttr.Command + (bchatAttr.SubCommand ?? " " + bchatAttr.SubCommand)).ToLowerInvariant(), out _);
                    }
                    else if (attr is ChatCommandAttribute chatAttr)
                    {
                        _ = this._chatCommandEventHandlers.TryRemove((chatAttr.Command + (chatAttr.SubCommand ?? " " + chatAttr.SubCommand)).ToLowerInvariant(), out _);
                    }
                    else if (attr is BotnameWhisperCommandAttribute bwhisperAttr)
                    {
                        _ = this._botnameWhisperCommandEventHandlers.TryRemove((bwhisperAttr.Command + (bwhisperAttr.SubCommand ?? " " + bwhisperAttr.SubCommand)).ToLowerInvariant(), out _);
                    }
                    else if (attr is WhisperCommandAttribute whisperAttr)
                    {
                        _ = this._whisperCommandEventHandlers.TryRemove((whisperAttr.Command + (whisperAttr.SubCommand ?? " " + whisperAttr.SubCommand)).ToLowerInvariant(), out _);
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

            foreach (MethodInfo mInfo in this._plugins[typeName].GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (CommandAttribute attr in Attribute.GetCustomAttributes(mInfo, typeof(CommandAttribute)))
                {
                    if (attr is BotnameChatCommandAttribute bchatAttr)
                    {
                        _ = this._botnameChatCommandEventHandlers.TryAdd((bchatAttr.Command + (bchatAttr.SubCommand ?? " " + bchatAttr.SubCommand)).ToLowerInvariant(), (ChatCommandReceivedEventHandler)Delegate.CreateDelegate(typeof(ChatCommandReceivedEventHandler), mInfo));
                        _ = this._defaultCommandPermissions.TryAdd((Program.Settings.BotLogin + " " + bchatAttr.Command + (bchatAttr.SubCommand ?? " " + bchatAttr.SubCommand)).ToLowerInvariant(), bchatAttr.Permission);
                        _ = this.RegisterBotnameChatCommand(typeName, bchatAttr.Command.ToLowerInvariant());
                    }
                    else if (attr is ChatCommandAttribute chatAttr)
                    {
                        _ = this._chatCommandEventHandlers.TryAdd((chatAttr.Command + (chatAttr.SubCommand ?? " " + chatAttr.SubCommand)).ToLowerInvariant(), (ChatCommandReceivedEventHandler)Delegate.CreateDelegate(typeof(ChatCommandReceivedEventHandler), mInfo));
                        _ = this._defaultCommandPermissions.TryAdd((chatAttr.Command + (chatAttr.SubCommand ?? " " + chatAttr.SubCommand)).ToLowerInvariant(), chatAttr.Permission);
                        _ = this.RegisterChatCommand(typeName, chatAttr.Command.ToLowerInvariant());
                    }
                    else if (attr is BotnameWhisperCommandAttribute bwhisperAttr)
                    {
                        _ = this._botnameWhisperCommandEventHandlers.TryAdd((bwhisperAttr.Command + (bwhisperAttr.SubCommand ?? " " + bwhisperAttr.SubCommand)).ToLowerInvariant(), (WhisperCommandReceivedEventHandler)Delegate.CreateDelegate(typeof(WhisperCommandReceivedEventHandler), mInfo));
                        _ = this.RegisterBotnameWhisperCommand(typeName, bwhisperAttr.Command.ToLowerInvariant());
                    }
                    else if (attr is WhisperCommandAttribute whisperAttr)
                    {
                        _ = this._whisperCommandEventHandlers.TryAdd((whisperAttr.Command + (whisperAttr.SubCommand ?? " " + whisperAttr.SubCommand)).ToLowerInvariant(), (WhisperCommandReceivedEventHandler)Delegate.CreateDelegate(typeof(WhisperCommandReceivedEventHandler), mInfo));
                        _ = this.RegisterWhisperCommand(typeName, whisperAttr.Command.ToLowerInvariant());
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

            if (!(OnMessageModeration is null))
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
                else if (typeof(IDocument).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                {
                    if (Activator.CreateInstance(type) is IDocument result)
                    {
                        result.Initialize();
                    }
                }
            }
        }

        /// <summary>
        /// Method called when to perform a moderation action on a user.
        /// </summary>
        /// <param name="result">The result of the moderation.</param>
        /// <param name="document">The channel's moderation document.</param>
        /// <param name="message">The chat message.</param>
        internal void PerformModerationAction(ModerationResult result, ModerationDocument document, ChatMessage message)
        {
            switch (result.Punishment)
            {
                case ModerationPunishment.Ban:
                    TwitchLibClient.Instance.SendMessage(message.RoomId, ".ban " + message.Username + (!(result.ModerationReason is null) ? " " + result.ModerationReason : ""));
                    break;

                case ModerationPunishment.Delete:
                    TwitchLibClient.Instance.SendMessage(message.RoomId, ".delete " + message.Id);
                    break;

                case ModerationPunishment.Timeout:
                    TwitchLibClient.Instance.SendMessage(message.RoomId, ".timeout " + message.Username + " " + result.TimeoutSeconds + (!(result.ModerationReason is null) ? " " + result.ModerationReason : ""));
                    break;

                case ModerationPunishment.Purge:
                    TwitchLibClient.Instance.SendMessage(message.RoomId, ".timeout " + message.Username + " 1" + (!(result.ModerationReason is null) ? " " + result.ModerationReason : ""));
                    break;
            }

            if (!(result.ModerationMessage is null) && !this.InLockdown && document.LastModerationMessageSent.AddSeconds(document.ModerationMessageCooldownSeconds) < DateTime.Now)
            {
                TwitchLibClient.Instance.SendMessage(message.RoomId, result.ModerationMessage);
            }

            // TODO: Update the user's last timeout time.
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
            this.UnregisterAllWhisperCommands(fullName);
            this.UnregisterAllBotnameWhisperCommands(fullName);
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
        /// Stores the provided default permissions of pre-registered commands.
        /// </summary>
        private readonly ConcurrentDictionary<string, UserLevels> _defaultCommandPermissions = new ConcurrentDictionary<string, UserLevels>();

        /// <summary>
        /// ConcurrentDictionary of currently loaded plugins.
        /// </summary>
        private readonly ConcurrentDictionary<string, IPlugin> _plugins = new ConcurrentDictionary<string, IPlugin>();

        /// <summary>
        /// ConcurrentDictionary of registered <c>!botname</c> chat commands.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _registeredBotnameChatCommands = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// ConcurrentDictionary of registered <c>!botname</c> whisper commands.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _registeredBotnameWhisperCommands = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// ConcurrentDictionary of registered chat commands.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _registeredChatCommands = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// ConcurrentDictionary of registered whisper commands.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<string>> _registeredWhisperCommands = new ConcurrentDictionary<string, List<string>>();

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
            this.ChatCommandIdentifier = Program.Settings.ChatCommandIdentifier == 0 ? Program.Settings.ChatCommandIdentifier : this.ChatCommandIdentifier;
            this.WhisperCommandIdentifier = Program.Settings.WhisperCommandIdentifier == 0 ? Program.Settings.WhisperCommandIdentifier : this.WhisperCommandIdentifier;
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
            _ = this.OnMessagePreModeration?.Invoke(this, e);

            ModerationResult harshestModeration = new ModerationResult();

            await Task.Run(async () =>
            {
                if (!(this.OnMessageModeration is null))
                {
                    // Run all moderation events and get the harshest one found.
                    foreach (MessageModerationEventHandler d in this.OnMessageModeration.GetInvocationList())
                    {
                        ModerationResult rs = await d.Invoke(this, e).ConfigureAwait(false);
                        if (harshestModeration.IsHarsher(rs))
                        {
                            harshestModeration = rs;
                        }
                    }
                }
            }).ConfigureAwait(false);

            await Task.Run(async () =>
            {
                if (harshestModeration.ShouldModerate)
                {
                    this.PerformModerationAction(harshestModeration, await ChatModerator.GetFilterDocumentForChannel(e.ChatMessage.RoomId).ConfigureAwait(false), e.ChatMessage);
                }
                else
                {
                    this.OnMessageReceived?.Invoke(this, e);

                    if (Equals(e.ChatMessage.Message[0], this.ChatCommandIdentifier))
                    {
                        ChatCommand chatCommand = new ChatCommand(e.ChatMessage);
                        ChatCommandReceivedEventHandler eventHandler;

                        if (string.Equals(chatCommand.CommandText, Program.Settings.BotLogin, StringComparison.OrdinalIgnoreCase) && chatCommand.ArgumentsAsList.Count > 0)
                        {
                            if (chatCommand.ArgumentsAsList.Count > 1 && this._botnameChatCommandEventHandlers.TryGetValue((chatCommand.ArgumentsAsList[0] + " " + chatCommand.ArgumentsAsList[1]).ToLowerInvariant(), out eventHandler))
                            {
                                CommandDocument commandDocument = await this.LoadCommandAsync(e.ChatMessage.RoomId, (chatCommand.CommandText + " " + chatCommand.ArgumentsAsList[0] + " " + chatCommand.ArgumentsAsList[1]).ToLowerInvariant()).ConfigureAwait(false);

                                if (!commandDocument.Cooldowns.IsOnCooldown(e.ChatMessage.UserId))
                                {
                                    eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                                    commandDocument.Cooldowns.StartCooldown(e.ChatMessage.UserId);
                                }
                            }
                            else if (this._botnameChatCommandEventHandlers.TryGetValue(chatCommand.ArgumentsAsList[0].ToLowerInvariant(), out eventHandler))
                            {
                                CommandDocument commandDocument = await this.LoadCommandAsync(e.ChatMessage.RoomId, (chatCommand.CommandText + " " + chatCommand.ArgumentsAsList[0]).ToLowerInvariant()).ConfigureAwait(false);

                                if (!commandDocument.Cooldowns.IsOnCooldown(e.ChatMessage.UserId))
                                {
                                    eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                                    commandDocument.Cooldowns.StartCooldown(e.ChatMessage.UserId);
                                }
                            }
                        }
                        else
                        {
                            if (chatCommand.ArgumentsAsList.Count > 1 && this._chatCommandEventHandlers.TryGetValue((chatCommand.CommandText + " " + chatCommand.ArgumentsAsList[0]).ToLowerInvariant(), out eventHandler))
                            {
                                CommandDocument commandDocument = await this.LoadCommandAsync(e.ChatMessage.RoomId, (chatCommand.CommandText + " " + chatCommand.ArgumentsAsList[0]).ToLowerInvariant()).ConfigureAwait(false);

                                if (!commandDocument.Cooldowns.IsOnCooldown(e.ChatMessage.UserId))
                                {
                                    eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                                    commandDocument.Cooldowns.StartCooldown(e.ChatMessage.UserId);
                                }
                            }
                            else if (this._chatCommandEventHandlers.TryGetValue(chatCommand.CommandText.ToLowerInvariant(), out eventHandler))
                            {
                                CommandDocument commandDocument = await this.LoadCommandAsync(e.ChatMessage.RoomId, chatCommand.CommandText.ToLowerInvariant()).ConfigureAwait(false);

                                if (!commandDocument.Cooldowns.IsOnCooldown(e.ChatMessage.UserId))
                                {
                                    eventHandler.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
                                    commandDocument.Cooldowns.StartCooldown(e.ChatMessage.UserId);
                                }
                            }
                            else
                            {
                                this.OnChatCommandReceived?.Invoke(this, new OnChatCommandReceivedArgs { Command = chatCommand });
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

                    if (string.Equals(whisperCommand.CommandText, Program.Settings.BotLogin, StringComparison.OrdinalIgnoreCase))
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