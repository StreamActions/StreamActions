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

namespace StreamActions.Attributes
{
    /// <summary>
    /// Marks a method as an EventHandler for a <see cref="TwitchLib.Client.TwitchClient.OnChatCommandReceived"/> event with a specified <see cref="TwitchLib.Client.Models.ChatCommand.CommandText"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ChatCommandAttribute : Attribute
    {
        #region Public Constructors

        /// <summary>
        /// Marks the method as being the handler for a <c>!command</c>.
        /// </summary>
        /// <param name="command">The command to detect, without the <c>!</c>.</param>
        public ChatCommandAttribute(string command) => this.Command = command;

        /// <summary>
        /// Marks the method as being the handler for a <c>!command subcommand</c>.
        /// </summary>
        /// <param name="command">The command to detect, without the <c>!</c>.</param>
        /// <param name="subcommand">The subcommand to detect.</param>
        public ChatCommandAttribute(string command, string subcommand)
        {
            this.Command = command;
            this.SubCommand = subcommand;
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// The command the marked method will handle.
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// The subcommand the marked method will handle. <c>null</c> if handling the main command.
        /// </summary>
        public string SubCommand { get; }

        #endregion Public Properties
    }
}