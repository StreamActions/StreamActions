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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Daemon.Telemetry;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using Xunit;

namespace StreamActions.Daemon.Tests;

[Collection("SentrySdk")]
public sealed class SentryBootstrapTests
{
    #region BuildOptions
    [Fact]
    [Trait("Member", "BuildOptions")]
    public void BuildOptions_WhenConfigured_SetsPiiFalseCachePathAndTags()
    {
        BootstrapOptions bootstrap = new()
        {
            Sentry = new SentryBootstrapOptions
            {
                Enabled = true,
                Dsn = "http://publickey@127.0.0.1:9999/1",
                Environment = "staging",
                TracesSampleRate = 0.25,
            },
            Paths = new PathBootstrapOptions
            {
                DataDir = Path.Combine(Path.GetTempPath(), "sa-test-data"),
            },
        };
        SecretRedactor redactor = new();

        SentryOptions options = SentryBootstrapInitializer.BuildOptions(bootstrap, redactor);

        _ = options.SendDefaultPii.Should().BeFalse();
        _ = options.Dsn.Should().Be("http://publickey@127.0.0.1:9999/1");
        _ = options.Environment.Should().Be("staging");
        _ = options.TracesSampleRate.Should().Be(0.25);
        _ = options.MaxCacheItems.Should().Be(30);
        _ = options.CacheDirectoryPath.Should().Be(Path.Combine(bootstrap.Paths.DataDir, "sentry-cache"));
        _ = options.Release.Should().NotBeNullOrWhiteSpace();
    }
    #endregion

    #region InitializePreDatabase
    [Fact]
    [Trait("Member", "InitializePreDatabase")]
    public async Task InitializePreDatabase_WhenCapturingEventAndBreadcrumb_ScrubsSecretsAndStampsTagsOnLoopbackKestrel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        EphemeralSentryReceiver receiver = await EphemeralSentryReceiver.StartAsync(ct);
        await using (receiver.ConfigureAwait(false))
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"sa-sentry-{Guid.NewGuid():N}");
            string dataDir = Path.Combine(tempRoot, "data");
            string configDir = Path.Combine(tempRoot, "config");
            _ = Directory.CreateDirectory(dataDir);
            _ = Directory.CreateDirectory(configDir);

            try
            {
                BootstrapOptions bootstrap = new()
                {
                    Database = new DatabaseBootstrapOptions { Provider = DatabaseProvider.Postgres },
                    Node = new NodeBootstrapOptions { Mode = NodeMode.Source },
                    Sentry = new SentryBootstrapOptions
                    {
                        Enabled = true,
                        Dsn = receiver.Dsn,
                        Environment = "ci-test",
                    },
                    Paths = new PathBootstrapOptions
                    {
                        DataDir = dataDir,
                        ConfigDir = configDir,
                    },
                };

                SecretRedactor redactor = new();
                redactor.RegisterSecret("super_secret_oauth_value_99");

                using IDisposable? sdk = SentryBootstrapInitializer.InitializePreDatabase(bootstrap, redactor);
                _ = sdk.Should().NotBeNull();

                SentrySdk.AddBreadcrumb(
                    message: "Calling API with Authorization: Bearer top_secret_jwt_123",
                    category: "http",
                    data: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["token"] = "super_secret_oauth_value_99",
                    });

                InvalidOperationException exception = new("Migration failed with super_secret_oauth_value_99 and Bearer inline_secret_abc");
                _ = SentrySdk.CaptureException(exception);

                await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));

                string envelope = await receiver.WaitForEnvelopeAsync(TimeSpan.FromSeconds(5), ct);
                _ = envelope.Should().Contain("\"db.provider\":\"Postgres\"");
                _ = envelope.Should().Contain("\"node.mode\":\"Source\"");
                _ = envelope.Should().Contain("\"runtime.container\":");
                _ = envelope.Should().Contain("\"plugins.external_count\":\"0\"");
                _ = envelope.Should().Contain("\"release\":");
                _ = envelope.Should().Contain("[REDACTED]");
                _ = envelope.Should().NotContain("super_secret_oauth_value_99");
                _ = envelope.Should().NotContain("top_secret_jwt_123");
                _ = envelope.Should().NotContain("inline_secret_abc");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }
    }

    [Fact]
    [Trait("Member", "FilterAndScrubEvent")]
    public void FilterAndScrubEvent_WhenNoiseOrStreamerSpaceExceptionsOccur_DropsEvents()
    {
        SecretRedactor redactor = new();

        SentryEvent canceledEvent = new(new OperationCanceledException("shutting down"));
        _ = SentryBootstrapInitializer.FilterAndScrubEvent(canceledEvent, redactor, isUpstreamOnly: true).Should().BeNull();

        SentryEvent taskCanceledEvent = new(new TaskCanceledException("task canceled"));
        _ = SentryBootstrapInitializer.FilterAndScrubEvent(taskCanceledEvent, redactor, isUpstreamOnly: true).Should().BeNull();

        InvalidOperationException streamerSpaceEx = new("user script error");
        streamerSpaceEx.Data["streamactions.streamer_space"] = true;
        SentryEvent streamerSpaceEvent = new(streamerSpaceEx);
        _ = SentryBootstrapInitializer.FilterAndScrubEvent(streamerSpaceEvent, redactor, isUpstreamOnly: true).Should().BeNull();

        SentryEvent scriptEngineEvent = new(new FakeScriptEngineException("SyntaxError"));
        _ = SentryBootstrapInitializer.FilterAndScrubEvent(scriptEngineEvent, redactor, isUpstreamOnly: true).Should().BeNull();

        SentryEvent externalHttpEvent = new(new StreamerExternalHttpException("502 Bad Gateway"));
        _ = SentryBootstrapInitializer.FilterAndScrubEvent(externalHttpEvent, redactor, isUpstreamOnly: true).Should().BeNull();

        InvalidOperationException thirdPartyPluginEx = new("third party plugin boom");
        thirdPartyPluginEx.Data["plugin.third_party"] = true;
        SentryEvent thirdPartyEvent = new(thirdPartyPluginEx);
        _ = SentryBootstrapInitializer.FilterAndScrubEvent(thirdPartyEvent, redactor, isUpstreamOnly: true).Should().BeNull();
        _ = SentryBootstrapInitializer.FilterAndScrubEvent(thirdPartyEvent, redactor, isUpstreamOnly: false).Should().NotBeNull();
    }

    [Fact]
    [Trait("Member", "ScrubBreadcrumb")]
    public void ScrubBreadcrumb_WhenBreadcrumbContainsSecrets_RedactsMessageAndData()
    {
        SecretRedactor redactor = new();
        redactor.RegisterSecret("twitch_secret_xyz");

        Breadcrumb original = new(
            message: "Request failed with Authorization: Bearer my_jwt_token",
            type: "http",
            data: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["secret"] = "twitch_secret_xyz",
            },
            category: "network",
            level: BreadcrumbLevel.Warning);

        Breadcrumb scrubbed = SentryBootstrapInitializer.ScrubBreadcrumb(original, redactor);

        _ = scrubbed.Message.Should().NotContain("my_jwt_token");
        _ = scrubbed.Message.Should().Contain("[REDACTED]");
        _ = scrubbed.Data.Should().ContainKey("secret");
        _ = scrubbed.Data!["secret"].Should().Be("[REDACTED]");
    }
    #endregion

    #region SyncSettings
    [Fact]
    [Trait("Member", "SyncSettings")]
    public async Task SyncSettings_WhenInstanceSettingsUpdated_WritesBootstrapSentryJsonAtomically()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string tempRoot = Path.Combine(Path.GetTempPath(), $"sa-sentry-sync-{Guid.NewGuid():N}");
        string configDir = Path.Combine(tempRoot, "config");
        _ = Directory.CreateDirectory(configDir);

        try
        {
            FakeInstanceSettingsService settingsService = new();
            FakePluginManager pluginManager = new();
            IOptions<BootstrapOptions> bootstrapOptions = Options.Create(new BootstrapOptions
            {
                Paths = new PathBootstrapOptions { ConfigDir = configDir },
                Sentry = new SentryBootstrapOptions { Environment = "production" },
            });

            using SentryBootstrapSyncService syncService = new(
                settingsService,
                pluginManager,
                bootstrapOptions,
                NullLogger<SentryBootstrapSyncService>.Instance);

            await syncService.StartAsync(ct);

            InstanceSettings updated = new()
            {
                Id = Guid.NewGuid(),
                SentryEnabled = true,
                SentryCustomDsn = "http://custom@127.0.0.1:1234/2",
                SentryTracesSampleRate = 0.5,
            };

            await settingsService.UpdateAsync(updated, ct);

            SentryBootstrapConfig? loaded = SentryBootstrapInitializer.LoadBootstrapConfig(configDir);
            _ = loaded.Should().NotBeNull();
            _ = loaded!.Enabled.Should().BeTrue();
            _ = loaded.Dsn.Should().Be("http://custom@127.0.0.1:1234/2");
            _ = loaded.TracesSampleRate.Should().Be(0.5);
            _ = loaded.IsCustomDsn.Should().BeTrue();

            await syncService.StopAsync(ct);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Member", "LoadBootstrapConfig")]
    public void LoadBootstrapConfig_WhenFileMalformed_ReturnsNullGracefully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"sa-sentry-bad-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, SentryBootstrapInitializer.BootstrapFileName), "{ invalid json ");
            SentryBootstrapConfig? result = SentryBootstrapInitializer.LoadBootstrapConfig(tempDir);
            _ = result.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
    #endregion

    private sealed class FakeScriptEngineException : Exception
    {
        public FakeScriptEngineException()
        {
        }

        public FakeScriptEngineException(string message) : base(message)
        {
        }

        public FakeScriptEngineException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    private sealed class StreamerExternalHttpException : Exception
    {
        public StreamerExternalHttpException()
        {
        }

        public StreamerExternalHttpException(string message) : base(message)
        {
        }

        public StreamerExternalHttpException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    private sealed class FakeInstanceSettingsService : IInstanceSettingsService
    {
        private InstanceSettings _current = new() { Id = Guid.NewGuid() };

        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged;

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._current);

        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default)
        {
            this._current = settings;
            this.SettingsChanged?.Invoke(this, new InstanceSettingsChangedEventArgs(settings));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakePluginManager : IPluginManager
    {
        public IReadOnlyCollection<IPlugin> Plugins => [];

        public ValueTask InitializeAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ShutdownAllAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public IPlugin? GetPlugin(string pluginId) => null;
    }

    private sealed class EphemeralSentryReceiver : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly ConcurrentQueue<string> _envelopes;

        private EphemeralSentryReceiver(
            WebApplication app,
            string dsn,
            ConcurrentQueue<string> envelopes)
        {
            this._app = app;
            this.Dsn = dsn;
            this._envelopes = envelopes;
        }

        public string Dsn { get; }

        public static async Task<EphemeralSentryReceiver> StartAsync(CancellationToken cancellationToken)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

            WebApplication app = builder.Build();
            ConcurrentQueue<string> envelopes = new();

            _ = app.MapPost("/api/1/envelope/", async (HttpContext context) =>
            {
                Stream bodyStream = context.Request.Body;
                bool isGzip = string.Equals(context.Request.Headers.ContentEncoding, "gzip", StringComparison.OrdinalIgnoreCase);

                if (isGzip)
                {
                    GZipStream gzip = new(bodyStream, CompressionMode.Decompress);
                    await using (gzip.ConfigureAwait(false))
                    {
                        using StreamReader reader = new(gzip);
                        envelopes.Enqueue(await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false));
                    }
                }
                else
                {
                    using StreamReader reader = new(bodyStream);
                    envelopes.Enqueue(await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false));
                }

                return Results.Ok(new { id = Guid.NewGuid().ToString("N") });
            });

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            string address = app.Urls.First();
            int port = new Uri(address).Port;
            return new EphemeralSentryReceiver(app, $"http://publickey@127.0.0.1:{port}/1", envelopes);
        }

        public async Task<string> WaitForEnvelopeAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (this._envelopes.TryDequeue(out string? envelope))
                {
                    return envelope;
                }

                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for Sentry envelope on loopback Kestrel receiver.");
        }

        public async ValueTask DisposeAsync()
        {
            await this._app.StopAsync().ConfigureAwait(false);
            await this._app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
