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
    /// Provides <see cref="LoggerMessage"/> and <see cref="EventId"/> instances for logging invalid arguments and operations.
    /// </summary>
    public static class InvalidOperationLogger
    {
        #region Public Properties

        /// <summary>
        /// An argument to a member was invalid, such as <see langword="null"/> or a value that is out of range.
        /// </summary>
        public static EventId InvalidArgumentId => new(1000, "InvalidArgument");

        /// <summary>
        /// An invalid operation was attempted, such as using a type that has an <c>Init</c> member without calling that <c>Init</c> member.
        /// </summary>
        public static EventId InvalidOperationId => new(1001, "InvalidOperation");

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Logs that an <see cref="ArgumentNullException"/> was thrown.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that threw the exception.</param>
        /// <param name="member">The member that threw the exception.</param>
        /// <param name="argument">The argument that was <see langword="null"/>.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void ArgumentNull(this ILogger logger, string type, string member, string argument, Exception? exception = null) => _argumentNull(logger, type, member, argument, exception);

        /// <summary>
        /// Logs that an <see cref="InvalidOperationException"/> was thrown.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="type">The type that threw the exception.</param>
        /// <param name="member">The member that threw the exception.</param>
        /// <param name="message">The message explaining why the operation was invalid.</param>
        /// <param name="exception">The exception that was thrown.</param>
        public static void InvalidOperation(this ILogger logger, string type, string member, string message, Exception? exception = null) => _invalidOperation(logger, type, member, message, exception);

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// A <see cref="LoggerMessage"/> for logging that a member threw <see cref="ArgumentNullException"/>.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _argumentNull = LoggerMessage.Define<string, string, string>(LogLevel.Debug, InvalidArgumentId, "[{Type}.{Member}] Argument Null: {Argument}");

        /// <summary>
        /// A <see cref="LoggerMessage"/> for logging that a member threw <see cref="InvalidOperationException"/>.
        /// </summary>
        private static readonly Action<ILogger, string, string, string, Exception?> _invalidOperation = LoggerMessage.Define<string, string, string>(LogLevel.Debug, InvalidOperationId, "[{Type}.{Member}] Invalid Operation: {Reason}");

        #endregion Private Fields
    }
}
