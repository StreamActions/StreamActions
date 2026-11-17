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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Represents the outcome of requesting or reusing a single-use console bootstrap token.
/// </summary>
/// <param name="Succeeded">A value indicating whether an active bootstrap token was issued or reused.</param>
/// <param name="Token">The active hexadecimal bootstrap token when <paramref name="Succeeded"/> is <see langword="true"/>; otherwise, <see langword="null"/>.</param>
/// <param name="CooldownRemaining">The remaining cooldown duration when token issuance is blocked after failed attempts; otherwise, <see langword="null"/>.</param>
public sealed record BootstrapTokenRequestResult(
    bool Succeeded,
    string? Token,
    TimeSpan? CooldownRemaining);

/// <summary>
/// Manages single-instance console bootstrap tokens used to authorize initial panel setup.
/// </summary>
public interface IBootstrapTokenService
{
    /// <summary>
    /// Returns the active bootstrap token if still valid, issues a fresh token if eligible, or reports the remaining cooldown.
    /// </summary>
    /// <returns>A <see cref="BootstrapTokenRequestResult"/> describing whether a token is available or in cooldown.</returns>
    BootstrapTokenRequestResult RequestOrReuseActiveToken();

    /// <summary>
    /// Returns the currently active bootstrap token if still within its TTL, or issues a fresh token.
    /// </summary>
    /// <returns>The active hexadecimal bootstrap token.</returns>
    /// <exception cref="InvalidOperationException">Thrown when token generation is currently in cooldown after repeated failed attempts.</exception>
    string GetOrCreateToken();

    /// <summary>
    /// Validates a candidate bootstrap token using constant-time comparison.
    /// </summary>
    /// <param name="candidate">The candidate token string to validate.</param>
    /// <returns><see langword="true"/> if the token is active and matches; otherwise, <see langword="false"/>.</returns>
    bool ValidateToken(string candidate);

    /// <summary>
    /// Immediately invalidates any active bootstrap token.
    /// </summary>
    void Invalidate();
}
