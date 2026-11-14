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

using Microsoft.Extensions.Configuration.Memory;
using StreamActions.Core.Configuration;
using StreamActions.Daemon.Telemetry;

namespace StreamActions.Daemon.Cli;

/// <summary>
/// Resolves Linux FHS filesystem defaults and pre-database <see cref="BootstrapOptions"/> before host DI initialization.
/// </summary>
internal static class DaemonBootstrapConfiguration
{
    /// <summary>
    /// Returns standard Linux Filesystem Hierarchy Standard (FHS) path defaults for privileged or container daemon runs.
    /// </summary>
    /// <returns>A <see cref="PathBootstrapOptions"/> populated with Linux FHS directories.</returns>
    public static PathBootstrapOptions GetLinuxFhsDefaults() => new()
    {
        ConfigDir = "/etc/streamactions",
        DataDir = "/var/lib/streamactions",
        LogDir = "/var/log/streamactions",
        PluginsDir = "/var/lib/streamactions/plugins",
        BackupDir = "/var/lib/streamactions/backups",
    };

    /// <summary>
    /// Determines whether Linux FHS directory defaults should be applied for the current process environment.
    /// </summary>
    /// <returns><see langword="true"/> when running on Linux as a privileged/container process or explicitly requested; otherwise <see langword="false"/>.</returns>
    public static bool ShouldUseLinuxFhsDefaults() => OperatingSystem.IsLinux() && (string.Equals(Environment.GetEnvironmentVariable("STREAMACTIONS_FHS_PATHS"), "true", StringComparison.OrdinalIgnoreCase) || Environment.IsPrivilegedProcess
            || string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns configuration key/value defaults for Linux FHS paths when active, or an empty dictionary otherwise.
    /// </summary>
    /// <param name="useLinuxFhs">Whether to emit Linux FHS path overrides.</param>
    /// <returns>A dictionary of <c>Bootstrap:Paths:*</c> configuration defaults.</returns>
    public static IReadOnlyDictionary<string, string?> GetDefaultPathConfiguration(bool useLinuxFhs)
    {
        if (!useLinuxFhs)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        PathBootstrapOptions fhs = GetLinuxFhsDefaults();
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bootstrap:Paths:ConfigDir"] = fhs.ConfigDir,
            ["Bootstrap:Paths:DataDir"] = fhs.DataDir,
            ["Bootstrap:Paths:LogDir"] = fhs.LogDir,
            ["Bootstrap:Paths:PluginsDir"] = fhs.PluginsDir,
            ["Bootstrap:Paths:BackupDir"] = fhs.BackupDir,
        };
    }

    /// <summary>
    /// Applies default path configuration and parsed CLI overrides to the host configuration builder.
    /// </summary>
    /// <param name="configurationBuilder">The configuration builder to update.</param>
    /// <param name="cliOptions">The parsed CLI options.</param>
    public static void ApplyToConfigurationBuilder(
        IConfigurationBuilder configurationBuilder,
        DaemonCliOptions cliOptions)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ArgumentNullException.ThrowIfNull(cliOptions);

        IReadOnlyDictionary<string, string?> fhsDefaults = GetDefaultPathConfiguration(ShouldUseLinuxFhsDefaults());
        if (fhsDefaults.Count > 0)
        {
            configurationBuilder.Sources.Insert(0, new MemoryConfigurationSource
            {
                InitialData = fhsDefaults,
            });
        }

        if (cliOptions.ConfigurationOverrides.Count > 0)
        {
            _ = configurationBuilder.AddInMemoryCollection(cliOptions.ConfigurationOverrides);
        }
    }

    /// <summary>
    /// Builds pre-database <see cref="BootstrapOptions"/> from FHS defaults, environment variables, CLI arguments, and optional <c>bootstrap.sentry.json</c>.
    /// </summary>
    /// <param name="cliOptions">The parsed CLI options.</param>
    /// <returns>The resolved <see cref="BootstrapOptions"/>.</returns>
    public static BootstrapOptions BuildPreDatabaseOptions(DaemonCliOptions cliOptions)
    {
        ArgumentNullException.ThrowIfNull(cliOptions);

        ConfigurationBuilder configBuilder = new();
        IReadOnlyDictionary<string, string?> fhsDefaults = GetDefaultPathConfiguration(ShouldUseLinuxFhsDefaults());
        if (fhsDefaults.Count > 0)
        {
            _ = configBuilder.AddInMemoryCollection(fhsDefaults);
        }

        _ = configBuilder.AddEnvironmentVariables();
        _ = configBuilder.AddEnvironmentVariables("STREAMACTIONS_");

        if (cliOptions.ConfigurationOverrides.Count > 0)
        {
            _ = configBuilder.AddInMemoryCollection(cliOptions.ConfigurationOverrides);
        }

        IConfigurationRoot config = configBuilder.Build();
        BootstrapOptions bootstrap = new();
        config.GetSection(BootstrapOptions.SectionName).Bind(bootstrap);

        SentryBootstrapInitializer.ApplyBootstrapFileToOptions(bootstrap, cliOptions.SentryOptIn);
        return bootstrap;
    }
}
