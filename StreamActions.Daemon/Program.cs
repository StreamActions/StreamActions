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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StreamActions.Core;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Services;
using StreamActions.Daemon.Cli;
using StreamActions.Daemon.Services;
using StreamActions.Daemon.Telemetry;
using StreamActions.Database;
using StreamActions.Database.Services;
using StreamActions.Web;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Daemon;

/// <summary>
/// Host entrypoint for the StreamActions daemon and integrated web server.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "WebApplicationFactory<Program> requires public partial class Program.")]
[SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "WebApplicationFactory<Program> requires non-static partial class Program.")]
public partial class Program
{
    /// <summary>
    /// Application entrypoint.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        DaemonCliOptions cliOptions = DaemonCliParser.Parse(args);
        if (cliOptions.IsExecMode)
        {
            BootstrapOptions ipcBootstrap = DaemonBootstrapConfiguration.BuildPreDatabaseOptions(cliOptions);
            return await LocalIpcCommandClient.ExecuteAsync(
                ipcBootstrap,
                cliOptions.ExecCommand ?? string.Empty,
                Console.Out,
                Console.Error).ConfigureAwait(false);
        }

        BootstrapOptions preDbBootstrap = DaemonBootstrapConfiguration.BuildPreDatabaseOptions(cliOptions);
        SecretRedactor preDbRedactor = new();
        using IDisposable? sentryHandle = SentryBootstrapInitializer.InitializePreDatabase(preDbBootstrap, preDbRedactor);

        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder([.. cliOptions.RemainingArgs]);
            DaemonBootstrapConfiguration.ApplyToConfigurationBuilder(builder.Configuration, cliOptions);

            _ = builder.Host.UseWindowsService();
            _ = builder.Host.UseSystemd();

            _ = builder.Host.ConfigureServices((hostContext, services) =>
            {
                BootstrapOptions bootstrap = new();
                hostContext.Configuration.GetSection(BootstrapOptions.SectionName).Bind(bootstrap);

                EnsureBootstrapDirectories(bootstrap);

                _ = services.AddStreamActionsCore(hostContext.Configuration);
                _ = services.AddStreamActionsDatabase(hostContext.Configuration);
                _ = services.AddStreamActionsWeb();

                string keyRingDir = string.IsNullOrWhiteSpace(bootstrap.DataProtection.KeyRingDir)
                    ? Path.Combine(bootstrap.Paths.DataDir, "keys")
                    : bootstrap.DataProtection.KeyRingDir;
                _ = Directory.CreateDirectory(keyRingDir);

                IDataProtectionBuilder dataProtection = services.AddDataProtection()
                    .SetApplicationName("StreamActions")
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingDir));

                if (OperatingSystem.IsWindows())
                {
                    _ = dataProtection.ProtectKeysWithDpapi();
                }

                _ = services.AddHealthChecks()
                    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);

                services.TryAddSingleton<ConsoleBootstrapTokenService>();
                services.TryAddSingleton<DaemonCommandProcessor>();
                services.TryAddSingleton<GracefulShutdownLifecycleService>();
                _ = services.AddHostedService(static sp => sp.GetRequiredService<GracefulShutdownLifecycleService>());
                services.TryAddSingleton<SentryBootstrapSyncService>();
                _ = services.AddHostedService(static sp => sp.GetRequiredService<SentryBootstrapSyncService>());
                _ = services.AddHostedService<LocalIpcCommandServer>();
                _ = services.AddHostedService<InteractiveConsoleService>();
            });

            _ = builder.Logging.Configure(static _ => { });
            if (Enum.TryParse(preDbBootstrap.BootstrapLogLevel, ignoreCase: true, out LogLevel minLevel))
            {
                _ = builder.Logging.SetMinimumLevel(minLevel);
            }

            WebApplication app = builder.Build();

            await InitializeDatabaseAsync(app.Services).ConfigureAwait(false);

            bool noWeb = cliOptions.NoWeb
                || app.Configuration.GetValue<bool>("NoWeb")
                || app.Configuration.GetValue<bool>("Bootstrap:NoWeb");

            if (noWeb)
            {
                _ = app.MapHealthChecks("/healthz", new HealthCheckOptions
                {
                    Predicate = static _ => false,
                });
                _ = app.MapHealthChecks("/readyz", new HealthCheckOptions
                {
                    Predicate = static r => r.Tags.Contains("ready"),
                });
            }
            else
            {
                _ = app.MapStreamActionsWeb();
            }

            await app.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = SentrySdk.CaptureException(ex);
            throw;
        }
        finally
        {
            await SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ensures all configured bootstrap directories exist on disk.
    /// </summary>
    /// <param name="bootstrap">The resolved host bootstrap options.</param>
    private static void EnsureBootstrapDirectories(BootstrapOptions bootstrap)
    {
        _ = Directory.CreateDirectory(bootstrap.Paths.ConfigDir);
        _ = Directory.CreateDirectory(bootstrap.Paths.DataDir);
        _ = Directory.CreateDirectory(bootstrap.Paths.LogDir);
        _ = Directory.CreateDirectory(bootstrap.Paths.PluginsDir);
        _ = Directory.CreateDirectory(bootstrap.Paths.BackupDir);
    }

    /// <summary>
    /// Creates or migrates the database schema on startup, taking a pre-migration backup for SQLite when pending migrations exist.
    /// </summary>
    /// <param name="services">The application service provider.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        IDbContextFactory<StreamActionsDbContext> contextFactory = services.GetRequiredService<IDbContextFactory<StreamActionsDbContext>>();
        BootstrapOptions bootstrap = services.GetRequiredService<IOptions<BootstrapOptions>>().Value;

        StreamActionsDbContext dbContext = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            IEnumerable<string> allMigrations = dbContext.Database.GetMigrations();
            if (!allMigrations.Any())
            {
                _ = await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
                return;
            }

            List<string> pendingMigrations = [.. await dbContext.Database.GetPendingMigrationsAsync().ConfigureAwait(false)];
            if (pendingMigrations.Count == 0)
            {
                return;
            }

            if (bootstrap.Database.Provider == DatabaseProvider.Sqlite)
            {
                _ = await DatabaseMigrationService.CreateSqlitePreMigrationBackupAsync(
                    dbContext,
                    pendingMigrations[^1],
                    bootstrap.Paths.BackupDir).ConfigureAwait(false);
            }

            await DatabaseMigrationService.AcquireAdvisoryLockAsync(dbContext, bootstrap.Database.Provider).ConfigureAwait(false);
            try
            {
                await dbContext.Database.MigrateAsync().ConfigureAwait(false);
            }
            finally
            {
                await DatabaseMigrationService.ReleaseAdvisoryLockAsync(dbContext, bootstrap.Database.Provider).ConfigureAwait(false);
            }
        }
    }
}
