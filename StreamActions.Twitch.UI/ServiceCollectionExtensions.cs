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
using StreamActions.Core.Actions;
using StreamActions.Core.Templates;
using StreamActions.Twitch.UI.Abstractions;
using StreamActions.Twitch.UI.Actions;
using StreamActions.Twitch.UI.Services;
using StreamActions.Twitch.UI.Templates;

namespace StreamActions.Twitch.UI;

/// <summary>
/// Dependency injection registration extensions for <c>StreamActions.Twitch.UI</c> services and template tag handlers.
/// </summary>
public static class ServiceCollectionExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers Twitch workspace session providers, stream history providers, and all Twitch <see cref="ICommandTagHandler"/> instances.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStreamActionsTwitchUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ITwitchWorkspaceSessionProvider, TwitchWorkspaceSessionProvider>();
        services.TryAddSingleton<ITwitchStreamHistoryProvider, KeyValueTwitchStreamHistoryProvider>();

        foreach (ICommandTagHandler handler in TwitchTagRegistryFactory.CreateDefaultHandlers())
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(handler));
        }

        foreach (IPlatformActionHandler actionHandler in TwitchActionBlockCatalogFactory.CreateDefaultActions())
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(actionHandler));
        }

        return services;
    }

    #endregion Public Methods
}
