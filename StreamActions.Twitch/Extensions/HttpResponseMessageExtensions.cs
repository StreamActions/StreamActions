/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Extensions;
using System.Net.Http.Json;
using System.Text.Json;

namespace StreamActions.Twitch.Extensions;

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
    public static async Task<T?> ReadFromJsonAsync<T>(this HttpResponseMessage message, JsonSerializerOptions? options, CancellationToken cancellationToken = default) where T : TwitchResponse
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        T? response = await message.Content.ReadFromJsonAsync<T>(options, cancellationToken).ConfigureAwait(false);

        response ??= (T)new TwitchResponse() { Status = message.StatusCode };

        if (response.Status != message.StatusCode)
        {
            response.Status = message.StatusCode;
        }

        return response;
    }

    #endregion Public Methods
}
