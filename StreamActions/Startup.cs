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

using AspNet.Security.OAuth.Twitch;
using GraphQL.Execution;
using GraphQL.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StreamActions
{
    public class Startup
    {
        #region Public Constructors

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            this.Configuration = configuration;
            this.Environment = environment;
        }

        #endregion Public Constructors

        #region Public Properties

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        #endregion Public Properties

        #region Public Methods

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                _ = app.UseDeveloperExceptionPage();
            }
            else
            {
                _ = app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                _ = app.UseHsts();
            }

            _ = app.UseHttpsRedirection();
            _ = app.UseStaticFiles();

            _ = app.UseRouting();

            _ = app.UseAuthentication();
            _ = app.UseAuthorization();

            _ = app.UseEndpoints(endpoints => endpoints.MapRazorPages());

            _ = app.UseWebSockets();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            AuthenticationBuilder authBuilder = services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = TwitchAuthenticationDefaults.AuthenticationScheme;
            })
                .AddCookie()
                .AddJwtBearer(options =>
                {
                    options.Audience = Program.Settings.BotLogin;
                    options.Authority = typeof(Program).Assembly.GetName().FullName + "/" + typeof(Program).Assembly.GetName().Version.ToString() + "/" + Program.Settings.BotLogin;
                });

            if (!string.IsNullOrWhiteSpace(Program.Settings.TwitchApiClientId) && !string.IsNullOrWhiteSpace(Program.Settings.TwitchApiSecret))
            {
                _ = authBuilder.AddTwitch(options =>
                 {
                     options.ClientId = Program.Settings.TwitchApiClientId;
                     options.ClientSecret = Program.Settings.TwitchApiSecret;
                 });
            }

            _ = services.AddRazorPages();
            _ = services.AddGraphQL((options, provider) =>
                                                                      {
                                                                          options.EnableMetrics = true;
                                                                          Microsoft.Extensions.Logging.ILogger logger = provider.GetRequiredService<ILogger<Startup>>();
                                                                          options.UnhandledExceptionDelegate = ctx => GraphQL_UnhandledException(ctx);
                                                                      })
                .AddSystemTextJson()
                .AddErrorInfoProvider(opt => opt.ExposeExceptionStackTrace = true)
                .AddWebSockets()
                .AddGraphTypes();
        }

        private static void GraphQL_UnhandledException(UnhandledExceptionContext e) => BotConsole.ExceptionOut.WriteException("[GraphQLUnhandledException]" + e.OriginalException.GetType().FullName, e.OriginalException, null, true);

        #endregion Public Methods
    }
}