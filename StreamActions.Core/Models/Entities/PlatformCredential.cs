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
/// Represents an encrypted OAuth or API token stored for a platform.
/// </summary>
public sealed class PlatformCredential
{
    /// <summary>
    /// Gets or sets the unique credential identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the optional <see cref="Workspace"/> identifier; <see langword="null"/> for instance-level default bot tokens.
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the platform identifier.
    /// </summary>
    public string PlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token role.
    /// </summary>
    public TokenRole TokenRole { get; set; }

    /// <summary>
    /// Gets or sets the Data Protection encrypted token payload JSON.
    /// </summary>
    public string EncryptedTokenJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current lifecycle status of the credential.
    /// </summary>
    public CredentialStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC expiration timestamp of the token.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the credential was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
