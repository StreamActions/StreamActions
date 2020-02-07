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
using StreamActions.Enums;
using StreamActions.GraphQL.Connections;
using StreamActions.MemoryDocuments;
using System;
using System.Globalization;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// A document structure representing a command.
    /// </summary>
    public class CommandDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// When is this command available to be used.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(StreamStatus.Offline | StreamStatus.Online)]
        public StreamStatus AvailableWhen { get; set; }

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
        /// Contains information about the commands cooldowns.
        /// </summary>
        [BsonIgnore]
        public CommandCooldownDocument CommandCooldown => this._commandCooldown;

        /// <summary>
        /// Cost of the command if set.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public uint Cost { get; set; }

        /// <summary>
        /// The number of times the command has been ran.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public uint Count { get; set; }

        /// <summary>
        /// Global cooldown for the command.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(5)]
        [BsonRequired]
        public uint GlobalCooldown { get; set; }

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
        [BsonDefaultValue(true)]
        public bool IsEnabled { get; set; }

        /// <summary>
        /// If the command should be shown in the command list.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(true)]
        public bool IsVisible { get; set; }

        /// <summary>
        /// The response for this command.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        public string Response { get; set; }

        /// <summary>
        /// Per-user cooldown of the command.
        /// </summary>
        [BsonElement]
        [BsonIgnoreIfNull]
        public uint UserCooldown { get; set; }

        /// <summary>
        /// Permission required to run this command.
        /// </summary>
        [BsonElement]
        [BsonDefaultValue(UserLevel.Viewer)]
        [BsonRequired]
        public UserLevel UserLevel { get; set; }

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.Id.ToString("D", CultureInfo.InvariantCulture);

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="CommandCooldown"/> property.
        /// </summary>
        private readonly CommandCooldownDocument _commandCooldown = new CommandCooldownDocument();

        #endregion Private Fields
    }
}