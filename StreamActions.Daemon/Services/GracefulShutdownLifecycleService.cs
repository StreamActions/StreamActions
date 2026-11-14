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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Database;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Coordinates the 3-phase graceful shutdown sequence (<c>StoppingAsync</c>, <c>StopAsync</c>, <c>StoppedAsync</c>)
/// to drain plugins, checkpoint SQLite WAL journals, and flush Sentry telemetry.
/// </summary>
internal sealed partial class GracefulShutdownLifecycleService : IHostedLifecycleService
{
    /// <summary>
    /// Plugin manager drained during <see cref="StopAsync"/>.
    /// </summary>
    private readonly IPluginManager _pluginManager;

    /// <summary>
    /// Database context factory used to checkpoint the SQLite WAL during shutdown.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _contextFactory;

    /// <summary>
    /// Host bootstrap options.
    /// </summary>
    private readonly IOptions<BootstrapOptions> _bootstrapOptions;

    /// <summary>
    /// Logger instance for shutdown lifecycle events.
    /// </summary>
    private readonly ILogger<GracefulShutdownLifecycleService> _logger;

    /// <summary>
    /// Synchronization lock protecting <see cref="_phases"/>.
    /// </summary>
    private readonly Lock _phaseGate = new();

    /// <summary>
    /// Ordered list of lifecycle phase names executed by this service.
    /// </summary>
    private readonly List<string> _phases = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="GracefulShutdownLifecycleService"/> class.
    /// </summary>
    /// <param name="pluginManager">The plugin manager to shut down during <see cref="StopAsync"/>.</param>
    /// <param name="contextFactory">The database context factory used for SQLite WAL checkpointing.</param>
    /// <param name="bootstrapOptions">The host bootstrap options.</param>
    /// <param name="logger">The logger instance.</param>
    public GracefulShutdownLifecycleService(
        IPluginManager pluginManager,
        IDbContextFactory<StreamActionsDbContext> contextFactory,
        IOptions<BootstrapOptions> bootstrapOptions,
        ILogger<GracefulShutdownLifecycleService> logger)
    {
        ArgumentNullException.ThrowIfNull(pluginManager);
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(bootstrapOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this._pluginManager = pluginManager;
        this._contextFactory = contextFactory;
        this._bootstrapOptions = bootstrapOptions;
        this._logger = logger;
    }

    /// <summary>
    /// Gets a snapshot of the lifecycle phase names recorded so far.
    /// </summary>
    internal IReadOnlyList<string> Phases
    {
        get
        {
            lock (this._phaseGate)
            {
                return [.. this._phases];
            }
        }
    }

    /// <inheritdoc/>
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        this.RecordPhase(nameof(this.StartingAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.RecordPhase(nameof(this.StartAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        this.RecordPhase(nameof(this.StartedAsync));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        this.RecordPhase(nameof(this.StoppingAsync));
        LogStopping(this._logger);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.RecordPhase(nameof(this.StopAsync));
        LogDrainingPlugins(this._logger, this._pluginManager.Plugins.Count);
        await this._pluginManager.ShutdownAllAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Shutdown cleanup must not prevent Sentry flush or throw out of StoppedAsync.")]
    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        this.RecordPhase(nameof(this.StoppedAsync));

        if (this._bootstrapOptions.Value.Database.Provider == DatabaseProvider.Sqlite)
        {
            try
            {
                StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                await using (dbContext.ConfigureAwait(false))
                {
                    await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        DbConnection connection = dbContext.Database.GetDbConnection();
                        DbCommand command = connection.CreateCommand();
                        await using (command.ConfigureAwait(false))
                        {
                            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
                    }

                    TryLog(() => LogWalCheckpointCompleted(this._logger));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TryLog(() => LogWalCheckpointFailed(this._logger, ex));
            }
        }

        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        TryLog(() => LogStopped(this._logger));
    }

    /// <summary>
    /// Invokes <paramref name="logAction"/> while ignoring exceptions from disposed logging providers during shutdown.
    /// </summary>
    /// <param name="logAction">The logging delegate to execute.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Shutdown logging must ignore disposed logger providers such as Windows EventLog during host teardown.")]
    private static void TryLog(Action logAction)
    {
        try
        {
            logAction();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Appends <paramref name="phase"/> to <see cref="_phases"/> under <see cref="_phaseGate"/>.
    /// </summary>
    /// <param name="phase">The lifecycle method name to record.</param>
    private void RecordPhase(string phase)
    {
        lock (this._phaseGate)
        {
            this._phases.Add(phase);
        }
    }

    /// <summary>
    /// Logs initiation of phase 1/3 graceful shutdown.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Phase 1/3 StoppingAsync: graceful daemon shutdown initiated.")]
    private static partial void LogStopping(ILogger logger);

    /// <summary>
    /// Logs initiation of phase 2/3 plugin draining.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="pluginCount">The number of loaded plugins being shut down.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Phase 2/3 StopAsync: draining and shutting down {PluginCount} plugin(s).")]
    private static partial void LogDrainingPlugins(ILogger logger, int pluginCount);

    /// <summary>
    /// Logs completion of the SQLite WAL truncate checkpoint during phase 3/3.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Phase 3/3 StoppedAsync: SQLite WAL checkpoint truncated.")]
    private static partial void LogWalCheckpointCompleted(ILogger logger);

    /// <summary>
    /// Logs a failure encountered while checkpointing the SQLite WAL during phase 3/3.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception thrown during checkpointing.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Phase 3/3 StoppedAsync: SQLite WAL checkpoint failed during shutdown.")]
    private static partial void LogWalCheckpointFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Logs completion of phase 3/3 graceful shutdown.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Phase 3/3 StoppedAsync: graceful shutdown complete.")]
    private static partial void LogStopped(ILogger logger);
}
