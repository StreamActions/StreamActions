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
using StreamActions.Core.Models.Entities;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides persistence and atomic mutation operations for <see cref="WorkspaceCounter"/> entities.
/// </summary>
public interface ICounterStore
{
    /// <summary>
    /// Retrieves a counter by name and scope within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="scopeMode">The counter scope mode.</param>
    /// <param name="gameCategory">The game or category name for <see cref="CounterScopeMode.PerGame"/> counters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="WorkspaceCounter"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceCounter?> GetAsync(
        Guid workspaceId,
        string counterName,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all counters configured within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="WorkspaceCounter"/> entities.</returns>
    ValueTask<IReadOnlyList<WorkspaceCounter>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically adjusts a counter's value by <paramref name="delta"/>, creating the counter if it does not yet exist.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="delta">The signed value delta to apply.</param>
    /// <param name="scopeMode">The counter scope mode.</param>
    /// <param name="gameCategory">The game or category name for <see cref="CounterScopeMode.PerGame"/> counters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated <see cref="WorkspaceCounter"/>.</returns>
    ValueTask<WorkspaceCounter> AdjustAsync(
        Guid workspaceId,
        string counterName,
        long delta,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a counter to an exact value, creating the counter if it does not yet exist.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="value">The new counter value.</param>
    /// <param name="scopeMode">The counter scope mode.</param>
    /// <param name="gameCategory">The game or category name for <see cref="CounterScopeMode.PerGame"/> counters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated <see cref="WorkspaceCounter"/>.</returns>
    ValueTask<WorkspaceCounter> SetAsync(
        Guid workspaceId,
        string counterName,
        long value,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a counter from a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="counterName">The counter name.</param>
    /// <param name="scopeMode">The counter scope mode.</param>
    /// <param name="gameCategory">The game or category name for <see cref="CounterScopeMode.PerGame"/> counters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the counter existed and was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(
        Guid workspaceId,
        string counterName,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default);
}
