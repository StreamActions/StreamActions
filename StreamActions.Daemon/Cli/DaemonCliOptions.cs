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

namespace StreamActions.Daemon.Cli;

/// <summary>
/// Represents parsed command-line options for the StreamActions daemon host.
/// </summary>
internal sealed class DaemonCliOptions
{
    /// <summary>
    /// Gets a value indicating whether the Blazor UI and SignalR hubs should be disabled while keeping health endpoints active.
    /// </summary>
    public bool NoWeb { get; init; }

    /// <summary>
    /// Gets a value indicating whether upstream Sentry telemetry opt-in was requested via CLI flag.
    /// </summary>
    public bool SentryOptIn { get; init; }

    /// <summary>
    /// Gets a value indicating whether the process was invoked in local IPC command client mode (<c>exec</c>).
    /// </summary>
    public bool IsExecMode { get; init; }

    /// <summary>
    /// Gets the command string passed after <c>exec</c>, if <see cref="IsExecMode"/> is <see langword="true"/>.
    /// </summary>
    public string? ExecCommand { get; init; }

    /// <summary>
    /// Gets configuration key/value overrides derived from recognized CLI flags.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ConfigurationOverrides { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets unrecognized arguments preserved for host/test framework processing.
    /// </summary>
    public IReadOnlyList<string> RemainingArgs { get; init; } = [];
}
