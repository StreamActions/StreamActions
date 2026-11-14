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

using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Services;

/// <summary>
/// Manages the lifecycle and lookup of registered <see cref="IPlugin"/> instances.
/// </summary>
public sealed partial class PluginManager : IPluginManager, IPluginContext
{
    /// <summary>
    /// Ordered collection of registered plugins.
    /// </summary>
    private readonly List<IPlugin> _plugins;

    /// <summary>
    /// Lookup table of registered plugins keyed by plugin identifier.
    /// </summary>
    private readonly Dictionary<string, IPlugin> _pluginsById;

    /// <summary>
    /// Logger used for plugin lifecycle diagnostics.
    /// </summary>
    private readonly ILogger<PluginManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginManager"/> class.
    /// </summary>
    /// <param name="plugins">The registered plugins to manage.</param>
    /// <param name="services">The application service provider.</param>
    /// <param name="eventBus">The in-process event bus.</param>
    /// <param name="loggerFactory">The logger factory for plugin and manager diagnostics.</param>
    public PluginManager(
        IEnumerable<IPlugin> plugins,
        IServiceProvider services,
        IEventBus eventBus,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this._plugins = [.. plugins];
        this._pluginsById = new Dictionary<string, IPlugin>(StringComparer.OrdinalIgnoreCase);
        foreach (IPlugin plugin in this._plugins)
        {
            this._pluginsById[plugin.Id] = plugin;
        }

        this.Services = services;
        this.EventBus = eventBus;
        this.LoggerFactory = loggerFactory;
        this._logger = loggerFactory.CreateLogger<PluginManager>();
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IPlugin> Plugins => this._plugins;

    /// <inheritdoc/>
    public IServiceProvider Services { get; }

    /// <inheritdoc/>
    public IEventBus EventBus { get; }

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory { get; }

    /// <inheritdoc/>
    public async ValueTask InitializeAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (IPlugin plugin in this._plugins)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogInitializingPlugin(this._logger, plugin.Id, plugin.Name, plugin.Version);
            await plugin.InitializeAsync(this, cancellationToken).ConfigureAwait(false);
            LogInitializedPlugin(this._logger, plugin.Id);
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Plugin shutdown exceptions must be isolated and logged so remaining plugins still shut down cleanly.")]
    public async ValueTask ShutdownAllAsync(CancellationToken cancellationToken = default)
    {
        for (int i = this._plugins.Count - 1; i >= 0; i--)
        {
            IPlugin plugin = this._plugins[i];
            try
            {
                LogShuttingDownPlugin(this._logger, plugin.Id);
                await plugin.ShutdownAsync(cancellationToken).ConfigureAwait(false);
                LogShutDownPlugin(this._logger, plugin.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPluginShutdownFailed(this._logger, plugin.Id, ex);
            }
        }
    }

    /// <inheritdoc/>
    public IPlugin? GetPlugin(string pluginId)
    {
        ArgumentNullException.ThrowIfNull(pluginId);

        return this._pluginsById.GetValueOrDefault(pluginId);
    }

    /// <summary>
    /// Logs that a plugin is about to be initialized.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="pluginName">The display name of the plugin.</param>
    /// <param name="pluginVersion">The version of the plugin.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing plugin {PluginId} ({PluginName} v{PluginVersion}).")]
    private static partial void LogInitializingPlugin(ILogger logger, string pluginId, string pluginName, Version pluginVersion);

    /// <summary>
    /// Logs that a plugin finished initializing.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Initialized plugin {PluginId}.")]
    private static partial void LogInitializedPlugin(ILogger logger, string pluginId);

    /// <summary>
    /// Logs that a plugin is about to be shut down.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Shutting down plugin {PluginId}.")]
    private static partial void LogShuttingDownPlugin(ILogger logger, string pluginId);

    /// <summary>
    /// Logs that a plugin finished shutting down.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Shut down plugin {PluginId}.")]
    private static partial void LogShutDownPlugin(ILogger logger, string pluginId);

    /// <summary>
    /// Logs an error encountered while shutting down a plugin.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <param name="exception">The exception thrown during shutdown.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to shut down plugin {PluginId}.")]
    private static partial void LogPluginShutdownFailed(ILogger logger, string pluginId, Exception exception);
}
