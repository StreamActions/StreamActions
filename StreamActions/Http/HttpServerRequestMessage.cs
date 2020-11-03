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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Principal;

namespace StreamActions.Http
{
    /// <summary>
    /// Represents a HTTP request message.
    /// </summary>
    public class HttpServerRequestMessage : HttpRequestMessage
    {
        #region Internal Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        internal HttpServerRequestMessage() : base()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="method">The method of the HTTP request.</param>
        /// <param name="requestUri">The Uri requested.</param>
        internal HttpServerRequestMessage(HttpMethod method, string requestUri) : base(method, requestUri)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="method">The method of the HTTP request.</param>
        /// <param name="requestUri">The Uri requested.</param>
        internal HttpServerRequestMessage(HttpMethod method, Uri requestUri) : base(method, requestUri)
        {
        }

        #endregion Internal Constructors

        #region Internal Properties

        /// <summary>
        /// The cookies passed to the request.
        /// </summary>
        public CookieCollection CookieCollection { get; internal set; }

        /// <summary>
        /// Indicates if the request came from the local host.
        /// </summary>
        public bool IsLocal { get; internal set; }

        /// <summary>
        /// A collection of post parameters from the request.
        /// </summary>
        public Dictionary<string, List<string>> PostParams { get; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// A collection of query parameters from the request.
        /// </summary>
        public Dictionary<string, List<string>> QueryParams { get; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// The underlying Stream of the request.
        /// </summary>
        public Stream Stream { get; internal set; }

        /// <summary>
        /// The TcpClient of the request.
        /// </summary>
        public TcpClient TcpClient { get; internal set; }

        /// <summary>
        /// A ClaimsPrincipal representing a user the client is authenticating as; <c>null</c> if no authentication was attempted.
        /// </summary>
        public IPrincipal User { get; internal set; }

        #endregion Internal Properties
    }
}