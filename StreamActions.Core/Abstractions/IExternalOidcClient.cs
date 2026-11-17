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
using StreamActions.Core.Models.Entities;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Executes OpenID Connect discovery, PKCE authorization requests, and ID token validation for <see cref="ExternalOidcProvider"/> accounts.
/// </summary>
public interface IExternalOidcClient
{
    /// <summary>
    /// Discovers OIDC endpoints and builds an authorization request with PKCE (<c>S256</c>), state, and nonce.
    /// </summary>
    /// <param name="provider">The external OIDC provider configuration.</param>
    /// <param name="redirectUri">The local callback redirect URI.</param>
    /// <param name="state">Optional explicit state token; generated cryptographically when omitted.</param>
    /// <param name="nonce">Optional explicit nonce token; generated cryptographically when omitted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The constructed <see cref="ExternalOidcAuthorizationRequest"/>.</returns>
    ValueTask<ExternalOidcAuthorizationRequest> BuildAuthorizationRequestAsync(
        ExternalOidcProvider provider,
        Uri redirectUri,
        string? state = null,
        string? nonce = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges an authorization code using PKCE, validates the returned ID token, and extracts identity and group claims.
    /// </summary>
    /// <param name="provider">The external OIDC provider configuration.</param>
    /// <param name="clientSecret">The decrypted OIDC client secret.</param>
    /// <param name="code">The authorization code returned by the provider.</param>
    /// <param name="redirectUri">The redirect URI used in the authorization request.</param>
    /// <param name="codeVerifier">The PKCE code verifier generated for the authorization request.</param>
    /// <param name="expectedNonce">The expected OIDC nonce bound to the authorization request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validated <see cref="ExternalOidcAuthenticationResult"/>.</returns>
    ValueTask<ExternalOidcAuthenticationResult> ExchangeCodeAsync(
        ExternalOidcProvider provider,
        string clientSecret,
        string code,
        Uri redirectUri,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken = default);
}
