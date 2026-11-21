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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Configuration;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using StreamActions.Core.Templates;

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
        _ = services.AddOptions<LocalizationOptions>()
            .Bind(configuration.GetSection(LocalizationOptions.SectionName));
        services.TryAddSingleton<ISecretRedactor, SecretRedactor>();
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();
        services.TryAddSingleton<IPluginManager, PluginManager>();
        services.TryAddSingleton<IPermissionRegistry, PermissionRegistry>();
        services.TryAddScoped<IPermissionService, PermissionService>();
        services.TryAddScoped<IOAuthClientConfigResolver, OAuthClientConfigResolver>();
        services.TryAddScoped<IBotCredentialResolver, BotCredentialResolver>();
        services.TryAddSingleton<ITokenLifecycleService, TokenLifecycleService>();
        services.TryAddSingleton<IScopeDriftDetector, ScopeDriftDetector>();
        services.TryAddScoped<IWorkspaceAccessService, WorkspaceAccessService>();
        services.TryAddSingleton<ITotpService, TotpService>();
        services.TryAddScoped<IMfaPolicyService, MfaPolicyService>();
        services.TryAddSingleton<IStreamActionsLocalizer, StreamActionsLocalizer>();
        services.TryAddSingleton<IStringLocalizer>(static sp => sp.GetRequiredService<IStreamActionsLocalizer>());
        services.TryAddSingleton<ISafeExternalHttpClient, SafeExternalHttpClient>();
        services.TryAddSingleton<IStreamStatusProvider, DefaultStreamStatusProvider>();
        _ = services.AddOptions<OutboundChatDispatcherOptions>();
        services.TryAddSingleton<OutboundChatDispatcher>();
        services.TryAddSingleton<IOutboundChatDispatcher>(static sp => sp.GetRequiredService<OutboundChatDispatcher>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboundChatDispatcher>(static sp => sp.GetRequiredService<OutboundChatDispatcher>()));
        _ = services.AddHybridCache();

        foreach (ICommandTagHandler handler in CoreTagRegistryFactory.CreateDefaultHandlers())
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(handler));
        }

        services.TryAddSingleton<ITagRegistry, TagRegistry>();
        services.TryAddSingleton<BotCommandSubCommandHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuiltInCommandHandler, BotCommandSubCommandHandler>(static sp => sp.GetRequiredService<BotCommandSubCommandHandler>()));
        services.TryAddSingleton<IBuiltInCommandRegistry, BuiltInCommandRegistry>();
        services.TryAddSingleton<CommandAstCache>();
        services.TryAddSingleton<CommandCooldownTracker>();
        services.TryAddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPlugin, CommandsPlugin>());

        return services;
    }
}
