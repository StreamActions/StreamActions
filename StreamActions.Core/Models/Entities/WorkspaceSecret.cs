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
/// Represents a named, encrypted custom secret scoped to a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspaceSecret
{
    /// <summary>
    /// Gets or sets the unique secret record identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the secret name used in <c>$secret.NAME</c> references.
    /// </summary>
    public string SecretName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Data Protection encrypted secret value.
    /// </summary>
    public string EncryptedValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized JSON array of allowed host patterns for outbound injection.
    /// </summary>
    public string AllowedHostPatternsJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifecycle status of the secret.
    /// </summary>
    public CredentialStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the secret was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
