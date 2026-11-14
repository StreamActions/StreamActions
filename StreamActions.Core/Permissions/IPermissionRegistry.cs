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

namespace StreamActions.Core.Permissions;

/// <summary>
/// Provides a thread-safe registry of built-in and dynamically registered permission descriptors.
/// </summary>
public interface IPermissionRegistry
{
    /// <summary>
    /// Registers or updates a permission descriptor in the registry.
    /// </summary>
    /// <param name="descriptor">The permission descriptor to register.</param>
    void Register(PermissionDescriptor descriptor);

    /// <summary>
    /// Unregisters a permission descriptor by key.
    /// </summary>
    /// <param name="key">The permission key to remove.</param>
    /// <returns><see langword="true"/> if the descriptor was removed; otherwise, <see langword="false"/>.</returns>
    bool Unregister(string key);

    /// <summary>
    /// Retrieves a registered permission descriptor by its key.
    /// </summary>
    /// <param name="key">The permission key to look up.</param>
    /// <returns>The matching <see cref="PermissionDescriptor"/>, or <see langword="null"/> if not found.</returns>
    PermissionDescriptor? GetByKey(string key);

    /// <summary>
    /// Retrieves all registered permission descriptors belonging to the specified category.
    /// </summary>
    /// <param name="category">The category name to filter by.</param>
    /// <returns>A read-only list of matching <see cref="PermissionDescriptor"/> records.</returns>
    IReadOnlyList<PermissionDescriptor> GetByCategory(string category);

    /// <summary>
    /// Retrieves all registered permission descriptors.
    /// </summary>
    /// <returns>A read-only list of all <see cref="PermissionDescriptor"/> records.</returns>
    IReadOnlyList<PermissionDescriptor> GetAll();

    /// <summary>
    /// Retrieves all distinct permission categories currently in the registry.
    /// </summary>
    /// <returns>A read-only list of category names.</returns>
    IReadOnlyList<string> GetCategories();
}
