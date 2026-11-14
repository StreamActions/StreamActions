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
/// Represents a global or workspace-scoped permission group.
/// </summary>
public sealed class PermissionGroup
{
    /// <summary>
    /// Gets or sets the unique permission group identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the optional <see cref="Workspace"/> identifier; <see langword="null"/> indicates a global group.
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the group display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional platform designation mapped to this group (for example, moderator or VIP).
    /// </summary>
    public string? MappedPlatformDesignation { get; set; }

    /// <summary>
    /// Gets or sets the optional OIDC group claim value mapped to this group.
    /// </summary>
    public string? MappedOidcGroupClaim { get; set; }
}
