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

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using StreamActions.Database.Documents.Users;
using StreamActions.Enums;
using StreamActions.GraphQL.Connections;
using System;
using System.Globalization;

namespace StreamActions.Database.Documents.Commands
{
    /// <summary>
    /// A document structure representing a command.
    /// </summary>
    public class CommandDocument : ICursorable, IDocument
    {
        #region Public Properties

        /// <summary>
        /// When is this command available to be used.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(StreamStatuses.Offline | StreamStatuses.Online)]
        public StreamStatuses AvailableWhen { get; set; }

        /// <summary>
        /// The <see cref="UserDocument.Id"/> of the channel this command belongs to.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string ChannelId { get; set; }

        /// <summary>
        /// The name and trigger for this command.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string Command { get; set; }

        /// <summary>
        /// The cooldowns for this command.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public CommandCooldownDocument Cooldowns { get; set; }

        /// <summary>
        /// Cost of the command.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(0)]
        public uint Cost { get; set; }

        /// <summary>
        /// The number of times the command has been ran.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(0)]
        public uint Count { get; set; }

        /// <summary>
        /// The Guid for referencing this command in the database.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public Guid Id { get; set; }

        /// <summary>
        /// If the command is enabled or not.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool IsEnabled { get; set; }

        /// <summary>
        /// If the command should be shown in the command list.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(true)]
        public bool IsVisible { get; set; }

        /// <summary>
        /// If this is a whisper command.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfDefault]
        [BsonDefaultValue(false)]
        public bool IsWhisperCommand { get; set; }

        /// <summary>
        /// The response for this command.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public string Response { get; set; }

        /// <summary>
        /// Permission required to run this command.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public UserLevels UserLevel { get; set; }

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.Id.ToString("D", CultureInfo.InvariantCulture);

        public async void Initialize()
        {
            IMongoCollection<CommandDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>("commands");
            IndexKeysDefinitionBuilder<CommandDocument> indexBuilder = Builders<CommandDocument>.IndexKeys;

            try
            {
                CreateIndexModel<CommandDocument> indexModel = new CreateIndexModel<CommandDocument>(indexBuilder.Ascending(d => d.ChannelId).Ascending(d => d.IsWhisperCommand).Ascending(d => d.Command),
                    new CreateIndexOptions { Name = "CommandDocument_unique_ChannelId-Command", Unique = true });
                _ = await collection.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
            }
            catch (MongoWriteConcernException)
            { }
        }

        #endregion Public Methods
    }
}