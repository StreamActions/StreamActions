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

using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Reads interactive console commands from standard input when running attached to an interactive terminal.
/// </summary>
internal sealed partial class InteractiveConsoleService : BackgroundService
{
    /// <summary>
    /// Command processor executing interactive console commands.
    /// </summary>
    private readonly DaemonCommandProcessor _commandProcessor;

    /// <summary>
    /// Host application lifetime used to initiate shutdown on <c>exit</c> or <c>quit</c>.
    /// </summary>
    private readonly IHostApplicationLifetime _applicationLifetime;

    /// <summary>
    /// Logger instance for console command errors.
    /// </summary>
    private readonly ILogger<InteractiveConsoleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveConsoleService"/> class.
    /// </summary>
    /// <param name="commandProcessor">The command processor executing console commands.</param>
    /// <param name="applicationLifetime">The host application lifetime used to trigger shutdown on <c>exit</c>/<c>quit</c>.</param>
    /// <param name="logger">The logger instance.</param>
    public InteractiveConsoleService(
        DaemonCommandProcessor commandProcessor,
        IHostApplicationLifetime applicationLifetime,
        ILogger<InteractiveConsoleService> logger)
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(logger);

        this._commandProcessor = commandProcessor;
        this._applicationLifetime = applicationLifetime;
        this._logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        await this.ProcessReaderAsync(Console.In, Console.Out, stoppingToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads and processes interactive commands from <paramref name="reader"/> until EOF, <c>exit</c>/<c>quit</c>, or cancellation.
    /// </summary>
    /// <param name="reader">The input text reader.</param>
    /// <param name="writer">The output text writer.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous console processing loop.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Interactive console command failures must be printed and logged without terminating the daemon.")]
    internal async Task ProcessReaderAsync(
        TextReader reader,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (string.Equals(trimmed, "exit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "quit", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("Shutting down StreamActions daemon...".AsMemory(), cancellationToken).ConfigureAwait(false);
                this._applicationLifetime.StopApplication();
                break;
            }

            try
            {
                string response = await this._commandProcessor.ExecuteCommandAsync(trimmed, cancellationToken).ConfigureAwait(false);
                await writer.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogConsoleCommandFailed(this._logger, ex);
                await writer.WriteLineAsync($"Error: {ex.Message}".AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Logs an unexpected failure while executing an interactive console command.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception thrown by command execution.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Interactive console command execution failed.")]
    private static partial void LogConsoleCommandFailed(ILogger logger, Exception exception);
}
