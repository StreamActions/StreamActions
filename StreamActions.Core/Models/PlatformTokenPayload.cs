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
/// Represents the decrypted JSON payload stored inside <see cref="Entities.PlatformCredential.EncryptedTokenJson"/>.
/// </summary>
/// <param name="AccessToken">The OAuth access token.</param>
/// <param name="RefreshToken">The optional OAuth refresh token.</param>
/// <param name="ExpiresAtUtc">The optional UTC expiration timestamp of the access token.</param>
/// <param name="Scopes">The scopes authorized for the token.</param>
/// <param name="TokenType">The OAuth token type (for example, <c>bearer</c>).</param>
/// <param name="PlatformUserId">The optional platform user identifier associated with the token.</param>
/// <param name="PlatformUsername">The optional platform username associated with the token.</param>
/// <param name="ClientId">The optional OAuth application client identifier used to issue the token.</param>
public sealed record PlatformTokenPayload(
    string AccessToken,
    string? RefreshToken = null,
    DateTimeOffset? ExpiresAtUtc = null,
    IReadOnlyList<string>? Scopes = null,
    string? TokenType = null,
    string? PlatformUserId = null,
    string? PlatformUsername = null,
    string? ClientId = null)
{
    /// <summary>
    /// Formats record members while redacting sensitive token values.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to append to.</param>
    /// <returns><see langword="true"/>.</returns>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by compiler-generated record ToString().")]
    private bool PrintMembers(StringBuilder builder)
    {
        _ = builder.Append(nameof(this.AccessToken)).Append(" = ").Append(string.IsNullOrEmpty(this.AccessToken) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.RefreshToken)).Append(" = ").Append(string.IsNullOrEmpty(this.RefreshToken) ? "[EMPTY]" : "[REDACTED]");
        _ = builder.Append(", ").Append(nameof(this.ExpiresAtUtc)).Append(" = ").Append(this.ExpiresAtUtc);
        _ = builder.Append(", ").Append(nameof(this.TokenType)).Append(" = ").Append(this.TokenType);
        _ = builder.Append(", ").Append(nameof(this.PlatformUserId)).Append(" = ").Append(this.PlatformUserId);
        _ = builder.Append(", ").Append(nameof(this.PlatformUsername)).Append(" = ").Append(this.PlatformUsername);
        _ = builder.Append(", ").Append(nameof(this.ClientId)).Append(" = ").Append(this.ClientId);
        return true;
    }
}
