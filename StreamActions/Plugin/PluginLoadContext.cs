/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
