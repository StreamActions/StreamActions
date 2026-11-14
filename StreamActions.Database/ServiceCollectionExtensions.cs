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

using Doka.EntityFrameworkCore.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Database.Interceptors;
using StreamActions.Database.Providers;
using StreamActions.Database.Security;
using StreamActions.Database.Services;

namespace StreamActions.Database;

/// <summary>
/// Dependency injection registration extensions for <c>StreamActions.Database</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core database context factory, provider configuration, Data Protection secret service, caching, backup, and archive services.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStreamActionsDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        BootstrapOptions bootstrap = new();
        configuration.GetSection(BootstrapOptions.SectionName).Bind(bootstrap);

        services.TryAddSingleton<SqliteConnectionInterceptor>();

        switch (bootstrap.Database.Provider)
        {
            case DatabaseProvider.Sqlite:
                string sqliteConnectionString = string.IsNullOrWhiteSpace(bootstrap.Database.ConnectionString)
                    ? $"Data Source={Path.Combine(bootstrap.Paths.DataDir, "streamactions.db")}"
                    : bootstrap.Database.ConnectionString;

                _ = services.AddPooledDbContextFactory<SqliteStreamActionsDbContext>((sp, options) =>
                {
                    SqliteConnectionInterceptor interceptor = sp.GetRequiredService<SqliteConnectionInterceptor>();
                    _ = StreamActionsDbContext.ConfigureDefaultOptions(options)
                        .UseSqlite(sqliteConnectionString)
                        .AddInterceptors(interceptor);
                });
                services.TryAddSingleton<IDbContextFactory<StreamActionsDbContext>>(static sp =>
                    new ForwardingDbContextFactory<SqliteStreamActionsDbContext>(
                        sp.GetRequiredService<IDbContextFactory<SqliteStreamActionsDbContext>>()));
                break;

            case DatabaseProvider.Postgres:
                string postgresConnectionString = RequireConnectionString(bootstrap.Database.ConnectionString, DatabaseProvider.Postgres);
                _ = services.AddPooledDbContextFactory<PostgresStreamActionsDbContext>(options =>
                    _ = StreamActionsDbContext.ConfigureDefaultOptions(options)
                        .UseNpgsql(postgresConnectionString));
                services.TryAddSingleton<IDbContextFactory<StreamActionsDbContext>>(static sp =>
                    new ForwardingDbContextFactory<PostgresStreamActionsDbContext>(
                        sp.GetRequiredService<IDbContextFactory<PostgresStreamActionsDbContext>>()));
                break;

            case DatabaseProvider.MySql:
            case DatabaseProvider.MariaDb:
                string mysqlConnectionString = RequireConnectionString(bootstrap.Database.ConnectionString, bootstrap.Database.Provider);
                MySqlServerVersion mysqlServerVersion = bootstrap.Database.Provider == DatabaseProvider.MariaDb
                    ? MySqlServerVersion.MariaDb(new Version(11, 8, 0))
                    : MySqlServerVersion.MySql(new Version(8, 4, 0));
                _ = services.AddPooledDbContextFactory<MySqlStreamActionsDbContext>(options =>
                    _ = StreamActionsDbContext.ConfigureDefaultOptions(options)
                        .UseMySql(mysqlConnectionString, mysqlServerVersion));
                services.TryAddSingleton<IDbContextFactory<StreamActionsDbContext>>(static sp =>
                    new ForwardingDbContextFactory<MySqlStreamActionsDbContext>(
                        sp.GetRequiredService<IDbContextFactory<MySqlStreamActionsDbContext>>()));
                break;

            case DatabaseProvider.SqlServer:
                string sqlServerConnectionString = RequireConnectionString(bootstrap.Database.ConnectionString, DatabaseProvider.SqlServer);
                _ = services.AddPooledDbContextFactory<SqlServerStreamActionsDbContext>(options =>
                    _ = StreamActionsDbContext.ConfigureDefaultOptions(options)
                        .UseSqlServer(sqlServerConnectionString));
                services.TryAddSingleton<IDbContextFactory<StreamActionsDbContext>>(static sp =>
                    new ForwardingDbContextFactory<SqlServerStreamActionsDbContext>(
                        sp.GetRequiredService<IDbContextFactory<SqlServerStreamActionsDbContext>>()));
                break;

            default:
                throw new InvalidOperationException($"Unsupported database provider: {bootstrap.Database.Provider}.");
        }

        services.TryAddSingleton<SecretProtectionService>();
        services.TryAddSingleton<IWorkspaceKeyValueStore, WorkspaceKeyValueStore>();
        services.TryAddSingleton<IPermissionStore, PermissionStore>();
        services.TryAddSingleton<WorkspaceCacheService>();
        services.TryAddSingleton<DatabaseMigrationService>();
        services.TryAddSingleton<IInstanceSettingsService, InstanceSettingsService>();
        services.TryAddSingleton<IWorkspacePortableArchiveService, WorkspacePortableArchiveService>();
        services.TryAddSingleton<DatabaseBackupService>();
        services.TryAddSingleton<IDatabaseBackupService>(static sp => sp.GetRequiredService<DatabaseBackupService>());
        _ = services.AddHostedService(static sp => sp.GetRequiredService<DatabaseBackupService>());

        return services;
    }

    /// <summary>
    /// Validates that a non-empty connection string is configured for the specified database provider.
    /// </summary>
    /// <param name="connectionString">The configured connection string.</param>
    /// <param name="provider">The database provider requiring the connection string.</param>
    /// <returns>The validated non-empty connection string.</returns>
    private static string RequireConnectionString(string? connectionString, DatabaseProvider provider) =>
        string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"Bootstrap:Database:ConnectionString is required for provider {provider}.")
            : connectionString;

    /// <summary>
    /// Forwards <see cref="IDbContextFactory{TContext}"/> calls for <see cref="StreamActionsDbContext"/> to a provider-specific pooled factory.
    /// </summary>
    /// <typeparam name="TContext">The provider-specific <see cref="StreamActionsDbContext"/> subtype.</typeparam>
    /// <param name="inner">The underlying provider-specific context factory.</param>
    private sealed class ForwardingDbContextFactory<TContext>(IDbContextFactory<TContext> inner)
        : IDbContextFactory<StreamActionsDbContext>
        where TContext : StreamActionsDbContext
    {
        /// <inheritdoc/>
        public StreamActionsDbContext CreateDbContext() => inner.CreateDbContext();

        /// <inheritdoc/>
        public async Task<StreamActionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            await inner.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    }
}
