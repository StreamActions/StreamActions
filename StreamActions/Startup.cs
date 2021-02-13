using GraphQL.Execution;
using GraphQL.Server;
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

            _ = app.UseAuthorization();

            _ = app.UseEndpoints(endpoints => endpoints.MapRazorPages());

            _ = app.UseWebSockets();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
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