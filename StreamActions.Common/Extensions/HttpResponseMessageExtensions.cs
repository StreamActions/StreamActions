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
                string content = await message.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                response = string.IsNullOrWhiteSpace(content) ? new T() { Status = message.StatusCode } : JsonSerializer.Deserialize<T>(content, options);
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
}
