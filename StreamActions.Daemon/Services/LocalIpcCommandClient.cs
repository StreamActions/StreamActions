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

using StreamActions.Core.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Sends local IPC <c>exec</c> commands to a running StreamActions daemon over a named pipe or Unix domain socket.
/// </summary>
internal static class LocalIpcCommandClient
{
    /// <summary>
    /// Default timeout applied when no explicit IPC timeout is specified.
    /// </summary>
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Executes an administrative command against a running daemon instance and writes the output to <paramref name="output"/>.
    /// </summary>
    /// <param name="options">The bootstrap options identifying the target daemon IPC endpoint.</param>
    /// <param name="command">The command line to execute.</param>
    /// <param name="output">The standard output writer.</param>
    /// <param name="error">The standard error writer.</param>
    /// <param name="timeout">Optional connection/response timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>0</c> if the command executed successfully; otherwise <c>1</c>.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI exec mode reports connection or transport failures cleanly on stderr and returns exit code 1.")]
    public static async Task<int> ExecuteAsync(
        BootstrapOptions options,
        string command,
        TextWriter output,
        TextWriter error,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            string response = await SendCommandAsync(options, command, timeout, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync($"Failed to execute daemon command: {ex.Message}".AsMemory(), cancellationToken).ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>
    /// Sends a command to the running daemon over local IPC and returns its response text.
    /// </summary>
    /// <param name="options">The bootstrap options identifying the target daemon IPC endpoint.</param>
    /// <param name="command">The command line to send.</param>
    /// <param name="timeout">Optional connection/response timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The trimmed response string returned by the daemon.</returns>
    public static async Task<string> SendCommandAsync(
        BootstrapOptions options,
        string command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        TimeSpan effectiveTimeout = timeout ?? _defaultTimeout;
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(effectiveTimeout);

        if (OperatingSystem.IsWindows())
        {
            string pipeName = LocalIpcEndpoint.GetNamedPipeName(options);
            NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await using (client.ConfigureAwait(false))
            {
                await client.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);
                return await ExchangeCommandAsync(client, command, null, timeoutCts.Token).ConfigureAwait(false);
            }
        }
        else
        {
            string socketPath = LocalIpcEndpoint.GetUnixSocketPath(options);
            using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeoutCts.Token).ConfigureAwait(false);
            NetworkStream stream = new(socket, ownsSocket: false);
            await using (stream.ConfigureAwait(false))
            {
                return await ExchangeCommandAsync(stream, command, socket, timeoutCts.Token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="command"/> to <paramref name="stream"/>, signals end-of-send on <paramref name="socket"/> if present, and reads the trimmed response.
    /// </summary>
    /// <param name="stream">The connected IPC stream.</param>
    /// <param name="command">The command line to send.</param>
    /// <param name="socket">Optional underlying Unix domain socket to half-close after sending.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The trimmed response text returned by the daemon.</returns>
    private static async Task<string> ExchangeCommandAsync(
        Stream stream,
        string command,
        Socket? socket,
        CancellationToken cancellationToken)
    {
        StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            await writer.WriteLineAsync((command ?? string.Empty).AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        socket?.Shutdown(SocketShutdown.Send);

        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        string response = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return response.TrimEnd('\r', '\n');
    }
}
