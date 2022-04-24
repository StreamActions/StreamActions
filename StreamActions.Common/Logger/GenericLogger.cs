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

using Microsoft.Extensions.Logging;

namespace StreamActions.Common.Logger
{
    /// <summary>
    /// Provides <see cref="LoggerMessage"/> members for writing generic log messages.
    /// </summary>
    public static partial class GenericLogger
    {
        #region Public Methods

        /// <summary>
        /// Logs a generic critical message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        [LoggerMessage(
            EventId = 9999,
            Level = LogLevel.Critical,
            Message = "[{Type}.{Member}] {Message}")]
        public static partial void Critical(this ILogger logger, string type, string member, string message, Exception? exception = null);

        /// <summary>
        /// Logs a generic debug message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        [LoggerMessage(
            EventId = 9999,
            Level = LogLevel.Debug,
            Message = "[{Type}.{Member}] {Message}")]
        public static partial void Debug(this ILogger logger, string type, string member, string message, Exception? exception = null);

        /// <summary>
        /// Logs a generic error message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        [LoggerMessage(
            EventId = 9999,
            Level = LogLevel.Error,
            Message = "[{Type}.{Member}] {Message}")]
        public static partial void Error(this ILogger logger, string type, string member, string message, Exception? exception = null);

        /// <summary>
        /// Logs a generic information message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        [LoggerMessage(
            EventId = 9999,
            Level = LogLevel.Information,
            Message = "[{Type}.{Member}] {Message}")]
        public static partial void Information(this ILogger logger, string type, string member, string message, Exception? exception = null);

        /// <summary>
        /// Logs a generic trace message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        [LoggerMessage(
            EventId = 9999,
            Level = LogLevel.Trace,
            Message = "[{Type}.{Member}] {Message}")]
        public static partial void Trace(this ILogger logger, string type, string member, string message, Exception? exception = null);

        /// <summary>
        /// Logs a generic warning message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        [LoggerMessage(
            EventId = 9999,
            Level = LogLevel.Warning,
            Message = "[{Type}.{Member}] {Message}")]
        public static partial void Warning(this ILogger logger, string type, string member, string message, Exception? exception = null);

        #endregion Public Methods
    }
}
