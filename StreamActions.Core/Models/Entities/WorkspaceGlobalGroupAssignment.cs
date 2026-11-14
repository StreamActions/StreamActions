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
/// Associates a <see cref="Workspace"/> with a global <see cref="PermissionGroup"/>.
/// </summary>
public sealed class WorkspaceGlobalGroupAssignment
{
    /// <summary>
    /// Gets or sets the unique assignment identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the assigned <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the assigned global <see cref="PermissionGroup"/> identifier.
    /// </summary>
    public Guid GlobalGroupId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the global group was assigned.
    /// </summary>
    public DateTimeOffset AssignedAtUtc { get; set; }
}
