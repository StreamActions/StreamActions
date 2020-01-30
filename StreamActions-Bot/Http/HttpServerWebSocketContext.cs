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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Principal;
using System.Threading;

namespace StreamActions.Http
{
    public class HttpServerWebSocketContext : WebSocketContext
    {
        #region Public Properties

        public CancellationToken CancellationToken { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public override CookieCollection CookieCollection => this._cookieCollection;
        public override NameValueCollection Headers => this._headers;
        public override bool IsAuthenticated => this._isAuthenticated;
        public override bool IsLocal => this._isLocal;
        public override bool IsSecureConnection => this._isSecureConnection;
        public override string Origin => this._origin;
        public override Uri RequestUri => this._requestUri;
        public override string SecWebSocketKey => this._secWebSocketKey;
        public override IEnumerable<string> SecWebSocketProtocols => this._secWebSocketProtocols;
        public override string SecWebSocketVersion => this._secWebSocketVersion;
        public TcpClient TcpClient => this._tcpClient;
        public override IPrincipal User => this._user;
        public override WebSocket WebSocket => this._webSocket;

        #endregion Public Properties

        #region Internal Constructors

        internal HttpServerWebSocketContext(Uri requestUri, NameValueCollection headers, CookieCollection cookieCollection, IPrincipal user, bool isAuthenticated,
            bool isLocal, bool isSecureConnection, string origin, IEnumerable<string> secWebSocketProtocols, string secWebSocketVersion, string secWebSocketKey, WebSocket webSocket)
        {
            this._cookieCollection = new CookieCollection
            {
                cookieCollection
            };

            this._headers = new NameValueCollection(headers);
            this._user = CopyPrincipal(user);

            this._requestUri = requestUri;
            this._isAuthenticated = isAuthenticated;
            this._isLocal = isLocal;
            this._isSecureConnection = isSecureConnection;
            this._origin = origin;
            this._secWebSocketProtocols = secWebSocketProtocols;
            this._secWebSocketVersion = secWebSocketVersion;
            this._secWebSocketKey = secWebSocketKey;
            this._webSocket = webSocket;
        }

        internal HttpServerWebSocketContext(Uri requestUri, NameValueCollection headers, CookieCollection cookieCollection, IPrincipal user, bool isAuthenticated,
            bool isLocal, bool isSecureConnection, string origin, IEnumerable<string> secWebSocketProtocols, string secWebSocketVersion, string secWebSocketKey, WebSocket webSocket,
            TcpClient client) : this(requestUri, headers, cookieCollection, user, isAuthenticated, isLocal, isSecureConnection, origin, secWebSocketProtocols, secWebSocketVersion, secWebSocketKey,
            webSocket) => this._tcpClient = client;

        #endregion Internal Constructors

        #region Private Fields

        private readonly CookieCollection _cookieCollection;
        private readonly NameValueCollection _headers;
        private readonly bool _isAuthenticated;
        private readonly bool _isLocal;
        private readonly bool _isSecureConnection;
        private readonly string _origin;
        private readonly Uri _requestUri;
        private readonly string _secWebSocketKey;
        private readonly IEnumerable<string> _secWebSocketProtocols;
        private readonly string _secWebSocketVersion;
        private readonly TcpClient _tcpClient;
        private readonly IPrincipal _user;
        private readonly WebSocket _webSocket;

        #endregion Private Fields

        #region Private Methods

        private static IPrincipal CopyPrincipal(IPrincipal user)
        {
            if (user != null)
            {
                if (user is GenericPrincipal)
                {
                    // AuthenticationSchemes.Basic.
                    if (user.Identity is HttpServerBasicIdentity basicIdentity)
                    {
                        return new GenericPrincipal(new HttpServerBasicIdentity(basicIdentity.Name, basicIdentity.Password), null);
                    }
                }
            }

            return null;
        }

        #endregion Private Methods
    }
}