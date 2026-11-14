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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;
using StreamActions.Daemon.Services;
using StreamActions.Daemon.Telemetry;
using StreamActions.Database;
using System.Net;
using Xunit;

namespace StreamActions.Daemon.Tests;

[Collection("SentrySdk")]
public sealed class DaemonHostIntegrationTests
{
    #region WebApplicationFactory
    [Fact]
    [Trait("Member", "Main")]
    public async Task Host_WhenBootedWithZeroStaticConfigAndNoWeb_CreatesSqliteDbProtectsKeysAndServesHealthProbes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using DaemonWebApplicationFactory factory = new(dirs, noWeb: true);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage healthResponse = await client.GetAsync(new Uri("/healthz", UriKind.Relative), ct);
        HttpResponseMessage readyResponse = await client.GetAsync(new Uri("/readyz", UriKind.Relative), ct);
        HttpResponseMessage webUiResponse = await client.GetAsync(new Uri("/", UriKind.Relative), ct);

        _ = healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = webUiResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string expectedDbPath = Path.Combine(dirs.DataDir, "streamactions.db");
        _ = File.Exists(expectedDbPath).Should().BeTrue();

        string expectedKeyRingDir = Path.Combine(dirs.DataDir, "keys");
        _ = Directory.Exists(expectedKeyRingDir).Should().BeTrue();

        IDataProtectionProvider dpProvider = factory.Services.GetRequiredService<IDataProtectionProvider>();
        IDataProtector protector = dpProvider.CreateProtector("StreamActions.Tests");
        string protectedValue = protector.Protect("secret-payload");
        _ = protector.Unprotect(protectedValue).Should().Be("secret-payload");

        IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
        InstanceSettings current = await settingsService.GetAsync(ct);
        current.SentryEnabled = true;
        current.SentryTracesSampleRate = 0.4;
        await settingsService.UpdateAsync(current, ct);

        SentryBootstrapConfig? synced = SentryBootstrapInitializer.LoadBootstrapConfig(dirs.ConfigDir);
        _ = synced.Should().NotBeNull();
        _ = synced!.Enabled.Should().BeTrue();
        _ = synced.TracesSampleRate.Should().Be(0.4);
    }

    [Fact]
    [Trait("Member", "Main")]
    public async Task Host_WhenBootedWithWebEnabled_ServesBlazorUiAndHealthProbes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        using DaemonWebApplicationFactory factory = new(dirs, noWeb: false);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage healthResponse = await client.GetAsync(new Uri("/healthz", UriKind.Relative), ct);
        HttpResponseMessage readyResponse = await client.GetAsync(new Uri("/readyz", UriKind.Relative), ct);
        HttpResponseMessage homeResponse = await client.GetAsync(new Uri("/", UriKind.Relative), ct);

        _ = healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        _ = homeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Member", "StoppedAsync")]
    public async Task Host_WhenShutDown_ExecutesThreePhaseGracefulShutdownLifecycle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDaemonDirectories dirs = new();
        GracefulShutdownLifecycleService lifecycleService;

        DaemonWebApplicationFactory factory = new(dirs, noWeb: true);
        await using (factory.ConfigureAwait(false))
        {
            using HttpClient client = factory.CreateClient();
            HttpResponseMessage readyResponse = await client.GetAsync(new Uri("/readyz", UriKind.Relative), ct);
            _ = readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            lifecycleService = factory.Services.GetRequiredService<GracefulShutdownLifecycleService>();
        }

        _ = lifecycleService.Phases.Should().ContainInOrder(
            "StartingAsync",
            "StartAsync",
            "StartedAsync",
            "StoppingAsync",
            "StopAsync",
            "StoppedAsync");
    }
    #endregion

    #region CheckHealthAsync
    [Fact]
    [Trait("Member", "CheckHealthAsync")]
    public async Task CheckHealthAsync_WhenDatabaseContextThrows_ReturnsUnhealthy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        DatabaseReadinessHealthCheck healthCheck = new(new ThrowingDbContextFactory());

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), ct);

        _ = result.Status.Should().Be(HealthStatus.Unhealthy);
    }
    #endregion

    private sealed class ThrowingDbContextFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<StreamActionsDbContext>
    {
        public StreamActionsDbContext CreateDbContext() => throw new InvalidOperationException("Database unavailable.");

        public Task<StreamActionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<StreamActionsDbContext>(new InvalidOperationException("Database unavailable."));
    }

    private sealed class TempDaemonDirectories : IDisposable
    {
        public TempDaemonDirectories()
        {
            this.Root = Path.Combine(Path.GetTempPath(), $"sa-daemon-test-{Guid.NewGuid():N}");
            this.ConfigDir = Path.Combine(this.Root, "config");
            this.DataDir = Path.Combine(this.Root, "data");
            this.LogDir = Path.Combine(this.Root, "logs");
            this.PluginsDir = Path.Combine(this.Root, "plugins");
            this.BackupDir = Path.Combine(this.Root, "backups");
        }

        public string Root { get; }

        public string ConfigDir { get; }

        public string DataDir { get; }

        public string LogDir { get; }

        public string PluginsDir { get; }

        public string BackupDir { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Root))
            {
                try
                {
                    Directory.Delete(this.Root, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private sealed class DaemonWebApplicationFactory(
        TempDaemonDirectories dirs,
        bool noWeb) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _ = builder.UseEnvironment("Testing");
            _ = builder.UseSetting("NoWeb", noWeb ? "true" : "false");
            _ = builder.UseSetting("Bootstrap:Paths:ConfigDir", dirs.ConfigDir);
            _ = builder.UseSetting("Bootstrap:Paths:DataDir", dirs.DataDir);
            _ = builder.UseSetting("Bootstrap:Paths:LogDir", dirs.LogDir);
            _ = builder.UseSetting("Bootstrap:Paths:PluginsDir", dirs.PluginsDir);
            _ = builder.UseSetting("Bootstrap:Paths:BackupDir", dirs.BackupDir);
            _ = builder.UseSetting("Bootstrap:Node:InstanceId", $"test-{Guid.NewGuid():N}");
            _ = builder.UseSetting("Bootstrap:Sentry:Enabled", "false");
        }
    }
}
