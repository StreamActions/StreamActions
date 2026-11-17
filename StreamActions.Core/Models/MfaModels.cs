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
using System.Text;

namespace StreamActions.Core.Models;

/// <summary>
/// Represents the resolved effective multi-factor authentication policy combining instance-wide and optional workspace-level settings.
/// </summary>
/// <param name="PasskeyAvailability">The effective WebAuthn passkey availability policy.</param>
/// <param name="TotpAvailability">The effective TOTP authenticator availability policy.</param>
/// <param name="LoginMfaRequirement">The effective panel login MFA requirement.</param>
/// <param name="StepUpSudoPolicy">The effective step-up ("Sudo Mode") MFA requirement.</param>
public sealed record EffectiveMfaPolicy(
    MfaMethodAvailability PasskeyAvailability,
    MfaMethodAvailability TotpAvailability,
    PanelLoginMfaRequirement LoginMfaRequirement,
    StepUpSudoPolicy StepUpSudoPolicy)
{
    /// <summary>
    /// Gets a value indicating whether WebAuthn passkey enrollment and verification are permitted.
    /// </summary>
    public bool IsPasskeyAllowed => this.PasskeyAvailability != MfaMethodAvailability.Disabled;

    /// <summary>
    /// Gets a value indicating whether TOTP authenticator enrollment and verification are permitted.
    /// </summary>
    public bool IsTotpAllowed => this.TotpAvailability != MfaMethodAvailability.Disabled;

    /// <summary>
    /// Gets a value indicating whether passkeys are the only permitted MFA method because TOTP is disabled.
    /// </summary>
    public bool IsPasskeysOnly =>
        this.TotpAvailability == MfaMethodAvailability.Disabled
        && this.PasskeyAvailability != MfaMethodAvailability.Disabled;

    /// <summary>
    /// Gets a value indicating whether all MFA methods are disabled by policy.
    /// </summary>
    public bool IsMfaDisabled =>
        this.PasskeyAvailability == MfaMethodAvailability.Disabled
        && this.TotpAvailability == MfaMethodAvailability.Disabled;

    /// <summary>
    /// Gets a value indicating whether sensitive actions require step-up ("Sudo Mode") MFA elevation.
    /// </summary>
    public bool IsStepUpRequired => this.StepUpSudoPolicy == StepUpSudoPolicy.Required;
}

/// <summary>
/// Represents the decrypted TOTP authenticator secret payload stored for a panel user.
/// </summary>
/// <param name="SecretBase32">The RFC 4648 Base32-encoded HMAC-SHA1 shared secret.</param>
/// <param name="Label">The optional account label associated with the authenticator entry.</param>
/// <param name="EnrolledAtUtc">The UTC timestamp when TOTP was enrolled.</param>
public sealed record TotpSecretPayload(
    string SecretBase32,
    string? Label = null,
    DateTimeOffset EnrolledAtUtc = default)
{
    /// <summary>
    /// Formats record members while redacting the sensitive TOTP secret.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.SecretBase32)).Append(" = ").Append(string.IsNullOrEmpty(this.SecretBase32) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.Label)).Append(" = ").Append(this.Label);
        _ = builder.Append(", ").Append(nameof(this.EnrolledAtUtc)).Append(" = ").Append(this.EnrolledAtUtc);
        return true;
    }
}

/// <summary>
/// Represents the decrypted WebAuthn passkey credential payload persisted for a panel user.
/// </summary>
/// <param name="CredentialId">The WebAuthn credential identifier bytes.</param>
/// <param name="PublicKey">The COSE-encoded credential public key bytes.</param>
/// <param name="CreatedAt">The UTC timestamp when the passkey was created.</param>
/// <param name="SignCount">The authenticator signature counter.</param>
/// <param name="Transports">The optional supported authenticator transports.</param>
/// <param name="IsUserVerified">A value indicating whether user verification was performed.</param>
/// <param name="IsBackupEligible">A value indicating whether the credential is eligible for backup.</param>
/// <param name="IsBackedUp">A value indicating whether the credential is currently backed up.</param>
/// <param name="AttestationObject">The WebAuthn attestation object bytes.</param>
/// <param name="ClientDataJson">The collected client data JSON bytes.</param>
/// <param name="Name">The optional user-friendly display name for the passkey.</param>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Matches ASP.NET Core Identity UserPasskeyInfo byte[] and string[] properties for JSON serialization.")]
public sealed record PasskeyCredentialPayload(
    byte[] CredentialId,
    byte[] PublicKey,
    DateTimeOffset CreatedAt,
    uint SignCount,
    string[]? Transports,
    bool IsUserVerified,
    bool IsBackupEligible,
    bool IsBackedUp,
    byte[] AttestationObject,
    byte[] ClientDataJson,
    string? Name = null)
{
    /// <summary>
    /// Formats record members while redacting cryptographic credential blobs.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.Name)).Append(" = ").Append(this.Name);
        _ = builder.Append(", ").Append(nameof(this.CredentialId)).Append(" = [").Append(this.CredentialId?.Length ?? 0).Append(" bytes]");
        _ = builder.Append(", ").Append(nameof(this.PublicKey)).Append(" = [REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.SignCount)).Append(" = ").Append(this.SignCount);
        _ = builder.Append(", ").Append(nameof(this.IsUserVerified)).Append(" = ").Append(this.IsUserVerified);
        _ = builder.Append(", ").Append(nameof(this.IsBackupEligible)).Append(" = ").Append(this.IsBackupEligible);
        _ = builder.Append(", ").Append(nameof(this.IsBackedUp)).Append(" = ").Append(this.IsBackedUp);
        _ = builder.Append(", ").Append(nameof(this.CreatedAt)).Append(" = ").Append(this.CreatedAt);
        return true;
    }
}

/// <summary>
/// Represents safe metadata summarizing an enrolled MFA credential without exposing secrets or hashes.
/// </summary>
/// <param name="Id">The credential record identifier.</param>
/// <param name="Method">The MFA method type.</param>
/// <param name="DisplayName">The optional friendly label for the credential.</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the credential was registered.</param>
/// <param name="RemainingRecoveryCodesCount">The count of unused one-time recovery codes associated with this credential.</param>
public sealed record MfaCredentialSummary(
    Guid Id,
    MfaMethod Method,
    string? DisplayName,
    DateTimeOffset CreatedAtUtc,
    int RemainingRecoveryCodesCount = 0);

/// <summary>
/// Represents the initial TOTP enrollment payload returned to the user before verification confirmation.
/// </summary>
/// <param name="SecretBase32">The generated Base32 shared secret for manual authenticator entry.</param>
/// <param name="OtpAuthUri">The <c>otpauth://totp/...</c> provisioning URI.</param>
/// <param name="RecoveryCodes">The plaintext one-time recovery codes generated alongside TOTP enrollment.</param>
public sealed record TotpEnrollmentResult(
    string SecretBase32,
    Uri OtpAuthUri,
    IReadOnlyList<string> RecoveryCodes)
{
    /// <summary>
    /// Formats record members while redacting the secret, provisioning URI, and recovery codes.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.SecretBase32)).Append(" = [REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.OtpAuthUri)).Append(" = [REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.RecoveryCodes)).Append(" = [").Append(this.RecoveryCodes?.Count ?? 0).Append(" codes]");
        return true;
    }
}
