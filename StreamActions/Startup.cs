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

using AspNet.Security.OAuth.Twitch;
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

        #endregion Public Methods

        #region Private Methods

        private static void GraphQL_UnhandledException(UnhandledExceptionContext e) => ExceptionOut.WriteException("[GraphQLUnhandledException]" + e.OriginalException.GetType().FullName, e.OriginalException, null, true);

        #endregion Private Methods
    }
}
