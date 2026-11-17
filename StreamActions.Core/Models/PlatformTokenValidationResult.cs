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
/// Represents the result of validating an OAuth access token with a platform's token introspection endpoint.
/// </summary>
/// <param name="IsValid">A value indicating whether the token is currently valid.</param>
/// <param name="ClientId">The OAuth application client identifier associated with the token.</param>
/// <param name="PlatformUserId">The platform-specific user identifier associated with the token.</param>
/// <param name="PlatformUsername">The platform login username associated with the token.</param>
/// <param name="Scopes">The scopes authorized for the token.</param>
/// <param name="ExpiresAtUtc">The optional UTC expiration timestamp of the token.</param>
public sealed record PlatformTokenValidationResult(
    bool IsValid,
    string? ClientId = null,
    string? PlatformUserId = null,
    string? PlatformUsername = null,
    IReadOnlyList<string>? Scopes = null,
    DateTimeOffset? ExpiresAtUtc = null);
