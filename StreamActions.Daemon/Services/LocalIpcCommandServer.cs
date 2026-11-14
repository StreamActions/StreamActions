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

using Microsoft.Extensions.Options;
using StreamActions.Core.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Listens on a scoped Windows named pipe or Unix domain socket to serve local IPC <c>exec</c> commands.
/// </summary>
internal sealed partial class LocalIpcCommandServer : BackgroundService
{
    /// <summary>
    /// Command processor executing incoming IPC commands.
    /// </summary>
    private readonly DaemonCommandProcessor _commandProcessor;

    /// <summary>
    /// Host bootstrap options used to derive the IPC endpoint.
    /// </summary>
    private readonly IOptions<BootstrapOptions> _bootstrapOptions;

    /// <summary>
    /// Logger instance for IPC request failures.
    /// </summary>
    private readonly ILogger<LocalIpcCommandServer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalIpcCommandServer"/> class.
    /// </summary>
    /// <param name="commandProcessor">The command processor executing incoming commands.</param>
    /// <param name="bootstrapOptions">The host bootstrap options.</param>
    /// <param name="logger">The logger instance.</param>
    public LocalIpcCommandServer(
        DaemonCommandProcessor commandProcessor,
        IOptions<BootstrapOptions> bootstrapOptions,
        ILogger<LocalIpcCommandServer> logger)
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(bootstrapOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this._commandProcessor = commandProcessor;
        this._bootstrapOptions = bootstrapOptions;
        this._logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        BootstrapOptions options = this._bootstrapOptions.Value;
        if (OperatingSystem.IsWindows())
        {
            string pipeName = LocalIpcEndpoint.GetNamedPipeName(options);
            await this.RunNamedPipeLoopAsync(pipeName, stoppingToken).ConfigureAwait(false);
        }
        else
        {
            string socketPath = LocalIpcEndpoint.GetUnixSocketPath(options);
            await this.RunUnixSocketLoopAsync(socketPath, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Accepts and handles incoming Windows named pipe connections until <paramref name="stoppingToken"/> is cancelled.
    /// </summary>
    /// <param name="pipeName">The scoped named pipe name.</param>
    /// <param name="stoppingToken">A token to monitor for shutdown requests.</param>
    /// <returns>A task representing the asynchronous listener loop.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "IPC server loop must isolate per-request errors and keep listening until cancelled.")]
    private async Task RunNamedPipeLoopAsync(string pipeName, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream server = new(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await using (server.ConfigureAwait(false))
            {
                try
                {
                    await server.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                    await this.HandleStreamAsync(server, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogIpcRequestFailed(this._logger, ex);
                }
            }
        }
    }

    /// <summary>
    /// Binds and accepts incoming Unix domain socket connections until <paramref name="stoppingToken"/> is cancelled, cleaning up the socket file on exit.
    /// </summary>
    /// <param name="socketPath">The Unix domain socket file path.</param>
    /// <param name="stoppingToken">A token to monitor for shutdown requests.</param>
    /// <returns>A task representing the asynchronous listener loop.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "IPC server loop must isolate per-request errors and clean up the socket file on shutdown.")]
    private async Task RunUnixSocketLoopAsync(string socketPath, CancellationToken stoppingToken)
    {
        string? directory = Path.GetDirectoryName(socketPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }

        using Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(8);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await listener.AcceptAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                using (client)
                {
                    try
                    {
                        await this.HandleUnixClientAsync(client, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogIpcRequestFailed(this._logger, ex);
                    }
                }
            }
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    /// <summary>
    /// Processes a single accepted Unix domain socket client and shuts down the socket.
    /// </summary>
    /// <param name="client">The connected client socket.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous client handling operation.</returns>
    private async Task HandleUnixClientAsync(Socket client, CancellationToken cancellationToken)
    {
        using NetworkStream stream = new(client, ownsSocket: false);
        await this.HandleStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        client.Shutdown(SocketShutdown.Both);
    }

    /// <summary>
    /// Reads a single command line from <paramref name="stream"/>, executes it, and writes the response line.
    /// </summary>
    /// <param name="stream">The connected IPC stream.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous stream exchange.</returns>
    private async Task HandleStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string? command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        string response = await this._commandProcessor.ExecuteCommandAsync(command ?? string.Empty, cancellationToken).ConfigureAwait(false);

        StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            await writer.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Logs a failed local IPC command request.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception thrown while handling the request.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Local IPC command request failed.")]
    private static partial void LogIpcRequestFailed(ILogger logger, Exception exception);
}
