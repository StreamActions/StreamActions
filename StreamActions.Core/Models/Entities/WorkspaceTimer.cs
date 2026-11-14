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
/// Represents a smart scheduled timer belonging to a timer pool within a <see cref="Workspace"/>.
/// </summary>
public sealed class WorkspaceTimer
{
    /// <summary>
    /// Gets or sets the unique timer identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent <see cref="Workspace"/> identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the timer pool name.
    /// </summary>
    public string PoolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selection strategy for the timer pool.
    /// </summary>
    public TimerSelectionMode SelectionMode { get; set; }

    /// <summary>
    /// Gets or sets the timer interval in seconds.
    /// </summary>
    public int IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the minimum chat line count required before the timer fires.
    /// </summary>
    public int MinChatLinesThreshold { get; set; }

    /// <summary>
    /// Gets or sets the stream status requirement for timer execution.
    /// </summary>
    public StreamStatusRequirement StreamStatusRequirement { get; set; }

    /// <summary>
    /// Gets or sets the optional game or category restriction.
    /// </summary>
    public string? GameRestriction { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON action routine executed when the timer fires.
    /// </summary>
    public string RoutineJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the timer is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}
