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

using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace StreamActions.Daemon.Telemetry;

/// <summary>
/// Synchronizes database <see cref="InstanceSettings"/> Sentry configuration changes to <c>{ConfigDir}/bootstrap.sentry.json</c>
/// and refreshes active Sentry scope tags.
/// </summary>
internal sealed partial class SentryBootstrapSyncService : IHostedService, IDisposable
{
    /// <summary>
    /// JSON serializer options used when writing <c>bootstrap.sentry.json</c>.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Instance settings service raising notifications when Sentry settings change.
    /// </summary>
    private readonly IInstanceSettingsService _instanceSettingsService;

    /// <summary>
    /// Plugin manager used to count loaded plugins for Sentry scope tags.
    /// </summary>
    private readonly IPluginManager _pluginManager;

    /// <summary>
    /// Host bootstrap options.
    /// </summary>
    private readonly IOptions<BootstrapOptions> _bootstrapOptions;

    /// <summary>
    /// Logger instance for bootstrap synchronization events.
    /// </summary>
    private readonly ILogger<SentryBootstrapSyncService> _logger;

    /// <summary>
    /// Synchronization lock serializing atomic writes to <c>bootstrap.sentry.json</c>.
    /// </summary>
    private readonly Lock _fileGate = new();

    /// <summary>
    /// Indicates whether the service is currently subscribed to <see cref="IInstanceSettingsService.SettingsChanged"/>.
    /// </summary>
    private bool _subscribed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentryBootstrapSyncService"/> class.
    /// </summary>
    /// <param name="instanceSettingsService">The instance settings service raising runtime updates.</param>
    /// <param name="pluginManager">The plugin manager used to count loaded plugins.</param>
    /// <param name="bootstrapOptions">The host bootstrap options.</param>
    /// <param name="logger">The logger instance.</param>
    public SentryBootstrapSyncService(
        IInstanceSettingsService instanceSettingsService,
        IPluginManager pluginManager,
        IOptions<BootstrapOptions> bootstrapOptions,
        ILogger<SentryBootstrapSyncService> logger)
    {
        ArgumentNullException.ThrowIfNull(instanceSettingsService);
        ArgumentNullException.ThrowIfNull(pluginManager);
        ArgumentNullException.ThrowIfNull(bootstrapOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this._instanceSettingsService = instanceSettingsService;
        this._pluginManager = pluginManager;
        this._bootstrapOptions = bootstrapOptions;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this._subscribed)
        {
            this._instanceSettingsService.SettingsChanged += this.OnSettingsChanged;
            this._subscribed = true;
        }

        SentryBootstrapInitializer.ApplyScopeTags(
            this._bootstrapOptions.Value,
            this._pluginManager.Plugins.Count);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (this._subscribed)
        {
            this._instanceSettingsService.SettingsChanged -= this.OnSettingsChanged;
            this._subscribed = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Atomically writes <paramref name="settings"/> Sentry values to <c>{ConfigDir}/bootstrap.sentry.json</c> and updates scope tags.
    /// </summary>
    /// <param name="settings">The updated instance settings.</param>
    public void SyncSettings(InstanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        BootstrapOptions bootstrap = this._bootstrapOptions.Value;
        string configDir = bootstrap.Paths.ConfigDir;
        _ = Directory.CreateDirectory(configDir);

        SentryBootstrapConfig fileConfig = new()
        {
            Enabled = settings.SentryEnabled,
            Dsn = string.IsNullOrWhiteSpace(settings.SentryCustomDsn) ? bootstrap.Sentry.Dsn : settings.SentryCustomDsn,
            Environment = string.IsNullOrWhiteSpace(bootstrap.Sentry.Environment) ? "production" : bootstrap.Sentry.Environment,
            TracesSampleRate = settings.SentryTracesSampleRate,
            IsCustomDsn = !string.IsNullOrWhiteSpace(settings.SentryCustomDsn),
        };

        string targetPath = Path.Combine(configDir, SentryBootstrapInitializer.BootstrapFileName);
        string tempPath = Path.Combine(configDir, $".{SentryBootstrapInitializer.BootstrapFileName}.{Guid.NewGuid():N}.tmp");
        string json = JsonSerializer.Serialize(fileConfig, _jsonOptions);

        lock (this._fileGate)
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, targetPath, overwrite: true);
        }

        SentryBootstrapInitializer.ApplyScopeTags(bootstrap, this._pluginManager.Plugins.Count);
        LogBootstrapSynced(this._logger, targetPath);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this._subscribed)
        {
            this._instanceSettingsService.SettingsChanged -= this.OnSettingsChanged;
            this._subscribed = false;
        }
    }

    /// <summary>
    /// Handles <see cref="IInstanceSettingsService.SettingsChanged"/> events by synchronizing Sentry bootstrap configuration.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments containing the updated <see cref="InstanceSettings"/>.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "SettingsChanged event handlers must log failures without crashing the caller.")]
    private void OnSettingsChanged(object? sender, InstanceSettingsChangedEventArgs e)
    {
        try
        {
            this.SyncSettings(e.Settings);
        }
        catch (Exception ex)
        {
            LogBootstrapSyncFailed(this._logger, ex);
        }
    }

    /// <summary>
    /// Logs that Sentry bootstrap configuration was written to disk.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="filePath">The target <c>bootstrap.sentry.json</c> file path.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Synchronized Sentry bootstrap configuration to {FilePath}.")]
    private static partial void LogBootstrapSynced(ILogger logger, string filePath);

    /// <summary>
    /// Logs an error encountered while synchronizing Sentry bootstrap configuration.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception thrown during synchronization.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to synchronize Sentry bootstrap configuration.")]
    private static partial void LogBootstrapSyncFailed(ILogger logger, Exception exception);
}
