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

using StreamActions.Core.Models;

namespace StreamActions.Core.Counters;

/// <summary>
/// Event published on <see cref="Abstractions.IEventBus"/> when a workspace counter is created, updated, or deleted.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier.</param>
/// <param name="CounterName">The counter name.</param>
/// <param name="ScopeMode">The counter scope mode.</param>
/// <param name="GameCategory">The game or category name for per-game counters, or empty for global counters.</param>
/// <param name="PreviousValue">The previous counter value, or <see langword="null"/> if the counter was newly created.</param>
/// <param name="CurrentValue">The updated counter value, or <see langword="null"/> if the counter was deleted.</param>
/// <param name="UpdatedAtUtc">The UTC timestamp when the change occurred.</param>
public sealed record CounterUpdatedEvent(
    Guid WorkspaceId,
    string CounterName,
    CounterScopeMode ScopeMode,
    string GameCategory,
    long? PreviousValue,
    long? CurrentValue,
    DateTimeOffset UpdatedAtUtc);
