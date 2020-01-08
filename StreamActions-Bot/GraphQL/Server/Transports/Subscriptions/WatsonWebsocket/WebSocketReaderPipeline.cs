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

using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StreamActions.GraphQL.Server.Transports.Subscriptions.WatsonWebsocket
{
    public class WebSocketReaderPipeline : IReaderPipeline
    {
        #region Public Constructors

        public WebSocketReaderPipeline(string ipPort, JsonSerializerSettings serializerSettings)
        {
            this._ipPort = ipPort;
            this._serializerSettings = serializerSettings;

            this._startBlock = this.CreateMessageReader();
            this._endBlock = this.CreateReaderJsonTransformer();
            _ = this._startBlock.LinkTo(this._endBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
        }

        #endregion Public Constructors

        #region Public Properties

        public Task Completion => this._endBlock.Completion;

        #endregion Public Properties

        #region Public Methods

        public async Task Complete()
        {
            await Task.Run(() => WebSocketServer.Instance.DisconnectClient(this._ipPort)).ConfigureAwait(false);
            this._startBlock.Complete();
        }

        public void LinkTo(ITargetBlock<OperationMessage> target) => _ = this._endBlock.LinkTo(target, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        #endregion Public Methods

        #region Internal Methods

        internal async void EnqueueMessage(string message) => _ = await this._bufferBlock.SendAsync(message).ConfigureAwait(false);

        #endregion Internal Methods

        #region Protected Methods

        protected ISourceBlock<string> CreateMessageReader()
        {
            IPropagatorBlock<string, string> source = new BufferBlock<string>(
                new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = true,
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1
                });

            _ = Task.Run(async () => await this.ReadMessageAsync(source).ConfigureAwait(false));

            return source;
        }

        protected IPropagatorBlock<string, OperationMessage> CreateReaderJsonTransformer()
        {
            TransformBlock<string, OperationMessage> transformer = new TransformBlock<string, OperationMessage>(
                input => JsonConvert.DeserializeObject<OperationMessage>(input, this._serializerSettings),
                new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = true
                });

            return transformer;
        }

        #endregion Protected Methods

        #region Private Fields

        private readonly BufferBlock<string> _bufferBlock = new BufferBlock<string>();
        private readonly IPropagatorBlock<string, OperationMessage> _endBlock;
        private readonly string _ipPort;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly ISourceBlock<string> _startBlock;

        #endregion Private Fields

        #region Private Methods

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Default Catch from original source")]
        private async Task ReadMessageAsync(ITargetBlock<string> target)
        {
            while (WebSocketServer.Instance.IsClientConnected(this._ipPort))
            {
                string message = await this._bufferBlock.ReceiveAsync().ConfigureAwait(false);

                _ = await target.SendAsync(message).ConfigureAwait(false);
            }
        }

        #endregion Private Methods
    }
}