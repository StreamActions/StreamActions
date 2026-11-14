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

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Daemon.Services;
using StreamActions.Database;
using StreamActions.Database.Providers;
using Xunit;

namespace StreamActions.Daemon.Tests;

public sealed class DaemonCommandProcessorAndIpcTests
{
    #region ExecuteCommandAsync
    [Fact]
    [Trait("Member", "ExecuteCommandAsync")]
    public async Task ExecuteCommandAsync_WhenInvokedWithStatusBootstrapTokenBackupAndUnlockUser_ExecutesEachCommand()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteDaemonEnvironment env = await TestSqliteDaemonEnvironment.CreateAsync(ct);

        string statusResult = await env.Processor.ExecuteCommandAsync("status", ct);
        _ = statusResult.Should().Contain("Status: Running");
        _ = statusResult.Should().Contain("Mode: AllInOne");
        _ = statusResult.Should().Contain("Database: Sqlite (Connected)");

        string tokenResult = await env.Processor.ExecuteCommandAsync("bootstrap-token", ct);
        _ = tokenResult.Should().Contain("Bootstrap token (valid for 5 minutes):");

        string backupResult = await env.Processor.ExecuteCommandAsync("backup", ct);
        _ = backupResult.Should().Contain("Backup created:");
        _ = Directory.GetFiles(env.BackupDir, "streamactions-backup-*.db").Should().ContainSingle();

        Guid userId = Guid.NewGuid();
        StreamActionsDbContext dbContext = await env.ContextFactory.CreateDbContextAsync(ct);
        await using (dbContext.ConfigureAwait(false))
        {
            _ = dbContext.PanelUsers.Add(new PanelUser
            {
                Id = userId,
                DisplayName = "streamer_admin",
                IsRoot = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            _ = dbContext.PanelUserMfaCredentials.Add(new PanelUserMfaCredential
            {
                Id = Guid.NewGuid(),
                PanelUserId = userId,
                Method = MfaMethod.Totp,
                CredentialDataEncrypted = "encrypted-seed",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            _ = await dbContext.SaveChangesAsync(ct);
        }

        string unlockResult = await env.Processor.ExecuteCommandAsync("unlock-user streamer_admin", ct);
        _ = unlockResult.Should().Contain("Unlocked user 'streamer_admin'");
        _ = unlockResult.Should().Contain("1 MFA credential(s)");

        string missingUserResult = await env.Processor.ExecuteCommandAsync("unlock-user nobody", ct);
        _ = missingUserResult.Should().Be("User 'nobody' not found.");

        string usageResult = await env.Processor.ExecuteCommandAsync("unlock-user", ct);
        _ = usageResult.Should().Be("Usage: unlock-user <username>");

        string unknownResult = await env.Processor.ExecuteCommandAsync("bogus", ct);
        _ = unknownResult.Should().Contain("Unknown command 'bogus'");
    }
    #endregion

    #region LocalIpc
    [Fact]
    [Trait("Member", "SendCommandAsync")]
    public async Task LocalIpc_WhenServerRunning_RoundTripsCommandsOverNamedPipeOrUnixSocket()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteDaemonEnvironment env = await TestSqliteDaemonEnvironment.CreateAsync(ct);

        using LocalIpcCommandServer server = new(
            env.Processor,
            env.BootstrapOptions,
            NullLogger<LocalIpcCommandServer>.Instance);

        await server.StartAsync(ct);

        try
        {
            string statusResponse = await LocalIpcCommandClient.SendCommandAsync(
                env.BootstrapOptions.Value,
                "status",
                TimeSpan.FromSeconds(5),
                ct);
            _ = statusResponse.Should().Contain("Status: Running");

            using StringWriter output = new();
            using StringWriter error = new();
            int exitCode = await LocalIpcCommandClient.ExecuteAsync(
                env.BootstrapOptions.Value,
                "bootstrap-token",
                output,
                error,
                TimeSpan.FromSeconds(5),
                ct);

            _ = exitCode.Should().Be(0);
            _ = output.ToString().Should().Contain("Bootstrap token (valid for 5 minutes):");
            _ = error.ToString().Should().BeEmpty();
        }
        finally
        {
            await server.StopAsync(ct);
        }
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task LocalIpcCommandClient_WhenNoDaemonListening_ReturnsExitCodeOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BootstrapOptions options = new()
        {
            Paths = new PathBootstrapOptions
            {
                DataDir = Path.Combine(Path.GetTempPath(), $"sa-no-ipc-{Guid.NewGuid():N}"),
            },
            Node = new NodeBootstrapOptions
            {
                InstanceId = Guid.NewGuid().ToString("N"),
            },
        };

        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = await LocalIpcCommandClient.ExecuteAsync(
            options,
            "status",
            output,
            error,
            TimeSpan.FromMilliseconds(200),
            ct);

        _ = exitCode.Should().Be(1);
        _ = error.ToString().Should().Contain("Failed to execute daemon command:");
    }
    #endregion

    #region ProcessReaderAsync
    [Fact]
    [Trait("Member", "ProcessReaderAsync")]
    public async Task InteractiveConsoleService_WhenCommandsAndExitEntered_ExecutesCommandsAndStopsHost()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteDaemonEnvironment env = await TestSqliteDaemonEnvironment.CreateAsync(ct);
        FakeHostApplicationLifetime lifetime = new();
        using InteractiveConsoleService consoleService = new(
            env.Processor,
            lifetime,
            NullLogger<InteractiveConsoleService>.Instance);

        using StringReader reader = new("status\nhelp\nexit\n");
        using StringWriter writer = new();

        await consoleService.ProcessReaderAsync(reader, writer, ct);

        string output = writer.ToString();
        _ = output.Should().Contain("Status: Running");
        _ = output.Should().Contain("Available commands:");
        _ = output.Should().Contain("Shutting down StreamActions daemon...");
        _ = lifetime.StopRequested.Should().BeTrue();
    }
    #endregion

    #region GracefulShutdownLifecycleService
    [Fact]
    [Trait("Member", "StopAsync")]
    public async Task GracefulShutdownLifecycleService_WhenStopped_ShutsDownPluginsAndCheckpointsSqlite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteDaemonEnvironment env = await TestSqliteDaemonEnvironment.CreateAsync(ct);
        FakeTrackingPluginManager pluginManager = new();

        GracefulShutdownLifecycleService lifecycle = new(
            pluginManager,
            env.ContextFactory,
            env.BootstrapOptions,
            NullLogger<GracefulShutdownLifecycleService>.Instance);

        await lifecycle.StoppingAsync(ct);
        await lifecycle.StopAsync(ct);
        await lifecycle.StoppedAsync(ct);

        _ = pluginManager.ShutdownCalled.Should().BeTrue();
        _ = lifecycle.Phases.Should().Equal(["StoppingAsync", "StopAsync", "StoppedAsync"]);
    }
    #endregion

    private sealed class FakeTrackingPluginManager : IPluginManager
    {
        public bool ShutdownCalled { get; private set; }

        public IReadOnlyCollection<IPlugin> Plugins => [];

        public ValueTask InitializeAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ShutdownAllAsync(CancellationToken cancellationToken = default)
        {
            this.ShutdownCalled = true;
            return ValueTask.CompletedTask;
        }

        public IPlugin? GetPlugin(string pluginId) => null;
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => this.StopRequested = true;
    }

    private sealed class TestSqliteDaemonEnvironment : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string _rootDir;

        private TestSqliteDaemonEnvironment(
            ServiceProvider provider,
            string rootDir,
            string backupDir,
            IDbContextFactory<StreamActionsDbContext> contextFactory,
            DaemonCommandProcessor processor,
            IOptions<BootstrapOptions> bootstrapOptions)
        {
            this._provider = provider;
            this._rootDir = rootDir;
            this.BackupDir = backupDir;
            this.ContextFactory = contextFactory;
            this.Processor = processor;
            this.BootstrapOptions = bootstrapOptions;
        }

        public string BackupDir { get; }

        public IDbContextFactory<StreamActionsDbContext> ContextFactory { get; }

        public DaemonCommandProcessor Processor { get; }

        public IOptions<BootstrapOptions> BootstrapOptions { get; }

        public static async Task<TestSqliteDaemonEnvironment> CreateAsync(CancellationToken cancellationToken)
        {
            string rootDir = Path.Combine(Path.GetTempPath(), $"sa-cmd-test-{Guid.NewGuid():N}");
            string dataDir = Path.Combine(rootDir, "data");
            string configDir = Path.Combine(rootDir, "config");
            string backupDir = Path.Combine(rootDir, "backups");
            _ = Directory.CreateDirectory(dataDir);
            _ = Directory.CreateDirectory(configDir);
            _ = Directory.CreateDirectory(backupDir);

            IOptions<BootstrapOptions> bootstrapOptions = Options.Create(new BootstrapOptions
            {
                Paths = new PathBootstrapOptions
                {
                    DataDir = dataDir,
                    ConfigDir = configDir,
                    BackupDir = backupDir,
                },
                Node = new NodeBootstrapOptions
                {
                    InstanceId = Guid.NewGuid().ToString("N"),
                    Mode = NodeMode.AllInOne,
                },
            });

            ServiceCollection services = new();
            _ = services.AddSingleton(bootstrapOptions);
            _ = services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));
            _ = services.AddLogging();
            _ = services.AddDbContextFactory<SqliteStreamActionsDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(dataDir, "streamactions.db")}"));
            _ = services.AddSingleton<IDbContextFactory<StreamActionsDbContext>>(static sp =>
                new ForwardingFactory(sp.GetRequiredService<IDbContextFactory<SqliteStreamActionsDbContext>>()));

            ServiceProvider provider = services.BuildServiceProvider();
            IDbContextFactory<StreamActionsDbContext> contextFactory = provider.GetRequiredService<IDbContextFactory<StreamActionsDbContext>>();

            StreamActionsDbContext dbContext = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                _ = await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            }

            ConsoleBootstrapTokenService tokenService = new(
                provider.GetRequiredService<TimeProvider>(),
                NullLogger<ConsoleBootstrapTokenService>.Instance);

            FakeBackupService backupService = new(backupDir);
            FakePluginManager pluginManager = new();
            DaemonCommandProcessor processor = new(
                contextFactory,
                pluginManager,
                tokenService,
                backupService,
                bootstrapOptions);

            return new TestSqliteDaemonEnvironment(
                provider,
                rootDir,
                backupDir,
                contextFactory,
                processor,
                bootstrapOptions);
        }

        public void Dispose()
        {
            this._provider.Dispose();
            if (Directory.Exists(this._rootDir))
            {
                try
                {
                    Directory.Delete(this._rootDir, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private sealed class ForwardingFactory(IDbContextFactory<SqliteStreamActionsDbContext> inner)
            : IDbContextFactory<StreamActionsDbContext>
        {
            public StreamActionsDbContext CreateDbContext() => inner.CreateDbContext();

            public async Task<StreamActionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                await inner.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        }

        private sealed class FakePluginManager : IPluginManager
        {
            public IReadOnlyCollection<IPlugin> Plugins => [];

            public ValueTask InitializeAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

            public ValueTask ShutdownAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

            public IPlugin? GetPlugin(string pluginId) => null;
        }

        private sealed class FakeBackupService(string backupDir) : IDatabaseBackupService
        {
            public async ValueTask<string> TriggerBackupAsync(CancellationToken cancellationToken = default)
            {
                string filePath = Path.Combine(backupDir, "streamactions-backup-test.db");
                await File.WriteAllTextAsync(filePath, "sqlite-backup", cancellationToken).ConfigureAwait(false);
                return filePath;
            }
        }
    }
}
