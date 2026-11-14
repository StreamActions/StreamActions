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

using StreamActions.Core.Models;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Configuration;

/// <summary>
/// Host-level bootstrap configuration strictly limited to pre-database startup categories.
/// </summary>
public sealed class BootstrapOptions
{
    /// <summary>
    /// The configuration section name for bootstrap options.
    /// </summary>
    public const string SectionName = "Bootstrap";

    /// <summary>
    /// Gets or sets the database connectivity bootstrap options.
    /// </summary>
    public DatabaseBootstrapOptions Database { get; set; } = new();

    /// <summary>
    /// Gets or sets the pre-database Sentry telemetry bootstrap options.
    /// </summary>
    public SentryBootstrapOptions Sentry { get; set; } = new();

    /// <summary>
    /// Gets or sets the Data Protection key-ring wrapping bootstrap options.
    /// </summary>
    public DataProtectionBootstrapOptions DataProtection { get; set; } = new();

    /// <summary>
    /// Gets or sets the per-node cluster identity bootstrap options.
    /// </summary>
    public NodeBootstrapOptions Node { get; set; } = new();

    /// <summary>
    /// Gets or sets the immutable host filesystem path bootstrap options.
    /// </summary>
    public PathBootstrapOptions Paths { get; set; } = new();

    /// <summary>
    /// Gets or sets the immutable host security kill-switch bootstrap options.
    /// </summary>
    public SecurityBootstrapOptions Security { get; set; } = new();

    /// <summary>
    /// Gets or sets the bootstrap log level used before runtime log levels load from the database.
    /// </summary>
    public string BootstrapLogLevel { get; set; } = "Information";
}

/// <summary>
/// Pre-database connectivity options.
/// </summary>
public sealed class DatabaseBootstrapOptions
{
    /// <summary>
    /// Gets or sets the database provider.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>
    /// Gets or sets the optional database connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
}

/// <summary>
/// Early pre-database Sentry initialization options.
/// </summary>
public sealed class SentryBootstrapOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Sentry is enabled at startup.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the Sentry DSN.
    /// </summary>
    public string? Dsn { get; set; }

    /// <summary>
    /// Gets or sets the Sentry environment name.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the Sentry traces sample rate.
    /// </summary>
    public double TracesSampleRate { get; set; }
}

/// <summary>
/// Cryptographic key-ring separation options for ASP.NET Core Data Protection.
/// </summary>
public sealed class DataProtectionBootstrapOptions
{
    /// <summary>
    /// Gets or sets the directory where Data Protection keys are persisted.
    /// </summary>
    public string? KeyRingDir { get; set; }

    /// <summary>
    /// Gets or sets the optional X.509 certificate path used to protect the Data Protection key ring.
    /// </summary>
    public string? CertificatePath { get; set; }
}

/// <summary>
/// Per-node identity and clustering options for shared-database deployments.
/// </summary>
public sealed class NodeBootstrapOptions
{
    /// <summary>
    /// Gets or sets the unique identifier for this node instance.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operational mode of this node.
    /// </summary>
    public NodeMode Mode { get; set; } = NodeMode.AllInOne;

    /// <summary>
    /// Gets or sets the zero-based shard index for this node.
    /// </summary>
    public int ShardId { get; set; }

    /// <summary>
    /// Gets or sets the total number of shards in the cluster.
    /// </summary>
    public int TotalShards { get; set; } = 1;

    /// <summary>
    /// Gets or sets the optional SignalR backplane URL for multi-node synchronization.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Bound from host configuration/environment variables.")]
    public string? SignalRBackplaneUrl { get; set; }

    /// <summary>
    /// Gets or sets the shared secret authenticating sink nodes to source nodes.
    /// </summary>
    public string? ClusterSharedSecret { get; set; }
}

/// <summary>
/// Host-level filesystem paths that cannot be overridden from the database or web UI.
/// </summary>
public sealed class PathBootstrapOptions
{
    /// <summary>
    /// Gets or sets the configuration directory path.
    /// </summary>
    public string ConfigDir { get; set; } = "config";

    /// <summary>
    /// Gets or sets the data directory path.
    /// </summary>
    public string DataDir { get; set; } = "data";

    /// <summary>
    /// Gets or sets the log directory path.
    /// </summary>
    public string LogDir { get; set; } = "logs";

    /// <summary>
    /// Gets or sets the external plugins directory path.
    /// </summary>
    public string PluginsDir { get; set; } = "plugins";

    /// <summary>
    /// Gets or sets the database backup directory path.
    /// </summary>
    public string BackupDir { get; set; } = "backups";
}

/// <summary>
/// Immutable host-level security ceiling flags.
/// </summary>
public sealed class SecurityBootstrapOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether loading external plugins is disabled at the host level.
    /// </summary>
    public bool DisableExternalPlugins { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether ClearScript JavaScript execution is disabled at the host level.
    /// </summary>
    public bool DisableClearScript { get; set; }
}
