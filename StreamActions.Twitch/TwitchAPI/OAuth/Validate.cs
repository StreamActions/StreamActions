/*
 * Copyright © 2019-2021 StreamActions Team
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

using StreamActions.Twitch.TwitchAPI.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.TwitchAPI.OAuth
{
    /// <summary>
    /// Record containing response data for the Validate endpoint.
    /// </summary>
    public record Validate : TwitchResponse
    {
        /// <summary>
        /// The returned Client Id attached to the validated OAuth token.
        /// </summary>
        [JsonPropertyName("client_id")]
        public string? ClientId { get; init; }

        /// <summary>
        /// The approximate DateTime that <see cref="ExpiresIn"/> refers to.
        /// </summary>
        [JsonIgnore]
        public DateTime Expires => DateTime.UtcNow.AddSeconds(this.ExpiresIn);

        /// <summary>
        /// The number of seconds, in server time, until the OAuth token expires.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        /// <summary>
        /// The login name associated with the OAuth token.
        /// </summary>
        [JsonPropertyName("login")]
        public string? Login { get; init; }

        /// <summary>
        /// The scopes that are authorized under the OAuth token.
        /// </summary>
        [JsonPropertyName("scopes")]
        public IReadOnlyCollection<string>? Scopes { get; init; }

        /// <summary>
        /// The User Id associated with the OAuth token.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }

        /// <summary>
        /// Validates the OAuth token assigned to the specified <see cref="TwitchSession"/>.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to validate.</param>
        /// <returns>A <see cref="Validate"/> with the current token data or a Twitch error.</returns>
        /// <exception cref="JsonException">The response is not valid JSON.</exception>
        public static async Task<Validate?> ValidateOAuth(TwitchSession session)
        {
            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Get, new Uri("https://id.twitch.tv/oauth2/validate"), session);
            return await response.Content.ReadFromJsonAsync<Validate>();
        }
    }
}
