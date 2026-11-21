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
/// Represents the outcome of resolving a workspace custom secret for an outbound host URI.
/// </summary>
public enum SecretResolutionStatus
{
    /// <summary>
    /// The secret was decrypted and authorized for the target host URI.
    /// </summary>
    Resolved = 0,

    /// <summary>
    /// No secret with the requested name exists in the workspace.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The target host URI does not match any allowed host pattern configured for the secret.
    /// </summary>
    HostNotAllowed = 2,

    /// <summary>
    /// The secret has passed its configured expiration timestamp.
    /// </summary>
    Expired = 3,

    /// <summary>
    /// The stored secret could not be decrypted with the active Data Protection key ring.
    /// </summary>
    KeyRingMismatch = 4,

    /// <summary>
    /// The secret has been revoked and cannot be used for outbound requests.
    /// </summary>
    Revoked = 5,
}

/// <summary>
/// Represents non-sensitive metadata for a workspace-scoped custom secret.
/// </summary>
/// <param name="Id">The unique secret record identifier.</param>
/// <param name="WorkspaceId">The owning workspace identifier.</param>
/// <param name="SecretName">The normalized secret name.</param>
/// <param name="AllowedHostPatterns">The allowed host patterns for outbound injection.</param>
/// <param name="Status">The current credential status.</param>
/// <param name="ExpiresAtUtc">The optional UTC expiration timestamp.</param>
/// <param name="IsExpired">A value indicating whether the secret has passed <paramref name="ExpiresAtUtc"/>.</param>
/// <param name="UpdatedAtUtc">The UTC timestamp when the secret record was last updated.</param>
/// <param name="HasSecretConfigured">A value indicating whether an encrypted secret payload is currently stored.</param>
public sealed record WorkspaceSecretMetadata(
    Guid Id,
    Guid WorkspaceId,
    string SecretName,
    IReadOnlyList<string> AllowedHostPatterns,
    CredentialStatus Status,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    DateTimeOffset UpdatedAtUtc,
    bool HasSecretConfigured);

/// <summary>
/// Represents the result of resolving a workspace custom secret for an outbound host URI.
/// </summary>
/// <param name="Status">The resolution status.</param>
/// <param name="PlaintextValue">The decrypted plaintext value when <paramref name="Status"/> is <see cref="SecretResolutionStatus.Resolved"/>.</param>
/// <param name="AllowedHostPatterns">The configured allowed host patterns when available.</param>
public sealed record SecretResolutionResult(
    SecretResolutionStatus Status,
    string? PlaintextValue = null,
    IReadOnlyList<string>? AllowedHostPatterns = null)
{
    /// <summary>
    /// Gets a value indicating whether the secret was resolved and a plaintext value is available.
    /// </summary>
    public bool IsSuccess => this.Status == SecretResolutionStatus.Resolved && this.PlaintextValue is not null;

    /// <summary>
    /// Formats record members while redacting the resolved plaintext secret value.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.Status)).Append(" = ").Append(this.Status);
        _ = builder.Append(", ").Append(nameof(this.PlaintextValue)).Append(" = ").Append(string.IsNullOrEmpty(this.PlaintextValue) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.AllowedHostPatterns)).Append(" = ").Append(this.AllowedHostPatterns);
        _ = builder.Append(", ").Append(nameof(this.IsSuccess)).Append(" = ").Append(this.IsSuccess);
        return true;
    }
}
