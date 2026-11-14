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
using StreamActions.Core.Models;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Database.Services;

/// <summary>
/// Provides pre-migration SQLite snapshot creation and multi-provider database advisory lock helpers.
/// </summary>
public sealed class DatabaseMigrationService
{
    /// <summary>
    /// Fixed 64-bit PostgreSQL advisory lock identifier for StreamActions migrations.
    /// </summary>
    private const long _postgresAdvisoryLockId = 8472910452839104L;

    /// <summary>
    /// Named MySQL/MariaDB advisory lock identifier for StreamActions migrations.
    /// </summary>
    private const string _mySqlAdvisoryLockName = "streamactions_migration_lock";

    /// <summary>
    /// Named SQL Server application lock resource identifier for StreamActions migrations.
    /// </summary>
    private const string _sqlServerAdvisoryLockName = "streamactions_migration_lock";

    /// <summary>
    /// Creates a consistent pre-migration SQLite backup using <c>VACUUM INTO</c>.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="targetMigration">The target migration name used in the backup filename.</param>
    /// <param name="outputDirectory">The directory where the pre-migration snapshot will be written.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The full path to the created pre-migration snapshot file.</returns>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "SQLite VACUUM INTO does not support bound parameters for the destination path; single quotes are escaped and the path is sanitized.")]
    public static async ValueTask<string> CreateSqlitePreMigrationBackupAsync(
        StreamActionsDbContext dbContext,
        string targetMigration,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetMigration);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        _ = Directory.CreateDirectory(outputDirectory);
        string sanitizedTarget = SanitizeFileNameSegment(targetMigration);
        string filePath = Path.Combine(outputDirectory, $"streamactions.pre-migration-{sanitizedTarget}.db");
        string escapedPath = filePath.Replace("'", "''", StringComparison.Ordinal);

        await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DbConnection connection = dbContext.Database.GetDbConnection();
            DbCommand command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = $"VACUUM INTO '{escapedPath}';";
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
        }

        return filePath;
    }

    /// <summary>
    /// Acquires a provider-specific advisory lock for safe multi-node migration coordination.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="provider">The configured database provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous lock acquisition.</returns>
    public static async ValueTask AcquireAdvisoryLockAsync(
        StreamActionsDbContext dbContext,
        DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                break;
            case DatabaseProvider.Postgres:
                _ = await dbContext.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_lock({_postgresAdvisoryLockId});",
                    cancellationToken).ConfigureAwait(false);
                break;
            case DatabaseProvider.MySql:
            case DatabaseProvider.MariaDb:
                _ = await dbContext.Database.ExecuteSqlRawAsync(
                    $"SELECT GET_LOCK('{_mySqlAdvisoryLockName}', 30);",
                    cancellationToken).ConfigureAwait(false);
                break;
            case DatabaseProvider.SqlServer:
                _ = await dbContext.Database.ExecuteSqlRawAsync(
                    $"EXEC sp_getapplock @Resource = '{_sqlServerAdvisoryLockName}', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 30000;",
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.");
        }
    }

    /// <summary>
    /// Releases a previously acquired provider-specific advisory lock.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="provider">The configured database provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous lock release.</returns>
    public static async ValueTask ReleaseAdvisoryLockAsync(
        StreamActionsDbContext dbContext,
        DatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                break;
            case DatabaseProvider.Postgres:
                _ = await dbContext.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_unlock({_postgresAdvisoryLockId});",
                    cancellationToken).ConfigureAwait(false);
                break;
            case DatabaseProvider.MySql:
            case DatabaseProvider.MariaDb:
                _ = await dbContext.Database.ExecuteSqlRawAsync(
                    $"SELECT RELEASE_LOCK('{_mySqlAdvisoryLockName}');",
                    cancellationToken).ConfigureAwait(false);
                break;
            case DatabaseProvider.SqlServer:
                _ = await dbContext.Database.ExecuteSqlRawAsync(
                    $"EXEC sp_releaseapplock @Resource = '{_sqlServerAdvisoryLockName}', @LockOwner = 'Session';",
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.");
        }
    }

    /// <summary>
    /// Replaces characters that are invalid in file names with underscores.
    /// </summary>
    /// <param name="input">The raw filename segment.</param>
    /// <returns>The sanitized filename segment.</returns>
    private static string SanitizeFileNameSegment(string input)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(input.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
