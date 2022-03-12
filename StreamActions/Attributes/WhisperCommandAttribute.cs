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

using StreamActions.Enums;
using System;

namespace StreamActions.Attributes
{
    /// <summary>
    /// Marks a method as an EventHandler for a specified WhisperCommand.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class WhisperCommandAttribute : CommandAttribute
    {
        #region Public Constructors

        /// <summary>
        /// Marks the method as being the whisper handler for a <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to detect.</param>
        /// <param name="permission">The default permission required to use the command.</param>
        public WhisperCommandAttribute(string command, UserLevels permission) : base(command, permission)
        {
        }

        /// <summary>
        /// Marks the method as being the whisper handler for a <c>!command subcommand</c>.
        /// </summary>
        /// <param name="command">The command to detect.</param>
        /// <param name="subcommand">The subcommand to detect.</param>
        /// <param name="permission">The default permission required to use the subcommand.</param>
        public WhisperCommandAttribute(string command, string subcommand, UserLevels permission) : base(command, subcommand, permission)
        {
        }

        #endregion Public Constructors
    }
}
