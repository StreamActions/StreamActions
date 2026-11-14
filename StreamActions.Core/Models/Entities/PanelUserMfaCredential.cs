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
/// Represents a second-factor authentication credential registered for a <see cref="PanelUser"/>.
/// </summary>
public sealed class PanelUserMfaCredential
{
    /// <summary>
    /// Gets or sets the unique MFA credential identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the associated <see cref="PanelUser"/> identifier.
    /// </summary>
    public Guid PanelUserId { get; set; }

    /// <summary>
    /// Gets or sets the MFA method type.
    /// </summary>
    public MfaMethod Method { get; set; }

    /// <summary>
    /// Gets or sets the encrypted TOTP seed or serialized WebAuthn public key credential data.
    /// </summary>
    public string CredentialDataEncrypted { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional serialized JSON array of SHA-256 hashed one-time recovery codes.
    /// </summary>
    public string? RecoveryCodesHashesJson { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the MFA credential was registered.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}
