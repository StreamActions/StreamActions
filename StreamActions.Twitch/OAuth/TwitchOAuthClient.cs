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

using Microsoft.IdentityModel.JsonWebTokens;
using StreamActions.Common;
using StreamActions.Common.Exceptions;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logging;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using System.Collections.Specialized;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// Provides OAuth 2.0 and OpenID Connect (OIDC) client operations for Twitch authentication flows.
/// </summary>
public static class TwitchOAuthClient
{
    #region Public Methods

    /// <summary>
    /// Builds a Twitch OAuth 2.0 authorization code grant URI.
    /// </summary>
    /// <param name="clientId">The application client ID.</param>
    /// <param name="redirectUri">The registered redirect URI where the authorization response is sent.</param>
    /// <param name="scopes">Optional space-separated OAuth scopes to request.</param>
    /// <param name="state">The CSRF protection state string.</param>
    /// <param name="forceVerify"><see langword="true"/> to require the user to re-verify authorization; otherwise, <see langword="false"/>.</param>
    /// <param name="authorizeEndpoint">The Twitch authorization endpoint URI.</param>
    /// <returns>The constructed authorization <see cref="Uri"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="clientId"/>, <paramref name="state"/>, or <paramref name="authorizeEndpoint"/> is <see langword="null"/> or whitespace, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
    public static Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IEnumerable<string>? scopes, string state, bool forceVerify = false, string authorizeEndpoint = "https://id.twitch.tv/oauth2/authorize") =>
        BuildAuthorizationUriCore(clientId, redirectUri, scopes, state, forceVerify, authorizeEndpoint, nonce: null, claimsJson: null);

    /// <summary>
    /// Builds a Twitch OpenID Connect (OIDC) authorization code grant URI, ensuring the <c>openid</c> scope is included.
    /// </summary>
    /// <param name="clientId">The application client ID.</param>
    /// <param name="redirectUri">The registered redirect URI where the authorization response is sent.</param>
    /// <param name="scopes">Optional OAuth/OIDC scopes to request. <c>openid</c> is automatically included if absent.</param>
    /// <param name="state">The CSRF protection state string.</param>
    /// <param name="nonce">The OIDC nonce value used to mitigate replay attacks.</param>
    /// <param name="forceVerify"><see langword="true"/> to require the user to re-verify authorization; otherwise, <see langword="false"/>.</param>
    /// <param name="claimsJson">Optional JSON string specifying individual OIDC claims requested in the ID token or UserInfo response.</param>
    /// <param name="authorizeEndpoint">The Twitch authorization endpoint URI.</param>
    /// <returns>The constructed OIDC authorization <see cref="Uri"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="clientId"/>, <paramref name="state"/>, <paramref name="nonce"/>, or <paramref name="authorizeEndpoint"/> is <see langword="null"/> or whitespace, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
    public static Uri BuildOidcAuthorizationUri(string clientId, Uri redirectUri, IEnumerable<string>? scopes, string state, string nonce, bool forceVerify = false, string? claimsJson = null, string authorizeEndpoint = "https://id.twitch.tv/oauth2/authorize")
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            throw new ArgumentNullException(nameof(nonce)).Log(TwitchApi.GetLogger());
        }

        List<string> oidcScopes = [];
        bool hasOpenId = false;
        string openIdScope = Scope.OpenID.Name ?? "openid";

        if (scopes is not null)
        {
            foreach (string scope in scopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                if (string.Equals(scope, openIdScope, StringComparison.Ordinal))
                {
                    hasOpenId = true;
                }

                oidcScopes.Add(scope);
            }
        }

        if (!hasOpenId)
        {
            oidcScopes.Insert(0, openIdScope);
        }

        return BuildAuthorizationUriCore(clientId, redirectUri, oidcScopes, state, forceVerify, authorizeEndpoint, nonce, claimsJson);
    }

    /// <summary>
    /// Exchanges an authorization code for a user access token and refresh token.
    /// </summary>
    /// <param name="code">The authorization code received from the authorization redirect.</param>
    /// <param name="redirectUri">The redirect URI used in the authorization request.</param>
    /// <param name="session">Optional <see cref="TwitchSession"/> providing custom client credentials or HTTP overrides.</param>
    /// <param name="tokenEndpoint">The token endpoint URI, or <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <returns>A <see cref="Token"/> containing the token response or error details.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <see langword="null"/> or whitespace, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully exchanged the authorization code for tokens.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The authorization code, redirect URI, or client credentials were invalid.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static Task<Token?> ExchangeAuthorizationCodeAsync(string code, Uri redirectUri, TwitchSession? session = null, string? tokenEndpoint = null) =>
        Token.AuthorizeOAuth(code, redirectUri, session, tokenEndpoint);

    /// <summary>
    /// Requests an application access token using the OAuth 2.0 client credentials grant.
    /// </summary>
    /// <param name="session">Optional <see cref="TwitchSession"/> providing custom client credentials or HTTP overrides.</param>
    /// <param name="tokenEndpoint">The token endpoint URI, or <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <returns>A <see cref="Token"/> containing the application access token or error details.</returns>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully issued an application access token.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The client ID or client secret was missing or invalid.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static Task<Token?> RequestAppTokenAsync(TwitchSession? session = null, string? tokenEndpoint = null) =>
        Token.AuthorizeApp(tokenEndpoint, session);

    /// <summary>
    /// Refreshes the user OAuth token assigned to the specified <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> containing the refresh token.</param>
    /// <param name="tokenEndpoint">The token endpoint URI, or <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <returns>A <see cref="Token"/> containing the refreshed token data or error details.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="session"/> does not have a valid refresh token, or <see cref="TwitchSession.Token"/> / <see cref="TwitchToken.OAuth"/> is invalid.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully refreshed the user OAuth token.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The refresh token or client credentials were invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The refresh token has been revoked or expired.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static Task<Token?> RefreshTokenAsync(TwitchSession session, string? tokenEndpoint = null) =>
        Token.RefreshOAuth(session, tokenEndpoint);

    /// <summary>
    /// Validates the OAuth token assigned to the specified <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to validate.</param>
    /// <param name="validateEndpoint">The validation endpoint URI.</param>
    /// <param name="dontRefresh"><see langword="true"/> to skip automatic token refresh if the server returns 401 Unauthorized; otherwise, <see langword="false"/>.</param>
    /// <returns>A <see cref="Validate"/> containing token metadata or error details.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>The token is valid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The token is invalid or expired.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static Task<Validate?> ValidateTokenAsync(TwitchSession session, string validateEndpoint = "https://id.twitch.tv/oauth2/validate", bool dontRefresh = true) =>
        Validate.ValidateOAuth(session, dontRefresh, validateEndpoint);

    /// <summary>
    /// Revokes the user or application OAuth token assigned to the specified <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> whose token will be revoked.</param>
    /// <param name="revokeEndpoint">The revocation endpoint URI.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the HTTP response status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="revokeEndpoint"/> is <see langword="null"/> (or whitespace for <paramref name="revokeEndpoint"/>).</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>, or <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is neither <see cref="TwitchToken.TokenType.App"/> nor <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>The token was revoked successfully.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The client ID or token was missing or invalid.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> RevokeTokenAsync(TwitchSession session, string revokeEndpoint = "https://id.twitch.tv/oauth2/revoke")
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(revokeEndpoint))
        {
            throw new ArgumentNullException(nameof(revokeEndpoint)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            { "client_id", session.CustomClientId ?? TwitchApi.ClientId ?? "" },
            { "token", session.Token?.OAuth ?? "" }
        });
        using TwitchSession requestSession = Token.CreateTokenRequestSession(session);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new(revokeEndpoint), requestSession, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the OIDC ID token in the specified <see cref="Token"/> response.
    /// </summary>
    /// <param name="token">The <see cref="Token"/> containing the <see cref="Token.IdToken"/> to validate.</param>
    /// <param name="expectedAudience">The expected JWT audience (<c>aud</c> claim). Defaults to <see cref="TwitchApi.ClientId"/> when <see langword="null"/>.</param>
    /// <param name="expectedNonce">The expected <c>nonce</c> claim value, or <see langword="null"/> or whitespace to skip nonce validation.</param>
    /// <returns>A validated <see cref="JsonWebToken"/>, or <see langword="null"/> if <see cref="Token.IdToken"/> is absent or invalid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public static Task<JsonWebToken?> ValidateIdTokenAsync(Token token, string? expectedAudience = null, string? expectedNonce = null) => token is null
            ? throw new ArgumentNullException(nameof(token)).Log(TwitchApi.GetLogger())
            : token.GetJwtTokenAsync(expectedAudience, expectedNonce);

    /// <summary>
    /// Retrieves OpenID Connect UserInfo claims for the user token assigned to the specified <see cref="TwitchSession"/>.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> authorized with the <see cref="Scope.OpenID"/> scope.</param>
    /// <param name="userInfoEndpoint">The UserInfo endpoint URI, or <see langword="null"/> to read from OpenID Connect configuration.</param>
    /// <returns>A <see cref="UserInfo"/> containing the requested user claims.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>, or <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the <see cref="Scope.OpenID"/> scope.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the UserInfo claims.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The access token is invalid or lacks the <c>openid</c> scope.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static Task<UserInfo?> GetUserInfoAsync(TwitchSession session, string? userInfoEndpoint = null) =>
        UserInfo.GetUserInfo(session, userInfoEndpoint);

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Builds an OAuth 2.0 or OIDC authorization URI with optional <c>nonce</c> and <c>claims</c> parameters.
    /// </summary>
    /// <param name="clientId">The application client ID.</param>
    /// <param name="redirectUri">The registered redirect URI.</param>
    /// <param name="scopes">Optional scopes to request.</param>
    /// <param name="state">The CSRF protection state string.</param>
    /// <param name="forceVerify"><see langword="true"/> to include <c>force_verify=true</c>; otherwise, omit it.</param>
    /// <param name="authorizeEndpoint">The authorization endpoint URI.</param>
    /// <param name="nonce">Optional OIDC nonce parameter.</param>
    /// <param name="claimsJson">Optional OIDC claims JSON parameter.</param>
    /// <returns>The constructed authorization <see cref="Uri"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="clientId"/>, <paramref name="state"/>, or <paramref name="authorizeEndpoint"/> is <see langword="null"/> or whitespace, or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
    private static Uri BuildAuthorizationUriCore(string clientId, Uri redirectUri, IEnumerable<string>? scopes, string state, bool forceVerify, string authorizeEndpoint, string? nonce, string? claimsJson)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentNullException(nameof(clientId)).Log(TwitchApi.GetLogger());
        }

        if (redirectUri is null)
        {
            throw new ArgumentNullException(nameof(redirectUri)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentNullException(nameof(state)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(authorizeEndpoint))
        {
            throw new ArgumentNullException(nameof(authorizeEndpoint)).Log(TwitchApi.GetLogger());
        }

        NameValueCollection queryParameters = new()
        {
            { "response_type", "code" },
            { "client_id", clientId },
            { "redirect_uri", redirectUri.ToString() }
        };

        List<string> scopeList = [];
        if (scopes is not null)
        {
            foreach (string scope in scopes)
            {
                if (!string.IsNullOrWhiteSpace(scope))
                {
                    scopeList.Add(scope);
                }
            }
        }

        if (scopeList.Count > 0)
        {
            queryParameters.Add("scope", string.Join(' ', scopeList));
        }

        queryParameters.Add("state", state);

        if (forceVerify)
        {
            queryParameters.Add("force_verify", "true");
        }

        if (!string.IsNullOrWhiteSpace(nonce))
        {
            queryParameters.Add("nonce", nonce);
        }

        if (!string.IsNullOrWhiteSpace(claimsJson))
        {
            queryParameters.Add("claims", claimsJson);
        }

        return Util.BuildUri(new(authorizeEndpoint), queryParameters);
    }

    #endregion Private Methods
}
