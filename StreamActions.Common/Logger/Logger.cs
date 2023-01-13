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
using System.Diagnostics;
using System.Reflection;

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

    /// <summary>
    /// Gets the calling members name.
    /// </summary>
    /// <param name="selfLocation">The location within a <see cref="StackTrace"/> where the caller will be found. <code>2</code> is the caller of the member calling this method.</param>
    /// <param name="atLocation">If not <see langword="null"/> and greater than <paramref name="selfLocation"/>, then <code>@[Namespace.]Type.Name</code> of the caller at this location is additionally appended.</param>
    /// <param name="addNamespace">If <see langword="true"/>, the namespace of the caller is also added.</param>
    /// <returns><code>[Namespace.]Type.Name</code> if a valid entry is available at the specified location in the stacktrace; <code>@[Namespace.]Type.Name</code> is appended if <paramref name="atLocation"/> also points to a valid entry; <code>null</code> if no valid entry.</returns>
    public static string GetCaller(int selfLocation = 2, int? atLocation = null, bool addNamespace = false)
    {
        StackTrace st = new(selfLocation);
        if (st.FrameCount is 0)
        {
            return "null";
        }

        if (atLocation.HasValue && (atLocation.Value < selfLocation || atLocation.Value - selfLocation < st.FrameCount))
        {
            atLocation = null;
        }

        StackFrame? f = st.GetFrame(0);
        if (f is null)
        {
            return "null";
        }

        string s = "";

        if (f.HasMethod())
        {
            MethodBase? m = f.GetMethod();

            if (m is null)
            {
                s += "null";
            }
            else
            {
                if (addNamespace)
                {
                    s += m.ReflectedType?.Namespace ?? "null";
                    s += ".";
                }
                s += m.ReflectedType?.Name ?? "null";
                s += ".";
                s += m.Name;
            }
        }
        else
        {
            s += "none";
        }

        if (atLocation.HasValue)
        {
            s += "@";

            f = st.GetFrame(atLocation.Value - selfLocation);
            if (f is null)
            {
                s += "null";
            }
            else
            {
                if (f.HasMethod())
                {
                    MethodBase? m = f.GetMethod();

                    if (m is null)
                    {
                        s += "null";
                    }
                    else
                    {
                        if (addNamespace)
                        {
                            s += m.ReflectedType?.Namespace ?? "null";
                            s += ".";
                        }
                        s += m.ReflectedType?.Name ?? "null";
                        s += ".";
                        s += m.Name;
                    }
                }
                else
                {
                    s += "none";
                }
            }
        }

        return s;
    }

    /// <summary>
    /// Gets the <see cref="LogLevel"/> from the method attributes.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="method">The method to check.</param>
    /// <returns><see cref="LogLevel.Debug"/> on failure; else, the <see cref="LogLevel"/> set in the attribute.</returns>
    public static LogLevel GetMessageLogLevel(Type type, string method)
    {
        if (type is null)
        {
            return LogLevel.Debug;
        }

        MethodInfo? mInfo = type.GetMethod(method, new[] { typeof(ILogger), typeof(string), typeof(string), typeof(Exception) });

        if (mInfo is null)
        {
            return LogLevel.Debug;
        }

        LoggerMessageAttribute? attr = (LoggerMessageAttribute?)Attribute.GetCustomAttribute(mInfo, typeof(LoggerMessageAttribute));

        return attr is null ? LogLevel.Debug : attr.Level;
    }
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
