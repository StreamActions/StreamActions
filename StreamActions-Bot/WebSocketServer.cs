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
using System.Net;
using System.Threading.Tasks;
using WatsonWebsocket;

namespace StreamActions
{
    /// <summary>
    /// Handles the WebSocket server for connections to the panel, other instances, and API clients.
    /// </summary>
    public class WebSocketServer : IDisposable
    {
        #region Public Properties

        /// <summary>
        /// Singleton of <see cref="WebSocketServer"/>.
        /// </summary>
        public static WebSocketServer Instance => _instance.Value;

        #endregion Public Properties

        #region Public Methods

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
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

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<WebSocketServer> _instance = new Lazy<WebSocketServer>(() => new WebSocketServer());

        private readonly WatsonWsServer _wsServer;

        #endregion Private Fields

        #region Private Constructors

        private WebSocketServer()
        {
            string listenerIp = "*";
            bool useSsl = false;
            int listenerPort;

            //TODO: Detect listenerIp, listenerPort, and useSsl options from settings file
            //TODO: Skip the next line if listenerPort is defined in the settings file and is not default
            listenerPort = useSsl ? 443 : 80;

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
            //TODO: Authenticate the connection and send an event if successful
            true).ConfigureAwait(false);

        /// <summary>
        /// Callback for when a client disconnects from <see cref="WebSocketServer._wsServer"/>.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that is disconnecting from the server.</param>
        /// <returns></returns>
        private async Task ClientDisconnected(string ipPort) => await Task.Run(() =>
        {
            //TODO: Send an event
        }).ConfigureAwait(false);

        /// <summary>
        /// Callback for when a client sends data to <see cref="WebSocketServer._wsServer"/>.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that has sent the message.</param>
        /// <param name="data">The data that was received.</param>
        /// <returns></returns>
        private async Task MessageReceived(string ipPort, byte[] data) => await Task.Run(() =>
        {
            //TODO: Process the message and send appropriate events
        }).ConfigureAwait(false);

        #endregion Private Methods
    }
}