/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Common.Interfaces
{
    /// <summary>
    /// An interface for a plugin that processes messages, commands, and other input.
    /// </summary>
    public abstract class IPlugin : IComponent
    {
        #region Public Properties

        /// <summary>
        /// Whether the plugin should always be enabled for all channels. This should only be true in core plugins and special circumstances.
        /// </summary>
        public bool AlwaysEnabled { get; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Called when the plugin is disabled either globally, or by all channels. EventHandler delegates must be unsubscribed here.
        /// </summary>
        public abstract void Disabled();

        /// <summary>
        /// Called when the plugin is enabled either globally, or by at least one channel. EventHandler delegates must be subscribed here.
        /// </summary>
        public abstract void Enabled();

        #endregion Public Methods
    }
}
