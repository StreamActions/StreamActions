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

using StreamActions.JsonDocuments;
using System.IO;
using System.Text.Json;
using TwitchLib.Api;

namespace StreamActions
{
    internal class Program
    {
        #region Public Properties

        /// <summary>
        /// The bot's settings.
        /// </summary>
        public static SettingsDocument Settings => _settings;

        /// <summary>
        /// Singleton of <see cref="TwitchAPI"/>.
        /// </summary>
        public static TwitchAPI TwitchApi => _twitchApi;

        #endregion Public Properties

        #region Private Fields

        /// <summary>
        /// Field that backs <see cref="Settings"/>.
        /// </summary>
        private static SettingsDocument _settings;

        /// <summary>
        /// Field that backs <see cref="TwitchAPI"/>.
        /// </summary>
        private static TwitchAPI _twitchApi;

        #endregion Private Fields

        #region Private Methods

        private static async void Main()
        {
            using FileStream fs = File.OpenRead(Path.GetFullPath(Path.Combine(typeof(Program).Assembly.Location, "settings.json")));
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            _settings = await JsonSerializer.DeserializeAsync<SettingsDocument>(fs, options);

            _twitchApi = new TwitchAPI();
            _twitchApi.Settings.ClientId = _settings.TwitchApiClientId;
            _twitchApi.Settings.AccessToken = _settings.BotOAuth;
            _twitchApi.Settings.Secret = _settings.TwitchApiSecret;
        }

        #endregion Private Methods
    }
}