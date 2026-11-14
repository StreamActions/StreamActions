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
/// Represents a hashed verifier token for OBS overlays and external integrations within a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspaceVerifierToken
{
    /// <summary>
    /// Gets or sets the unique verifier token record identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable token name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hex-encoded SHA-256 hash of the raw verifier token.
    /// </summary>
    public string TokenHashSha256 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized JSON array of granted scopes.
    /// </summary>
    public string ScopesJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional UTC expiration timestamp.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the verifier token was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}
