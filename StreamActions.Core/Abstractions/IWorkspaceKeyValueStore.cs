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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides workspace-scoped Tier B key-value persistence for plugins, scripts, and custom commands.
/// </summary>
public interface IWorkspaceKeyValueStore
{
    /// <summary>
    /// Retrieves a string value from the key-value store.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The stored string value, or <paramref name="defaultValue"/> if not found.</returns>
    ValueTask<string?> GetStringAsync(Guid workspaceId, string table, string section, string variable, string? defaultValue = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a boolean value from the key-value store using invariant culture parsing.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist or cannot be parsed.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The parsed boolean value, or <paramref name="defaultValue"/>.</returns>
    ValueTask<bool> GetBoolAsync(Guid workspaceId, string table, string section, string variable, bool defaultValue = false, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a 32-bit integer value from the key-value store using invariant culture parsing.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist or cannot be parsed.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The parsed integer value, or <paramref name="defaultValue"/>.</returns>
    ValueTask<int> GetIntAsync(Guid workspaceId, string table, string section, string variable, int defaultValue = 0, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a 64-bit integer value from the key-value store using invariant culture parsing.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist or cannot be parsed.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The parsed 64-bit integer value, or <paramref name="defaultValue"/>.</returns>
    ValueTask<long> GetLongAsync(Guid workspaceId, string table, string section, string variable, long defaultValue = 0L, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single-precision floating-point value from the key-value store using invariant culture parsing.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist or cannot be parsed.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The parsed float value, or <paramref name="defaultValue"/>.</returns>
    ValueTask<float> GetFloatAsync(Guid workspaceId, string table, string section, string variable, float defaultValue = 0f, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a double-precision floating-point value from the key-value store using invariant culture parsing.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist or cannot be parsed.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The parsed double value, or <paramref name="defaultValue"/>.</returns>
    ValueTask<double> GetDoubleAsync(Guid workspaceId, string table, string section, string variable, double defaultValue = 0d, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates a value in the key-value store using invariant culture formatting.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous write operation.</returns>
    ValueTask SetAsync<T>(Guid workspaceId, string table, string section, string variable, T value, CancellationToken ct = default);

    /// <summary>
    /// Atomically increments a 64-bit integer value in the key-value store.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="delta">The amount to increment by.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The updated 64-bit integer value.</returns>
    ValueTask<long> IncrementLongAsync(Guid workspaceId, string table, string section, string variable, long delta = 1L, CancellationToken ct = default);

    /// <summary>
    /// Deletes a key-value entry if it exists.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="variable">The variable key name.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if an entry was deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(Guid workspaceId, string table, string section, string variable, CancellationToken ct = default);

    /// <summary>
    /// Lists all variable key names within the specified table and section.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="table">The logical table name.</param>
    /// <param name="section">The section name.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of variable key names.</returns>
    ValueTask<IReadOnlyList<string>> GetKeysAsync(Guid workspaceId, string table, string section, CancellationToken ct = default);
}
