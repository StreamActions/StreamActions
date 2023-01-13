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

using System;

namespace StreamActions.Database
{
    /// <summary>
    /// Handles connections to the database.
    /// </summary>
    public class DatabaseClient
    {
        #region Public Properties

        /// <summary>
        /// Singleton of <see cref="DatabaseClient"/>.
        /// </summary>
        public static DatabaseClient Instance => _instance.Value;

        /// <summary>
        /// The <see cref="MongoDatabase"/>.
        /// </summary>
        public IMongoDatabase MongoDatabase => this._mongoClient.GetDatabase("streamactions_bot");

        #endregion Public Properties

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<DatabaseClient> _instance = new Lazy<DatabaseClient>(() => new DatabaseClient());

        /// <summary>
        /// The <see cref="MongoClient"/>.
        /// </summary>
        private readonly MongoClient _mongoClient;

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        private DatabaseClient()
        {
            MongoClientSettings settings = new MongoClientSettings
            {
                AllowInsecureTls = Program.Settings.DBAllowInsecureTls,
                ApplicationName = "StreamActions",
                ConnectionMode = ConnectionMode.Automatic,
                ConnectTimeout = TimeSpan.FromSeconds(30),
                GuidRepresentation = GuidRepresentation.Standard,
                UseTls = Program.Settings.DBUseTls
            };

            if (!string.IsNullOrWhiteSpace(Program.Settings.DBHost))
            {
                settings.Server = Program.Settings.DBPort > 0
                    ? new MongoServerAddress(Program.Settings.DBHost, Program.Settings.DBPort)
                    : new MongoServerAddress(Program.Settings.DBHost);
            }

            if (!string.IsNullOrWhiteSpace(Program.Settings.DBName) || !string.IsNullOrWhiteSpace(Program.Settings.DBUsername))
            {
                settings.Credential = MongoCredential.CreateCredential(Program.Settings.DBName, Program.Settings.DBUsername, Program.Settings.DBPassword);
            }

            this._mongoClient = new MongoClient(settings);
        }

        #endregion Private Constructors
    }
}
