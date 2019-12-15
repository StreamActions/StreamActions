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
        #region Public Properties

        /// <summary>
        /// Whether the plugin should always be enabled. This should only be true in core plugins and special circumstances.
        /// </summary>
        public bool AlwaysEnabled { get; }

        /// <summary>
        /// A description of the plugin.
        /// </summary>
        public string PluginDescription { get; }

        /// <summary>
        /// The name of the plugin.
        /// </summary>
        public string PluginName { get; }

        /// <summary>
        /// The plugin version.
        /// </summary>
        public string PluginVersion { get; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Called when the plugin is disabled. EventHandler delegates should be unsubscribed here.
        /// </summary>
        public void Disabled();

        /// <summary>
        /// Called when the plugin is enabled. EventHandler delegates should be subscribed here.
        /// </summary>
        public void Enabled();

        #endregion Public Methods
    }
}