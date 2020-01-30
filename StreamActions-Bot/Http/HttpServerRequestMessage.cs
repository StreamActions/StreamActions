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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Principal;

namespace StreamActions.Http
{
    public class HttpServerRequestMessage : HttpRequestMessage
    {
        #region Internal Constructors

        internal HttpServerRequestMessage() : base()
        {
        }

        internal HttpServerRequestMessage(HttpMethod method, string requestUri) : base(method, requestUri)
        {
        }

        internal HttpServerRequestMessage(HttpMethod method, Uri requestUri) : base(method, requestUri)
        {
        }

        #endregion Internal Constructors

        #region Internal Properties

        internal CookieCollection CookieCollection { get; set; }
        internal Stream Stream { get; set; }
        internal TcpClient TcpClient { get; set; }
        internal IPrincipal User { get; set; }

        #endregion Internal Properties
    }
}