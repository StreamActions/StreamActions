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

using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Daemon.Telemetry;

/// <summary>
/// Represents the persisted <c>bootstrap.sentry.json</c> file read before database initialization.
/// </summary>
internal sealed record SentryBootstrapConfig
{
    /// <summary>
    /// Gets a value indicating whether Sentry telemetry is enabled at startup.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the Sentry DSN endpoint, or <see langword="null"/> when using default or disabled telemetry.
    /// </summary>
    public string? Dsn { get; init; }

    /// <summary>
    /// Gets the Sentry environment name, or <see langword="null"/>.
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>
    /// Gets the performance traces sample rate between 0.0 and 1.0.
    /// </summary>
    public double TracesSampleRate { get; init; }

    /// <summary>
    /// Gets a value indicating whether a custom operator DSN is configured rather than upstream-only telemetry.
    /// </summary>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Sentry DSN is represented as a configuration string.")]
    public bool IsCustomDsn { get; init; }
}
