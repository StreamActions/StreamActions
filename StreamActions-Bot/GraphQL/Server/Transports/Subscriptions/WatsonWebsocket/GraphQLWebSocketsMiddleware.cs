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

using GraphQL.Server.Internal;
using GraphQL.Http;
using StreamActions.EventArgs;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Microsoft.Extensions.Logging;
using GraphQL;
using GraphQL.Server;
using GraphQL.Execution;
using System.Linq;
using GraphQL.Validation;
using Microsoft.Extensions.Options;
using System;

namespace StreamActions.GraphQL.Server.Transports.Subscriptions.WatsonWebsocket
{
    public class GraphQLWebSocketsMiddleware : System.IDisposable
    {
        #region Public Constructors

        public GraphQLWebSocketsMiddleware()
        {
            List<IOperationMessageListener> messageListeners = new List<IOperationMessageListener>();
            this._connectionFactory = new WebSocketConnectionFactory<StreamActionsSchema>(new LoggerFactory(),
                new DefaultGraphQLExecuter<StreamActionsSchema>(this._schema,
                                                                new DocumentExecuter(),
                                                                Options.Create(new GraphQLOptions()),
                                                                Enumerable.Empty<IDocumentExecutionListener>(),
                                                                Enumerable.Empty<IValidationRule>()),
                messageListeners, new DocumentWriter());

            WebSocketServer.Instance.SubscribeClientConnectedEventHandler("graphql-ws", this.GraphQL_WebSocketClientConnected);
            WebSocketServer.Instance.SubscribeMessageReceivedEventHandler("graphql-ws", this.GraphQL_WebSocketMessageReceived);
            WebSocketServer.Instance.SubscribeClientDisconnectedEventHandler("graphql-ws", this.GraphQL_WebSocketClientDisconnected);
        }

        #endregion Public Constructors

        #region Public Methods

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._schema.Dispose();
            }
        }

        #endregion Protected Methods

        #region Private Fields

        private readonly ConcurrentDictionary<string, WebSocketConnection> _clients = new ConcurrentDictionary<string, WebSocketConnection>();
        private readonly WebSocketConnectionFactory<StreamActionsSchema> _connectionFactory;
        private readonly StreamActionsSchema _schema = new StreamActionsSchema();

        #endregion Private Fields

        #region Private Methods

        private bool GraphQL_WebSocketClientConnected(object sender, OnWebSocketClientConnectedArgs e)
        {
            WebSocketConnection connection = this._connectionFactory.CreateConnection(e.IpPort, e.IpPort + e.Path);
            _ = connection.Connect();
            return this._clients.TryAdd(e.IpPort, connection);
        }

        private void GraphQL_WebSocketClientDisconnected(object sender, OnWebSocketClientDisconnectedArgs e) => _ = this._clients.TryRemove(e.IpPort, out _);

        private void GraphQL_WebSocketMessageReceived(object sender, OnWebSocketMessageReceivedArgs e)
        {
            if (this._clients.TryGetValue(e.IpPort, out WebSocketConnection connection))
            {
                ((WebSocketReaderPipeline)connection._transport.Reader).EnqueueMessage(e.DataAsString);
            }
        }

        #endregion Private Methods
    }
}