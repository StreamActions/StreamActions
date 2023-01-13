/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using StreamActions.Common.Interfaces;
using StreamActions.Database.Documents.Users;
using StreamActions.Enums;
using System;

namespace StreamActions.Database.Documents.Commands
{
    /// <summary>
    /// A document structure representing a command.
    /// </summary>
    public class CommandDocument : IDocument
    {
        #region Public Properties

        /// <summary>
        /// The name of the collection that holds documents of this type.
        /// </summary>
        [BsonIgnore]
        public static string CollectionName => "commands";

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

        public async void Initialize()
        {
            IMongoCollection<CommandDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>(CollectionName);
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
