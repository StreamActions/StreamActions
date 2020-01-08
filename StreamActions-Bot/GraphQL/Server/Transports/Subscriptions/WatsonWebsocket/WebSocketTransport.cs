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

/*
 * Copied from https://github.com/graphql-dotnet/server
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2017 Pekka Heikura
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 * IN THE SOFTWARE.
 */

using GraphQL.Http;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace StreamActions.GraphQL.Server.Transports.Subscriptions.WatsonWebsocket
{
    public class WebSocketTransport : IMessageTransport
    {
        #region Public Constructors

        public WebSocketTransport(string ipPort, IDocumentWriter documentWriter)
        {
            this._ipPort = ipPort;
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF'Z'",
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            this.Reader = new WebSocketReaderPipeline(this._ipPort, serializerSettings);
            this.Writer = new WebSocketWriterPipeline(this._ipPort, documentWriter);
        }

        #endregion Public Constructors

        #region Public Properties

        public WebSocketCloseStatus? CloseStatus => WebSocketServer.Instance.IsClientConnected(this._ipPort) ? WebSocketCloseStatus.Empty : WebSocketCloseStatus.NormalClosure;
        public IReaderPipeline Reader { get; }
        public IWriterPipeline Writer { get; }

        #endregion Public Properties

        #region Public Methods

        public Task CloseAsync()
        {
            WebSocketServer.Instance.DisconnectClient(this._ipPort);
            return Task.CompletedTask;
        }

        #endregion Public Methods

        #region Private Fields

        private readonly string _ipPort;

        #endregion Private Fields
    }
}