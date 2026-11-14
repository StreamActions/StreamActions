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

using StreamActions.Core.Models.Metadata;

namespace StreamActions.Core.Models.Entities;

/// <summary>
/// Represents a chat or streaming platform attached to a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspacePlatform
{
    /// <summary>
    /// Gets or sets the unique workspace platform attachment identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the platform identifier (for example, <c>twitch</c> or <c>discord</c>).
    /// </summary>
    public string PlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role of this platform within the workspace.
    /// </summary>
    public PlatformRole Role { get; set; }

    /// <summary>
    /// Gets or sets the platform-specific channel or guild identifier.
    /// </summary>
    public string PlatformChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional per-workspace custom OAuth client identifier.
    /// </summary>
    public string? CustomClientId { get; set; }

    /// <summary>
    /// Gets or sets an optional Data Protection encrypted custom OAuth client secret.
    /// </summary>
    public string? EncryptedCustomClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Tier A JSON metadata for platform-specific settings.
    /// </summary>
    public PlatformMetadata Metadata { get; set; } = new();
}
