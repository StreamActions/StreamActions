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
using Microsoft.Extensions.Logging.Console;

namespace StreamActions.Common.Logger;

/// <summary>
/// Provides <see cref="ILogger"/> instances.
/// </summary>
public static class Logger
{
    #region Public Methods

    /// <summary>
    /// Creates an <see cref="ILogger{TCategoryName}"/>.
    /// </summary>
    /// <typeparam name="T">The type to create the logger for. The fully qualified name of the type will be the logging category.</typeparam>
    /// <returns>An <see cref="ILogger{TCategoryName}"/>.</returns>
    public static ILogger<T> GetLogger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>
    /// Creates an <see cref="ILogger"/>.
    /// </summary>
    /// <param name="t">The type to create the logger for. The fully qualified name of the type will be the logging category.</param>
    /// <returns>An <see cref="ILogger"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="t"/> is null; <see cref="Type.FullName"/> is null, blank, or whitespace.</exception>
    public static ILogger GetLogger(Type t) => GetLogger(t?.FullName);

    /// <summary>
    /// Creates an <see cref="ILogger"/>.
    /// </summary>
    /// <param name="category">The logging category.</param>
    /// <returns>An <see cref="ILogger"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="category"/> is null, blank, or whitespace.</exception>
    public static ILogger GetLogger(string? category) => string.IsNullOrWhiteSpace(category) ? throw new ArgumentNullException(nameof(category)) : _loggerFactory.CreateLogger(category);

    #endregion Public Methods

    #region Private Fields

    /// <summary>
    /// <see cref="ILoggerFactory"/> used by <see cref="GetLogger{T}"/>.
    /// </summary>
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffzzz";
            options.ColorBehavior = LoggerColorBehavior.Default;
            options.SingleLine = false;
        }));

    #endregion Private Fields
}
