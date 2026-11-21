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

using StreamActions.Core.Models;

namespace StreamActions.Core.Http;

/// <summary>
/// Wraps a readable response stream and aborts immediately once more than the configured maximum number of decompressed bytes are read.
/// </summary>
public sealed class BoundedReadStream : Stream
{
    /// <summary>
    /// Default maximum permitted decompressed response size (64 KiB).
    /// </summary>
    public const int DefaultMaxBytes = 64 * 1024;

    /// <summary>
    /// The underlying response stream.
    /// </summary>
    private readonly Stream _innerStream;

    /// <summary>
    /// The maximum number of bytes permitted before aborting.
    /// </summary>
    private readonly int _maxBytes;

    /// <summary>
    /// Whether this wrapper should dispose <see cref="_innerStream"/> when disposed.
    /// </summary>
    private readonly bool _leaveOpen;

    /// <summary>
    /// Total number of bytes read so far.
    /// </summary>
    private int _bytesRead;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedReadStream"/> class.
    /// </summary>
    /// <param name="innerStream">The underlying readable stream.</param>
    /// <param name="maxBytes">The maximum number of bytes allowed before aborting (defaults to <see cref="DefaultMaxBytes"/>).</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="innerStream"/> open when this stream is disposed.</param>
    public BoundedReadStream(
        Stream innerStream,
        int maxBytes = DefaultMaxBytes,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxBytes, 0);

        this._innerStream = innerStream;
        this._maxBytes = maxBytes;
        this._leaveOpen = leaveOpen;
    }

    /// <inheritdoc/>
    public override bool CanRead => this._innerStream.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => this._bytesRead;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return this.Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        int remaining = this._maxBytes - this._bytesRead;
        int toRead = Math.Min(buffer.Length, remaining + 1);
        int read = this._innerStream.Read(buffer[..toRead]);
        if (read == 0)
        {
            return 0;
        }

        if (read > remaining)
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.ResponseTooLarge,
                $"Decompressed response payload exceeded the maximum allowed limit of {this._maxBytes} bytes.");
        }

        this._bytesRead += read;
        return read;
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        Span<byte> oneByte = stackalloc byte[1];
        int read = this.Read(oneByte);
        return read == 0 ? -1 : oneByte[0];
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        int remaining = this._maxBytes - this._bytesRead;
        int toRead = Math.Min(buffer.Length, remaining + 1);
        int read = await this._innerStream.ReadAsync(buffer[..toRead], cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return 0;
        }

        if (read > remaining)
        {
            throw new SafeExternalHttpException(
                SafeExternalHttpFailureReason.ResponseTooLarge,
                $"Decompressed response payload exceeded the maximum allowed limit of {this._maxBytes} bytes.");
        }

        this._bytesRead += read;
        return read;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !this._leaveOpen)
        {
            this._innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!this._leaveOpen)
        {
            await this._innerStream.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}

