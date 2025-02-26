﻿/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Exceptions;
using System.Diagnostics;

namespace StreamActions.Common.Logger;

/// <summary>
/// Provides <see cref="LoggerMessage"/> members for logging invalid arguments and operations.
/// </summary>
public static partial class InvalidOperationLogger
{
    #region Public Methods

    /// <summary>
    /// Logs that an <see cref="Argument<see langword="null"/>Exception"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="message">A message that describes the error..</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "[{Member}] Argument Null: {Message}")]
    public static partial void ArgumentNull(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs that an <see cref="Argument<see langword="null"/>Exception"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that threw the exception.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="paramName">The message that was <see langword="null"/>.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void ArgumentNull(this ILogger logger, string type, string member, string? paramName = null, string? message = null, Exception? exception = null) => logger.ArgumentNull(type + "." + member, Logger.ConstructLogMessage(new Dictionary<string, object?>() { { nameof(paramName), paramName } }, message), exception);

    /// <summary>
    /// Logs that an <see cref="Argument<see langword="null"/>Exception"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="paramName">The message that was <see langword="null"/>.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void ArgumentNull(this ILogger logger, string? paramName = null, string? message = null, Exception? exception = null, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            logger.ArgumentNull(Logger.GetCaller(selfLocation, atLocation, addNamespace), Logger.ConstructLogMessage(new Dictionary<string, object?>() { { nameof(paramName), paramName } }, message), exception);
        }
    }

    /// <summary>
    /// Logs that an <see cref="Argument<see langword="null"/>Exception"/> was thrown.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    /// <returns><paramref name="exception"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> was <see langword="null"/>.</exception>
    public static ArgumentNullException Log(this ArgumentNullException exception, ILogger logger, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        ArgumentNullException.ThrowIfNull(exception);

        logger?.ArgumentNull(exception.ParamName, exception.Message, exception, selfLocation, atLocation, addNamespace);

        return exception;
    }

    /// <summary>
    /// Logs that an <see cref="ArgumentOutOfRangeException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="message">A message that describes the error..</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "[{Member}] Argument Out of Range: {Message}")]
    public static partial void ArgumentOutOfRange(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs that an <see cref="ArgumentOutOfRangeException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that threw the exception.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="paramName">The message that was out of range.</param>
    /// <param name="actualValue">The value of <paramref name="paramName"/> that caused this exception.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void ArgumentOutOfRange(this ILogger logger, string type, string member, string? paramName = null, object? actualValue = null, string? message = null, Exception? exception = null) => logger.ArgumentNull(type + "." + member, Logger.ConstructLogMessage(new Dictionary<string, object?>() { { nameof(paramName), paramName }, { nameof(actualValue), actualValue?.ToString() } }, message), exception);

    /// <summary>
    /// Logs that an <see cref="ArgumentOutOfRangeException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="paramName">The message that was out of range.</param>
    /// <param name="actualValue">The value of <paramref name="paramName"/> that caused this exception.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void ArgumentOutOfRange(this ILogger logger, string? paramName = null, object? actualValue = null, string? message = null, Exception? exception = null, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            logger.ArgumentOutOfRange(Logger.GetCaller(selfLocation, atLocation, addNamespace), Logger.ConstructLogMessage(new Dictionary<string, object?>() { { nameof(paramName), paramName }, { nameof(actualValue), actualValue?.ToString() } }, message), exception);
        }
    }

    /// <summary>
    /// Logs that an <see cref="ArgumentOutOfRangeException"/> was thrown.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    /// <returns><paramref name="exception"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> was <see langword="null"/>.</exception>
    public static ArgumentOutOfRangeException Log(this ArgumentOutOfRangeException exception, ILogger logger, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        ArgumentNullException.ThrowIfNull(exception);

        logger?.ArgumentOutOfRange(exception.ParamName, exception.ActualValue, exception.Message, exception, selfLocation, atLocation, addNamespace);

        return exception;
    }

    /// <summary>
    /// Logs that an <see cref="InvalidOperationException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="message">A message that describes the error..</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "[{Member}] Invalid Operation: {Message}")]
    public static partial void InvalidOperation(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs that an <see cref="InvalidOperationException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that threw the exception.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void InvalidOperation(this ILogger logger, string type, string member, string message, Exception? exception = null) => logger.InvalidOperation(type + "." + member, message, exception);

    /// <summary>
    /// Logs that an <see cref="InvalidOperationException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void InvalidOperation(this ILogger logger, string message, Exception? exception = null, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Debug) ?? false)
        {
            logger.InvalidOperation(Logger.GetCaller(selfLocation, atLocation, addNamespace), message, exception);
        }
    }

    /// <summary>
    /// Logs that an <see cref="InvalidOperationException"/> was thrown.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    /// <returns><paramref name="exception"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> was <see langword="null"/>.</exception>
    public static InvalidOperationException Log(this InvalidOperationException exception, ILogger logger, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        ArgumentNullException.ThrowIfNull(exception);

        logger?.InvalidOperation(exception.Message, exception, selfLocation, atLocation, addNamespace);

        return exception;
    }

    /// <summary>
    /// Logs that a <see cref="Exceptions.ScopeMissingException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Error,
        Message = "[{Member}] Scope Missing: {Message}")]
    public static partial void ScopeMissing(this ILogger logger, string member, string message, Exception? exception = null);

    /// <summary>
    /// Logs that a <see cref="Exceptions.ScopeMissingException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="type">The type that threw the exception.</param>
    /// <param name="member">The member that threw the exception.</param>
    /// <param name="scope">The message that was missing.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    public static void ScopeMissing(this ILogger logger, string type, string member, string? scope = null, string? message = null, Exception? exception = null) => logger.ScopeMissing(type + "." + member, Logger.ConstructLogMessage(new Dictionary<string, object?>() { { nameof(scope), scope } }, message), exception);

    /// <summary>
    /// Logs that a <see cref="Exceptions.ScopeMissingException"/> was thrown.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="scope">The message that was missing.</param>
    /// <param name="message">A message that describes the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    public static void ScopeMissing(this ILogger logger, string? scope = null, string? message = null, Exception? exception = null, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        if (logger?.IsEnabled(LogLevel.Error) ?? false)
        {
            logger.ScopeMissing(Logger.GetCaller(selfLocation, atLocation, addNamespace), Logger.ConstructLogMessage(new Dictionary<string, object?>() { { nameof(scope), scope } }, message), exception);
        }
    }

    /// <summary>
    /// Logs that a <see cref="Exceptions.ScopeMissingException"/> was thrown.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the message calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    /// <returns><paramref name="exception"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> was <see langword="null"/>.</exception>
    public static ScopeMissingException Log(this ScopeMissingException exception, ILogger logger, int selfLocation = 2, int? atLocation = 3, bool addNamespace = false)
    {
        ArgumentNullException.ThrowIfNull(exception);

        logger?.ScopeMissing(exception.Scope, exception.Message, exception, selfLocation, atLocation, addNamespace);

        return exception;
    }

    #endregion Public Methods
}
