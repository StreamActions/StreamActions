/*
 * Copyright © 2019-2021 StreamActions Team
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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StreamActions.JsonDocuments;
using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;

[assembly: Exceptionless("API_KEY_HERE", ServerUrl = "*/Exceptionless-API-URL/*")]

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

        #region Public Methods

        /// <summary>
        /// Creates the IHostBuilder for the ASP.Net Core server and loads the SSL certificate settings into Kestrel.
        /// </summary>
        /// <param name="args">The command line arguments of the assembly.</param>
        /// <returns>An IHostBuilder ready to build.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => webBuilder.ConfigureKestrel(serverOptions =>
                {
                    if (!string.IsNullOrWhiteSpace(Program.Settings.KestrelSslCert))
                    {
                        string path = Program.Settings.KestrelSslCert;
                        if (!Path.IsPathFullyQualified(path))
                        {
                            path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "settings", Program.Settings.KestrelSslCert));
                        }
                        serverOptions.ConfigureEndpointDefaults(listenOptions => listenOptions.UseHttps(Program.Settings.KestrelSslCert, Program.Settings.KestrelSslCertPass));
                    }
                }).UseStartup<Startup>());

        public static async Task Main(string[] args)
        {
            await BotStartup().ConfigureAwait(false);
            await CreateHostBuilder(args).Build().RunAsync().ConfigureAwait(false);
            await WaitForShutdown().ConfigureAwait(false);
        }

        #endregion Public Methods

        #region Internal Methods

        /// <summary>
        /// Generates a JWT Bearer token.
        /// </summary>
        /// <param name="principal">A ClaimsPrincipal representing the user.</param>
        /// <param name="notBefore">The validity start timestamp.</param>
        /// <param name="expires">The validity expiration timestamp.</param>
        /// <param name="signingCredentials">Credentials for signing the token.</param>
        /// <returns>A JWT token.</returns>
        internal static string GenerateBearer(ClaimsPrincipal principal, DateTime? notBefore = null, DateTime? expires = null, SigningCredentials signingCredentials = null)
        {
            string botlogin = Program.Settings?.BotLogin ?? "";
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtPayload payload = new JwtPayload(typeof(Program).Assembly.GetName().FullName + "/" + typeof(Program).Assembly.GetName().Version.ToString() + "/" + botlogin,
                botlogin, principal.Claims, notBefore, expires);
            JwtHeader header = new JwtHeader(signingCredentials);
            JwtSecurityToken token = new JwtSecurityToken(header, payload);
            return tokenHandler.WriteToken(token);
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        ///
        /// </summary>
        private static readonly ManualResetEventSlim _done = new ManualResetEventSlim(false);

        private static readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();

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
        /// Initializes and starts the bot.
        /// </summary>
        /// <returns>A Task that can be awaited.</returns>
        private static async Task BotStartup()
        {
            // Attach shutdown handler.
            AttachCtrlcSigtermShutdown(Program._shutdownCts, Program._done);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            _ = Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            // Load settings.
            _settings = await SettingsDocument.Read().ConfigureAwait(false);

            // Start Exceptionless session, if enabled.
            if (Settings.SendExceptions)
            {
                // Set Exceptionless to not send GPDR-protected information.
                ExceptionlessClient.Default.Configuration.IncludeCookies = false;
                ExceptionlessClient.Default.Configuration.IncludeIpAddress = false;
                ExceptionlessClient.Default.Configuration.IncludeMachineName = false;
                ExceptionlessClient.Default.Configuration.IncludePostData = false;
                ExceptionlessClient.Default.Configuration.IncludeQueryString = false;
                ExceptionlessClient.Default.Configuration.IncludeUserName = false;
                ExceptionlessClient.Default.Configuration.IncludePrivateInformation = false;
                if (Settings.ShowDebugMessages)
                {
                    ExceptionlessClient.Default.Configuration.UseTraceLogger(Exceptionless.Logging.LogLevel.Debug);
                }
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
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) => BotConsole.ExceptionOut.WriteException("[AppDomainUnhandledException]" + e.ExceptionObject.GetType().FullName, (Exception)e.ExceptionObject, null, true);

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e) => BotConsole.ExceptionOut.WriteException("[UnobservedTaskException]" + e.Exception.InnerExceptions[0].GetType().FullName, e.Exception.InnerExceptions[0], null, true);

        /// <summary>
        /// Waits for the shutdown signal and processes shutdown actions.
        /// </summary>
        /// <returns>A Task that can be awaited.</returns>
        private static async Task WaitForShutdown()
        {
            try
            {
                // Await shutdown event to fire.
                await Program._shutdownCts.Token.WaitAsync().ConfigureAwait(false);

                // Send all remaining Exceptionless events and shutdown, if enabled.
                if (Settings.SendExceptions)
                {
                    ExceptionlessClient.Default.ProcessQueue();
                    ExceptionlessClient.Default.Shutdown();
                }
            }
            finally
            {
                Program._done.Set();
            }
        }

        #endregion Private Methods
    }
}