/*
 * Copyright © 2019-2022 StreamActions Team
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
using System.Threading.Tasks;

namespace StreamActions
{
    public class TwitchLibPubSub
    {
        #region Public Events

        /// <summary>
        /// Fires when PubSub receives notice a viewer gets banned.
        /// </summary>
        public event EventHandler<OnBanArgs> OnBan;

        /// <summary>
        /// Fires when PubSub receives notice of a bit donation.
        /// </summary>
        public event EventHandler<OnBitsReceivedArgs> OnBitsReceived;

        /// <summary>
        /// Fires when PubSub receives notice of a commerce transaction.
        /// </summary>
        public event EventHandler<OnChannelCommerceReceivedArgs> OnChannelCommerceReceived;

        /// <summary>
        /// Fires when PubSub receives notice when the channel being listened to gets a subscription.
        /// </summary>
        public event EventHandler<OnChannelSubscriptionArgs> OnChannelSubscription;

        /// <summary>
        /// Fires when PubSub receives notice that chat gets cleared.
        /// </summary>
        public event EventHandler<OnClearArgs> OnClear;

        /// <summary>
        /// Fires when PubSub receives notice that Emote-Only Mode gets turned on.
        /// </summary>
        public event EventHandler<OnEmoteOnlyArgs> OnEmoteOnly;

        /// <summary>
        /// Fires when PubSub receives notice that Emote-Only Mode gets turned off.
        /// </summary>
        public event EventHandler<OnEmoteOnlyOffArgs> OnEmoteOnlyOff;

        /// <summary>
        /// Fires when PubSub receives notice when a user follows the designated channel.
        /// </summary>
        public event EventHandler<OnFollowArgs> OnFollow;

        /// <summary>
        /// Fires when PubSub receives notice that the channel being listened to is hosting another channel.
        /// </summary>
        public event EventHandler<OnHostArgs> OnHost;

        /// <summary>
        /// Fires when PubSub receives any response.
        /// </summary>
        public event EventHandler<OnListenResponseArgs> OnListenResponse;

        /// <summary>
        /// Fires when PubSub receives any data from Twitch
        /// </summary>
        public event EventHandler<OnLogArgs> OnLog;

        /// <summary>
        /// Fires when PubSub receives notice a message was deleted.
        /// </summary>
        public event EventHandler<OnMessageDeletedArgs> OnMessageDeleted;

        /// <summary>
        /// Fires when PubSub Service is closed.
        /// </summary>
        public event EventHandler OnPubSubServiceClosed;

        /// <inheritdoc />
        /// <summary>
        /// Fires when PubSub Service is connected.
        /// </summary>
        public event EventHandler OnPubSubServiceConnected;

        /// <summary>
        /// Fires when PubSub Service has an error.
        /// </summary>
        public event EventHandler<OnPubSubServiceErrorArgs> OnPubSubServiceError;

        /// <summary>
        /// Fires when PubSub receives notice that the chat option R9kBeta gets turned on.
        /// </summary>
        public event EventHandler<OnR9kBetaArgs> OnR9kBeta;

        /// <summary>
        /// Fires when PubSub receives notice that the chat option R9kBeta gets turned off.
        /// </summary>
        public event EventHandler<OnR9kBetaOffArgs> OnR9kBetaOff;

        /// <summary>
        /// Fires when PubSub receives notice that the stream of the channel being listened to goes offline.
        /// </summary>
        public event EventHandler<OnStreamDownArgs> OnStreamDown;

        /// <summary>
        /// Fires when PubSub receives notice that the stream of the channel being listened to goes online.
        /// </summary>
        public event EventHandler<OnStreamUpArgs> OnStreamUp;

        /// <summary>
        /// Fires when PubSub receives notice that Sub-Only Mode gets turned on.
        /// </summary>
        public event EventHandler<OnSubscribersOnlyArgs> OnSubscribersOnly;

        /// <summary>
        /// Fires when PubSub receives notice that Sub-Only Mode gets turned off.
        /// </summary>
        public event EventHandler<OnSubscribersOnlyOffArgs> OnSubscribersOnlyOff;

        /// <summary>
        /// Fires when PubSub receives notice a viewer gets a timeout.
        /// </summary>
        public event EventHandler<OnTimeoutArgs> OnTimeout;

        /// <summary>
        /// Fires when PubSub receives notice a viewer gets unbanned.
        /// </summary>
        public event EventHandler<OnUnbanArgs> OnUnban;

        /// <summary>
        /// Fires when PubSub receives notice a viewer gets a timeout removed.
        /// </summary>
        public event EventHandler<OnUntimeoutArgs> OnUntimeout;

        /// <summary>
        /// Fires when PubSub receives notice view count has changed.
        /// </summary>
        public event EventHandler<OnViewCountArgs> OnViewCount;

        /// <summary>
        /// Fires when PubSub receives a whisper.
        /// </summary>
        public event EventHandler<OnWhisperArgs> OnWhisper;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Singleton of <see cref="TwitchLibPubSub"/>.
        /// </summary>
        public static TwitchLibPubSub Instance => _instance.Value;

        #endregion Public Properties

        #region Internal Properties

        /// <summary>
        /// Provides access to the underlying <see cref="TwitchPubSub"/> to the core.
        /// </summary>
        internal TwitchPubSub TwitchPubSub => this._twitchPubSub;

        #endregion Internal Properties

        #region Internal Methods

        /// <summary>
        /// Connects the <see cref="TwitchPubSub"/>.
        /// </summary>
        internal void Connect() => this._twitchPubSub.Connect();

        /// <summary>
        /// Shuts down the <see cref="TwitchPubSub"/>.
        /// </summary>
        internal void Shutdown()
        {
            this._shutdown = true;
            this._twitchPubSub.Disconnect();
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<TwitchLibPubSub> _instance = new Lazy<TwitchLibPubSub>(() => new TwitchLibPubSub());

        /// <summary>
        /// Exponential backoff values, in seconds.
        /// </summary>
        private static readonly int[] _nextConnectAttemptBackoffValues = { 1, 1, 5, 5, 5, 10, 10, 15, 20, 30, 30, 60, 60, 120, 120, 240, 300, 420, 600 };

        /// <summary>
        /// Lock used for destroying the instance of this class when we lose connection with PubSub.
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// The <see cref="TwitchClient"/>.
        /// </summary>
        private readonly TwitchPubSub _twitchPubSub;

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
        /// Class constructor. Preps the <see cref="TwitchPubSub"/> class.
        /// </summary>
        private TwitchLibPubSub()
        {
            this._twitchPubSub = new TwitchPubSub();

            this.SetListeners();

            this._twitchPubSub.OnPubSubServiceClosed += this.TwitchPubSub_OnPubSubServiceClosed;
            this._twitchPubSub.OnPubSubServiceConnected += this.TwitchPubSub_OnPubSubServiceConnected;
            this._twitchPubSub.OnPubSubServiceError += this.TwitchPubSub_OnPubSubServiceError;
            this._twitchPubSub.OnListenResponse += this.TwitchPubSub_OnListenResponse;
            this._twitchPubSub.OnTimeout += this.TwitchPubSub_OnTimeout;
            this._twitchPubSub.OnBan += this.TwitchPubSub_OnBan;
            this._twitchPubSub.OnMessageDeleted += this.TwitchPubSub_OnMessageDeleted;
            this._twitchPubSub.OnUnban += this.TwitchPubSub_OnUnban;
            this._twitchPubSub.OnUntimeout += this.TwitchPubSub_OnUntimeout;
            this._twitchPubSub.OnHost += this.TwitchPubSub_OnHost;
            this._twitchPubSub.OnSubscribersOnly += this.TwitchPubSub_OnSubscribersOnly;
            this._twitchPubSub.OnSubscribersOnlyOff += this.TwitchPubSub_OnSubscribersOnlyOff;
            this._twitchPubSub.OnClear += this.TwitchPubSub_OnClear;
            this._twitchPubSub.OnEmoteOnly += this.TwitchPubSub_OnEmoteOnly;
            this._twitchPubSub.OnEmoteOnlyOff += this.TwitchPubSub_OnEmoteOnlyOff;
            this._twitchPubSub.OnR9kBeta += this.TwitchPubSub_OnR9kBeta;
            this._twitchPubSub.OnR9kBetaOff += this.TwitchPubSub_OnR9kBetaOff;
            this._twitchPubSub.OnBitsReceived += this.TwitchPubSub_OnBitsReceived;
            this._twitchPubSub.OnChannelCommerceReceived += this.TwitchPubSub_OnChannelCommerceReceived;
            this._twitchPubSub.OnStreamUp += this.TwitchPubSub_OnStreamUp;
            this._twitchPubSub.OnStreamDown += this.TwitchPubSub_OnStreamDown;
            this._twitchPubSub.OnViewCount += this.TwitchPubSub_OnViewCount;
            this._twitchPubSub.OnWhisper += this.TwitchPubSub_OnWhisper;
            this._twitchPubSub.OnChannelSubscription += this.TwitchPubSub_OnChannelSubscription;
            this._twitchPubSub.OnFollow += this.TwitchPubSub_OnFollow;
            this._twitchPubSub.OnLog += this.TwitchPubSub_OnLog;
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Method that sets all the PubSub listeners for all channels.
        /// </summary>
        private void SetListeners()
        {
            // TODO: Get bot ID.
            string botId = "";
            // TODO: Get all channel IDs.
            List<string> channelIds = new List<string>();

            foreach (string channelId in channelIds)
            {
                // TODO: Listen to all topics.
                this._twitchPubSub.ListenToFollows(channelId);
                this._twitchPubSub.ListenToChatModeratorActions(botId, channelId);
            }
        }

        /// <summary>
        /// Passes <see cref="OnBan"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnBanArgs"/> object.</param>
        private void TwitchPubSub_OnBan(object sender, OnBanArgs e) => OnBan?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnBitsReceived"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnBitsReceivedArgs"/> object.</param>
        private void TwitchPubSub_OnBitsReceived(object sender, OnBitsReceivedArgs e) => OnBitsReceived?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnChannelCommerceReceived"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChannelCommerceReceivedArgs"/> object.</param>
        private void TwitchPubSub_OnChannelCommerceReceived(object sender, OnChannelCommerceReceivedArgs e) => OnChannelCommerceReceived?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnChannelSubscription"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnChannelSubscriptionArgs"/> object.</param>
        private void TwitchPubSub_OnChannelSubscription(object sender, OnChannelSubscriptionArgs e) => OnChannelSubscription?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnClear"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnClearArgs"/> object.</param>
        private void TwitchPubSub_OnClear(object sender, OnClearArgs e) => OnClear?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnEmoteOnly"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnEmoteOnlyArgs"/> object.</param>
        private void TwitchPubSub_OnEmoteOnly(object sender, OnEmoteOnlyArgs e) => OnEmoteOnly?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnEmoteOnlyOff"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnEmoteOnlyOffArgs"/> object.</param>
        private void TwitchPubSub_OnEmoteOnlyOff(object sender, OnEmoteOnlyOffArgs e) => OnEmoteOnlyOff?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnFollow"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnFollowArgs"/> object.</param>
        private void TwitchPubSub_OnFollow(object sender, OnFollowArgs e) => OnFollow?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnHost"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnHostArgs"/> object.</param>
        private void TwitchPubSub_OnHost(object sender, OnHostArgs e) => OnHost?.Invoke(this, e);

        /// <summary>
        /// Makes sure we were able to listen to a response then passes <see cref="OnListenResponse"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnListenResponseArgs"/> object.</param>
        private void TwitchPubSub_OnListenResponse(object sender, OnListenResponseArgs e)
        {
            OnListenResponse?.Invoke(this, e);

            if (!e.Successful)
            {
                // TODO: Handle error/response.
            }
        }

        /// <summary>
        /// Passes <see cref="OnLog"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnLogArgs"/> object.</param>
        private void TwitchPubSub_OnLog(object sender, OnLogArgs e) => OnLog?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnMessageDeleted"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnMessageDeletedArgs"/> object.</param>
        private void TwitchPubSub_OnMessageDeleted(object sender, OnMessageDeletedArgs e) => OnMessageDeleted?.Invoke(this, e);

        /// <summary>
        /// Handles everything if the PubSub service closes then passes <see cref="System.EventArgs"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="System.EventArgs"/> object.</param>
        private async void TwitchPubSub_OnPubSubServiceClosed(object sender, System.EventArgs e)
        {
            this.OnPubSubServiceClosed?.Invoke(this, e);

            if (!this._shutdown)
            {
                await Task.Run(async () =>
                {
                    while (DateTime.Now.CompareTo(this._nextConnectAttempt) < 0)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }

                    this._nextConnectAttempt = DateTime.Now.AddSeconds(_nextConnectAttemptBackoffValues[this._nextConnectAttemptBackoffKey]);

                    // TODO: Handle reconnect better.
                    lock (this._lock)
                    {
                        this._twitchPubSub.Disconnect();
                        this.SetListeners();
                        this._twitchPubSub.Connect();
                    }

                    this._nextConnectAttemptBackoffKey++;
                    if (this._nextConnectAttemptBackoffKey >= _nextConnectAttemptBackoffValues.Length)
                    {
                        this._nextConnectAttemptBackoffKey = _nextConnectAttemptBackoffValues.Length - 1;
                    }
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Handles everything if the PubSub service connects then passes <see cref="System.EventArgs"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="System.EventArgs"/> object.</param>
        private async void TwitchPubSub_OnPubSubServiceConnected(object sender, System.EventArgs e)
        {
            this.OnPubSubServiceConnected?.Invoke(this, e);

            this._twitchPubSub.SendTopics();

            DateTime nextConnectAttempt = this._nextConnectAttempt;
            await Task.Delay(30000).ConfigureAwait(false);

            if (this._nextConnectAttempt.CompareTo(nextConnectAttempt) == 0)
            {
                this._nextConnectAttemptBackoffKey = 0;
            }
        }

        /// <summary>
        /// Handles everything if the PubSub service fails then passes <see cref="System.EventArgs"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnPubSubServiceErrorArgs"/> object.</param>
        private void TwitchPubSub_OnPubSubServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            this.OnPubSubServiceError?.Invoke(this, e);
            this.Shutdown();
        }

        /// <summary>
        /// Passes <see cref="OnR9kBeta"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnR9kBetaArgs"/> object.</param>
        private void TwitchPubSub_OnR9kBeta(object sender, OnR9kBetaArgs e) => OnR9kBeta?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnR9kBetaOff"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnR9kBetaOffArgs"/> object.</param>
        private void TwitchPubSub_OnR9kBetaOff(object sender, OnR9kBetaOffArgs e) => OnR9kBetaOff?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnStreamDown"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnStreamDownArgs"/> object.</param>
        private void TwitchPubSub_OnStreamDown(object sender, OnStreamDownArgs e) => OnStreamDown?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnStreamUp"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnStreamUpArgs"/> object.</param>
        private void TwitchPubSub_OnStreamUp(object sender, OnStreamUpArgs e) => OnStreamUp?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnSubscribersOnly"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnSubscribersOnlyArgs"/> object.</param>
        private void TwitchPubSub_OnSubscribersOnly(object sender, OnSubscribersOnlyArgs e) => OnSubscribersOnly?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnSubscribersOnlyOff"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnSubscribersOnlyOffArgs"/> object.</param>
        private void TwitchPubSub_OnSubscribersOnlyOff(object sender, OnSubscribersOnlyOffArgs e) => OnSubscribersOnlyOff?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnTimeout"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnTimeoutArgs"/> object.</param>
        private void TwitchPubSub_OnTimeout(object sender, OnTimeoutArgs e) => OnTimeout?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUnban"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUnbanArgs"/> object.</param>
        private void TwitchPubSub_OnUnban(object sender, OnUnbanArgs e) => OnUnban?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnUntimeout"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnUntimeoutArgs"/> object.</param>
        private void TwitchPubSub_OnUntimeout(object sender, OnUntimeoutArgs e) => OnUntimeout?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnViewCount"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnViewCountArgs"/> object.</param>
        private void TwitchPubSub_OnViewCount(object sender, OnViewCountArgs e) => OnViewCount?.Invoke(this, e);

        /// <summary>
        /// Passes <see cref="OnWhisper"/> events down to subscribed plugins.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="OnWhisperArgs"/> object.</param>
        private void TwitchPubSub_OnWhisper(object sender, OnWhisperArgs e) => OnWhisper?.Invoke(this, e);

        #endregion Private Methods
    }
}
