/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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
    /// Marks a method as an EventHandler for a OnCommandReceived event with a specified CommandText.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class CommandAttribute : Attribute
    {
        #region Public Constructors

        /// <summary>
        /// Marks the method as being the handler for a <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to detect, without the <c>!</c>.</param>
        /// <param name="permission">The default permission required to use the command.</param>
        public CommandAttribute(string command, UserLevels permission)
        {
            this.Command = command;
            this.Permission = permission;
        }

        /// <summary>
        /// Marks the method as being the handler for a <c>!command subcommand</c>.
        /// </summary>
        /// <param name="command">The command to detect, without the <c>!</c>.</param>
        /// <param name="subcommand">The subcommand to detect.</param>
        /// <param name="permission">The default permission required to use the subcommand.</param>
        public CommandAttribute(string command, string subcommand, UserLevels permission)
        {
            this.Command = command;
            this.SubCommand = subcommand;
            this.Permission = permission;
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// The command the marked method will handle.
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// The default permission required to use the main command.
        /// </summary>
        public UserLevels Permission { get; }

        /// <summary>
        /// The subcommand the marked method will handle. <c>null</c> if handling the main command.
        /// </summary>
        public string SubCommand { get; }

        #endregion Public Properties
    }
}
