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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace StreamActions.Http
{
    /// <summary>
    /// Handles the WebSocket Upgrade handshake.
    /// </summary>
    internal static class WebSocketUpgradeHandler
    {
        #region Internal Methods

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "RFC6455")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Upstream type is responsible for this")]
        internal static async Task<HttpServerWebSocketContext> UpgradeWebSocketRequest(HttpServerRequestMessage request, string selectedSubProtocol = null, Dictionary<string, List<string>> httpHeaders = null)
        {
            if (httpHeaders is null)
            {
                httpHeaders = new Dictionary<string, List<string>>();
            }
            else
            {
                _ = httpHeaders.Remove("Connection");
                _ = httpHeaders.Remove("Upgrade");
                _ = httpHeaders.Remove("Sec-WebSocket-Accept");
                _ = httpHeaders.Remove("Sec-WebSocket-Protocol");
            }

            if (!ValidateWebSocketUpgradeRequest(request))
            {
                httpHeaders.Clear();
                HttpStatusCode statusCode = HttpStatusCode.BadRequest;

                if (HttpServer.IsWebSocketUpgradeRequest(request) && request.Headers.Contains("Sec-WebSocket-Version") && request.Headers.GetValues("Sec-WebSocket-Version").Count() == 1
                        && request.Headers.GetValues("Sec-WebSocket-Version").Single() != "13")
                {
                    statusCode = HttpStatusCode.UpgradeRequired;
                    httpHeaders.Add("Sec-WebSocket-Version", new List<string> { "13" });
                }

                await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, statusCode, Array.Empty<byte>(), httpHeaders).ConfigureAwait(false);
                request.Dispose();

                return null;
            }

            SHA1 sha1 = SHA1.Create();
            string swk = Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(request.Headers.GetValues("Sec-WebSocket-Key").Single() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            sha1.Dispose();

            httpHeaders.Add("Connection", new List<string> { "Upgrade" });
            httpHeaders.Add("Upgrade", new List<string> { "websocket" });
            httpHeaders.Add("Sec-WebSocket-Accept", new List<string> { swk });

            if (!(selectedSubProtocol is null))
            {
                httpHeaders.Add("Sec-WebSocket-Protocol", new List<string> { selectedSubProtocol });
            }

            await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, HttpStatusCode.SwitchingProtocols, Array.Empty<byte>(), httpHeaders, false).ConfigureAwait(false);

            WebSocket webSocket = WebSocket.CreateFromStream(request.Stream, true, selectedSubProtocol, _keepAliveInterval);

            WebHeaderCollection requestHeaders = new WebHeaderCollection();

            foreach (KeyValuePair<string, IEnumerable<string>> kvp in request.Headers)
            {
                foreach (string value in kvp.Value)
                {
                    requestHeaders.Add(kvp.Key, value);
                }
            }

            IPEndPoint remoteEndpoint = (IPEndPoint)request.TcpClient.Client.RemoteEndPoint;
            IPEndPoint localEndpoint = (IPEndPoint)request.TcpClient.Client.LocalEndPoint;

            return new HttpServerWebSocketContext(request.RequestUri, requestHeaders, request.CookieCollection, request.User, request.User.Identity.IsAuthenticated,
                localEndpoint.Address.Equals(remoteEndpoint.Address), request.Stream is SslStream, requestHeaders.Get("Origin"),
                requestHeaders.Get("Sec-WebSocket-Protocol").Split(',').Select(s => s.Trim()), "13", requestHeaders.Get("Sec-WebSocket-Key"), webSocket, request.TcpClient);
        }

        internal static bool ValidateWebSocketUpgradeRequest(HttpServerRequestMessage request) => request.Method == HttpMethod.Get && request.Headers.Contains("Upgrade")
            && request.Headers.GetValues("Upgrade").Count() == 1 && request.Headers.GetValues("Upgrade").Single() == "websocket" && request.Headers.Contains("Connection")
            && request.Headers.GetValues("Connection").Count() == 1 && request.Headers.GetValues("Connection").Single() == "Upgrade" && request.Headers.Contains("Sec-WebSocket-Key")
            && request.Headers.GetValues("Sec-WebSocket-Key").Count() == 1 && Convert.FromBase64String(request.Headers.GetValues("Sec-WebSocket-Key").Single()).Length == 16
            && request.Headers.Contains("Sec-WebSocket-Version") && request.Headers.GetValues("Sec-WebSocket-Version").Count() == 1
            && request.Headers.GetValues("Sec-WebSocket-Version").Single() == "13";

        #endregion Internal Methods

        #region Private Fields

        private static readonly TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(30);

        #endregion Private Fields
    }
}