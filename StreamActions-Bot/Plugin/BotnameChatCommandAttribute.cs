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

using System;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Marks a method as an EventHandler for a specified <see cref="TwitchLib.Client.Models.ChatCommand"/> under the <c>!botname</c> tree.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class BotnameChatCommandAttribute : ChatCommandAttribute
    {
        #region Public Constructors

        /// <summary>
        /// Marks the method as being the handler for a <c>!botname command</c>.
        /// </summary>
        /// <param name="command">The command to detect.</param>
        public BotnameChatCommandAttribute(string command) : base(command)
        { }

        /// <summary>
        /// Marks the method as being the handler for a <c>!botname command subcommand</c>.
        /// </summary>
        /// <param name="command">The command to detect.</param>
        /// <param name="subcommand">The subcommand to detect.</param>
        public BotnameChatCommandAttribute(string command, string subcommand) : base(command, subcommand)
        { }

        #endregion Public Constructors
    }
}