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
    /// Gets or sets the operator-configured display name for this instance.
    /// </summary>
    public string InstanceName { get; set; } = "StreamActions";

    /// <summary>
    /// Gets or sets the issuer label displayed in generated <c>otpauth://</c> TOTP provisioning URIs.
    /// </summary>
    public string TotpIssuerName { get; set; } = "StreamActions";

    /// <summary>
    /// Gets or sets the instance-wide OAuth application policy.
    /// </summary>
    public string InstanceOAuthAppPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional serialized JSON map of per-platform OAuth application policy overrides (<c>platformId -&gt; policy</c>).
    /// </summary>
    public string? PlatformOAuthAppPoliciesJson { get; set; }

    /// <summary>
    /// Gets or sets the instance-wide bot account policy.
    /// </summary>
    public string InstanceBotAccountPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional encrypted JSON containing platform OAuth client configurations.
    /// </summary>
    public string? EncryptedPlatformOAuthConfigJson { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether open self-registration via external OIDC is permitted.
    /// </summary>
    public bool AllowOpenRegistration { get; set; }

    /// <summary>
    /// Gets or sets the external OpenID Connect login mode for panel authentication.
    /// </summary>
    public ExternalOidcLoginMode ExternalOidcLoginMode { get; set; } = ExternalOidcLoginMode.Allowed;

    /// <summary>
    /// Gets or sets the instance-wide availability policy for WebAuthn passkeys.
    /// </summary>
    public MfaMethodAvailability MfaPasskeyAvailability { get; set; } = MfaMethodAvailability.Allowed;

    /// <summary>
    /// Gets or sets the instance-wide availability policy for TOTP authenticator apps.
    /// </summary>
    public MfaMethodAvailability MfaTotpAvailability { get; set; } = MfaMethodAvailability.Allowed;

    /// <summary>
    /// Gets or sets the instance-wide panel login MFA requirement.
    /// </summary>
    public PanelLoginMfaRequirement LoginMfaRequirement { get; set; } = PanelLoginMfaRequirement.Optional;

    /// <summary>
    /// Gets or sets the instance-wide step-up ("Sudo Mode") MFA policy for sensitive actions.
    /// </summary>
    public StepUpSudoPolicy StepUpSudoPolicy { get; set; } = StepUpSudoPolicy.Required;

    /// <summary>
    /// Gets or sets the UTC timestamp when settings were last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
