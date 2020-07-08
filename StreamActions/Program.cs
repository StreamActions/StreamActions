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

using Exceptionless;
using Exceptionless.Configuration;
using StreamActions.JsonDocuments;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;

[assembly: Exceptionless("*/Exceptionless-API-Key/*", ServerUrl = "*/Exceptionless-API-URL/*")]

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

        /// <summary>
        /// The version of the bot core.
        /// </summary>
        public static string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();

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

        /// <summary>
        /// Attaches handlers for all graceful shutdown signals.
        /// </summary>
        /// <param name="cts">A CancellationTokenSource to signal that a program shutdown has been initiated.</param>
        /// <param name="resetEvent">A ManualResetEventSlim used to signal that shutdown actions have completed.</param>
        private static void AttachCtrlcSigtermShutdown(CancellationTokenSource cts, ManualResetEventSlim resetEvent)
        {
            void Shutdown()
            {
                if (resetEvent.IsSet)
                {
                    return;
                }
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
                resetEvent.Wait();
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => Shutdown();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Shutdown();
                eventArgs.Cancel = true;
            };
        }

        /// <summary>
        /// Main member.
        /// </summary>
        /// <returns>A Task that can be awaited.</returns>
        private static async Task Main()
        {
            using ManualResetEventSlim done = new ManualResetEventSlim(false);
            using CancellationTokenSource shutdownCts = new CancellationTokenSource();

            try
            {
                // Attach shutdown handler.
                AttachCtrlcSigtermShutdown(shutdownCts, done);
                // Set Exceptionless to not send GPDR-protected information.
                ExceptionlessClient.Default.Configuration.IncludeCookies = false;
                ExceptionlessClient.Default.Configuration.IncludeIpAddress = false;
                ExceptionlessClient.Default.Configuration.IncludeMachineName = false;
                ExceptionlessClient.Default.Configuration.IncludePostData = false;
                ExceptionlessClient.Default.Configuration.IncludeQueryString = false;
                ExceptionlessClient.Default.Configuration.IncludeUserName = false;
                ExceptionlessClient.Default.Configuration.IncludePrivateInformation = false;

                // Load settings.
                using FileStream fs = File.OpenRead(Path.GetFullPath(Path.Combine(typeof(Program).Assembly.Location, "settings.json")));
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                using Task<SettingsDocument> t = JsonSerializer.DeserializeAsync<SettingsDocument>(fs, options).AsTask();
                t.Wait();
                _settings = t.Result;

                // Start Exceptionless session, if enabled.
                if (Settings.SendExceptions)
                {
                    ExceptionlessClient.Default.Configuration.SetUserIdentity(Settings.BotLogin);
                    ExceptionlessClient.Default.Configuration.SetVersion(Version);
                    ExceptionlessClient.Default.Configuration.UseReferenceIds();
                    ExceptionlessClient.Default.Configuration.UseSessions();
                    ExceptionlessClient.Default.Startup();
                }

                // Initialize Twitch API.
                _twitchApi = new TwitchAPI();
                _twitchApi.Settings.ClientId = _settings.TwitchApiClientId;
                _twitchApi.Settings.AccessToken = _settings.BotOAuth;
                _twitchApi.Settings.Secret = _settings.TwitchApiSecret;

                // Await shutdown event to fire.
                await shutdownCts.Token.WaitAsync().ConfigureAwait(false);

                // Send all remaining Exceptionless events and shutdown, if enabled.
                if (Settings.SendExceptions)
                {
                    ExceptionlessClient.Default.ProcessQueue();
                    ExceptionlessClient.Default.Shutdown();
                }
            }
            finally
            {
                done.Set();
            }
        }

        #endregion Private Methods
    }
}