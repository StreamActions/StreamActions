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
/// Represents a global or per-game counter scoped to a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspaceCounter
{
    /// <summary>
    /// Gets or sets the unique counter identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the counter name.
    /// </summary>
    public string CounterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the counter scope mode.
    /// </summary>
    public CounterScopeMode ScopeMode { get; set; }

    /// <summary>
    /// Gets or sets the game or category name for per-game counters.
    /// </summary>
    public string GameCategory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current counter value.
    /// </summary>
    public long CurrentValue { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the counter was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
