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
/// Provides RFC 6238 HMAC-SHA1 TOTP secret generation, code computation, constant-time verification, and one-time recovery code helpers.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generates a cryptographically random RFC 4648 Base32-encoded secret key.
    /// </summary>
    /// <param name="numBytes">The number of random key bytes to generate (default 20 bytes / 160 bits).</param>
    /// <returns>An uppercase unpadded Base32-encoded secret string.</returns>
    string GenerateSecretBase32(int numBytes = 20);

    /// <summary>
    /// Builds an <c>otpauth://totp/...</c> provisioning URI compatible with standard authenticator apps.
    /// </summary>
    /// <param name="issuer">The issuer display name (for example, <c>StreamActions</c>).</param>
    /// <param name="accountName">The user account label or display name.</param>
    /// <param name="secretBase32">The Base32-encoded TOTP secret.</param>
    /// <returns>The formatted <c>otpauth://</c> provisioning <see cref="Uri"/>.</returns>
    Uri BuildOtpAuthUri(string issuer, string accountName, string secretBase32);

    /// <summary>
    /// Computes the 6-digit RFC 6238 HMAC-SHA1 TOTP code for the specified secret and UTC timestamp using a 30-second step.
    /// </summary>
    /// <param name="secretBase32">The Base32-encoded shared secret.</param>
    /// <param name="timestamp">The timestamp at which to compute the code.</param>
    /// <returns>A zero-padded 6-digit TOTP code.</returns>
    string ComputeCode(string secretBase32, DateTimeOffset timestamp);

    /// <summary>
    /// Verifies a user-supplied 6-digit TOTP code against the shared secret across <c>+/- allowedStepDrift</c> 30-second steps using constant-time comparison.
    /// </summary>
    /// <param name="secretBase32">The Base32-encoded shared secret.</param>
    /// <param name="code">The candidate 6-digit code.</param>
    /// <param name="allowedStepDrift">The maximum number of 30-second steps before or after the current time to accept (default 1).</param>
    /// <returns><see langword="true"/> if the code matches any step within the drift window; otherwise, <see langword="false"/>.</returns>
    bool VerifyCode(string secretBase32, string code, int allowedStepDrift = 1);

    /// <summary>
    /// Generates a set of unique, cryptographically random one-time recovery codes.
    /// </summary>
    /// <param name="count">The number of recovery codes to generate (default 10).</param>
    /// <returns>A read-only list of plaintext recovery codes.</returns>
    IReadOnlyList<string> GenerateRecoveryCodes(int count = 10);

    /// <summary>
    /// Computes the normalized lowercase SHA-256 hex digest of a recovery code for persistence.
    /// </summary>
    /// <param name="code">The plaintext recovery code.</param>
    /// <returns>The lowercase SHA-256 hexadecimal hash.</returns>
    string HashRecoveryCode(string code);

    /// <summary>
    /// Verifies a plaintext recovery code against a stored SHA-256 hexadecimal hash in constant time.
    /// </summary>
    /// <param name="code">The candidate recovery code.</param>
    /// <param name="expectedHashHex">The stored lowercase SHA-256 hexadecimal hash.</param>
    /// <returns><see langword="true"/> if the code matches the stored hash; otherwise, <see langword="false"/>.</returns>
    bool VerifyRecoveryCode(string code, string expectedHashHex);
}
