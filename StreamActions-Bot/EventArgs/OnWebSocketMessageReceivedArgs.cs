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

using StreamActions.Http;
using System;
using System.Text;

namespace StreamActions.EventArgs
{
    /// <summary>
    /// Represents a WebSocket message received event.
    /// </summary>
    public class OnWebSocketMessageReceivedArgs
    {
        #region Public Properties

        /// <summary>
        /// A WeakReference to the <see cref="HttpServerWebSocketContext"/> of the client.
        /// </summary>
        public WeakReference<HttpServerWebSocketContext> Context { get; internal set; }

        /// <summary>
        /// The raw data received by the WebSocket.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// The data received by the WebSocket, converted to a UTF8-encoded string.
        /// </summary>
        public string DataAsString => Encoding.UTF8.GetString(this.Data);

        /// <summary>
        /// The IP:Port combination that identifies the connection sending the data.
        /// </summary>
        public string IpPort { get; set; }

        /// <summary>
        /// The URI path that is being accessed.
        /// </summary>
        public string Path { get; set; }

        #endregion Public Properties
    }
}