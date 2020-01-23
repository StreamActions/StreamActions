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

using StreamActions.EventArgs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using WatsonWebsocket;

namespace StreamActions
{
    /// <summary>
    /// Handles the WebSocket server for connections to the panel, other instances, and API clients.
    /// </summary>
    public class WebSocketServer : IDisposable
    {
        #region Public Delegates

        /// <summary>
        /// Represents a method that will handle a ClientConnected event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the client's request.</param>
        /// <returns><c>true</c> to allow the connection; <c>false</c> otherwise. Only one handler has to return <c>true</c> to allow the connection.</returns>
        public delegate bool WebSocketClientConnectedEventHandler(object sender, OnWebSocketClientConnectedArgs e);

        /// <summary>
        /// Represents a method that will handle a ClientDisconnected event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the disconnecting client.</param>
        public delegate void WebSocketClientDisconnectedEventHandler(object sender, OnWebSocketClientDisconnectedArgs e);

        /// <summary>
        /// Represents a method that will handle a MessageReceived event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received data.</param>
        public delegate void WebSocketMessageReceivedEventHandler(object sender, OnWebSocketMessageReceivedArgs e);

        #endregion Public Delegates

        #region Public Properties

        /// <summary>
        /// Singleton of <see cref="WebSocketServer"/>.
        /// </summary>
        public static WebSocketServer Instance => _instance.Value;

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Forcefully disconnect a client.
        /// </summary>
        /// <param name="ipPort">IP:port of the client.</param>
        public void DisconnectClient(string ipPort) => this._wsServer.DisconnectClient(ipPort);

        /// <summary>
        /// Disposes of the <see cref="WatsonWsServer"/>.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Determine whether or not the specified client is connected to the server.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <returns>Boolean indicating if the client is connected to the server.</returns>
        public bool IsClientConnected(string ipPort) => this._wsServer.IsClientConnected(ipPort);

        /// <summary>
        /// Determine whether or not the specified client is connected to the server on the specified URI path.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="path">The URI path to check.</param>
        /// <returns>Boolean indicating if the client is connected to the server.</returns>
        public bool IsClientConnected(string ipPort, string path) => this.IsClientConnected(ipPort) && this._connectionPaths.GetValueOrDefault(ipPort, "/") == path;

        /// <summary>
        /// Send data to the specified client, asynchronously.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="data">String containing data.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        public async Task<bool> SendAsync(string ipPort, string data) => await this._wsServer.SendAsync(ipPort, Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text).ConfigureAwait(false);

        /// <summary>
        /// Send data to the specified client, asynchronously.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="data">Byte array containing data.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        public async Task<bool> SendAsync(string ipPort, byte[] data) => await this._wsServer.SendAsync(ipPort, data, WebSocketMessageType.Binary).ConfigureAwait(false);

        /// <summary>
        /// Attempts to subscribe the provided Delegate to ClientConnected events for the designated URI path.
        /// </summary>
        /// <param name="path">The URI path to handle events for.</param>
        /// <param name="handler">The <see cref="WebSocketClientConnectedEventHandler"/> to subscribe.</param>
        public void SubscribeClientConnectedEventHandler(string path, WebSocketClientConnectedEventHandler handler)
        {
            if (!this._connectedEventHandlers.ContainsKey(path))
            {
                _ = this._connectedEventHandlers.TryAdd(path, new List<WebSocketClientConnectedEventHandler>());
            }

            this._connectedEventHandlers[path].Add(handler);
        }

        /// <summary>
        /// Attempts to subscribe the provided Delegate to ClientDisconnected events for the designated URI path.
        /// </summary>
        /// <param name="path">The URI path to handle events for.</param>
        /// <param name="handler">The <see cref="WebSocketClientDisconnectedEventHandler"/> to subscribe.</param>
        public void SubscribeClientDisconnectedEventHandler(string path, WebSocketClientDisconnectedEventHandler handler)
        {
            if (!this._disconnectedEventHandlers.ContainsKey(path))
            {
                _ = this._disconnectedEventHandlers.TryAdd(path, new List<WebSocketClientDisconnectedEventHandler>());
            }

            this._disconnectedEventHandlers[path].Add(handler);
        }

        /// <summary>
        /// Attempts to subscribe the provided Delegate to MessageReceived events for the designated URI path.
        /// </summary>
        /// <param name="path">The URI path to handle events for.</param>
        /// <param name="handler">The <see cref="WebSocketMessageReceivedEventHandler"/> to subscribe.</param>
        public void SubscribeMessageReceivedEventHandler(string path, WebSocketMessageReceivedEventHandler handler)
        {
            if (!this._messageEventHandlers.ContainsKey(path))
            {
                _ = this._messageEventHandlers.TryAdd(path, new List<WebSocketMessageReceivedEventHandler>());
            }

            this._messageEventHandlers[path].Add(handler);
        }

        /// <summary>
        /// Attempts to unsubscribe the provided Delegate from ClientConnected events for the designated URI path.
        /// </summary>
        /// <param name="path">The URI path to unsubscribe from.</param>
        /// <param name="handler">The <see cref="WebSocketClientConnectedEventHandler"/> to unsubscribe.</param>
        public void UnsubscribeClientConnectedEventHandler(string path, WebSocketClientConnectedEventHandler handler)
        {
            if (this._connectedEventHandlers.ContainsKey(path))
            {
                _ = this._connectedEventHandlers[path].Remove(handler);
            }
        }

        /// <summary>
        /// Attempts to unsubscribe the provided Delegate from ClientDisconnected events for the designated URI path.
        /// </summary>
        /// <param name="path">The URI path to unsubscribe from.</param>
        /// <param name="handler">The <see cref="WebSocketClientDisconnectedEventHandler"/> to unsubscribe.</param>
        public void UnsubscribeClientDisconnectedEventHandler(string path, WebSocketClientDisconnectedEventHandler handler)
        {
            if (this._disconnectedEventHandlers.ContainsKey(path))
            {
                _ = this._disconnectedEventHandlers[path].Remove(handler);
            }
        }

        /// <summary>
        /// Attempts to unsubscribe the provided Delegate from MessageReceived events for the designated URI path.
        /// </summary>
        /// <param name="path">The URI path to unsubscribe from.</param>
        /// <param name="handler">The <see cref="WebSocketMessageReceivedEventHandler"/> to unsubscribe.</param>
        public void UnsubscribeMessageReceivedEventHandler(string path, WebSocketMessageReceivedEventHandler handler)
        {
            if (this._messageEventHandlers.ContainsKey(path))
            {
                _ = this._messageEventHandlers[path].Remove(handler);
            }
        }

        #endregion Public Methods

        #region Protected Methods

        /// <summary>
        /// Disposes of the <see cref="WatsonWsServer"/>.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._wsServer.Dispose();
            }
        }

        #endregion Protected Methods

        #region Internal Methods

        /// <summary>
        /// Starts the <see cref="WatsonWsServer"/>.
        /// </summary>
        internal void Start() => this._wsServer.Start();

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<WebSocketServer> _instance = new Lazy<WebSocketServer>(() => new WebSocketServer());

        /// <summary>
        /// A Dictionary of event handlers for newly connected clients requesting authorization. Key is URI path; value is List of <see cref="WebSocketClientConnectedEventHandler"/>.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<WebSocketClientConnectedEventHandler>> _connectedEventHandlers = new ConcurrentDictionary<string, List<WebSocketClientConnectedEventHandler>>();

        /// <summary>
        /// A dictionary mapping connections to the URI paths they are accessing. Key is IP:Port; value is URI path.
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _connectionPaths = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// A Dictionary of event handlers for disconnecting clients. Key is URI path; value is List of <see cref="WebSocketClientDisconnectedEventHandler"/>.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<WebSocketClientDisconnectedEventHandler>> _disconnectedEventHandlers = new ConcurrentDictionary<string, List<WebSocketClientDisconnectedEventHandler>>();

        /// <summary>
        /// A Dictionary of event handlers for incoming messages. Key is URI path; value is List of <see cref="WebSocketMessageReceivedEventHandler"/>.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<WebSocketMessageReceivedEventHandler>> _messageEventHandlers = new ConcurrentDictionary<string, List<WebSocketMessageReceivedEventHandler>>();

        /// <summary>
        /// The <see cref="WatsonWsServer"/> instance.
        /// </summary>
        private readonly WatsonWsServer _wsServer;

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Constructor. Sets up the <see cref="WatsonWsServer"/>.
        /// </summary>
        private WebSocketServer()
        {
            string listenerIp = Program.Settings.WSListenIp ?? "*";
            bool useSsl = Program.Settings.WSUseSSL;
            int listenerPort = Program.Settings.WSListenPort > 0 ? Program.Settings.WSListenPort : (useSsl ? 443 : 80);

            this._wsServer = new WatsonWsServer(listenerIp, listenerPort, useSsl)
            {
                ClientConnected = this.ClientConnected,
                ClientDisconnected = this.ClientDisconnected,
                MessageReceived = this.MessageReceived
            };
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Callback for when <see cref="WebSocketServer._wsServer"/> has an incoming connection.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that is connecting to the server.</param>
        /// <param name="request">The HttpListenerRequest used to open the connection.</param>
        /// <returns>A bool indicating if the connection should be accepted.</returns>
        private async Task<bool> ClientConnected(string ipPort, HttpListenerRequest request) => await Task.Run(() =>
        {
            bool authorized = false;

            string path = request.Url.AbsolutePath;

            OnWebSocketClientConnectedArgs e = new OnWebSocketClientConnectedArgs
            {
                IpPort = ipPort,
                Path = path,
                Request = request
            };

            foreach (WebSocketClientConnectedEventHandler d in this._connectedEventHandlers.GetValueOrDefault(path, new List<WebSocketClientConnectedEventHandler>()))
            {
                if (d.Invoke(this, e))
                {
                    authorized = true;
                }
            }

            if (authorized)
            {
                _ = this._connectionPaths.TryAdd(ipPort, path);
            }

            return authorized;
        }).ConfigureAwait(false);

        /// <summary>
        /// Callback for when a client disconnects from <see cref="WebSocketServer._wsServer"/>.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that is disconnecting from the server.</param>
        /// <returns></returns>
        private async Task ClientDisconnected(string ipPort) => await Task.Run(() =>
        {
            string path = this._connectionPaths.GetValueOrDefault(ipPort, "/");

            OnWebSocketClientDisconnectedArgs e = new OnWebSocketClientDisconnectedArgs
            {
                IpPort = ipPort,
                Path = path
            };

            foreach (WebSocketClientDisconnectedEventHandler d in this._disconnectedEventHandlers.GetValueOrDefault(path, new List<WebSocketClientDisconnectedEventHandler>()))
            {
                d.Invoke(this, e);
            }

            _ = this._connectionPaths.TryRemove(ipPort, out _);
        }).ConfigureAwait(false);

        /// <summary>
        /// Callback for when a client sends data to <see cref="WebSocketServer._wsServer"/>.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that has sent the message.</param>
        /// <param name="data">The data that was received.</param>
        /// <returns></returns>
        private async Task MessageReceived(string ipPort, byte[] data) => await Task.Run(() =>
        {
            string path = this._connectionPaths.GetValueOrDefault(ipPort, "/");

            OnWebSocketMessageReceivedArgs e = new OnWebSocketMessageReceivedArgs
            {
                IpPort = ipPort,
                Path = path,
                Data = (byte[])data.Clone()
            };

            foreach (WebSocketMessageReceivedEventHandler d in this._messageEventHandlers.GetValueOrDefault(path, new List<WebSocketMessageReceivedEventHandler>()))
            {
                d.Invoke(this, e);
            }
        }).ConfigureAwait(false);

        #endregion Private Methods
    }
}