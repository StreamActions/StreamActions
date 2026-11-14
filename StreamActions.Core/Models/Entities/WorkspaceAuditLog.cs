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
/// Represents an immutable audit log entry for a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspaceAuditLog
{
    /// <summary>
    /// Gets or sets the unique audit log entry identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the optional <see cref="PanelUser"/> identifier of the actor.
    /// </summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the actor who performed the action.
    /// </summary>
    public string ActorDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the origin source of the audited action.
    /// </summary>
    public AuditSource Source { get; set; }

    /// <summary>
    /// Gets or sets the action type identifier.
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional identifier of the affected entity.
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the optional redacted JSON snapshot prior to the change.
    /// </summary>
    public string? BeforeJsonRedacted { get; set; }

    /// <summary>
    /// Gets or sets the optional redacted JSON snapshot after the change.
    /// </summary>
    public string? AfterJsonRedacted { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the action occurred.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; }
}
