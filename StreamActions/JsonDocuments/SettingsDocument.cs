/*
 * Copyright © 2019-2022 StreamActions Team
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

using System.IO;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamActions.JsonDocuments
{
    /// <summary>
    /// A document containing the bot's settings.
    /// </summary>
    public class SettingsDocument
    {
        #region Public Properties

        /// <summary>
        /// The bot's Twitch login name.
        /// </summary>
        public string BotLogin { get; set; } = "";

        /// <summary>
        /// The prefix character that identifies chat commands. <c>(char)0</c> to use the default.
        /// </summary>
        public char ChatCommandIdentifier { get; set; } = '!';

        /// <summary>
        /// The global culture. <c>null</c> to use the default.
        /// </summary>
        public string GlobalCulture { get; set; } = "en-US";

        /// <summary>
        /// The Client ID for the Twitch developer app.
        /// </summary>
        public string TwitchApiClientId { get; set; } = "";

        /// <summary>
        /// The prefix character that identifies whisper commands. <c>(char)0</c> to use the default.
        /// </summary>
        public char WhisperCommandIdentifier { get; set; } = '!';

        #endregion Public Properties

        #region Internal Properties

        /// <summary>
        /// The bot's Twitch OAuth token.
        /// </summary>
        internal string BotOAuth { get; set; } = "";

        /// <summary>
        /// The bot's Twitch OAuth refresh token.
        /// </summary>
        internal string BotRefreshToken { get; set; } = "";

        /// <summary>
        /// Whether the database connection should use TLS.
        /// </summary>
        internal bool DBAllowInsecureTls { get; set; }

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
        internal int DBPort { get; set; }

        /// <summary>
        /// The username for the database. <c>null</c> for no authentication.
        /// </summary>
        internal string DBUsername { get; set; } = "";

        /// <summary>
        /// Whether the database connection should use TLS.
        /// </summary>
        internal bool DBUseTls { get; set; }

        /// <summary>
        /// Path to the SSL certificate, in PKCS12 (.pfx) format, for the internal Kestrel server, if used. Relative paths start in the Program.Assembly.Location/settings directory.
        /// </summary>
        internal string KestrelSslCert { get; set; } = "";

        /// <summary>
        /// Password to the SSL certificate for the internal Kestrel server, if used.
        /// </summary>
        internal string KestrelSslCertPass { get; set; } = "";

        /// <summary>
        /// Whether exceptions should be sent to the remote Exceptionless server.
        /// </summary>
        internal bool SendExceptions { get; set; } = true;

        /// <summary>
        /// Whether debugging messages should be sent to the console.
        /// </summary>
        internal bool ShowDebugMessages { get; set; }

        /// <summary>
        /// The secret for the Twitch developer app.
        /// </summary>
        internal string TwitchApiSecret { get; set; } = "";

        /// <summary>
        /// The super admin bearer token for the WS Server.
        /// </summary>
        internal string WSSuperadminBearer { get; set; } = Program.GenerateBearer(new ClaimsPrincipal(new GenericIdentity("superadmin", "GlobalToken")));

        #endregion Internal Properties

        #region Internal Methods

        /// <summary>
        /// Loads the settings from disk. Creates settings.json if it doesn't exist.
        /// </summary>
        /// <returns>A <see cref="SettingsDocument"/> with the settings from disk, and default values where no value existed.</returns>
        internal static async Task<SettingsDocument> Read()
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "settings", "settings.json"));
            SettingsDocument settings;
            if (File.Exists(path))
            {
                using FileStream fs = File.OpenRead(path);
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                using Task<SettingsDocument> t = JsonSerializer.DeserializeAsync<SettingsDocument>(fs, options).AsTask();
                settings = await t.ConfigureAwait(false);
            }
            else
            {
                settings = new SettingsDocument();
                await Write(settings).ConfigureAwait(false);
            }

            return settings;
        }

        /// <summary>
        /// Writes settings to disk.
        /// </summary>
        /// <returns></returns>
        internal static async Task Write(SettingsDocument settings)
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "settings", "settings.json"));
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            using FileStream fs = File.OpenWrite(path);
            using Task t = JsonSerializer.SerializeAsync<SettingsDocument>(fs, settings);
            await t.ConfigureAwait(false);
        }

        #endregion Internal Methods
    }
}
