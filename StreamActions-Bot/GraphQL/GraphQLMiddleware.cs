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

using EntityGraphQL;
using EntityGraphQL.Schema;
using MongoDB.Driver;
using StreamActions.Database;
using StreamActions.Database.Documents.Users;
using StreamActions.EventArgs;
using System;
using System.Collections.Concurrent;
using System.Text.Json;

namespace StreamActions.GraphQL
{
    /// <summary>
    /// Middleware for <see cref="WebSocketServer"/> that handles GraphQL connections.
    /// </summary>
    public class GraphQLMiddleware
    {
        #region Public Properties

        /// <summary>
        /// An instance of <see cref="GraphQLMiddleware"/>.
        /// </summary>
        public static GraphQLMiddleware Instance => _instance.Value;

        /// <summary>
        /// The <see cref="SchemaProvider{TContextType, TArgType}"/> that has mapped out available fields.
        /// </summary>
        public SchemaProvider<GraphQLSchema, UserDocument> SchemaProvider => this._schemaProvider;

        #endregion Public Properties

        #region Private Fields

        /// <summary>
        /// Field that backs <see cref="Instance"/>.
        /// </summary>
        private static readonly Lazy<GraphQLMiddleware> _instance = new Lazy<GraphQLMiddleware>(() => new GraphQLMiddleware());

        /// <summary>
        /// The <see cref="GraphQLSchema"/>.
        /// </summary>
        private readonly GraphQLSchema _context = new GraphQLSchema();

        /// <summary>
        /// Field that backs <see cref="SchemaProvider"/>.
        /// </summary>
        private readonly SchemaProvider<GraphQLSchema, UserDocument> _schemaProvider;

        /// <summary>
        /// Contains a Dictionary of user contexts. Key is IpPort; value is <see cref="UserDocument.Id"/>.
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _userContext = new ConcurrentDictionary<string, string>();

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Constructor. Preps <see cref="_schemaProvider"/> and the event handlers for <see cref="WebSocketServer"/>.
        /// </summary>
        private GraphQLMiddleware()
        {
            this._schemaProvider = SchemaBuilder.FromObject<GraphQLSchema, UserDocument>();
            WebSocketServer.Instance.SubscribeClientConnectedEventHandler("graphql-ws", this.GraphQL_WebSocketClientConnectedEventHandler);
            WebSocketServer.Instance.SubscribeMessageReceivedEventHandler("graphql-ws", this.GraphQL_WebSocketMessageReceivedEventHandler);
            WebSocketServer.Instance.SubscribeClientDisconnectedEventHandler("graphql-ws", this.GraphQL_WebSocketClientDisconnectedEventHandler);
        }

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Handles authentication and authorization for incoming connections to the graphql-ws endpoint
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the client's request.</param>
        /// <returns><c>true</c> to allow the connection; <c>false</c> otherwise.</returns>
        private bool GraphQL_WebSocketClientConnectedEventHandler(object sender, OnWebSocketClientConnectedArgs e) =>
            //TODO: Authenticate client, place into _userContext as <ipPort, UserId>
            false;

        /// <summary>
        /// Handles disconnecting clients.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the disconnecting client.</param>
        private void GraphQL_WebSocketClientDisconnectedEventHandler(object sender, OnWebSocketClientDisconnectedArgs e) => _ = this._userContext.TryRemove(e.IpPort, out _);

        /// <summary>
        /// Handles incoming WebSocket messages from authenticated clients and transmits the result.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An object that contains the received data.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "JSON Error")]
        private async void GraphQL_WebSocketMessageReceivedEventHandler(object sender, OnWebSocketMessageReceivedArgs e)
        {
            if (this._userContext.ContainsKey(e.IpPort))
            {
                QueryResult result;
                try
                {
                    QueryRequest query = JsonSerializer.Deserialize<QueryRequest>(e.Data, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    });

                    IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");

                    FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Id.Equals(this._userContext[e.IpPort], StringComparison.OrdinalIgnoreCase));

                    if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) == 0)
                    {
                        throw new JsonException("invalid user");
                    }

                    using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

                    UserDocument userDocument = await cursor.SingleAsync().ConfigureAwait(false);

                    result = this._schemaProvider.ExecuteQuery(query, this._context, userDocument, null);
                }
                catch (JsonException ex)
                {
                    result = new QueryResult();
                    result.Errors.Add(new GraphQLError(ex.Message));
                }
                await WebSocketServer.Instance.SendAsync(e.IpPort, JsonSerializer.SerializeToUtf8Bytes(new GraphQLResult(result))).ConfigureAwait(false);
            }
        }

        #endregion Private Methods
    }
}