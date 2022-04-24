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
    /// Provides <see cref="LoggerMessage"/> and <see cref="EventId"/> instances for wriring generic log messages.
    /// </summary>
    public static class GenericLogger
    {
        #region Public Properties

        /// <summary>
        /// A generic critical event.
        /// </summary>
        public static EventId CriticalId => new(9999, "Critical");

        /// <summary>
        /// A generic debug event.
        /// </summary>
        public static EventId DebugId => new(9995, "Debug");

        /// <summary>
        /// A generic error event.
        /// </summary>
        public static EventId ErrorId => new(9998, "Error");

        /// <summary>
        /// A generic information event.
        /// </summary>
        public static EventId InformationId => new(9996, "Information");

        /// <summary>
        /// A generic trace event.
        /// </summary>
        public static EventId TraceId => new(9994, "Trace");

        /// <summary>
        /// A generic warning event.
        /// </summary>
        public static EventId WarningId => new(9997, "Warning");

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Logs a generic critical message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void Critical(this ILogger logger, string type, string member, string message, Exception? exception = null) => _critical(logger, type, member, message, exception);

        /// <summary>
        /// Logs a generic debug message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void Debug(this ILogger logger, string type, string member, string message, Exception? exception = null) => _debug(logger, type, member, message, exception);

        /// <summary>
        /// Logs a generic error message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void Error(this ILogger logger, string type, string member, string message, Exception? exception = null) => _error(logger, type, member, message, exception);

        /// <summary>
        /// Logs a generic information message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void Information(this ILogger logger, string type, string member, string message, Exception? exception = null) => _information(logger, type, member, message, exception);

        /// <summary>
        /// Logs a generic trace message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void Trace(this ILogger logger, string type, string member, string message, Exception? exception = null) => _trace(logger, type, member, message, exception);

        /// <summary>
        /// Logs a generic warning message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that is logging the message.</param>
        /// <param name="member">The member that is logging the message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void Warning(this ILogger logger, string type, string member, string message, Exception? exception = null) => _warning(logger, type, member, message, exception);

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// A <see cref="LoggerMessage"/> for a generic critical message.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _critical = LoggerMessage.Define<string, string, string>(LogLevel.Critical, CriticalId, "[{Type}.{Member}] {Message}");

        /// <summary>
        /// A <see cref="LoggerMessage"/> for a generic debug message.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _debug = LoggerMessage.Define<string, string, string>(LogLevel.Debug, DebugId, "[{Type}.{Member}] {Message}");

        /// <summary>
        /// A <see cref="LoggerMessage"/> for a generic error message.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _error = LoggerMessage.Define<string, string, string>(LogLevel.Error, ErrorId, "[{Type}.{Member}] {Message}");

        /// <summary>
        /// A <see cref="LoggerMessage"/> for a generic informaion message.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _information = LoggerMessage.Define<string, string, string>(LogLevel.Information, InformationId, "[{Type}.{Member}] {Message}");

        /// <summary>
        /// A <see cref="LoggerMessage"/> for a generic trace message.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _trace = LoggerMessage.Define<string, string, string>(LogLevel.Trace, TraceId, "[{Type}.{Member}] {Message}");

        /// <summary>
        /// A <see cref="LoggerMessage"/> for a generic warning message.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _warning = LoggerMessage.Define<string, string, string>(LogLevel.Warning, WarningId, "[{Type}.{Member}] {Message}");

        #endregion Private Fields
    }
}
