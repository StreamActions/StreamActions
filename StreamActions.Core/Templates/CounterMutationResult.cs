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

namespace StreamActions.Core.Templates;

/// <summary>
/// Categorizes the counter operation recorded by a counter tag evaluation.
/// </summary>
public enum CounterMutationKind
{
    /// <summary>
    /// The counter value was read without modifying storage.
    /// </summary>
    Read = 0,

    /// <summary>
    /// The counter value was incremented.
    /// </summary>
    Incremented = 1,

    /// <summary>
    /// The counter value was decremented.
    /// </summary>
    Decremented = 2,

    /// <summary>
    /// The counter value was explicitly set to a target integer.
    /// </summary>
    Set = 3,

    /// <summary>
    /// The counter value was reset to zero.
    /// </summary>
    Reset = 4,
}

/// <summary>
/// Records the outcome of a counter read or mutation performed during template evaluation.
/// </summary>
/// <param name="CounterName">The counter name.</param>
/// <param name="ScopeMode">The resolved counter scope mode.</param>
/// <param name="GameCategory">The game/category key for <see cref="CounterScopeMode.PerGame"/> counters, or empty for global counters.</param>
/// <param name="PreviousValue">The counter value prior to mutation when known.</param>
/// <param name="NewValue">The counter value after the operation.</param>
/// <param name="Kind">The kind of operation performed.</param>
public sealed record CounterMutationResult(
    string CounterName,
    CounterScopeMode ScopeMode,
    string GameCategory,
    long? PreviousValue,
    long NewValue,
    CounterMutationKind Kind)
{
    /// <summary>
    /// Gets a value indicating whether the counter was mutated in storage.
    /// </summary>
    public bool Mutated => this.Kind is not CounterMutationKind.Read;
}
