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
using System.Security.Cryptography;
using System.Text;

namespace StreamActions.Daemon.Services;

/// <summary>
/// Computes deterministic local IPC endpoint names scoped to <see cref="PathBootstrapOptions.DataDir"/> and <see cref="NodeBootstrapOptions.InstanceId"/>.
/// </summary>
internal static class LocalIpcEndpoint
{
    /// <summary>
    /// Maximum UTF-8 byte length allowed for a Unix domain socket path before falling back to the system temp directory.
    /// </summary>
    private const int _maxUnixSocketPathUtf8Bytes = 100;

    /// <summary>
    /// Computes the Windows named pipe name for the configured daemon instance.
    /// </summary>
    /// <param name="options">The host bootstrap options.</param>
    /// <returns>A scoped named pipe identifier.</returns>
    public static string GetNamedPipeName(BootstrapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"streamactions-daemon-{ComputeScopeHash(options)}";
    }

    /// <summary>
    /// Computes the Unix domain socket path for the configured daemon instance.
    /// </summary>
    /// <param name="options">The host bootstrap options.</param>
    /// <returns>The Unix domain socket file path.</returns>
    public static string GetUnixSocketPath(BootstrapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string dataDir = Path.GetFullPath(options.Paths.DataDir);
        string candidate = Path.Combine(dataDir, "daemon.sock");
        return Encoding.UTF8.GetByteCount(candidate) <= _maxUnixSocketPathUtf8Bytes
            ? candidate
            : Path.Combine(Path.GetTempPath(), $"sa-{ComputeScopeHash(options)}.sock");
    }

    /// <summary>
    /// Computes a short deterministic SHA-256 hex digest scoping the IPC endpoint to <see cref="PathBootstrapOptions.DataDir"/> and <see cref="NodeBootstrapOptions.InstanceId"/>.
    /// </summary>
    /// <param name="options">The host bootstrap options.</param>
    /// <returns>A 16-character lowercase hexadecimal hash.</returns>
    private static string ComputeScopeHash(BootstrapOptions options)
    {
        string scopeInput = $"{Path.GetFullPath(options.Paths.DataDir)}|{options.Node.InstanceId}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(scopeInput));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }
}
