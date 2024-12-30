/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using System.Runtime.Loader;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Handles loading of plugin assemblies.
    /// </summary>
    internal class PluginLoadContext : AssemblyLoadContext
    {
        #region Public Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="pluginPath">The path to search for plugins.</param>
        public PluginLoadContext(string pluginPath) => this._resolver = new AssemblyDependencyResolver(pluginPath);

        #endregion Public Constructors

        #region Protected Methods

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = this._resolver.ResolveAssemblyToPath(assemblyName);

            return !(assemblyPath is null) ? this.LoadFromAssemblyPath(assemblyPath) : null;
        }

        #endregion Protected Methods

        #region Private Fields

        /// <summary>
        /// Holds an AssemblyDependencyResolver.
        /// </summary>
        private readonly AssemblyDependencyResolver _resolver;

        #endregion Private Fields
    }
}
