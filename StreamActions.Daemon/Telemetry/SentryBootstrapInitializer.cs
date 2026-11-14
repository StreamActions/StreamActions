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

using Sentry.Protocol;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace StreamActions.Daemon.Telemetry;

/// <summary>
/// Configures and initializes pre-database Sentry telemetry with secret redaction, noise filtering, and runtime tags.
/// </summary>
internal static class SentryBootstrapInitializer
{
    /// <summary>
    /// File name for the optional pre-database Sentry configuration file inside <c>ConfigDir</c>.
    /// </summary>
    public const string BootstrapFileName = "bootstrap.sentry.json";

    /// <summary>
    /// JSON serializer options used when reading <c>bootstrap.sentry.json</c>.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Reads <c>{ConfigDir}/bootstrap.sentry.json</c> when present and valid.
    /// </summary>
    /// <param name="configDir">The daemon configuration directory path.</param>
    /// <returns>The parsed <see cref="SentryBootstrapConfig"/>, or <see langword="null"/> if absent or invalid.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Corrupted or unreadable bootstrap.sentry.json must never block daemon startup.")]
    public static SentryBootstrapConfig? LoadBootstrapConfig(string configDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDir);

        string filePath = Path.Combine(configDir, BootstrapFileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SentryBootstrapConfig>(json, _jsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Merges values from <c>{ConfigDir}/bootstrap.sentry.json</c> and CLI opt-in into <paramref name="bootstrap"/>.
    /// </summary>
    /// <param name="bootstrap">The bootstrap options to enrich.</param>
    /// <param name="sentryOptIn">Whether <c>--sentry-opt-in</c> was supplied on the command line.</param>
    public static void ApplyBootstrapFileToOptions(BootstrapOptions bootstrap, bool sentryOptIn)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        SentryBootstrapConfig? fileConfig = LoadBootstrapConfig(bootstrap.Paths.ConfigDir);
        if (fileConfig is not null)
        {
            if (!bootstrap.Sentry.Enabled && fileConfig.Enabled)
            {
                bootstrap.Sentry.Enabled = true;
            }

            if (string.IsNullOrWhiteSpace(bootstrap.Sentry.Dsn) && !string.IsNullOrWhiteSpace(fileConfig.Dsn))
            {
                bootstrap.Sentry.Dsn = fileConfig.Dsn;
            }

            if (string.IsNullOrWhiteSpace(bootstrap.Sentry.Environment) && !string.IsNullOrWhiteSpace(fileConfig.Environment))
            {
                bootstrap.Sentry.Environment = fileConfig.Environment;
            }

            if ((bootstrap.Sentry.TracesSampleRate <= 0D) && (fileConfig.TracesSampleRate > 0D))
            {
                bootstrap.Sentry.TracesSampleRate = fileConfig.TracesSampleRate;
            }
        }

        if (sentryOptIn)
        {
            bootstrap.Sentry.Enabled = true;
        }
    }

    /// <summary>
    /// Builds a configured <see cref="SentryOptions"/> instance for StreamActions telemetry.
    /// </summary>
    /// <param name="bootstrap">The resolved host bootstrap options.</param>
    /// <param name="redactor">The secret redactor applied to events and breadcrumbs.</param>
    /// <param name="isUpstreamOnly">Whether telemetry targets the upstream-only project DSN (dropping third-party plugin exceptions).</param>
    /// <returns>The configured <see cref="SentryOptions"/>.</returns>
    public static SentryOptions BuildOptions(
        BootstrapOptions bootstrap,
        ISecretRedactor redactor,
        bool isUpstreamOnly = true)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(redactor);

        SentryOptions options = new();
        ConfigureOptions(options, bootstrap, redactor, isUpstreamOnly);
        return options;
    }

    /// <summary>
    /// Applies StreamActions Sentry configuration, secret scrubbing, and noise filters to an existing <see cref="SentryOptions"/> instance.
    /// </summary>
    /// <param name="options">The Sentry options instance to configure.</param>
    /// <param name="bootstrap">The resolved host bootstrap options.</param>
    /// <param name="redactor">The secret redactor applied to events and breadcrumbs.</param>
    /// <param name="isUpstreamOnly">Whether telemetry targets the upstream-only project DSN (dropping third-party plugin exceptions).</param>
    public static void ConfigureOptions(
        SentryOptions options,
        BootstrapOptions bootstrap,
        ISecretRedactor redactor,
        bool isUpstreamOnly = true)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(redactor);

        options.Dsn = bootstrap.Sentry.Enabled ? bootstrap.Sentry.Dsn : null;
        options.Environment = string.IsNullOrWhiteSpace(bootstrap.Sentry.Environment)
            ? "production"
            : bootstrap.Sentry.Environment;
        options.TracesSampleRate = bootstrap.Sentry.TracesSampleRate;
        options.SendDefaultPii = false;
        options.CacheDirectoryPath = Path.Combine(bootstrap.Paths.DataDir, "sentry-cache");
        options.MaxCacheItems = 30;
        options.Release = GetReleaseVersion();

        options.SetBeforeSend((sentryEvent, _) => FilterAndScrubEvent(sentryEvent, redactor, isUpstreamOnly));
        options.SetBeforeBreadcrumb((breadcrumb, _) => ScrubBreadcrumb(breadcrumb, redactor));
    }

    /// <summary>
    /// Initializes Sentry before DI and database setup when enabled and a DSN is configured.
    /// </summary>
    /// <param name="bootstrap">The pre-database bootstrap options.</param>
    /// <param name="redactor">The secret redactor instance.</param>
    /// <returns>The initialized Sentry SDK disposable handle, or <see langword="null"/> when Sentry is disabled.</returns>
    public static IDisposable? InitializePreDatabase(BootstrapOptions bootstrap, ISecretRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(redactor);

        if (!bootstrap.Sentry.Enabled || string.IsNullOrWhiteSpace(bootstrap.Sentry.Dsn))
        {
            return null;
        }

        string cacheDir = Path.Combine(bootstrap.Paths.DataDir, "sentry-cache");
        _ = Directory.CreateDirectory(cacheDir);

        SentryBootstrapConfig? fileConfig = LoadBootstrapConfig(bootstrap.Paths.ConfigDir);
        bool isUpstreamOnly = fileConfig?.IsCustomDsn != true;

        IDisposable sdk = SentrySdk.Init(options => ConfigureOptions(options, bootstrap, redactor, isUpstreamOnly));
        ApplyScopeTags(bootstrap, externalPluginCount: 0);
        return sdk;
    }

    /// <summary>
    /// Applies standard StreamActions diagnostic tags to the active Sentry scope.
    /// </summary>
    /// <param name="bootstrap">The host bootstrap options.</param>
    /// <param name="externalPluginCount">The number of loaded external plugins.</param>
    public static void ApplyScopeTags(BootstrapOptions bootstrap, int externalPluginCount)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        SentrySdk.ConfigureScope(scope =>
        {
            scope.SetTag("db.provider", bootstrap.Database.Provider.ToString());
            scope.SetTag("node.mode", bootstrap.Node.Mode.ToString());
            scope.SetTag("runtime.container", IsRunningInContainer() ? "true" : "false");
            scope.SetTag("plugins.external_count", externalPluginCount.ToString(CultureInfo.InvariantCulture));
            scope.SetTag("release", GetReleaseVersion());
        });
    }

    /// <summary>
    /// Returns the semantic release version string stamped onto Sentry events.
    /// </summary>
    /// <returns>The informational or assembly version string.</returns>
    public static string GetReleaseVersion()
    {
        Assembly assembly = typeof(SentryBootstrapInitializer).Assembly;
        AssemblyInformationalVersionAttribute? infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return (infoAttr is not null) && !string.IsNullOrWhiteSpace(infoAttr.InformationalVersion)
            ? infoAttr.InformationalVersion
            : assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    /// <summary>
    /// Filters out noisy or third-party events and redacts secrets from messages, exceptions, and extra data on <paramref name="sentryEvent"/>.
    /// </summary>
    /// <param name="sentryEvent">The Sentry event to evaluate and scrub.</param>
    /// <param name="redactor">The secret redactor applied to event text fields.</param>
    /// <param name="isUpstreamOnly">Whether events tagged as third-party plugin errors should be dropped.</param>
    /// <returns>The scrubbed <see cref="SentryEvent"/>, or <see langword="null"/> if the event should be dropped.</returns>
    internal static SentryEvent? FilterAndScrubEvent(
        SentryEvent sentryEvent,
        ISecretRedactor redactor,
        bool isUpstreamOnly)
    {
        if (ShouldDropEvent(sentryEvent, isUpstreamOnly))
        {
            return null;
        }

        if (sentryEvent.Message is not null)
        {
            sentryEvent.Message = new SentryMessage
            {
                Message = redactor.Redact(sentryEvent.Message.Message),
                Formatted = redactor.Redact(sentryEvent.Message.Formatted),
            };
        }

        if (sentryEvent.SentryExceptions is not null)
        {
            foreach (SentryException sentryException in sentryEvent.SentryExceptions)
            {
                sentryException.Value = redactor.Redact(sentryException.Value);
            }
        }

        if (sentryEvent.Extra.Count > 0)
        {
            List<KeyValuePair<string, string?>> scrubbedExtras = [];
            foreach (KeyValuePair<string, object?> pair in sentryEvent.Extra)
            {
                if (pair.Value is string stringValue)
                {
                    scrubbedExtras.Add(new KeyValuePair<string, string?>(pair.Key, redactor.Redact(stringValue)));
                }
            }

            foreach (KeyValuePair<string, string?> pair in scrubbedExtras)
            {
                sentryEvent.SetExtra(pair.Key, pair.Value);
            }
        }

        return sentryEvent;
    }

    /// <summary>
    /// Returns a copy of <paramref name="breadcrumb"/> with its message and data values redacted by <paramref name="redactor"/>.
    /// </summary>
    /// <param name="breadcrumb">The breadcrumb to scrub.</param>
    /// <param name="redactor">The secret redactor applied to breadcrumb strings.</param>
    /// <returns>A redacted <see cref="Breadcrumb"/> instance.</returns>
    internal static Breadcrumb ScrubBreadcrumb(Breadcrumb breadcrumb, ISecretRedactor redactor)
    {
        Dictionary<string, string>? scrubbedData = null;
        if (breadcrumb.Data is { Count: > 0 })
        {
            scrubbedData = new Dictionary<string, string>(breadcrumb.Data.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in breadcrumb.Data)
            {
                scrubbedData[pair.Key] = redactor.Redact(pair.Value) ?? string.Empty;
            }
        }

        return new Breadcrumb(
            message: redactor.Redact(breadcrumb.Message) ?? string.Empty,
            type: breadcrumb.Type ?? "default",
            data: scrubbedData,
            category: breadcrumb.Category,
            level: breadcrumb.Level);
    }

    /// <summary>
    /// Determines whether <paramref name="sentryEvent"/> should be dropped based on exception types and telemetry scope tags.
    /// </summary>
    /// <param name="sentryEvent">The Sentry event to inspect.</param>
    /// <param name="isUpstreamOnly">Whether third-party plugin events should be dropped.</param>
    /// <returns><see langword="true"/> if the event should be discarded; otherwise <see langword="false"/>.</returns>
    private static bool ShouldDropEvent(SentryEvent sentryEvent, bool isUpstreamOnly)
    {
        if ((sentryEvent.Exception is not null) && ShouldDropException(sentryEvent.Exception, isUpstreamOnly))
        {
            return true;
        }

        if (sentryEvent.SentryExceptions is not null)
        {
            foreach (SentryException sentryException in sentryEvent.SentryExceptions)
            {
                if (IsFilteredExceptionTypeName(sentryException.Type))
                {
                    return true;
                }
            }
        }

        return sentryEvent.Tags.ContainsKey("streamactions.streamer_space")
            || (isUpstreamOnly && sentryEvent.Tags.ContainsKey("plugin.third_party"));
    }

    /// <summary>
    /// Recursively determines whether <paramref name="exception"/> or any inner exception should be filtered from Sentry reporting.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <param name="isUpstreamOnly">Whether third-party plugin exceptions should be dropped.</param>
    /// <returns><see langword="true"/> if the exception should be discarded; otherwise <see langword="false"/>.</returns>
    private static bool ShouldDropException(Exception exception, bool isUpstreamOnly)
    {
        if (exception is OperationCanceledException)
        {
            return true;
        }

        if (exception.Data.Contains("streamactions.streamer_space"))
        {
            return true;
        }

        if (isUpstreamOnly && exception.Data.Contains("plugin.third_party"))
        {
            return true;
        }

        if (IsFilteredExceptionTypeName(exception.GetType().FullName))
        {
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                if (ShouldDropException(inner, isUpstreamOnly))
                {
                    return true;
                }
            }
        }

        return (exception.InnerException is not null) && ShouldDropException(exception.InnerException, isUpstreamOnly);
    }

    /// <summary>
    /// Determines whether an exception type name matches a known cancellation, script-engine, or external-HTTP noise type.
    /// </summary>
    /// <param name="typeName">The exception type name to check.</param>
    /// <returns><see langword="true"/> if the type name is filtered; otherwise <see langword="false"/>.</returns>
    private static bool IsFilteredExceptionTypeName(string? typeName) => !string.IsNullOrWhiteSpace(typeName) && (typeName.Contains("OperationCanceledException", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("TaskCanceledException", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("ScriptEngineException", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("ClearScript", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("StreamerExternalHttp", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Determines whether the process is running inside a .NET container environment.
    /// </summary>
    /// <returns><see langword="true"/> when <c>DOTNET_RUNNING_IN_CONTAINER</c> is <c>true</c>; otherwise <see langword="false"/>.</returns>
    private static bool IsRunningInContainer() =>
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
}
