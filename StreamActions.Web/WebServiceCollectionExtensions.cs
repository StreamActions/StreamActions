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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Web.Components;
using StreamActions.Web.Health;
using StreamActions.Web.Hubs;
using System.Reflection;

namespace StreamActions.Web;

/// <summary>
/// Extension methods for registering and mapping StreamActions web services and endpoints.
/// </summary>
public static class WebServiceCollectionExtensions
{
    /// <summary>
    /// Registers Blazor Server, SignalR hubs, and health check services for StreamActions.
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
        _ = services.AddRazorComponents()
            .AddInteractiveServerComponents();
        _ = services.AddSignalR();
        _ = services.AddHealthChecks()
            .AddCheck<PluginReadinessHealthCheck>("plugins", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Maps health check probes, SignalR hubs, static assets, and Blazor Server components.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication MapStreamActionsWeb(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

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
