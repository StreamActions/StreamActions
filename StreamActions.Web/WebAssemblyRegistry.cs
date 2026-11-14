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

using System.Reflection;

namespace StreamActions.Web;

/// <summary>
/// Stores the plugin assemblies registered for Blazor component discovery.
/// </summary>
/// <param name="pluginAssemblies">The plugin assemblies containing additional Razor components.</param>
internal sealed class WebAssemblyRegistry(IReadOnlyCollection<Assembly> pluginAssemblies)
{
    /// <summary>
    /// Gets the registered plugin assemblies containing additional Razor components.
    /// </summary>
    public IReadOnlyCollection<Assembly> PluginAssemblies { get; } = pluginAssemblies;
}
