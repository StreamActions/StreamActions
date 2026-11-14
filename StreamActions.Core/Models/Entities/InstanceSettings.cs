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

namespace StreamActions.Core.Models.Entities;

/// <summary>
/// Represents global instance-wide settings persisted in the database.
/// </summary>
public sealed class InstanceSettings
{
    /// <summary>
    /// Gets or sets the unique settings row identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the anonymous installation identifier for opt-in telemetry.
    /// </summary>
    public Guid AnonymousInstallId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operator has opted into Sentry telemetry.
    /// </summary>
    public bool SentryOptIn { get; set; }

    /// <summary>
    /// Gets or sets an optional custom Sentry DSN override.
    /// </summary>
    public string? SentryCustomDsn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Sentry telemetry is currently enabled.
    /// </summary>
    public bool SentryEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Sentry performance traces sample rate.
    /// </summary>
    public double SentryTracesSampleRate { get; set; }

    /// <summary>
    /// Gets or sets the optional serialized JSON map of runtime log level overrides.
    /// </summary>
    public string? LogLevelsJson { get; set; }

    /// <summary>
    /// Gets or sets the interval in hours between automated database backups.
    /// </summary>
    public int BackupIntervalHours { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of rolling database backup files to retain.
    /// </summary>
    public int MaxBackupFiles { get; set; }

    /// <summary>
    /// Gets or sets the instance-wide OAuth application policy.
    /// </summary>
    public string InstanceOAuthAppPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the instance-wide bot account policy.
    /// </summary>
    public string InstanceBotAccountPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional encrypted JSON containing platform OAuth client configurations.
    /// </summary>
    public string? EncryptedPlatformOAuthConfigJson { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when settings were last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
