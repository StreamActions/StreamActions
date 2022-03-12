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

using StreamActions.Database;
using StreamActions.Database.Documents.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StreamActions
{
    /// <summary>
    /// Manages the <see cref="TwitchClient"/> for this bot instance.
    /// </summary>
    public class TwitchLibClient
    {
        #region Public Events

        /// <summary>
        /// Fires when the library detects another channel has started hosting the broadcaster's stream. MUST BE CONNECTED AS BROADCASTER.
        /// </summary>
        public event EventHandler<OnBeingHostedArgs> OnBeingHosted;

        /// <summary>
        /// Fires when connecting and channel state is changed, returns ChannelState.
        /// </summary>
        public event EventHandler<OnChannelStateChangedArgs> OnChannelStateChanged;

        /// <summary>
        /// Fires when a channel's chat is cleared.
        /// </summary>
        public event EventHandler<OnChatClearedArgs> OnChatCleared;

        /// <summary>
        /// Fires when confirmation of a chat color change request was received.
        /// </summary>
        public event EventHandler<OnChatColorChangedArgs> OnChatColorChanged;

        /// <summary>
        /// Fires when a community subscription is announced in chat
        /// </summary>
        public event EventHandler<OnCommunitySubscriptionArgs> OnCommunitySubscription;

        /// <summary>
        /// Fires when client connects to Twitch.
        /// </summary>
        public event EventHandler<OnConnectedArgs> OnConnected;

        /// <summary>
        /// Forces when bot suffers connection error.
        /// </summary>
        public event EventHandler<OnConnectionErrorArgs> OnConnectionError;

        /// <summary>
        /// Fires when bot has disconnected.
        /// </summary>
        public event EventHandler<OnDisconnectedEventArgs> OnDisconnected;

        /// <summary>
        /// Occurs when an Error is thrown in the protocol client
        /// </summary>
        public event EventHandler<OnErrorEventArgs> OnError;

        /// <summary>
        /// Fires when Twitch notifies client of existing users in chat.
        /// </summary>
        public event EventHandler<OnExistingUsersDetectedArgs> OnExistingUsersDetected;

        /// <summary>
        /// Fires when the client was unable to join a channel.
        /// </summary>
        public event EventHandler<OnFailureToReceiveJoinConfirmationArgs> OnFailureToReceiveJoinConfirmation;

        /// <summary>
        /// Fires when a subscription is gifted and announced in chat
        /// </summary>
        public event EventHandler<OnGiftedSubscriptionArgs> OnGiftedSubscription;

        /// <summary>
        /// Fires when the joined channel begins hosting another channel.
        /// </summary>
        public event EventHandler<OnHostingStartedArgs> OnHostingStarted;

        /// <summary>
        /// Fires when the joined channel quits hosting another channel.
        /// </summary>
        public event EventHandler<OnHostingStoppedArgs> OnHostingStopped;

        /// <summary>
        /// Fires when a hosted streamer goes offline and hosting is killed.
        /// </summary>
        public event EventHandler OnHostLeft;

        /// <summary>
        /// Fires on logging in with incorrect details, returns ErrorLoggingInException.
        /// </summary>
        public event EventHandler<OnIncorrectLoginArgs> OnIncorrectLogin;

        /// <summary>
        /// Fires when client joins a channel.
        /// </summary>
        public event EventHandler<OnJoinedChannelArgs> OnJoinedChannel;

        /// <summary>
        /// Fires when client successfully leaves a channel.
        /// </summary>
        public event EventHandler<OnLeftChannelArgs> OnLeftChannel;

        /// <summary>
        /// Fires whenever a log write happens.
        /// </summary>
        public event EventHandler<OnLogArgs> OnLog;

        /// <summary>
        /// Fires when a message gets deleted in chat.
        /// </summary>
        public event EventHandler<OnMessageClearedArgs> OnMessageCleared;

        /// <summary>
        /// Fires when a chat message is sent, returns username, channel and message.
        /// </summary>
        public event EventHandler<OnMessageSentArgs> OnMessageSent;

        /// <summary>
        /// Fires when a Message has been throttled.
        /// </summary>
        public event EventHandler<OnMessageThrottledEventArgs> OnMessageThrottled;

        /// <summary>
        /// Fires when a moderator joined the channel's chat room, returns username and channel.
        /// </summary>
        public event EventHandler<OnModeratorJoinedArgs> OnModeratorJoined;

        /// <summary>
        /// Fires when a moderator joins the channel's chat room, returns username and channel.
        /// </summary>
        public event EventHandler<OnModeratorLeftArgs> OnModeratorLeft;

        /// <summary>
        /// Fires when a list of moderators is received.
        /// </summary>
        public event EventHandler<OnModeratorsReceivedArgs> OnModeratorsReceived;

        /// <summary>
        /// Fires when new subscriber is announced in chat, returns Subscriber.
        /// </summary>
        public event EventHandler<OnNewSubscriberArgs> OnNewSubscriber;

        /// <summary>
        /// Fires when TwitchClient receives generic no permission error from Twitch.
        /// </summary>
        public event EventHandler OnNoPermissionError;

        /// <summary>
        /// Fires when client receives notice that a joined channel is hosting another channel.
        /// </summary>
        public event EventHandler<OnNowHostingArgs> OnNowHosting;

        /// <summary>
        /// Fires when newly raided channel is mature audience only.
        /// </summary>
        public event EventHandler OnRaidedChannelIsMatureAudience;

        /// <summary>
        /// Fires when a raid notification is detected in chat
        /// </summary>
        public event EventHandler<OnRaidNotificationArgs> OnRaidNotification;

        /// <summary>
        /// Occurs when a reconnection occurs.
        /// </summary>
        public event EventHandler<OnReconnectedEventArgs> OnReconnected;

        /// <summary>
        /// Fires when current subscriber renews subscription, returns ReSubscriber.
        /// </summary>
        public event EventHandler<OnReSubscriberArgs> OnReSubscriber;

        /// <summary>
        /// Fires when a ritual for a new chatter is received.
        /// </summary>
        public event EventHandler<OnRitualNewChatterArgs> OnRitualNewChatter;

        /// <summary>
        /// Fires when TwitchClient attempts to host a channel it is in.
        /// </summary>
        public event EventHandler OnSelfRaidError;

        /// <summary>
        /// Fires when data is received from Twitch that is not able to be parsed.
        /// </summary>
        public event EventHandler<OnUnaccountedForArgs> OnUnaccountedFor;

        /// <summary>
        /// Fires when a viewer gets banned by any moderator.
        /// </summary>
        public event EventHandler<OnUserBannedArgs> OnUserBanned;

        /// <summary>
        /// Fires when a new viewer/chatter joined the channel's chat room, returns username and channel.
        /// </summary>
        public event EventHandler<OnUserJoinedArgs> OnUserJoined;

        /// <summary>
        /// Fires when a PART message is received from Twitch regarding a particular viewer
        /// </summary>
        public event EventHandler<OnUserLeftArgs> OnUserLeft;

        /// <summary>
        /// Fires when a user state is received, returns UserState.
        /// </summary>
        public event EventHandler<OnUserStateChangedArgs> OnUserStateChanged;

        /// <summary>
        /// Fires when a viewer gets timed out by any moderator.
        /// </summary>
        public event EventHandler<OnUserTimedoutArgs> OnUserTimedout;

        /// <summary>
        /// Fires when VIPs are received from chat
        /// </summary>
        public event EventHandler<OnVIPsReceivedArgs> OnVIPsReceived;

        /// <summary>
        /// Fires when a whisper message is sent, returns username and message.
        /// </summary>
        public event EventHandler<OnWhisperSentArgs> OnWhisperSent;

        /// <summary>
        /// Fires when a Whisper has been throttled.
        /// </summary>
        public event EventHandler<OnWhisperThrottledEventArgs> OnWhisperThrottled;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Singleton of <see cref="TwitchLibClient"/>.
        /// </summary>
        public static TwitchLibClient Instance => _instance.Value;

        /// <summary>
        /// The emotes this channel replaces.
        /// </summary>
        /// <value>The channel emotes.</value>
        /// <remarks>Twitch-handled emotes are automatically added to this collection (which also accounts for
        /// managing user emote permissions such as sub-only emotes). Third-party emotes will have to be manually
        /// added according to the availability rules defined by the third-party.</remarks>
        public MessageEmoteCollection ChannelEmotes => this._twitchClient.ChannelEmotes;

        /// <summary>
        /// The current connection status of the client.
        /// </summary>
        /// <value><c>true</c> if this instance is connected; otherwise, <c>false</c>.</value>
        public bool IsConnected => this._twitchClient.IsConnected;

        /// <summary>
        /// Checks if underlying client has been initialized.
        /// </summary>
        /// <value><c>true</c> if this instance is initialized; otherwise, <c>false</c>.</value>
        public bool IsInitialized => this._twitchClient.IsInitialized;

        /// <summary>
        /// A list of all channels the client is currently in.
        /// </summary>
        /// <value>The joined channels.</value>
        public IReadOnlyList<JoinedChannel> JoinedChannels => this._twitchClient.JoinedChannels;

        /// <summary>
        /// If set to true, the library will not check upon channel join that if BeingHosted event is subscribed, that the bot is connected as broadcaster. Only override if the broadcaster is joining multiple channels, including the broadcaster's.
        /// </summary>
        /// <value><c>true</c> if [override being hosted check]; otherwise, <c>false</c>.</value>
        public bool OverrideBeingHostedCheck => this._twitchClient.OverrideBeingHostedCheck;

        /// <summary>
        /// The most recent whisper received.
        /// </summary>
        /// <value>The previous whisper.</value>
        public WhisperMessage PreviousWhisper => this._twitchClient.PreviousWhisper;

        /// <summary>
        /// Username of the user connected via this library.
        /// </summary>
        /// <value>The twitch username.</value>
        public string TwitchUsername => this._twitchClient.TwitchUsername;

        /// <summary>
        /// Assembly version of TwitchLib.Client.
        /// </summary>
        /// <value>The version.</value>
        public Version Version => this._twitchClient.Version;

        /// <summary>
        /// Determines whether Emotes will be replaced in messages.
        /// </summary>
        /// <value><c>true</c> if [will replace emotes]; otherwise, <c>false</c>.</value>
        public bool WillReplaceEmotes => this._twitchClient.WillReplaceEmotes;

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Sends a formatted Twitch channel chat message.
        /// </summary>
        /// <param name="channel">Channel to send message to.</param>
        /// <param name="message">The message to be sent.</param>
        /// <exception cref="ArgumentNullException"><paramref name="channel"/> or <paramref name="message"/> is null.</exception>
        public void SendMessage(string channel, string message)
        {
            if (channel is null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.Split('\n').ToList().ForEach(m => this._twitchClient.SendMessage(channel, m));
        }

        /// <summary>
        /// Sends a formatted whisper message to someone.
        /// </summary>
        /// <param name="receiver">The receiver of the whisper.</param>
        /// <param name="message">The message to be sent.</param>
        /// <exception cref="ArgumentNullException"><paramref name="receiver"/> or <paramref name="message"/> is null.</exception>
        public void SendWhisper(string receiver, string message)
        {
            if (receiver is null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            message.Split('\n').ToList().ForEach(m => this._twitchClient.SendWhisper(receiver, m));
        }

        #endregion Public Methods

        #region Internal Properties

        /// <summary>
        /// Provides access to the underlying <see cref="TwitchClient"/> to the core.
        /// </summary>
        internal TwitchClient TwitchClient => this._twitchClient;

        #endregion Internal Properties

        #region Internal Methods

        /// <summary>
        /// Connects the <see cref="TwitchClient"/>.
        /// </summary>
        internal void Connect() => this._twitchClient.Connect();

        /// <summary>
        /// Join the Twitch IRC chat of <paramref name="channel"/>.
        /// </summary>
        /// <param name="channel">The channel to join.</param>
        internal void JoinChannel(string channel) => this._twitchClient.JoinChannel(channel);

        /// <summary>
        /// Leaves (PART) the Twitch IRC chat of <paramref name="channel"/>.
        /// </summary>
        /// <param name="channel">The channel to leave.</param>
        internal void LeaveChannel(string channel) => this._twitchClient.LeaveChannel(channel);

        /// <summary>
        /// Reconnects the <see cref="TwitchClient"/>.
        /// </summary>
        internal void Reconnect() => this._twitchClient.Reconnect();

        /// <summary>
        /// Shuts down the <see cref="TwitchClient"/>.
        /// </summary>
        internal void Shutdown()
        {
            this._shutdown = true;
            this._twitchClient.Disconnect();
        }

        /// <summary>
        /// Task that loops until the client is shutdown and disconnected.
        /// </summary>
        /// <returns>A Task that can be awaited.</returns>
        internal Task WaitForFinalDisconnect() => Task.Run(async () =>
                   {
                       while (!this._shutdown || this.IsConnected)
                       {
                           await Task.Delay(1000).ConfigureAwait(false);
                       }
                   });

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<TwitchLibClient> _instance = new Lazy<TwitchLibClient>(() => new TwitchLibClient());

        /// <summary>
        /// Exponential backoff values, in seconds.
        /// </summary>
        private static readonly int[] _nextConnectAttemptBackoffValues = { 1, 1, 5, 5, 5, 10, 10, 15, 20, 30, 30, 60, 60, 120, 120, 240, 300, 420, 600 };

        /// <summary>
        /// The <see cref="TwitchClient"/>.
        /// </summary>
        private readonly TwitchClient _twitchClient;

        /// <summary>
        /// A DateTime indicating when the next connection attempt can occur in the OnDisconnected event.
        /// </summary>
        private DateTime _nextConnectAttempt = DateTime.Now;

        /// <summary>
        /// The key indicating the next <see cref="_nextConnectAttemptBackoffValues"/> to use.
        /// </summary>
        private int _nextConnectAttemptBackoffKey = 0;

        /// <summary>
        /// Indicates if the client is being shutdown and should allow disconnecting.
        /// </summary>
        private bool _shutdown = false;

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Constructor. Preps the <see cref="TwitchClient"/>.
        /// </summary>
        private TwitchLibClient()
        {
            ConnectionCredentials credentials = new ConnectionCredentials(Program.Settings.BotLogin, Program.Settings.BotOAuth);
            ClientOptions clientOptions = new ClientOptions()
            {
                MessagesAllowedInPeriod = 30,
                ThrottlingPeriod = TimeSpan.FromSeconds(30),
                ReconnectionPolicy = null
            };
            WebSocketClient webSocketClient = new WebSocketClient(clientOptions);

            this._twitchClient = new TwitchClient(webSocketClient);
            this._twitchClient.Initialize(credentials);
            this._twitchClient.OnBeingHosted += this.TwitchClient_OnBeingHosted;
            this._twitchClient.OnChannelStateChanged += this.TwitchClient_OnChannelStateChanged;
            this._twitchClient.OnChatCleared += this.TwitchClient_OnChatCleared;
            this._twitchClient.OnChatColorChanged += this.TwitchClient_OnChatColorChanged;
            this._twitchClient.OnCommunitySubscription += this.TwitchClient_OnCommunitySubscription;
            this._twitchClient.OnConnected += this.TwitchClient_OnConnected;
            this._twitchClient.OnConnectionError += this.TwitchClient_OnConnectionError;
            this._twitchClient.OnDisconnected += this.TwitchClient_OnDisconnected;
            this._twitchClient.OnError += this.TwitchClient_OnError;
            this._twitchClient.OnExistingUsersDetected += this.TwitchClient_OnExistingUsersDetected;
            this._twitchClient.OnFailureToReceiveJoinConfirmation += this.TwitchClient_OnFailureToReceiveJoinConfirmation;
            this._twitchClient.OnGiftedSubscription += this.TwitchClient_OnGiftedSubscription;
            this._twitchClient.OnHostingStarted += this.TwitchClient_OnHostingStarted;
            this._twitchClient.OnHostingStopped += this.TwitchClient_OnHostingStopped;
            this._twitchClient.OnHostLeft += this.TwitchClient_OnHostLeft;
            this._twitchClient.OnIncorrectLogin += this.TwitchClient_OnIncorrectLogin;
            this._twitchClient.OnJoinedChannel += this.TwitchClient_OnJoinedChannel;
            this._twitchClient.OnLeftChannel += this.TwitchClient_OnLeftChannel;
            this._twitchClient.OnLog += this.TwitchClient_OnLog;
            this._twitchClient.OnMessageCleared += this.TwitchClient_OnMessageCleared;
            this._twitchClient.OnMessageSent += this.TwitchClient_OnMessageSent;
            this._twitchClient.OnMessageThrottled += this.TwitchClient_OnMessageThrottled;
            this._twitchClient.OnModeratorJoined += this.TwitchClient_OnModeratorJoined;
            this._twitchClient.OnModeratorLeft += this.TwitchClient_OnModeratorLeft;
            this._twitchClient.OnModeratorsReceived += this.TwitchClient_OnModeratorsReceived;
            this._twitchClient.OnNewSubscriber += this.TwitchClient_OnNewSubscriber;
            this._twitchClient.OnNoPermissionError += this.TwitchClient_OnNoPermissionError;
            this._twitchClient.OnNowHosting += this.TwitchClient_OnNowHosting;
            this._twitchClient.OnRaidedChannelIsMatureAudience += this.TwitchClient_OnRaidedChannelIsMatureAudience;
            this._twitchClient.OnRaidNotification += this.TwitchClient_OnRaidNotification;
            this._twitchClient.OnReconnected += this.TwitchClient_OnReconnected;
            this._twitchClient.OnReSubscriber += this.TwitchClient_OnReSubscriber;
            this._twitchClient.OnRitualNewChatter += this.TwitchClient_OnRitualNewChatter;
            this._twitchClient.OnSelfRaidError += this.TwitchClient_OnSelfRaidError;
            this._twitchClient.OnUnaccountedFor += this.TwitchClient_OnUnaccountedFor;
            this._twitchClient.OnUserBanned += this.TwitchClient_OnUserBanned;
            this._twitchClient.OnUserJoined += this.TwitchClient_OnUserJoined;
            this._twitchClient.OnUserLeft += this.TwitchClient_OnUserLeft;
            this._twitchClient.OnUserStateChanged += this.TwitchClient_OnUserStateChanged;
            this._twitchClient.OnUserTimedout += this.TwitchClient_OnUserTimedout;
            this._twitchClient.OnVIPsReceived += this.TwitchClient_OnVIPsReceived;
            this._twitchClient.OnWhisperSent += this.TwitchClient_OnWhisperSent;
            this._twitchClient.OnWhisperThrottled += this.TwitchClient_OnWhisperThrottled;
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Passes <see cref="OnBeingHosted"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnBeingHostedArgs"/> object.</param>
        private void TwitchClient_OnBeingHosted(object sender, OnBeingHostedArgs e) => this.OnBeingHosted?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnChannelStateChanged"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChannelStateChangedArgs"/> object.</param>
        private void TwitchClient_OnChannelStateChanged(object sender, OnChannelStateChangedArgs e) => this.OnChannelStateChanged?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnChatCleared"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChatClearedArgs"/> object.</param>
        private void TwitchClient_OnChatCleared(object sender, OnChatClearedArgs e) => this.OnChatCleared?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnChatColorChanged"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChatColorChangedArgs"/> object.</param>
        private void TwitchClient_OnChatColorChanged(object sender, OnChatColorChangedArgs e) => this.OnChatColorChanged?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnCommunitySubscription"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnCommunitySubscriptionArgs"/> object.</param>
        private void TwitchClient_OnCommunitySubscription(object sender, OnCommunitySubscriptionArgs e) => this.OnCommunitySubscription?.Invoke(this, e);

        /// <summary>
        /// Resets the <see cref="_nextConnectAttemptBackoffKey"/> once a stable connection has been established for 30 seconds.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnConnectedArgs"/> object.</param>
        private async void TwitchClient_OnConnected(object sender, OnConnectedArgs e)
        {
            this.OnConnected?.Invoke(this, e);

            IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>(UserDocument.CollectionName);

            FilterDefinitionBuilder<UserDocument> builder = Builders<UserDocument>.Filter;

            FilterDefinition<UserDocument> filter = builder.Eq(d => d.ShouldJoin, true);

            using IAsyncCursor<string> cursor = await collection.FindAsync(filter, new FindOptions<UserDocument, string> { Projection = Builders<UserDocument>.Projection.Expression(d => d.Login) }).ConfigureAwait(false);

            List<string> channelsToJoin = await cursor.ToListAsync().ConfigureAwait(false);

            foreach (string channel in channelsToJoin)
            {
                this.JoinChannel(channel);
            }

            DateTime nextConnectAttempt = this._nextConnectAttempt;
            await Task.Delay(30000).ConfigureAwait(false);

            if (this.IsConnected && this._nextConnectAttempt.CompareTo(nextConnectAttempt) == 0)
            {
                this._nextConnectAttemptBackoffKey = 0;
            }
        }

        /// <summary>
        /// Shuts down the client if a fatal network error occurs.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnConnectionErrorArgs"/> object.</param>
        private void TwitchClient_OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            this.OnConnectionError?.Invoke(this, e);
            this.Shutdown();
        }

        /// <summary>
        /// Detects disconnects outside the shutdown state and attempts to reconnect, using exponential backoff.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnDisconnectedEventArgs"/> object.</param>
        private async void TwitchClient_OnDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            this.OnDisconnected?.Invoke(this, e);

            if (!this._shutdown)
            {
                await Task.Run(async () =>
                {
                    while (DateTime.Now.CompareTo(this._nextConnectAttempt) < 0)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }

                    this._nextConnectAttempt = DateTime.Now.AddSeconds(_nextConnectAttemptBackoffValues[this._nextConnectAttemptBackoffKey]);
                    this.Reconnect();
                    this._nextConnectAttemptBackoffKey++;
                    if (this._nextConnectAttemptBackoffKey >= _nextConnectAttemptBackoffValues.Length)
                    {
                        this._nextConnectAttemptBackoffKey = _nextConnectAttemptBackoffValues.Length - 1;
                    }
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Passes <see cref="OnError"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnErrorEventArgs"/> object.</param>
        private void TwitchClient_OnError(object sender, OnErrorEventArgs e) => this.OnError?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnExistingUsersDetected"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnExistingUsersDetectedArgs"/> object.</param>
        private void TwitchClient_OnExistingUsersDetected(object sender, OnExistingUsersDetectedArgs e) => this.OnExistingUsersDetected?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnFailureToReceivejoinConfirmation"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnFailureToReceiveJoinConfirmationArgs"/> object.</param>
        private void TwitchClient_OnFailureToReceiveJoinConfirmation(object sender, OnFailureToReceiveJoinConfirmationArgs e) => this.OnFailureToReceiveJoinConfirmation?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnGiftedSubscription"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnGiftedSubscriptionArgs"/> object.</param>
        private void TwitchClient_OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e) => this.OnGiftedSubscription?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnHostingStarted"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnHostingStartedArgs"/> object.</param>
        private void TwitchClient_OnHostingStarted(object sender, OnHostingStartedArgs e) => this.OnHostingStarted?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnHostingStopped"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnHostingStoppedArgs"/> object.</param>
        private void TwitchClient_OnHostingStopped(object sender, OnHostingStoppedArgs e) => this.OnHostingStopped?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnHostLeft"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An EventArgs object.</param>
        private void TwitchClient_OnHostLeft(object sender, System.EventArgs e) => this.OnHostLeft?.Invoke(this, e);

        /// <summary>
        /// Shuts down the client if the connection fails with an invalid login error.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnIncorrectLoginArgs"/> object.</param>
        private void TwitchClient_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {
            this.OnIncorrectLogin?.Invoke(this, e);
            this.Shutdown();
        }

        /// <summary>
        /// Passes <see cref="OnJoinedChannel"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnJoinedChannelArgs"/> object.</param>
        private void TwitchClient_OnJoinedChannel(object sender, OnJoinedChannelArgs e) => this.OnJoinedChannel?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnLeftChannel"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnLeftChannelArgs"/> object.</param>
        private void TwitchClient_OnLeftChannel(object sender, OnLeftChannelArgs e) => this.OnLeftChannel?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnLog"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnLogArgs"/> object.</param>
        private void TwitchClient_OnLog(object sender, OnLogArgs e) => this.OnLog?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnMessageCleared"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageClearedArgs"/> object.</param>
        private void TwitchClient_OnMessageCleared(object sender, OnMessageClearedArgs e) => this.OnMessageCleared?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnMessageSent"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageSentArgs"/> object.</param>
        private void TwitchClient_OnMessageSent(object sender, OnMessageSentArgs e) => this.OnMessageSent?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnMessageThrottled"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnAnonGiftedSubscriptionArgs"/> object.</param>
        private void TwitchClient_OnMessageThrottled(object sender, OnMessageThrottledEventArgs e) => this.OnMessageThrottled?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnModeratorJoined"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnModeratorJoinedArgs"/> object.</param>
        private void TwitchClient_OnModeratorJoined(object sender, OnModeratorJoinedArgs e) => this.OnModeratorJoined?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnModeratorLeft"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnModeratorLeftArgs"/> object.</param>
        private void TwitchClient_OnModeratorLeft(object sender, OnModeratorLeftArgs e) => this.OnModeratorLeft?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnModeratorsReceived"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnModeratorsReceivedArgs"/> object.</param>
        private void TwitchClient_OnModeratorsReceived(object sender, OnModeratorsReceivedArgs e) => this.OnModeratorsReceived?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnNewSubscriber"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnNewSubscriberArgs"/> object.</param>
        private void TwitchClient_OnNewSubscriber(object sender, OnNewSubscriberArgs e) => this.OnNewSubscriber?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnNoPermissionError"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An EventArgs object.</param>
        private void TwitchClient_OnNoPermissionError(object sender, System.EventArgs e) => this.OnNoPermissionError?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnNowHosting"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnNowHostingArgs"/> object.</param>
        private void TwitchClient_OnNowHosting(object sender, OnNowHostingArgs e) => this.OnNowHosting?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnRaidedChannelIsMatureAudience"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An EventArgs object.</param>
        private void TwitchClient_OnRaidedChannelIsMatureAudience(object sender, System.EventArgs e) => this.OnRaidedChannelIsMatureAudience?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnRaidNotification"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnRaidNotificationArgs"/> object.</param>
        private void TwitchClient_OnRaidNotification(object sender, OnRaidNotificationArgs e) => this.OnRaidNotification?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnReconnected"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnReconnectedEventArgs"/> object.</param>
        private void TwitchClient_OnReconnected(object sender, OnReconnectedEventArgs e) => this.OnReconnected?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnReSubscriber"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnReSubscriberArgs"/> object.</param>
        private void TwitchClient_OnReSubscriber(object sender, OnReSubscriberArgs e) => this.OnReSubscriber?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnRitualNewChatter"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnRitualNewChatterArgs"/> object.</param>
        private void TwitchClient_OnRitualNewChatter(object sender, OnRitualNewChatterArgs e) => this.OnRitualNewChatter?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnSelfRaidError"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An EventArgs object.</param>
        private void TwitchClient_OnSelfRaidError(object sender, System.EventArgs e) => this.OnSelfRaidError?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUnaccountedFor"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUnaccountedForArgs"/> object.</param>
        private void TwitchClient_OnUnaccountedFor(object sender, OnUnaccountedForArgs e) => this.OnUnaccountedFor?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUserBanned"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUserBannedArgs"/> object.</param>
        private void TwitchClient_OnUserBanned(object sender, OnUserBannedArgs e) => this.OnUserBanned?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUserJoined"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUserJoinedArgs"/> object.</param>
        private void TwitchClient_OnUserJoined(object sender, OnUserJoinedArgs e) => this.OnUserJoined?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUserLeft"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUserLeftArgs"/> object.</param>
        private void TwitchClient_OnUserLeft(object sender, OnUserLeftArgs e) => this.OnUserLeft?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUserStateChanged"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUserStateChangedArgs"/> object.</param>
        private void TwitchClient_OnUserStateChanged(object sender, OnUserStateChangedArgs e) => this.OnUserStateChanged?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUserTimedout"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUserTimedoutArgs"/> object.</param>
        private void TwitchClient_OnUserTimedout(object sender, OnUserTimedoutArgs e) => this.OnUserTimedout?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnVIPsReceived"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnVIPsReceivedArgs"/> object.</param>
        private void TwitchClient_OnVIPsReceived(object sender, OnVIPsReceivedArgs e) => this.OnVIPsReceived?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnWhisperSent"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnWhisperSentArgs"/> object.</param>
        private void TwitchClient_OnWhisperSent(object sender, OnWhisperSentArgs e) => this.OnWhisperSent?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnWhisperThrottled"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnWhisperThrottledEventArgs"/> object.</param>
        private void TwitchClient_OnWhisperThrottled(object sender, OnWhisperThrottledEventArgs e) => this.OnWhisperThrottled?.Invoke(this, e);

        #endregion Private Methods
    }
}
