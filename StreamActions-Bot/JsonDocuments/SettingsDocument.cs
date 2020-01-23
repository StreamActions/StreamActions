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

using System.Collections.Generic;

namespace StreamActions.JsonDocuments
{
    /// <summary>
    /// A document containing the bot's settings.
    /// </summary>
    internal class SettingsDocument
    {
        #region Internal Properties

        /// <summary>
        /// The bot's Twitch login name.
        /// </summary>
        internal string BotLogin { get; set; }

        /// <summary>
        /// The bot's Twitch OAuth token.
        /// </summary>
        internal string BotOAuth { get; set; }

        /// <summary>
        /// The channels the bot should join.
        /// </summary>
        internal List<string> ChannelsToJoin { get; } = new List<string>();

        /// <summary>
        /// The prefix character that identifies chat commands. <c>(char)0</c> to use the default.
        /// </summary>
        internal char ChatCommandIdentifier { get; set; }

        /// <summary>
        /// The IP address of the database host. <c>null</c> to use the default on localhost.
        /// </summary>
        internal string DBHost { get; set; }

        /// <summary>
        /// The name of the Database to use. <c>null</c> to use the default.
        /// </summary>
        internal string DBName { get; set; }

        /// <summary>
        /// The password for the database. <c>null</c> for no authentication.
        /// </summary>
        internal string DBPassword { get; set; }

        /// <summary>
        /// The port of the database host. <c>0</c> to use the default.
        /// </summary>
        internal int DBPort { get; set; }

        /// <summary>
        /// The username for the database. <c>null</c> for no authentication.
        /// </summary>
        internal string DBUsername { get; set; }

        /// <summary>
        /// Whether the database connection should use TLS.
        /// </summary>
        internal bool DBUseTLS { get; set; }

        /// <summary>
        /// The global culture. <c>null</c> to use the default.
        /// </summary>
        internal string GlobalCulture { get; set; }

        /// <summary>
        /// The Client ID for the Twitch developer app.
        /// </summary>
        internal string TwitchClientId { get; set; }

        /// <summary>
        /// The prefix character that identifies whisper commands. <c>(char)0</c> to use the default.
        /// </summary>
        internal char WhisperCommandIdentifier { get; set; }

        /// <summary>
        /// The IP address the WebSocket server should listen on. <c>null</c> to use the default or <c>""</c> (empty string) to limit to localhost.
        /// </summary>
        internal string WSListenIp { get; set; }

        /// <summary>
        /// The port the WebSocket server should listen on. <c>0</c> to use the default (80 for no ssl, 443 for ssl).
        /// </summary>
        internal int WSListenPort { get; set; }

        /// <summary>
        /// Whether the WebSocket server should use SSL.
        /// </summary>
        internal bool WSUseSSL { get; set; }

        #endregion Internal Properties
    }
}