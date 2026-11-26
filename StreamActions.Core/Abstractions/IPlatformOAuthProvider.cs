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

using StreamActions.Core.Models;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides OAuth 2.0 and OpenID Connect protocol operations for a chat or streaming platform.
/// </summary>
public interface IPlatformOAuthProvider
{
    /// <summary>
    /// Gets the normalized lowercase platform identifier handled by this provider (for example, <c>twitch</c>).
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the default OAuth scopes requested for panel sign-in and identity linking flows on this platform.
    /// </summary>
    IReadOnlyList<string> DefaultLoginScopes { get; }

    /// <summary>
    /// Gets the default OAuth scopes requested for broadcaster workspace authorization flows on this platform.
    /// </summary>
    IReadOnlyList<string> DefaultBroadcasterScopes { get; }

    /// <summary>
    /// Gets the default OAuth scopes requested for bot chat account authorization flows on this platform.
    /// </summary>
    IReadOnlyList<string> DefaultBotScopes { get; }

    /// <summary>
    /// Builds an authorization URL for initiating an OAuth 2.0 or OpenID Connect authorization code flow.
    /// </summary>
    /// <param name="clientId">The OAuth application client identifier.</param>
    /// <param name="redirectUri">The registered callback redirect URI.</param>
    /// <param name="scopes">The scopes to request.</param>
    /// <param name="state">The CSRF protection state token.</param>
    /// <param name="forceVerify"><see langword="true"/> to force the user to re-approve authorization; otherwise, <see langword="false"/>.</param>
    /// <param name="nonce">An optional OIDC nonce value; when provided, requests an OIDC ID token.</param>
    /// <param name="codeChallenge">An optional RFC 7636 PKCE code challenge.</param>
    /// <param name="codeChallengeMethod">An optional RFC 7636 PKCE code challenge method (for example, <c>S256</c>).</param>
    /// <returns>The constructed authorization <see cref="Uri"/>.</returns>
    Uri BuildAuthorizationUri(
        string clientId,
        Uri redirectUri,
        IReadOnlyCollection<string>? scopes,
        string state,
        bool forceVerify = false,
        string? nonce = null,
        string? codeChallenge = null,
        string? codeChallengeMethod = null);

    /// <summary>
    /// Exchanges an authorization code for OAuth tokens and resolves the authenticated user's platform identity.
    /// </summary>
    /// <param name="clientId">The OAuth application client identifier.</param>
    /// <param name="clientSecret">The optional OAuth application client secret, or <see langword="null"/> for public clients.</param>
    /// <param name="code">The authorization code returned by the platform.</param>
    /// <param name="redirectUri">The redirect URI used when initiating the authorization request.</param>
    /// <param name="expectedNonce">The expected OIDC nonce when OpenID Connect was requested, or <see langword="null"/>.</param>
    /// <param name="codeVerifier">An optional RFC 7636 PKCE code verifier matching the challenge sent during authorization.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The exchanged <see cref="PlatformOAuthTokenResult"/>.</returns>
    ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(
        string clientId,
        string? clientSecret,
        string code,
        Uri redirectUri,
        string? expectedNonce = null,
        string? codeVerifier = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an expired or expiring OAuth access token using its refresh token.
    /// </summary>
    /// <param name="clientId">The OAuth application client identifier.</param>
    /// <param name="clientSecret">The optional OAuth application client secret, or <see langword="null"/> for public clients.</param>
    /// <param name="refreshToken">The OAuth refresh token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The refreshed <see cref="PlatformOAuthTokenResult"/>.</returns>
    ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(
        string clientId,
        string? clientSecret,
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an existing OAuth access token against the platform's introspection endpoint.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="PlatformTokenValidationResult"/> describing whether the token is valid and its metadata.</returns>
    ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an OAuth token with the platform.
    /// </summary>
    /// <param name="clientId">The OAuth application client identifier.</param>
    /// <param name="token">The access or refresh token to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous revocation operation.</returns>
    ValueTask RevokeTokenAsync(
        string clientId,
        string token,
        CancellationToken cancellationToken = default);
}
