/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using StreamActions.Twitch.API.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.API.Ads
{
    /// <summary>
    /// Sends and represents a response element for a request to start a commercial.
    /// </summary>
    public record Commercial
    {
        /// <summary>
        /// Length of the triggered commercial.
        /// </summary>
        [JsonPropertyName("length")]
        public int? Length { get; init; }

        /// <summary>
        /// Seconds until the next commercial can be served on this channel.
        /// </summary>
        [JsonPropertyName("retry_after")]
        public int? RetryAfter { get; init; }

        /// <summary>
        /// Provides contextual information on why the request failed.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; init; }

        /// <summary>
        /// Starts a commercial on a specified channel.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="parameters">The <see cref="StartCommercialParameters"/> with the request parameters.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Commercial"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="session"/> contains a scope list, and <c>channel:edit:commercial</c> is not present.</exception>
        public static async Task<ResponseData<Commercial>?> StartCommercial(TwitchSession session, StartCommercialParameters parameters)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!session.Token?.HasScope("channel:edit:commercial") ?? false)
            {
                throw new InvalidOperationException("Missing scope channel:edit:commercial.");
            }

            using JsonContent content = JsonContent.Create(parameters);
            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Post, new("/channels/commercial"), session, content).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<Commercial>>().ConfigureAwait(false);
        }
    }
}
