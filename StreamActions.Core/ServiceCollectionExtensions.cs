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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;

namespace StreamActions.Core;

/// <summary>
/// Dependency injection registration extensions for <c>StreamActions.Core</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core StreamActions infrastructure services, configuration options, and caching.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStreamActionsCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        _ = services.AddOptions<BootstrapOptions>()
            .Bind(configuration.GetSection(BootstrapOptions.SectionName));
        services.TryAddSingleton<ISecretRedactor, SecretRedactor>();
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();
        services.TryAddSingleton<IPluginManager, PluginManager>();
        services.TryAddSingleton<IPermissionRegistry, PermissionRegistry>();
        services.TryAddScoped<IPermissionService, PermissionService>();
        _ = services.AddHybridCache();

        return services;
    }
}
