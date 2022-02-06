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

using StreamActions.Common;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.OAuth;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StreamActions.Twitch.Api
{
    /// <summary>
    /// Handles validation of OAuth tokens and performs HTTP calls for the API.
    /// </summary>
    public static class TwitchApi
    {
        #region Public Events

        /// <summary>
        /// Fires when a <see cref="TwitchSession"/> has its OAuth token automatically refreshed as a result of a 401 response.
        /// </summary>
        public static event EventHandler<TwitchSession>? OnTokenRefreshed;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// The currently initialized Client Id.
        /// </summary>
        public static string? ClientId { get; private set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Initializes the HTTP client.
        /// </summary>
        /// <param name="clientId">A valid Twitch App Client Id.</param>
        /// <param name="clientSecret">A valid Twitch App Client Secret.</param>
        /// <param name="baseAddress">The base uri to Helix.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clientId"/> is null or whitespace.</exception>
        public static void Init(string clientId, string? clientSecret = null, string baseAddress = "https://api.twitch.tv/helix/")
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            ClientId = clientId;
            ClientSecret = clientSecret;

            _ = _httpClient.DefaultRequestHeaders.Remove("Client-Id");
            _ = _httpClient.DefaultRequestHeaders.Remove("User-Agent");

            _httpClient.DefaultRequestHeaders.Add("Client-Id", clientId);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StreamActions/TwitchAPI/" + typeof(TwitchApi).Assembly.GetName()?.Version?.ToString() ?? "0.0.0.0");
            _httpClient.BaseAddress = new Uri(baseAddress);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        #endregion Public Methods

        #region Internal Properties

        /// <summary>
        /// The currently initialized Client Secret.
        /// </summary>
        internal static string? ClientSecret { get; private set; }

        #endregion Internal Properties

        #region Internal Methods

        /// <summary>
        /// Submits a HTTP request to the specified Uri and returns the response.
        /// </summary>
        /// <remarks>
        /// Non-JSON responses are converted to the standard <c>{status,error,message}</c> format.
        ///
        /// If a 401 is returned, one attempt will be made to refresh the OAuth token and try again.
        /// The <paramref name="session"/> object is updated and <see cref="OnTokenRefreshed"/> is called on refresh success.
        /// </remarks>
        /// <param name="method">The <see cref="HttpMethod"/> of the request.</param>
        /// <param name="uri">The Uri to request. Relative Uris resolve against the Helix base.</param>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="content">The body of the request, for methods that require it.</param>
        /// <returns>A <see cref="HttpResponseMessage"/> containing the response data.</returns>
        /// <exception cref="InvalidOperationException">Did not call <see cref="Init(string)"/> with a valid Client Id or <paramref name="session"/> does not have an OAuth token.</exception>
        internal static async Task<HttpResponseMessage> PerformHttpRequest(HttpMethod method, Uri uri, TwitchSession session, HttpContent? content = null)
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("Client-Id"))
            {
                throw new InvalidOperationException("Must call TwitchAPI.Init.");
            }

            if (string.IsNullOrWhiteSpace(session.Token?.OAuth))
            {
                throw new InvalidOperationException("Invalid OAuth token in session.");
            }

            bool retry = false;
        performhttprequest_start:
            try
            {
                await session.RateLimiter.WaitForRateLimit(TimeSpan.FromSeconds(30)).ConfigureAwait(false);

                using HttpRequestMessage request = new(method, uri) { Version = _httpClient.DefaultRequestVersion, VersionPolicy = _httpClient.DefaultVersionPolicy };
                request.Content = content;

                if (session.Token.OAuth != "__NEW")
                {
                    request.Headers.Add("Authorization", "Bearer " + session.Token.OAuth);
                }

                HttpResponseMessage response = await _httpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

                if (!retry && response.StatusCode == System.Net.HttpStatusCode.Unauthorized && session.Token.OAuth != "__NEW" && !string.IsNullOrWhiteSpace(session.Token.Refresh))
                {
                    Token? refresh = await Token.RefreshOAuth(session).ConfigureAwait(false);

                    if (refresh is not null && refresh.IsSuccessStatusCode)
                    {
                        session.Token = new() { OAuth = refresh.AccessToken, Refresh = refresh.RefreshToken, Expires = refresh.Expires };
                        _ = (OnTokenRefreshed?.InvokeAsync(null, session));
                        retry = true;
                        goto performhttprequest_start;
                    }
                }

                if (response.Content.Headers?.ContentType?.MediaType != "application/json")
                {
                    string rcontent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!Regex.IsMatch(rcontent, @"\s*{.*}\s*"))
                    {
                        response.Content = new StringContent(JsonSerializer.Serialize(new TwitchResponse { Status = (int)response.StatusCode, Message = rcontent }, new JsonSerializerOptions()));
                    }
                }

                session.RateLimiter.ParseHeaders(response.Headers);

                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
            {
                return new(0) { ReasonPhrase = ex.GetType().Name, Content = new StringContent(ex.Message) };
            }
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
