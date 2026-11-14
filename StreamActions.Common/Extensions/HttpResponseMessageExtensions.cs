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

using StreamActions.Common.Net;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace StreamActions.Common.Extensions;

/// <summary>
/// Contains extension members to <see cref="HttpContent"/>.
/// </summary>
public static class HttpResponseMessageExtensions
{
    #region Public Methods

    /// <summary>
    /// Reads the HTTP content and returns the value that results from deserializing the content as JSON in an asynchronous operation. Injects the response code if needed.
    /// </summary>
    /// <param name="message">The response message to read from.</param>
    /// <param name="options">Options to control the behavior during deserialization.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public static async Task<T?> ReadFromJsonAsync<T>(this HttpResponseMessage message, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default) where T : JsonApiResponse, new()
    {
        ArgumentNullException.ThrowIfNull(message);

        cancellationToken.ThrowIfCancellationRequested();

        T? response = null;

        if (message.Content != null)
        {
            if (message.Content.Headers.ContentLength == 0)
            {
                response = new T() { Status = message.StatusCode };
            }
            else
            {
                Stream stream = await message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    byte[] firstByteBuffer = new byte[1];
                    bool hasContent = false;

                    while (await stream.ReadAsync(firstByteBuffer, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        if (firstByteBuffer[0] is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
                        {
                            hasContent = true;
                            break;
                        }
                    }

                    if (hasContent)
                    {
                        PrefixedStream prefixedStream = new(firstByteBuffer[0], stream);
                        await using (prefixedStream.ConfigureAwait(false))
                        {
                            response = await JsonSerializer.DeserializeAsync<T>(prefixedStream, options, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        response = new T() { Status = message.StatusCode };
                    }
                }
            }
        }

        response ??= new T() { Status = message.StatusCode };

        if (response.Status != message.StatusCode)
        {
            response.SetStatus(message.StatusCode);
        }

        return response;
    }

    #endregion Public Methods

    #region Private Types

    /// <summary>
    /// A read-only stream wrapper that prepends a single already-read byte before delegating to an inner stream.
    /// </summary>
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by ReadFromJsonAsync.")]
    private sealed class PrefixedStream(byte firstByte, Stream inner) : Stream
    {
        private bool _firstByteRead;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (count == 0)
            {
                return 0;
            }

            if (!this._firstByteRead)
            {
                buffer[offset] = firstByte;
                this._firstByteRead = true;
                return 1 + (count > 1 ? inner.Read(buffer, offset + 1, count - 1) : 0);
            }

            return inner.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            if (!this._firstByteRead)
            {
                buffer.Span[0] = firstByte;
                this._firstByteRead = true;
                return buffer.Length > 1
                    ? 1 + await inner.ReadAsync(buffer[1..], cancellationToken).ConfigureAwait(false)
                    : 1;
            }

            return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    #endregion Private Types
}
