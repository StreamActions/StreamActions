/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;
using StreamActions.Web.Components;
using StreamActions.Web.Endpoints;
using StreamActions.Web.Health;
using StreamActions.Web.Hubs;
using StreamActions.Web.Security;
using StreamActions.Web.Services;
using System.Reflection;
using System.Threading.RateLimiting;

namespace StreamActions.Web;

/// <summary>
/// Extension methods for registering and mapping StreamActions web services and endpoints.
/// </summary>
public static class WebServiceCollectionExtensions
{
    /// <summary>
    /// Registers Blazor Server, SignalR hubs, rate limiting, and health check services for StreamActions.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="pluginAssemblies">Optional plugin assemblies containing additional Razor components.</param>
    /// <returns>The configured <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddStreamActionsWeb(
        this IServiceCollection services,
        IReadOnlyCollection<Assembly>? pluginAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddSingleton(new WebAssemblyRegistry(pluginAssemblies ?? []));
        _ = services.AddHttpClient();
        services.TryAddSingleton<IExternalOidcClient>(
            static sp => new ExternalOidcClient(sp.GetRequiredService<IHttpClientFactory>()));
        _ = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(static options =>
            {
                options.Cookie.Name = AuthClaims.AuthCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/access-denied";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = static async context =>
                    {
                        if (AuthClaims.GetSessionId(context.Principal) is Guid sessionId
                            && context.HttpContext.RequestServices.GetService<IPanelSessionService>() is IPanelSessionService sessionService)
                        {
                            bool valid = await sessionService
                                .IsSessionValidAsync(
                                    sessionId,
                                    AuthClaims.GetPanelUserId(context.Principal),
                                    context.HttpContext.RequestAborted)
                                .ConfigureAwait(false);
                            if (!valid)
                            {
                                context.RejectPrincipal();
                                await context.HttpContext
                                    .SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                                    .ConfigureAwait(false);
                            }
                        }
                    },
                    OnRedirectToLogin = static context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = static context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    },
                };
            });
        _ = services.AddIdentityCore<PanelUser>()
            .AddUserStore<PanelUserIdentityStore>();
        services.TryAddScoped<IPasskeyHandler<PanelUser>, PasskeyHandler<PanelUser>>();
        _ = services.AddAuthorization(static options =>
        {
            options.AddPolicy(AuthClaims.MfaPendingPolicy, static policy =>
                _ = policy.RequireAuthenticatedUser());

            options.AddPolicy(AuthClaims.PanelAuthenticatedPolicy, static policy =>
            {
                _ = policy.RequireAuthenticatedUser();
                _ = policy.RequireAssertion(static context =>
                    !AuthClaims.HasTrueClaim(context.User, AuthClaims.MfaPendingClaimType));
            });

            options.AddPolicy(AuthClaims.RootOrGlobalAdminPolicy, static policy =>
            {
                _ = policy.RequireAuthenticatedUser();
                _ = policy.RequireAssertion(static context =>
                    !AuthClaims.HasTrueClaim(context.User, AuthClaims.MfaPendingClaimType)
                    && (AuthClaims.HasTrueClaim(context.User, AuthClaims.IsRootClaimType)
                        || AuthClaims.HasTrueClaim(context.User, AuthClaims.IsGlobalAdminClaimType)));
            });

            options.AddPolicy(AuthClaims.RootOnlyPolicy, static policy =>
            {
                _ = policy.RequireAuthenticatedUser();
                _ = policy.RequireAssertion(static context =>
                    !AuthClaims.HasTrueClaim(context.User, AuthClaims.MfaPendingClaimType)
                    && AuthClaims.HasTrueClaim(context.User, AuthClaims.IsRootClaimType));
            });
        });
        _ = services.AddCascadingAuthenticationState();
        _ = services.AddHttpContextAccessor();
        services.TryAddScoped<IWorkspaceSessionContext, WorkspaceSessionContext>();
        _ = services.AddRazorComponents()
            .AddInteractiveServerComponents();
        _ = services.AddSignalR();
        _ = services.AddHostedService<PermissionHubEventBridge>();
        _ = services.AddRateLimiter(static options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            _ = options.AddFixedWindowLimiter(SetupEndpoints.RateLimitPolicyName, static limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });
            _ = options.AddFixedWindowLimiter(AuthEndpoints.LoginRateLimitPolicyName, static limiterOptions =>
            {
                limiterOptions.PermitLimit = 30;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });
        });
        _ = services.AddHealthChecks()
            .AddCheck<PluginReadinessHealthCheck>("plugins", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Maps health check probes, SignalR hubs, setup endpoints, static assets, and Blazor Server components.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication MapStreamActionsWeb(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.UseRateLimiter();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.UseAntiforgery();
        _ = app.MapStaticAssets();

        _ = app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            Predicate = static _ => false
        });
        _ = app.MapHealthChecks("/readyz", new HealthCheckOptions
        {
            Predicate = static r => r.Tags.Contains("ready")
        });

        _ = app.MapSetupEndpoints();
        _ = app.MapAuthEndpoints();
        _ = app.MapMfaEndpoints();
        _ = app.MapHub<BotStatusHub>("/hubs/bot-status");
        _ = app.MapHub<ModerationHub>("/hubs/moderation");
        _ = app.MapHub<CommandHub>("/hubs/commands");
        _ = app.MapHub<PermissionHub>("/hubs/permissions");

        RazorComponentsEndpointConventionBuilder razorComponents = app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        WebAssemblyRegistry assemblyRegistry = app.Services.GetRequiredService<WebAssemblyRegistry>();
        if (assemblyRegistry.PluginAssemblies.Count > 0)
        {
            _ = razorComponents.AddAdditionalAssemblies([.. assemblyRegistry.PluginAssemblies]);
        }

        return app;
    }
}
