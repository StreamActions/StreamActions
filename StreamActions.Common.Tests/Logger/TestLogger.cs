/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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

using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace StreamActions.Common.Tests.Logger;

public class LogEntry
{
    public LogLevel LogLevel { get; }
    public EventId EventId { get; }
    public Exception? Exception { get; }
    public string Message { get; }

    public LogEntry(LogLevel logLevel, EventId eventId, Exception? exception, string message)
    {
        LogLevel = logLevel;
        EventId = eventId;
        Exception = exception;
        Message = message;
    }
}

public class TestLogger : ILogger
{
    public Collection<LogEntry> Logs { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Logs.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
    }
}
