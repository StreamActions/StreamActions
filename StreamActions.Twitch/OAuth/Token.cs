﻿/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Common;
using StreamActions.Common.Attributes;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// Record containing response data for the Token endpoint.
/// </summary>
[ETag("Refresh OAuth", 82,
    "https://dev.twitch.tv/docs/authentication/refresh-tokens", "983bd1581e310444688acbb2ea1205fc26ce7ee4ab6801d3cb97dc2bafbff121",
    "2022-10-16T20:40Z", "TwitchRefreshParser", [])]
[ETag("Revoke OAuth", 82,
    "https://dev.twitch.tv/docs/authentication/revoke-tokens", "36c9e9126bb4e99710352235c034c12a751192af8b009d9df491cf87d580de6c",
    "2022-10-16T20:44Z", "TwitchRevokeParser", [])]
public sealed record Token : JsonApiResponse
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
    public IReadOnlyList<Scope>? Scopes { get; init; }

    /// <summary>
    /// The OIDC JWT token string, if authorizing with the <see cref="Scope.OpenID"/> scope.
    /// </summary>
    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    /// <summary>
    /// The OIDC JWT token, if authorizing with the <see cref="Scope.OpenID"/> scope.
    /// </summary>
    [JsonIgnore]
    public JsonWebToken? JwtToken => this.GetJsonWebTokenAsync();

    /// <summary>
    /// The type of token that <see cref="AccessToken"/> represents.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    /// <summary>
    /// Attempts to refresh the OAuth token assigned to the specified <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to refresh.</param>
    /// <param name="baseAddress">The uri to the Token endpoint. <see langword="null"/> for default.</param>
    /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="session"/> does not have a valid refresh token.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static async Task<Token?> RefreshOAuth(TwitchSession session, string? baseAddress = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        if (string.IsNullOrWhiteSpace(session.Token?.Refresh))
        {
            throw new InvalidOperationException(nameof(TwitchToken.Refresh) + " is not a valid refresh token").Log(TwitchApi.GetLogger());
        }

        baseAddress ??= _openIdConnectConfiguration.Value.TokenEndpoint;

        using StringContent content = new("");
        Uri uri = Util.BuildUri(new(baseAddress), new() { { "grant_type", "refresh_token" }, { "refresh_token", session.Token.Refresh ?? "" },
            { "client_id", TwitchApi.ClientId ?? "" }, { "client_secret", TwitchApi.ClientSecret ?? "" } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, content, dontRefresh: true).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<Token>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to authorize a new OAuth token using an authorization code.
    /// </summary>
    /// <param name="code">The authorization code.</param>
    /// <param name="redirectUri">The redirect Uri that was used.</param>
    /// <param name="baseAddress">The uri to the Token endpoint. <see langword="null"/> for default.</param>
    /// <returns>A <see cref="Token"/> with the new token data or a Twitch error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/> or whitespace, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
    public static async Task<Token?> AuthorizeOAuth(string code, Uri redirectUri, string? baseAddress = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code)).Log(TwitchApi.GetLogger());
        }

        if (redirectUri is null)
        {
            throw new ArgumentNullException(nameof(redirectUri)).Log(TwitchApi.GetLogger());
        }

        baseAddress ??= _openIdConnectConfiguration.Value.TokenEndpoint;

        using StringContent content = new("");
        Uri uri = Util.BuildUri(new(baseAddress), new() { { "grant_type", "authorization_code" }, { "code", code },
            { "client_id", TwitchApi.ClientId ?? "" }, { "client_secret", TwitchApi.ClientSecret ?? "" }, { "redirect_uri", redirectUri.ToString() } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, TwitchSession.Empty, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<Token>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Revokes the OAuth token.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to revoke.</param>
    /// <param name="baseAddress">The uri to the Revoke endpoint.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static async Task<JsonApiResponse?> RevokeOAuth(TwitchSession session, string baseAddress = "https://id.twitch.tv/oauth2/revoke")
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        using StringContent content = new("");
        Uri uri = Util.BuildUri(new(baseAddress), new() { { "client_id", TwitchApi.ClientId ?? "" }, { "token", session.Token?.OAuth ?? "" } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// The OpenID Connect configuration from Twitch.
    /// </summary>
    internal static readonly Lazy<OpenIdConnectConfiguration> _openIdConnectConfiguration = new(() => OpenIdConnectConfigurationRetriever.GetAsync("https://id.twitch.tv/oauth2/.well-known/openid-configuration", CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult());

    /// <summary>
    /// Validates and converts <see cref="IdToken"/>, if present, to a <see cref="JsonWebToken"/>.
    /// </summary>
    /// <returns>A <see cref="JsonWebToken"/> if <see cref="IdToken"/> is a valid JWT, else <see langword="null"/>.</returns>
    private JsonWebToken? GetJsonWebTokenAsync()
    {
        if (this.IdToken is not null)
        {
            Task<TokenValidationResult> t = new JsonWebTokenHandler().ValidateTokenAsync(this.IdToken, new()
            {
                IssuerSigningKeys = _openIdConnectConfiguration.Value.JsonWebKeySet.Keys,
                ValidAudience = TwitchApi.ClientId,
                ValidIssuer = _openIdConnectConfiguration.Value.Issuer
            });
            t.Wait();

            if (t.Result.IsValid)
            {
                return (JsonWebToken?)t.Result.SecurityToken;
            }
        }

        return null;
    }
}
