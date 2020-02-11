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

using Microsoft.IdentityModel.Tokens;
using StreamActions.EventArgs;
using StreamActions.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public async void DisconnectClient(string ipPort, WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string closeReason = "Goodbye")
        {
            if (this._clients.ContainsKey(ipPort))
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                await this._clients[ipPort].WebSocket.CloseAsync(closeStatus, closeReason, tokenSource.Token).ConfigureAwait(false);
                this._clients[ipPort].CancellationTokenSource.Cancel();
                tokenSource.Dispose();
                this._clients[ipPort].WebSocket.Dispose();
                this._clients[ipPort].TcpClient.Dispose();
                _ = this._clients.TryRemove(ipPort, out _);
            }
        }

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
        public bool IsClientConnected(string ipPort) => this._clients.ContainsKey(ipPort) && this._clients[ipPort].WebSocket.State == WebSocketState.Open;

        /// <summary>
        /// Determine whether or not the specified client is connected to the server on the specified URI path.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="path">The URI path to check.</param>
        /// <returns>Boolean indicating if the client is connected to the server.</returns>
        public bool IsClientConnected(string ipPort, string path) => this.IsClientConnected(ipPort) && this._clients[ipPort].RequestUri.OriginalString.StartsWith(path, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Send data to the specified client, asynchronously.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="data">String containing data.</param>
        public async Task SendAsync(string ipPort, string data)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            await this.SendAsync(ipPort, Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, tokenSource.Token).ConfigureAwait(false);
            tokenSource.Dispose();
        }

        /// <summary>
        /// Send data to the specified client, asynchronously.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="data">Byte array containing data.</param>
        public async Task SendAsync(string ipPort, byte[] data)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            await this.SendAsync(ipPort, data, WebSocketMessageType.Binary, true, tokenSource.Token).ConfigureAwait(false);
            tokenSource.Dispose();
        }

        /// <summary>
        /// Send data to the specified client, asynchronously.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="buffer">The buffer to be sent over the connection.</param>
        /// <param name="messageType">Indicates whether the application is sending a binary or text message.</param>
        /// <param name="endOfmessage">Indicates whether the data in <paramref name="buffer"/> is the last part of a message.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task SendAsync(string ipPort, ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfmessage, CancellationToken cancellationToken)
        {
            if (!this.IsClientConnected(ipPort))
            {
                return;
            }

            await this._clients[ipPort].WebSocket.SendAsync(buffer, messageType, endOfmessage, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the specified client, asynchronously.
        /// </summary>
        /// <param name="ipPort">IP:port of the recipient client.</param>
        /// <param name="buffer">The buffer to be sent over the connection.</param>
        /// <param name="messageType">Indicates whether the application is sending a binary or text message.</param>
        /// <param name="endOfmessage">Indicates whether the data in <paramref name="buffer"/> is the last part of a message.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task SendAsync(string ipPort, ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfmessage, CancellationToken cancellationToken)
        {
            if (!this.IsClientConnected(ipPort))
            {
                return;
            }

            await this._clients[ipPort].WebSocket.SendAsync(buffer, messageType, endOfmessage, cancellationToken).ConfigureAwait(false);
        }

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
                this._server.Stop();
                this._server.Dispose();
                this._tokenSource.Cancel();
            }
        }

        #endregion Protected Methods

        #region Internal Methods

        /// <summary>
        /// Generates a JWT Bearer token.
        /// </summary>
        /// <param name="principal">A ClaimsPrincipal representing the user.</param>
        /// <param name="notBefore">The validity start timestamp.</param>
        /// <param name="expires">The validity expiration timestamp.</param>
        /// <param name="signingCredentials">Credentials for signing the token.</param>
        /// <returns>A JWT token.</returns>
        internal static string GenerateBearer(ClaimsPrincipal principal, DateTime? notBefore = null, DateTime? expires = null, SigningCredentials signingCredentials = null)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtPayload payload = new JwtPayload(typeof(Program).Assembly.GetName().FullName + "/" + typeof(Program).Assembly.GetName().Version.ToString(),
                Program.Settings.BotLogin, principal.Claims, notBefore, expires);
            JwtHeader header = new JwtHeader(signingCredentials);
            JwtSecurityToken token = new JwtSecurityToken(header, payload);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        internal void Start() => Task.Run(this.AcceptConnections, this._token);

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<WebSocketServer> _instance = new Lazy<WebSocketServer>(() => new WebSocketServer());

        private readonly ConcurrentDictionary<string, HttpServerWebSocketContext> _clients = new ConcurrentDictionary<string, HttpServerWebSocketContext>();

        /// <summary>
        /// A Dictionary of event handlers for newly connected clients requesting authorization. Key is URI path; value is List of <see cref="WebSocketClientConnectedEventHandler"/>.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<WebSocketClientConnectedEventHandler>> _connectedEventHandlers = new ConcurrentDictionary<string, List<WebSocketClientConnectedEventHandler>>();

        /// <summary>
        /// A Dictionary of event handlers for disconnecting clients. Key is URI path; value is List of <see cref="WebSocketClientDisconnectedEventHandler"/>.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<WebSocketClientDisconnectedEventHandler>> _disconnectedEventHandlers = new ConcurrentDictionary<string, List<WebSocketClientDisconnectedEventHandler>>();

        /// <summary>
        /// A Dictionary of event handlers for incoming messages. Key is URI path; value is List of <see cref="WebSocketMessageReceivedEventHandler"/>.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<WebSocketMessageReceivedEventHandler>> _messageEventHandlers = new ConcurrentDictionary<string, List<WebSocketMessageReceivedEventHandler>>();

        /// <summary>
        /// The <see cref="HttpServer"/> that handles incoming connections.
        /// </summary>
        private readonly HttpServer _server;

        /// <summary>
        /// A CancellationToken for shutting down the Accept loop.
        /// </summary>
        private readonly CancellationToken _token;

        /// <summary>
        /// A CancellationTokenSource for shutting down the Accept loop.
        /// </summary>
        private readonly CancellationTokenSource _tokenSource;

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Constructor. Sets up the <see cref="HttpServer"/>.
        /// </summary>
        private WebSocketServer()
        {
            string listenerIp = Program.Settings.WSListenIp ?? "*";
            bool useSsl = Program.Settings.WSUseSsl;
            int listenerPort = Program.Settings.WSListenPort > 0 ? Program.Settings.WSListenPort : (useSsl ? 443 : 80);
            string sslCertFile = useSsl ? Program.Settings.WSSslCert : null;

            this._server = new HttpServer(listenerIp, listenerPort, useSsl, sslCertFile);

            this._tokenSource = new CancellationTokenSource();
            this._token = this._tokenSource.Token;
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Accepts connections from <see cref="_server"/>.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed in task and disconnect members")]
        private async void AcceptConnections()
        {
            try
            {
                this._server.Start();

                while (!this._token.IsCancellationRequested)
                {
                    HttpServerRequestMessage request = await this._server.WaitForRequestAsync().ConfigureAwait(false);

                    if (!HttpServer.IsWebSocketUpgradeRequest(request))
                    {
                        await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, HttpStatusCode.NotFound, Array.Empty<byte>()).ConfigureAwait(false);
                        request.Dispose();
                        continue;
                    }

                    IPEndPoint remoteEndpoint = (IPEndPoint)request.TcpClient.Client.RemoteEndPoint;
                    string ipPort = remoteEndpoint.Address.ToString() + ":" + remoteEndpoint.Port;

                    CancellationTokenSource killTokenSource = new CancellationTokenSource();
                    CancellationToken killToken = killTokenSource.Token;

                    _ = Task.Run(() => _ = Task.Run(async () =>
                    {
                        if (!await this.ClientConnected(ipPort, request).ConfigureAwait(false))
                        {
                            if (request.Stream.CanWrite)
                            {
                                await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, HttpStatusCode.Forbidden, Array.Empty<byte>()).ConfigureAwait(false);
                            }
                            request.Dispose();
                            return;
                        }

                        HttpServerWebSocketContext context = await WebSocketUpgradeHandler.UpgradeWebSocketRequest(request).ConfigureAwait(false);
                        context.CancellationTokenSource = killTokenSource;
                        context.CancellationToken = killToken;

                        if (context is null)
                        {
                            request.Dispose();
                            killTokenSource.Dispose();
                            return;
                        }

                        if (!this._clients.TryAdd(ipPort, context))
                        {
                            CancellationTokenSource tokenSource = new CancellationTokenSource();
                            await context.WebSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Unable to store client context", tokenSource.Token).ConfigureAwait(false);
                            tokenSource.Dispose();
                            context.WebSocket.Dispose();
                            context.TcpClient.Dispose();
                            killTokenSource.Dispose();
                            return;
                        }

                        await this.DataReceiver(ipPort, context, killToken).ConfigureAwait(false);

                        await this.ClientDisconnected(ipPort).ConfigureAwait(false);
                        this._clients[ipPort].WebSocket.Dispose();
                        this._clients[ipPort].TcpClient.Dispose();
                        _ = this._clients.TryRemove(ipPort, out _);
                        killTokenSource.Dispose();
                    }, killToken), this._token);
                }
            }
            finally
            {
                foreach (string ipPort in this._clients.Keys)
                {
                    this.DisconnectClient(ipPort, WebSocketCloseStatus.EndpointUnavailable, "Server shutting down");
                }

                this._server.Dispose();
            }
        }

        /// <summary>
        /// Callback for when the server has an incoming connection.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that is connecting to the server.</param>
        /// <param name="request">The <see cref="HttpServerRequestMessage"/> used to open the connection.</param>
        /// <returns>A bool indicating if the connection should be accepted.</returns>
        private async Task<bool> ClientConnected(string ipPort, HttpServerRequestMessage request) => await Task.Run(() =>
        {
            bool authorized = false;

            OnWebSocketClientConnectedArgs e = new OnWebSocketClientConnectedArgs
            {
                IpPort = ipPort,
                Path = request.RequestUri.AbsolutePath,
                Request = new WeakReference<HttpServerRequestMessage>(request)
            };

            foreach (WebSocketClientConnectedEventHandler d in this._connectedEventHandlers.GetValueOrDefault(request.RequestUri.AbsolutePath, new List<WebSocketClientConnectedEventHandler>()))
            {
                if (d.Invoke(this, e))
                {
                    authorized = true;
                }
            }

            return authorized;
        }).ConfigureAwait(false);

        /// <summary>
        /// Callback for when a client disconnects from the server.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that is disconnecting from the server.</param>
        /// <returns>A Task that can be awaited.</returns>
        private async Task ClientDisconnected(string ipPort) => await Task.Run(() =>
        {
            if (this._clients.ContainsKey(ipPort))
            {
                OnWebSocketClientDisconnectedArgs e = new OnWebSocketClientDisconnectedArgs
                {
                    IpPort = ipPort,
                    Path = this._clients[ipPort].RequestUri.AbsolutePath
                };

                foreach (WebSocketClientDisconnectedEventHandler d in this._disconnectedEventHandlers.GetValueOrDefault(this._clients[ipPort].RequestUri.AbsolutePath,
                    new List<WebSocketClientDisconnectedEventHandler>()))
                {
                    d.Invoke(this, e);
                }

                _ = this._clients.TryRemove(ipPort, out _);
            }
        }).ConfigureAwait(false);

        /// <summary>
        /// Receive loop for a client.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> of the client.</param>
        /// <param name="context">The <see cref="HttpServerWebSocketContext"/> of the client.</param>
        /// <param name="cancellationToken">A CancellationToken for shutting down the event loop.</param>
        /// <returns>A Task that can be awaited.</returns>
        private async Task DataReceiver(string ipPort, HttpServerWebSocketContext context, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] message;
                    using (MemoryStream dataStream = new MemoryStream())
                    {
                        byte[] buffer = new byte[16384];
                        ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
                        while (context.WebSocket.State == WebSocketState.Open)
                        {
                            WebSocketReceiveResult result = await context.WebSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                return;
                            }

                            dataStream.Write(buffer, 0, result.Count);

                            if (result.EndOfMessage)
                            {
                                break;
                            }
                        }

                        message = dataStream.ToArray();
                    }

                    if (message.Length > 0)
                    {
                        _ = Task.Run(() => this.MessageReceived(ipPort, message));
                    }
                    else
                    {
                        await Task.Delay(30, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Callback for when a client sends data to the server.
        /// </summary>
        /// <param name="ipPort">The remote <c>IP:Port</c> that has sent the message.</param>
        /// <param name="data">The data that was received.</param>
        /// <returns>A Task that can be awaited.</returns>
        private async Task MessageReceived(string ipPort, byte[] data) => await Task.Run(() =>
        {
            if (this._clients.ContainsKey(ipPort))
            {
                OnWebSocketMessageReceivedArgs e = new OnWebSocketMessageReceivedArgs
                {
                    IpPort = ipPort,
                    Path = this._clients[ipPort].RequestUri.AbsolutePath,
                    Data = (byte[])data.Clone(),
                    Context = new WeakReference<HttpServerWebSocketContext>(this._clients[ipPort])
                };

                foreach (WebSocketMessageReceivedEventHandler d in this._messageEventHandlers.GetValueOrDefault(this._clients[ipPort].RequestUri.AbsolutePath,
                    new List<WebSocketMessageReceivedEventHandler>()))
                {
                    d.Invoke(this, e);
                }
            }
        }).ConfigureAwait(false);

        #endregion Private Methods
    }
}