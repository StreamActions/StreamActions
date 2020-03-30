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
using System.Security.Claims;
using System.Security.Principal;

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
        internal string BotLogin { get; set; } = "";

        /// <summary>
        /// The bot's Twitch OAuth token.
        /// </summary>
        internal string BotOAuth { get; set; } = "";

        /// <summary>
        /// The bot's Twitch OAuth refresh token.
        /// </summary>
        internal string BotRefreshToken { get; set; } = "";

        /// <summary>
        /// The channels the bot should join.
        /// </summary>
        internal List<string> ChannelsToJoin { get; } = new List<string>();

        /// <summary>
        /// The prefix character that identifies chat commands. <c>(char)0</c> to use the default.
        /// </summary>
        internal char ChatCommandIdentifier { get; set; } = '!';

        /// <summary>
        /// Whether the database connection should use TLS.
        /// </summary>
        internal bool DBAllowInsecureTls { get; set; } = false;

        /// <summary>
        /// The IP address of the database host. <c>null</c> to use the default on localhost.
        /// </summary>
        internal string DBHost { get; set; } = "mongod";

        /// <summary>
        /// The name of the Database to use. <c>null</c> to use the default.
        /// </summary>
        internal string DBName { get; set; } = "";

        /// <summary>
        /// The password for the database. <c>null</c> for no authentication.
        /// </summary>
        internal string DBPassword { get; set; } = "";

        /// <summary>
        /// The port of the database host. <c>0</c> to use the default.
        /// </summary>
        internal int DBPort { get; set; } = 0;

        /// <summary>
        /// The username for the database. <c>null</c> for no authentication.
        /// </summary>
        internal string DBUsername { get; set; } = "";

        /// <summary>
        /// Whether the database connection should use TLS.
        /// </summary>
        internal bool DBUseTls { get; set; } = false;

        /// <summary>
        /// The global culture. <c>null</c> to use the default.
        /// </summary>
        internal string GlobalCulture { get; set; } = "en-US";

        /// <summary>
        /// Indicates if the <see cref="BotOAuth"/> will be used for API calls, instead of individual Broadcaster OAuths.
        /// </summary>
        internal bool SingleOAuth { get; set; } = false;

        /// <summary>
        /// The Client ID for the Twitch developer app.
        /// </summary>
        internal string TwitchApiClientId { get; set; } = "";

        /// <summary>
        /// The secret for the Twitch developer app.
        /// </summary>
        internal string TwitchApiSecret { get; set; } = "";

        /// <summary>
        /// The prefix character that identifies whisper commands. <c>(char)0</c> to use the default.
        /// </summary>
        internal char WhisperCommandIdentifier { get; set; } = '!';

        /// <summary>
        /// The IP address the WebSocket server should listen on. <c>null</c> or <c>""</c> (empty string) to limit to localhost. <c>*</c> for IPAddress.Any.
        /// </summary>
        internal string WSListenIp { get; set; } = "";

        /// <summary>
        /// The port the WebSocket server should listen on. <c>0</c> to use the default (80 for no ssl, 443 for ssl).
        /// </summary>
        internal int WSListenPort { get; set; } = 0;

        /// <summary>
        /// Path to the SSL certificate for the WS Server, if used.
        /// </summary>
        internal string WSSslCert { get; set; } = "";

        /// <summary>
        /// Password to the SSL certificate for the WS Server, if used.
        /// </summary>
        internal string WSSslCertPass { get; set; } = "";

        /// <summary>
        /// The super admin bearer token for the WS Server.
        /// </summary>
        internal string WSSuperadminBearer { get; set; } = WebSocketServer.GenerateBearer(new ClaimsPrincipal(new GenericIdentity("superadmin", "GlobalToken")));

        /// <summary>
        /// Whether the WebSocket server should use SSL.
        /// </summary>
        internal bool WSUseSsl { get; set; } = false;

        #endregion Internal Properties
    }
}