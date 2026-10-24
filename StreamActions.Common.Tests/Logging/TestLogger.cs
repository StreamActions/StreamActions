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

using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace StreamActions.Common.Tests.Logging;

public class LogEntry(LogLevel logLevel, EventId eventId, Exception? exception, string message)
{
    public LogLevel LogLevel { get; } = logLevel;
    public EventId EventId { get; } = eventId;
    public Exception? Exception { get; } = exception;
    public string Message { get; } = message;
}

public class TestLogger : ILogger
{
    public Collection<LogEntry> Logs { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        this.Logs.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
    }
}
