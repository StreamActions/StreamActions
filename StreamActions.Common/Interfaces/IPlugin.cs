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

namespace StreamActions.Common.Interfaces
{
    /// <summary>
    /// An interface for a plugin that processes messages, commands, and other input.
    /// </summary>
    public interface IPlugin
    {
        #region Public Properties

        /// <summary>
        /// Whether the plugin should always be enabled for all channels. This should only be true in core plugins and special circumstances.
        /// </summary>
        public bool AlwaysEnabled { get; }

        /// <summary>
        /// A collection of <see cref="Guid"/> of other Plugins, APIs, Chat Backends, etc that this plugin is dependent on.
        /// </summary>
        public IReadOnlyCollection<Guid> Dependencies { get; }

        /// <summary>
        /// The author of the plugin.
        /// </summary>
        public string PluginAuthor { get; }

        /// <summary>
        /// A description of the plugin.
        /// </summary>
        public string PluginDescription { get; }

        /// <summary>
        /// A Guid used to identify the plugin.
        /// </summary>
        public Guid PluginId { get; }

        /// <summary>
        /// The name of the plugin.
        /// </summary>
        public string PluginName { get; }

        /// <summary>
        /// The Uri of the plugin.
        /// </summary>
        public Uri PluginUri { get; }

        /// <summary>
        /// The plugin version. Usage of semver is highly preferred.
        /// </summary>
        public string PluginVersion { get; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Called when the plugin is disabled either globally, or by all channels. EventHandler delegates must be unsubscribed here.
        /// </summary>
        public void Disabled();

        /// <summary>
        /// Called when the plugin is enabled either globally, or by at least one channel. EventHandler delegates must be subscribed here.
        /// </summary>
        public void Enabled();

        #endregion Public Methods
    }
}
