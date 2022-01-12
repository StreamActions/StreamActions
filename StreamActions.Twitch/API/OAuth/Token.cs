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

namespace StreamActions.Twitch.API.OAuth
{
    /// <summary>
    /// Record containing response data for the Token endpoint.
    /// </summary>
    public record Token : TwitchResponse
    {
        /// <summary>
        /// The OAuth token that has been granted.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        /// <summary>
        /// The refresh token associated with the <see cref="AccessToken"/>.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

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
        /// The scopes that are authorized under the OAuth token.
        /// </summary>
        [JsonPropertyName("scope")]
        public IReadOnlyCollection<string>? Scopes { get; init; }

        /// <summary>
        /// The type of token that <see cref="AccessToken"/> represents.
        /// </summary>
        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }

        /// <summary>
        /// Attempts to refresh the OAuth token assigned to the specified <see cref="TwitchSession"/>.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to refresh.</param>
        /// <param name="baseAddress">The uri to the Token endpoint.</param>
        /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
        /// <exception cref="JsonException">The response is not valid JSON.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
        public static async Task<Token?> RefreshOAuth(TwitchSession session, string baseAddress = "https://id.twitch.tv/oauth2/token")
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Get, new Uri(baseAddress + "?grant_type=refresh_token&refresh_token="
                + session.Token?.Refresh + "&client_id=" + TwitchAPI.ClientId + "&client_secret=" + TwitchAPI.ClientSecret), session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<Token>().ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to authorize a new OAuth token using an authorization code.
        /// </summary>
        /// <param name="code">The authorization code.</param>
        /// <param name="redirectUri">The redirect Uri that was used.</param>
        /// <param name="baseAddress">The uri to the Token endpoint.</param>
        /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
        /// <exception cref="JsonException">The response is not valid JSON.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="code"/> is null or whitespace, or <paramref name="redirectUri"/> is null.</exception>
        public static async Task<Token?> AuthorizeOAuth(string code, Uri redirectUri, string baseAddress = "https://id.twitch.tv/oauth2/token")
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (redirectUri is null)
            {
                throw new ArgumentNullException(nameof(redirectUri));
            }

            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Get, new Uri(baseAddress + "?grant_type=authorization_code&code="
                + code + "&client_id=" + TwitchAPI.ClientId + "&client_secret=" + TwitchAPI.ClientSecret + "&redirect_uri="
                + Uri.EscapeDataString(redirectUri.ToString())), new() { RateLimiter = new(1, 1), Token = new() { OAuth = "__NEW" } }).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<Token>().ConfigureAwait(false);
        }
    }
}
