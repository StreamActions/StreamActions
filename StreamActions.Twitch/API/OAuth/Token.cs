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

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Common;
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
        /// The OIDC JWT token string, if authorizing with the <c>openid</c> scope.
        /// </summary>
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        /// <summary>
        /// The OIDC JWT token, if authorizing with the <c>openid</c> scope.
        /// </summary>
        [JsonIgnore]
        public JsonWebToken? JwtToken => this.GetJsonWebToken();

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
        public static async Task<Token?> RefreshOAuth(TwitchSession session, string? baseAddress = null)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (baseAddress is null)
            {
                baseAddress = _openIdConnectConfiguration.Value.TokenEndpoint;
            }

            using StringContent content = new("");
            Uri uri = Util.BuildUri(new(baseAddress), new Dictionary<string, IEnumerable<string>> { { "grant_type", new List<string>() { "refresh_token" } },
                { "refresh_token", new List<string>() { session.Token?.Refresh ?? "" } }, { "client_id", new List<string>() { TwitchAPI.ClientId ?? "" } },
                { "client_secret", new List<string>() { TwitchAPI.ClientSecret ?? "" } } });
            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Post, uri, session, content).ConfigureAwait(false);
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
        public static async Task<Token?> AuthorizeOAuth(string code, Uri redirectUri, string? baseAddress = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (redirectUri is null)
            {
                throw new ArgumentNullException(nameof(redirectUri));
            }

            if (baseAddress is null)
            {
                baseAddress = _openIdConnectConfiguration.Value.TokenEndpoint;
            }

            using StringContent content = new("");
            Uri uri = Util.BuildUri(new(baseAddress), new Dictionary<string, IEnumerable<string>> { { "grant_type", new List<string>() { "authorization_code" } },
                { "code", new List<string>() { code } }, { "client_id", new List<string>() { TwitchAPI.ClientId ?? "" } },
                { "client_secret", new List<string>() { TwitchAPI.ClientSecret ?? "" } }, { "redirect_uri", new List<string>() { redirectUri.ToString() } } });
            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Post, uri, new() { RateLimiter = new(1, 1), Token = new() { OAuth = "__NEW" } }, content).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<Token>().ConfigureAwait(false);
        }

        /// <summary>
        /// Revokes the OAuth token.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to revoke.</param>
        /// <param name="baseAddress">The uri to the Revoke endpoint.</param>
        /// <returns>A <see cref="TwitchResponse"/> with the response code.</returns>
        /// <exception cref="JsonException">The response is not valid JSON.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
        public static async Task<TwitchResponse?> RevokeOAuth(TwitchSession session, string baseAddress = "https://id.twitch.tv/oauth2/revoke")
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            using StringContent content = new("");
            Uri uri = Util.BuildUri(new(baseAddress), new Dictionary<string, IEnumerable<string>> { { "client_id", new List<string>() { TwitchAPI.ClientId ?? "" } },
                { "token", new List<string>() { session.Token?.OAuth ?? "" } } });
            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Post, uri, session, content).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<TwitchResponse>().ConfigureAwait(false);
        }

        /// <summary>
        /// The OpenID Connect configuration from Twitch.
        /// </summary>
        internal static readonly Lazy<OpenIdConnectConfiguration> _openIdConnectConfiguration = new(() => OpenIdConnectConfigurationRetriever.GetAsync("https://id.twitch.tv/oauth2/.well-known/openid-configuration", CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult());

        /// <summary>
        /// Validates and converts <see cref="IdToken"/>, if present, to a <see cref="JsonWebToken"/>.
        /// </summary>
        /// <returns>A <see cref="JsonWebToken"/> if <see cref="IdToken"/> is a valid JWT, else <c>null</c>.</returns>
        private JsonWebToken? GetJsonWebToken()
        {
            if (this.IdToken is not null)
            {
                TokenValidationResult validationResult = new JsonWebTokenHandler().ValidateToken(this.IdToken, new()
                {
                    IssuerSigningKeys = _openIdConnectConfiguration.Value.JsonWebKeySet.Keys,
                    ValidAudience = TwitchAPI.ClientId,
                    ValidIssuer = _openIdConnectConfiguration.Value.Issuer
                });

                if (validationResult.IsValid)
                {
                    return (JsonWebToken?)validationResult.SecurityToken;
                }
            }

            return null;
        }
    }
}
