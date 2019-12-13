/*
 * Copyright © 2019-2020 StreamActions Team
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

namespace StreamActions.Plugin
{
    /// <summary>
    /// An interface for a plugin that processes messages, commands, and other input.
    /// </summary>
    public interface IPlugin
    {
        #region Public Methods

        /// <summary>
        /// Called when the plugin is disabled. Event delegates should be unregistered here.
        /// </summary>
        public void Disabled();

        /// <summary>
        /// Called when the plugin is enabled. Event delegates should be registered here.
        /// </summary>
        public void Enabled();

        /// <summary>
        /// Returns a basic, friendly description of the plugin.
        /// </summary>
        /// <returns>A description of the plugin..</returns>
        public string GetPluginDescription();

        /// <summary>
        /// Returns a friendly name of the plugin.
        /// </summary>
        /// <returns>The name of the plugin.</returns>
        public string GetPluginName();

        /// <summary>
        /// Returns the plugin version.
        /// </summary>
        /// <returns>The plugin version.</returns>
        public string GetPluginVersion();

        #endregion Public Methods
    }
}