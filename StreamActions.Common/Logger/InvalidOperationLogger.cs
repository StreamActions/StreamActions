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

namespace StreamActions.Common.Logger;

/// <summary>
/// Provides <see cref="LoggerMessage"/> members for logging invalid arguments and operations.
/// </summary>
public static partial class InvalidOperationLogger
{
    #region Public Methods

    /// <summary>
    /// Logs that an <see cref="ArgumentNullException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that threw the exception.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="argument">The argument that was <see langword="null"/>.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "[{Type}.{Member}] Argument Null: {Argument}")]
    public static partial void ArgumentNull(this ILogger logger, string type, string member, string argument, Exception? exception = null);

    /// <summary>
    /// Logs that an <see cref="InvalidOperationException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that threw the exception.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="message">The message explaining why the operation was invalid.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "[{Type}.{Member}] Invalid Operation: {Message}")]
    public static partial void InvalidOperation(this ILogger logger, string type, string member, string message, Exception? exception = null);

    #endregion Public Methods
}
