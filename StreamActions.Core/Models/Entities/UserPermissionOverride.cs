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
/// Represents a per-user tri-state permission override within a <see cref="Workspace"/>.
/// </summary>
public sealed class UserPermissionOverride
{
    /// <summary>
    /// Gets or sets the unique override identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the platform identifier (for example, <c>twitch</c>).
    /// </summary>
    public string PlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the platform-specific user identifier.
    /// </summary>
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the permission key being overridden.
    /// </summary>
    public string PermissionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tri-state override rule.
    /// </summary>
    public PermissionState State { get; set; }
}
