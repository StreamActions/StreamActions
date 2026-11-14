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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Database.Services;

/// <summary>
/// Periodically creates online SQLite database backups using <c>VACUUM INTO</c> and enforces rolling file retention.
/// </summary>
public sealed partial class DatabaseBackupService : BackgroundService, IDatabaseBackupService
{
    /// <summary>
    /// Glob pattern matching rolling SQLite backup files created by this service.
    /// </summary>
    private const string _backupFileSearchPattern = "streamactions-backup-*.db";

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _contextFactory;

    /// <summary>
    /// The instance settings service providing backup interval and retention configuration.
    /// </summary>
    private readonly IInstanceSettingsService _instanceSettingsService;

    /// <summary>
    /// The host bootstrap options.
    /// </summary>
    private readonly IOptions<BootstrapOptions> _bootstrapOptions;

    /// <summary>
    /// The time provider for periodic scheduling and backup filenames.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger instance.
    /// </summary>
    private readonly ILogger<DatabaseBackupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseBackupService"/> class.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="bootstrapOptions">The host bootstrap options.</param>
    /// <param name="timeProvider">The time provider for periodic scheduling and timestamps.</param>
    /// <param name="logger">The logger instance.</param>
    public DatabaseBackupService(
        IDbContextFactory<StreamActionsDbContext> contextFactory,
        IInstanceSettingsService instanceSettingsService,
        IOptions<BootstrapOptions> bootstrapOptions,
        TimeProvider timeProvider,
        ILogger<DatabaseBackupService> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(instanceSettingsService);
        ArgumentNullException.ThrowIfNull(bootstrapOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._contextFactory = contextFactory;
        this._instanceSettingsService = instanceSettingsService;
        this._bootstrapOptions = bootstrapOptions;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "SQLite VACUUM INTO does not support bound parameters for the destination path; single quotes are escaped.")]
    public async ValueTask<string> TriggerBackupAsync(CancellationToken cancellationToken = default)
    {
        BootstrapOptions options = this._bootstrapOptions.Value;
        if (options.Database.Provider != DatabaseProvider.Sqlite)
        {
            throw new NotSupportedException($"Online VACUUM INTO backups are only supported for SQLite, not {options.Database.Provider}.");
        }

        string backupDir = options.Paths.BackupDir;
        _ = Directory.CreateDirectory(backupDir);

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        string fileName = $"streamactions-backup-{now:yyyyMMdd-HHmmss-fff}.db";
        string backupPath = Path.Combine(backupDir, fileName);
        string escapedPath = backupPath.Replace("'", "''", StringComparison.Ordinal);

        StreamActionsDbContext dbContext = await this._contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                DbConnection connection = dbContext.Database.GetDbConnection();
                DbCommand checkpointCommand = connection.CreateCommand();
                await using (checkpointCommand.ConfigureAwait(false))
                {
                    checkpointCommand.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    _ = await checkpointCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                DbCommand vacuumCommand = connection.CreateCommand();
                await using (vacuumCommand.ConfigureAwait(false))
                {
                    vacuumCommand.CommandText = $"VACUUM INTO '{escapedPath}';";
                    _ = await vacuumCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }

        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        int maxFiles = settings.MaxBackupFiles;
        if (maxFiles > 0)
        {
            PruneOldBackups(backupDir, maxFiles);
        }

        LogBackupCreated(this._logger, backupPath);
        return backupPath;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        InstanceSettings settings = await this._instanceSettingsService.GetAsync(stoppingToken).ConfigureAwait(false);
        int intervalHours = Math.Max(1, settings.BackupIntervalHours);
        TimeSpan interval = TimeSpan.FromHours(intervalHours);

        using PeriodicTimer timer = new(interval, this._timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                _ = await this.TriggerBackupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Background loop must log and continue on transient backup failures
            catch (Exception ex)
            {
                LogBackupFailed(this._logger, ex);
            }
#pragma warning restore CA1031
        }
    }

    /// <summary>
    /// Deletes backup files beyond the configured rolling retention count, keeping the newest files by filename order.
    /// </summary>
    /// <param name="backupDir">The directory containing backup files.</param>
    /// <param name="maxBackupFiles">The maximum number of backup files to retain.</param>
    private static void PruneOldBackups(string backupDir, int maxBackupFiles)
    {
        List<FileInfo> files = [.. new DirectoryInfo(backupDir)
            .GetFiles(_backupFileSearchPattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(static f => f.Name, StringComparer.Ordinal)];

        for (int i = maxBackupFiles; i < files.Count; i++)
        {
            files[i].Delete();
        }
    }

    /// <summary>
    /// Logs an informational message when a SQLite backup file is created.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="backupPath">The full path to the created backup file.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Created SQLite backup at {BackupPath}.")]
    private static partial void LogBackupCreated(ILogger logger, string backupPath);

    /// <summary>
    /// Logs an error message when a scheduled database backup fails.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception thrown during the backup attempt.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled database backup failed.")]
    private static partial void LogBackupFailed(ILogger logger, Exception exception);
}
