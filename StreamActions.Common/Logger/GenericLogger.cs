/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using System.Diagnostics;

namespace StreamActions.Common.Logger;

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
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 9999,
        Level = LogLevel.Critical,
        Message = "[{Member}] {Message}")]
    public static partial void Critical(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs a generic critical message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that is logging the message.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void Critical(this ILogger logger, string type, string member, string message, Exception? exception = null) => logger.Critical(type + "." + member, message, exception);

    /// <summary>
    /// Logs a generic critical message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the argument calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void Critical(this ILogger logger, string message, Exception? exception = null, int selfLocation = 2, int? atLocation = null, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Critical) ?? false)
        {
            logger.Critical(Logger.GetCaller(selfLocation, atLocation, addNamespace), message, exception);
        }
    }

    /// <summary>
    /// Logs a generic debug message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 9995,
        Level = LogLevel.Debug,
        Message = "[{Member}] {Message}")]
    public static partial void Debug(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs a generic debug message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that is logging the message.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void Debug(this ILogger logger, string type, string member, string message, Exception? exception = null) => logger.Debug(type + "." + member, message, exception);

    /// <summary>
    /// Logs a generic debug message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the argument calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void Debug(this ILogger logger, string message, Exception? exception = null, int selfLocation = 2, int? atLocation = null, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            logger.Debug(Logger.GetCaller(selfLocation, atLocation, addNamespace), message, exception);
        }
    }

    /// <summary>
    /// Logs a generic error message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 9998,
        Level = LogLevel.Error,
        Message = "[{Member}] {Message}")]
    public static partial void Error(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs a generic error message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that is logging the message.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void Error(this ILogger logger, string type, string member, string message, Exception? exception = null) => logger.Error(type + "." + member, message, exception);

    /// <summary>
    /// Logs a generic error message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the argument calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void Error(this ILogger logger, string message, Exception? exception = null, int selfLocation = 2, int? atLocation = null, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Error) ?? false)
        {
            logger.Error(Logger.GetCaller(selfLocation, atLocation, addNamespace), message, exception);
        }
    }

    /// <summary>
    /// Logs a generic information message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 9996,
        Level = LogLevel.Information,
        Message = "[{Member}] {Message}")]
    public static partial void Information(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs a generic information message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that is logging the message.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void Information(this ILogger logger, string type, string member, string message, Exception? exception = null) => logger.Information(type + "." + member, message, exception);

    /// <summary>
    /// Logs a generic information message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the argument calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void Information(this ILogger logger, string message, Exception? exception = null, int selfLocation = 2, int? atLocation = null, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Information) ?? false)
        {
            logger.Information(Logger.GetCaller(selfLocation, atLocation, addNamespace), message, exception);
        }
    }

    /// <summary>
    /// Logs a generic trace message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 9994,
        Level = LogLevel.Trace,
        Message = "[{Member}] {Message}")]
    public static partial void Trace(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs a generic trace message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that is logging the message.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void Trace(this ILogger logger, string type, string member, string message, Exception? exception = null) => logger.Trace(type + "." + member, message, exception);

    /// <summary>
    /// Logs a generic trace message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the argument calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void Trace(this ILogger logger, string message, Exception? exception = null, int selfLocation = 2, int? atLocation = null, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Trace) ?? false)
        {
            logger.Trace(Logger.GetCaller(selfLocation, atLocation, addNamespace), message, exception);
        }
    }

    /// <summary>
    /// Logs a generic warning message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 9997,
        Level = LogLevel.Warning,
        Message = "[{Member}] {Message}")]
    public static partial void Warning(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs a generic warning message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that is logging the message.</param>
    /// <param name="member">The member that is logging the message.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void Warning(this ILogger logger, string type, string member, string message, Exception? exception = null) => logger.Warning(type + "." + member, message, exception);

    /// <summary>
    /// Logs a generic warning message.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the argument calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void Warning(this ILogger logger, string message, Exception? exception = null, int selfLocation = 2, int? atLocation = null, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Warning) ?? false)
        {
            logger.Warning(Logger.GetCaller(selfLocation, atLocation, addNamespace), message, exception);
        }
    }

    #endregion Public Methods
}
