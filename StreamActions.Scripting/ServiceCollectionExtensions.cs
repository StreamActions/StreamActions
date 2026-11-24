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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StreamActions.Core.Scripting;
using StreamActions.Scripting.Runtime;

namespace StreamActions.Scripting;

/// <summary>
/// Dependency injection registration extensions for <c>StreamActions.Scripting</c> ClearScript V8 services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the sandboxed ClearScript V8 runtime service and automatic lifecycle manager.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStreamActionsScripting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ClearScriptRuntimeService>();
        services.TryAddSingleton<IRunJsActionExecutor>(static sp => sp.GetRequiredService<ClearScriptRuntimeService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ClearScriptRuntimeService>(static sp => sp.GetRequiredService<ClearScriptRuntimeService>()));

        return services;
    }
}

