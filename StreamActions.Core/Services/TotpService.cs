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

using StreamActions.Core.Abstractions;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace StreamActions.Core.Services;

/// <summary>
/// Pure-.NET implementation of <see cref="ITotpService"/> providing RFC 6238 HMAC-SHA1 TOTP and SHA-256 recovery codes.
/// </summary>
public sealed class TotpService : ITotpService
{
    /// <summary>
    /// RFC 4648 Base32 alphabet.
    /// </summary>
    private const string _base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Unambiguous 32-character alphabet used for human-readable one-time recovery codes.
    /// </summary>
    private const string _recoveryCodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>
    /// Number of TOTP digits per code.
    /// </summary>
    private const int _totpDigits = 6;

    /// <summary>
    /// Modulo divisor corresponding to 6-digit TOTP codes.
    /// </summary>
    private const int _totpModulo = 1_000_000;

    /// <summary>
    /// Standard RFC 6238 time step in seconds.
    /// </summary>
    private const long _timeStepSeconds = 30;

    /// <summary>
    /// The time provider used to obtain the current UTC time during verification.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TotpService"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider used to evaluate TOTP windows.</param>
    public TotpService(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public string GenerateSecretBase32(int numBytes = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(numBytes, 0);

        byte[] secretBytes = RandomNumberGenerator.GetBytes(numBytes);
        try
        {
            return EncodeBase32(secretBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }

    /// <inheritdoc/>
    public Uri BuildOtpAuthUri(string issuer, string accountName, string secretBase32)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretBase32);

        string trimmedIssuer = issuer.Trim();
        string trimmedAccount = accountName.Trim();
        string normalizedSecret = NormalizeBase32(secretBase32);

        string label = $"{Uri.EscapeDataString(trimmedIssuer)}:{Uri.EscapeDataString(trimmedAccount)}";
        return new($"otpauth://totp/{label}?secret={Uri.EscapeDataString(normalizedSecret)}&issuer={Uri.EscapeDataString(trimmedIssuer)}&algorithm=SHA1&digits={_totpDigits}&period={_timeStepSeconds}");
    }

    /// <inheritdoc/>
    public string ComputeCode(string secretBase32, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretBase32);

        byte[] keyBytes = DecodeBase32(secretBase32);
        try
        {
            long unixSeconds = timestamp.ToUnixTimeSeconds();
            long counter = unixSeconds >= 0
                ? unixSeconds / _timeStepSeconds
                : (unixSeconds - (_timeStepSeconds - 1)) / _timeStepSeconds;

            return ComputeCodeFromCounter(keyBytes, counter);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    /// <inheritdoc/>
    public bool VerifyCode(string secretBase32, string code, int allowedStepDrift = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(allowedStepDrift);

        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string trimmedCode = code.Trim();
        if (trimmedCode.Length != _totpDigits)
        {
            return false;
        }

        for (int i = 0; i < trimmedCode.Length; i++)
        {
            if (!char.IsAsciiDigit(trimmedCode[i]))
            {
                return false;
            }
        }

        byte[] keyBytes;
        try
        {
            keyBytes = DecodeBase32(secretBase32);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            long unixSeconds = now.ToUnixTimeSeconds();
            long baseCounter = unixSeconds >= 0
                ? unixSeconds / _timeStepSeconds
                : (unixSeconds - (_timeStepSeconds - 1)) / _timeStepSeconds;

            Span<byte> submittedBytes = stackalloc byte[_totpDigits];
            _ = Encoding.ASCII.GetBytes(trimmedCode, submittedBytes);

            Span<byte> candidateBytes = stackalloc byte[_totpDigits];
            bool matched = false;

            for (int delta = -allowedStepDrift; delta <= allowedStepDrift; delta++)
            {
                string expected = ComputeCodeFromCounter(keyBytes, baseCounter + delta);
                _ = Encoding.ASCII.GetBytes(expected, candidateBytes);
                if (CryptographicOperations.FixedTimeEquals(candidateBytes, submittedBytes))
                {
                    matched = true;
                }
            }

            return matched;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GenerateRecoveryCodes(int count = 10)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);

        List<string> codes = new(count);
        HashSet<string> seen = new(StringComparer.Ordinal);
        Span<byte> randomBytes = stackalloc byte[10];
        Span<char> chars = stackalloc char[11];

        while (codes.Count < count)
        {
            RandomNumberGenerator.Fill(randomBytes);
            for (int i = 0; i < 5; i++)
            {
                chars[i] = _recoveryCodeAlphabet[randomBytes[i] & 0x1F];
            }

            chars[5] = '-';
            for (int i = 0; i < 5; i++)
            {
                chars[6 + i] = _recoveryCodeAlphabet[randomBytes[5 + i] & 0x1F];
            }

            string code = new(chars);
            if (seen.Add(code))
            {
                codes.Add(code);
            }
        }

        return codes;
    }

    /// <inheritdoc/>
    public string HashRecoveryCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        string normalized = NormalizeRecoveryCode(code);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(digest);
    }

    /// <inheritdoc/>
    public bool VerifyRecoveryCode(string code, string expectedHashHex)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(expectedHashHex))
        {
            return false;
        }

        string normalizedCode = NormalizeRecoveryCode(code);
        if (normalizedCode.Length == 0)
        {
            return false;
        }

        string actualHashHex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode)));
        string normalizedExpected = expectedHashHex.Trim().ToUpperInvariant();

        byte[] actualBytes = Encoding.ASCII.GetBytes(actualHashHex);
        byte[] expectedBytes = Encoding.ASCII.GetBytes(normalizedExpected);
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    /// <summary>
    /// Computes the 6-digit RFC 6238 code for a given key and counter value.
    /// </summary>
    /// <param name="keyBytes">The decoded HMAC-SHA1 secret bytes.</param>
    /// <param name="counter">The 30-second time step counter.</param>
    /// <returns>A zero-padded 6-digit TOTP code.</returns>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "RFC 6238 TOTP authenticator app compatibility requires HMAC-SHA1.")]
    private static string ComputeCodeFromCounter(ReadOnlySpan<byte> keyBytes, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        Span<byte> hash = stackalloc byte[20];
        _ = HMACSHA1.HashData(keyBytes, counterBytes, hash);

        int offset = hash[hash.Length - 1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % _totpModulo;
        return otp.ToString("D6", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Normalizes a recovery code by removing whitespace and hyphens and uppercasing using invariant culture.
    /// </summary>
    /// <param name="code">The raw recovery code.</param>
    /// <returns>The normalized recovery code string.</returns>
    private static string NormalizeRecoveryCode(string code)
    {
        StringBuilder sb = new(code.Length);
        foreach (char ch in code)
        {
            if (ch != '-' && !char.IsWhiteSpace(ch))
            {
                _ = sb.Append(char.ToUpperInvariant(ch));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Normalizes a Base32 secret string by removing whitespace and padding and uppercasing using invariant culture.
    /// </summary>
    /// <param name="secretBase32">The raw Base32 secret.</param>
    /// <returns>The normalized unpadded uppercase Base32 string.</returns>
    private static string NormalizeBase32(string secretBase32)
    {
        StringBuilder sb = new(secretBase32.Length);
        foreach (char ch in secretBase32)
        {
            if (ch != '=' && !char.IsWhiteSpace(ch))
            {
                _ = sb.Append(char.ToUpperInvariant(ch));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Encodes a byte span to an unpadded uppercase RFC 4648 Base32 string.
    /// </summary>
    /// <param name="data">The input bytes.</param>
    /// <returns>The Base32-encoded string.</returns>
    private static string EncodeBase32(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        int charCount = ((data.Length * 8) + 4) / 5;
        StringBuilder sb = new(charCount);

        int buffer = data[0];
        int next = 1;
        int bitsLeft = 8;

        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer <<= 8;
                    buffer |= data[next++] & 0xFF;
                    bitsLeft += 8;
                }
                else
                {
                    int pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }

            int index = (buffer >> (bitsLeft - 5)) & 0x1F;
            bitsLeft -= 5;
            _ = sb.Append(_base32Alphabet[index]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Decodes an RFC 4648 Base32 string into raw bytes.
    /// </summary>
    /// <param name="secretBase32">The Base32-encoded secret string.</param>
    /// <returns>The decoded key bytes.</returns>
    /// <exception cref="FormatException">Thrown if <paramref name="secretBase32"/> contains invalid Base32 characters.</exception>
    private static byte[] DecodeBase32(string secretBase32)
    {
        string normalized = NormalizeBase32(secretBase32);
        if (normalized.Length == 0)
        {
            throw new FormatException("Base32 secret must not be empty.");
        }

        int byteCount = normalized.Length * 5 / 8;
        byte[] result = new byte[byteCount];

        int buffer = 0;
        int bitsLeft = 0;
        int count = 0;

        foreach (char ch in normalized)
        {
            int val = ch switch
            {
                >= 'A' and <= 'Z' => ch - 'A',
                >= '2' and <= '7' => 26 + (ch - '2'),
                _ => throw new FormatException($"Invalid Base32 character '{ch}'."),
            };

            buffer <<= 5;
            buffer |= val & 0x1F;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                result[count++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                bitsLeft -= 8;
            }
        }

        return result;
    }
}
