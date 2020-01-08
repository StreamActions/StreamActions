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
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StreamActions.GraphQL.Server.Transports.Subscriptions.WatsonWebsocket
{
    public class WebSocketWriterPipeline : IWriterPipeline
    {
        #region Public Constructors

        public WebSocketWriterPipeline(string ipPort, IDocumentWriter documentWriter)
        {
            this._ipPort = ipPort;
            this._documentWriter = documentWriter;

            this._startBlock = this.CreateMessageWriter();
        }

        #endregion Public Constructors

        #region Public Properties

        public Task Completion => this._startBlock.Completion;

        #endregion Public Properties

        #region Public Methods

        public Task Complete()
        {
            this._startBlock.Complete();
            return Task.CompletedTask;
        }

        public bool Post(OperationMessage message) => this._startBlock.Post(message);

        public Task SendAsync(OperationMessage message) => this._startBlock.SendAsync(message);

        #endregion Public Methods

        #region Private Fields

        private readonly IDocumentWriter _documentWriter;
        private readonly string _ipPort;
        private readonly ITargetBlock<OperationMessage> _startBlock;

        #endregion Private Fields

        #region Private Methods

        private ITargetBlock<OperationMessage> CreateMessageWriter()
        {
            ActionBlock<OperationMessage> target = new ActionBlock<OperationMessage>(
                this.WriteMessageAsync, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1,
                    EnsureOrdered = true
                });

            return target;
        }

        private async Task WriteMessageAsync(OperationMessage message)
        {
            if (!WebSocketServer.Instance.IsClientConnected(this._ipPort))
            {
                return;
            }

            WebsocketWriterStream stream = new WebsocketWriterStream(this._ipPort);
            try
            {
                await this._documentWriter.WriteAsync(stream, message)
                    .ConfigureAwait(false);
            }
            finally
            {
                await stream.FlushAsync().ConfigureAwait(false);
                stream.Dispose();
            }
        }

        #endregion Private Methods
    }
}