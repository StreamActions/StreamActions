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

namespace StreamActions.Twitch.TwitchAPI
{
    /// <summary>
    /// Handles validation of OAuth tokens and performs HTTP calls for the API.
    /// </summary>
    public static class TwitchAPI
    {
        #region Public Methods

        /// <summary>
        /// Initializes the http client.
        /// </summary>
        /// <param name="clientId">A valid Twitch App Client ID.</param>
        public static void Init(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            _httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StreamActions/TwitchAPI/" + typeof(TwitchAPI).Assembly.GetName()?.Version?.ToString() ?? "0.0.0");
            _httpClient.BaseAddress = new Uri("https://api.twitch.tv/helix/");
        }

        #endregion Public Methods

        #region Internal Methods

        /// <summary>
        /// Submits a http requeest to the specified uri and returns the response.
        /// </summary>
        /// <param name="method">The <see cref="HttpMethod"/> of the request.</param>
        /// <param name="uri">The uri to request. Relative uris resolve against the Helix base.</param>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="content">The body of the request, for methods that require it.</param>
        /// <returns>A <see cref="HttpResponseMessage"/> containing the response data.</returns>
        /// <exception cref="InvalidOperationException">Did not call <see cref="Init(string)"/> with a valid Client Id.</exception>
        internal static async Task<HttpResponseMessage> PerformHttpRequest(HttpMethod method, Uri uri, TwitchSession session, HttpContent? content = null)
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("Client-Id"))
            {
                throw new InvalidOperationException("Must call TwitchAPI.Init.");
            }

            if (string.IsNullOrWhiteSpace(session.OAuth))
            {
                throw new InvalidOperationException("Invalid OAuth token in session.");
            }

            await session.RateLimiter.WaitForRateLimit();

            HttpRequestMessage request = new(method, uri) { Version = _httpClient.DefaultRequestVersion, VersionPolicy = _httpClient.DefaultVersionPolicy };
            request.Content = content;
            request.Headers.Add("Authorization", "Bearer " + session.OAuth);
            HttpResponseMessage response = await _httpClient.SendAsync(request, CancellationToken.None);

            session.RateLimiter.ParseHeaders(response.Headers);

            return response;
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// The <see cref="HttpClient"/> that is used for all requests.
        /// </summary>
        private static readonly HttpClient _httpClient = new();

        #endregion Private Fields
    }
}
