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
/// Represents a viewer's identity and loyalty profile within a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspaceViewerProfile
{
    /// <summary>
    /// Gets or sets the unique viewer profile identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the platform identifier.
    /// </summary>
    public string PlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the immutable platform-specific user identifier.
    /// </summary>
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the viewer's last known login handle.
    /// </summary>
    public string LastKnownLogin { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the viewer's last known display name.
    /// </summary>
    public string LastKnownDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the viewer's loyalty points balance.
    /// </summary>
    public long Points { get; set; }

    /// <summary>
    /// Gets or sets the viewer's accumulated watch time in seconds.
    /// </summary>
    public long WatchTimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the viewer's accumulated chat XP.
    /// </summary>
    public long ChatXp { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
