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

using MongoDB.Bson;
using MongoDB.Driver;
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
                AllowInsecureTls = false,
                ApplicationName = "StreamActions-Bot",
                ConnectionMode = ConnectionMode.Automatic,
                ConnectTimeout = TimeSpan.FromSeconds(30),
                GuidRepresentation = GuidRepresentation.Standard,
                UseTls = Program.Settings.DBUseTLS
            };

            if (!(Program.Settings.DBHost is null))
            {
                settings.Server = Program.Settings.DBPort > 0
                    ? new MongoServerAddress(Program.Settings.DBHost, Program.Settings.DBPort)
                    : new MongoServerAddress(Program.Settings.DBHost);
            }

            if (!(Program.Settings.DBName is null || Program.Settings.DBUsername is null))
            {
                settings.Credential = MongoCredential.CreateCredential(Program.Settings.DBName, Program.Settings.DBUsername, Program.Settings.DBPassword);
            }

            this._mongoClient = new MongoClient(settings);
        }

        #endregion Private Constructors
    }
}