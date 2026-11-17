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

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace StreamActions.Core.Models;

/// <summary>
/// Represents a generated OpenID Connect authorization request including PKCE and anti-forgery state.
/// </summary>
/// <param name="AuthorizationUri">The external provider authorization URI to redirect the user to.</param>
/// <param name="State">The CSRF protection state value.</param>
/// <param name="Nonce">The OIDC replay-protection nonce value.</param>
/// <param name="CodeVerifier">The PKCE code verifier to supply during the authorization code exchange.</param>
/// <param name="RedirectUri">The callback redirect URI bound to the authorization request.</param>
public sealed record ExternalOidcAuthorizationRequest(
    Uri AuthorizationUri,
    string State,
    string Nonce,
    string CodeVerifier,
    Uri RedirectUri)
{
    /// <summary>
    /// Formats record members while redacting the PKCE code verifier.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.AuthorizationUri)).Append(" = ").Append(this.AuthorizationUri);
        _ = builder.Append(", ").Append(nameof(this.State)).Append(" = ").Append(this.State);
        _ = builder.Append(", ").Append(nameof(this.Nonce)).Append(" = ").Append(this.Nonce);
        _ = builder.Append(", ").Append(nameof(this.CodeVerifier)).Append(" = ").Append(string.IsNullOrEmpty(this.CodeVerifier) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.RedirectUri)).Append(" = ").Append(this.RedirectUri);
        return true;
    }
}
