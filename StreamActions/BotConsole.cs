﻿/*
 * Copyright © 2019-2021 StreamActions Team
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

using Exceptionless;
using System;

/// <summary>
/// Contains members that handle console output.
/// </summary>
namespace StreamActions.BotConsole
{
    /// <summary>
    /// Handles writing debug messages to standard output.
    /// </summary>
    public static class Debug
    {
        #region Public Methods

        /// <summary>
        /// Writes the specified content, if debugging is enabled.
        /// </summary>
        /// <param name="content">The content to write.</param>
        public static void Write(dynamic content)
        {
            if (Program.Settings.ShowDebugMessages)
            {
                _ = Console.Out.Write(content);
            }
        }

        /// <summary>
        /// Writes the specified content, followed by a line terminator, if debugging is enabled.
        /// </summary>
        /// <param name="content">The content to write.</param>
        public static void WriteLine(dynamic content)
        {
            if (Program.Settings.ShowDebugMessages)
            {
                _ = Console.Out.WriteLine(content);
            }
        }

        /// <summary>
        /// Writes a line terminator, if debugging is enabled.
        /// </summary>
        public static void WriteLine()
        {
            if (Program.Settings.ShowDebugMessages)
            {
                Console.Out.WriteLine();
            }
        }

        #endregion Public Methods
    }

    /// <summary>
    /// Handles writing to standard error.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Matching Microsoft Naming of Type.")]
    public static class Error
    {
        #region Public Methods

        /// <summary>
        /// Writes the specified content.
        /// </summary>
        /// <param name="content">The content to write.</param>
        public static void Write(dynamic content) => Console.Error.Write(content);

        /// <summary>
        /// Writes the specified content, followed by a line terminator.
        /// </summary>
        /// <param name="content">The content to write.</param>
        public static void WriteLine(dynamic content) => Console.Error.WriteLine(content);

        /// <summary>
        /// Writes a line terminator.
        /// </summary>
        public static void WriteLine() => Console.Error.WriteLine();

        #endregion Public Methods
    }

    /// <summary>
    /// Handles writing exceptions to standard error, and sending to the remote Exceptionless server, if enabled.
    /// </summary>
    public static class ExceptionOut
    {
        #region Public Methods

        /// <summary>
        /// Sends the exception to the remote Exceptionless server, if enabled.
        /// </summary>
        /// <param name="e">The exception that was thrown.</param>
        /// <param name="data">Any extra data that needs to be sent to the remote Exceptionless server.</param>
        public static void SendException(Exception e, object data)
        {
            if (!Program.Settings.SendExceptions || e is null)
            {
                return;
            }

            if (!ExceptionlessClient.Default.Configuration.IsValid)
            {
                Error.WriteLine("Unable to send to Exceptionless");
                foreach (string msg in ExceptionlessClient.Default.Configuration.Validate().Messages)
                {
                    Error.WriteLine(msg);
                }
                return;
            }

            if (data is null)
            {
                data = "N/A";
            }

            e.ToExceptionless().AddObject(data, "Data").Submit();
        }

        /// <summary>
        /// Writes the exception to standard error and sends it to the remote Exceptionless server, according to the settings.
        /// </summary>
        /// <param name="niceDescription">The description printed to the console if debug mode is turned off.</param>
        /// <param name="e">The exception that was thrown.</param>
        /// <param name="data">Any extra data that needs to be sent to the remote Exceptionless server.</param>
        /// <param name="sendException">Whether to send the exception to Exceptionless, if enabled.</param>
        public static void WriteException(string niceDescription, Exception e, object data, bool sendException = true)
        {
            if (e is null)
            {
                return;
            }

            if (data is null)
            {
                data = "N/A";
            }

            if (!string.IsNullOrWhiteSpace(niceDescription))
            {
                Console.Error.WriteLine("[Exception] " + niceDescription);
            }

            if (Program.Settings.ShowDebugMessages)
            {
                Console.Error.WriteLine("[" + e.GetType().FullName + "] " + e.Message);
                Console.Error.WriteLine("--StackTrace: " + e.StackTrace);
                Console.Error.WriteLine("--Data: " + data.ToString());
            }

            if (sendException)
            {
                SendException(e, data);
            }
        }

        #endregion Public Methods
    }

    /// <summary>
    /// Handles writing to standard output.
    /// </summary>
    public static class Out
    {
        #region Public Methods

        /// <summary>
        /// Writes the specified content.
        /// </summary>
        /// <param name="content">The content to write.</param>
        public static void Write(dynamic content) => Console.Out.Write(content);

        /// <summary>
        /// Writes the specified content, followed by a line terminator.
        /// </summary>
        /// <param name="content">The content to write.</param>
        public static void WriteLine(dynamic content) => Console.Out.WriteLine(content);

        /// <summary>
        /// Writes a line terminator.
        /// </summary>
        public static void WriteLine() => Console.Out.WriteLine();

        #endregion Public Methods
    }
}