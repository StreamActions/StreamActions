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

namespace StreamActions.Core.Models;

/// <summary>
/// Represents the outcome of ensuring or refreshing a stored platform OAuth token.
/// </summary>
public enum TokenLifecycleStatus
{
    /// <summary>
    /// The existing stored token is active and valid without requiring a refresh.
    /// </summary>
    Valid = 0,

    /// <summary>
    /// The token was refreshed with the platform OAuth provider and persisted.
    /// </summary>
    Refreshed = 1,

    /// <summary>
    /// No credential record exists for the requested scope, platform, and token role.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// The stored credential has no refresh token available to renew an expiring or invalid access token.
    /// </summary>
    MissingRefreshToken = 3,

    /// <summary>
    /// No OAuth provider or client application configuration was available to perform a refresh.
    /// </summary>
    OAuthClientNotConfigured = 4,

    /// <summary>
    /// Refreshing the token with the platform OAuth provider failed.
    /// </summary>
    RefreshFailed = 5,

    /// <summary>
    /// The stored credential could not be decrypted with the active Data Protection key ring and requires re-authentication.
    /// </summary>
    KeyRingMismatchRequiresReauth = 6,

    /// <summary>
    /// The stored credential has been revoked and requires re-authentication.
    /// </summary>
    Revoked = 7,

    /// <summary>
    /// The stored credential has expired and cannot be used.
    /// </summary>
    Expired = 8,
}

/// <summary>
/// Represents the result of <see cref="Abstractions.ITokenLifecycleService.EnsureValidTokenAsync"/>.
/// </summary>
/// <param name="Status">The lifecycle outcome status.</param>
/// <param name="Payload">The usable token payload when available.</param>
/// <param name="CredentialStatus">The persisted credential status, if a record exists.</param>
/// <param name="WasRefreshed">A value indicating whether this call executed a token refresh.</param>
/// <param name="ErrorMessage">An optional diagnostic message describing a failure.</param>
public sealed record TokenLifecycleResult(
    TokenLifecycleStatus Status,
    PlatformTokenPayload? Payload = null,
    CredentialStatus? CredentialStatus = null,
    bool WasRefreshed = false,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Payload"/> contains a valid, active token.
    /// </summary>
    public bool IsUsable =>
        this.Status is TokenLifecycleStatus.Valid or TokenLifecycleStatus.Refreshed
        && this.Payload is not null;
}
