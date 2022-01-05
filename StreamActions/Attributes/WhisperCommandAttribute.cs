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
