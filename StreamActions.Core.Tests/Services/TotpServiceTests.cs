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

using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TotpService"/>.
/// </summary>
public sealed class TotpServiceTests
{
    /// <summary>
    /// RFC 6238 Appendix B ASCII test secret <c>12345678901234567890</c> encoded in Base32.
    /// </summary>
    private const string _rfc6238Sha1SecretBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    #region GenerateSecretBase32

    /// <summary>
    /// Verifies that <see cref="TotpService.GenerateSecretBase32"/> returns an uppercase unpadded Base32 secret of the expected length.
    /// </summary>
    [Fact]
    [Trait("Member", "GenerateSecretBase32")]
    public void GenerateSecretBase32WithDefaultLengthReturns32UppercaseBase32Characters()
    {
        FakeTimeProvider timeProvider = new();
        TotpService service = new(timeProvider);

        string secretA = service.GenerateSecretBase32();
        string secretB = service.GenerateSecretBase32();

        _ = secretA.Should().HaveLength(32);
        _ = secretA.Should().MatchRegex("^[A-Z2-7]+$");
        _ = secretB.Should().HaveLength(32);
        _ = secretA.Should().NotBe(secretB);
    }

    /// <summary>
    /// Verifies that <see cref="TotpService.GenerateSecretBase32"/> rejects non-positive byte counts.
    /// </summary>
    [Theory]
    [Trait("Member", "GenerateSecretBase32")]
    [InlineData(0)]
    [InlineData(-5)]
    public void GenerateSecretBase32WithNonPositiveBytesThrowsArgumentOutOfRangeException(int numBytes)
    {
        TotpService service = new(new FakeTimeProvider());

        Action act = () => _ = service.GenerateSecretBase32(numBytes);

        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region BuildOtpAuthUri

    /// <summary>
    /// Verifies that <see cref="TotpService.BuildOtpAuthUri"/> produces a properly escaped <c>otpauth://totp/</c> URI.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildOtpAuthUri")]
    public void BuildOtpAuthUriWithValidInputsReturnsFormattedOtpAuthUri()
    {
        TotpService service = new(new FakeTimeProvider());

        Uri uri = service.BuildOtpAuthUri("Stream Actions", "alice@example.com", " gezd gnbv= ");

        _ = uri.Scheme.Should().Be("otpauth");
        _ = uri.Host.Should().Be("totp");
        _ = uri.AbsolutePath.Should().Be("/Stream%20Actions:alice%40example.com");
        _ = uri.Query.Should().Contain("secret=GEZDGNBV");
        _ = uri.Query.Should().Contain("issuer=Stream%20Actions");
        _ = uri.Query.Should().Contain("algorithm=SHA1");
        _ = uri.Query.Should().Contain("digits=6");
        _ = uri.Query.Should().Contain("period=30");
    }

    #endregion

    #region ComputeCode

    /// <summary>
    /// Verifies that <see cref="TotpService.ComputeCode"/> matches RFC 6238 Appendix B HMAC-SHA1 test vectors.
    /// </summary>
    /// <param name="unixSeconds">The Unix epoch timestamp in seconds.</param>
    /// <param name="expectedCode">The expected 6-digit TOTP code.</param>
    [Theory]
    [Trait("Member", "ComputeCode")]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void ComputeCodeWithRfc6238TestVectorsReturnsExpectedSixDigitCode(long unixSeconds, string expectedCode)
    {
        TotpService service = new(new FakeTimeProvider());
        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        string code = service.ComputeCode(_rfc6238Sha1SecretBase32, timestamp);

        _ = code.Should().Be(expectedCode);
    }

    #endregion

    #region VerifyCode

    /// <summary>
    /// Verifies that <see cref="TotpService.VerifyCode"/> accepts codes within plus or minus one 30-second step and rejects codes at plus or minus two steps.
    /// </summary>
    /// <param name="stepOffset">The number of 30-second steps offset from the current time.</param>
    /// <param name="expectedResult">Whether verification is expected to succeed.</param>
    [Theory]
    [Trait("Member", "VerifyCode")]
    [InlineData(-2, false)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void VerifyCodeWithStepDriftRespectsOneStepToleranceWindow(int stepOffset, bool expectedResult)
    {
        DateTimeOffset now = new(2026, 6, 1, 12, 0, 15, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(now);
        TotpService service = new(timeProvider);

        string codeAtStep = service.ComputeCode(_rfc6238Sha1SecretBase32, now.AddSeconds(stepOffset * 30));
        bool verified = service.VerifyCode(_rfc6238Sha1SecretBase32, codeAtStep);

        _ = verified.Should().Be(expectedResult);
    }

    /// <summary>
    /// Verifies that <see cref="TotpService.VerifyCode"/> returns <see langword="false"/> for invalid codes or secrets.
    /// </summary>
    [Theory]
    [Trait("Member", "VerifyCode")]
    [InlineData(_rfc6238Sha1SecretBase32, "")]
    [InlineData(_rfc6238Sha1SecretBase32, "12345")]
    [InlineData(_rfc6238Sha1SecretBase32, "1234567")]
    [InlineData(_rfc6238Sha1SecretBase32, "12ab56")]
    [InlineData("INVALID!SECRET", "123456")]
    public void VerifyCodeWithMalformedInputReturnsFalse(string secret, string code)
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 15, TimeSpan.Zero));
        TotpService service = new(timeProvider);

        bool verified = service.VerifyCode(secret, code);

        _ = verified.Should().BeFalse();
    }

    #endregion

    #region GenerateRecoveryCodes

    /// <summary>
    /// Verifies that <see cref="TotpService.GenerateRecoveryCodes"/> produces 10 unique formatted recovery codes by default.
    /// </summary>
    [Fact]
    [Trait("Member", "GenerateRecoveryCodes")]
    public void GenerateRecoveryCodesWithDefaultCountReturnsTenUniqueFormattedCodes()
    {
        TotpService service = new(new FakeTimeProvider());

        IReadOnlyList<string> codes = service.GenerateRecoveryCodes();

        _ = codes.Should().HaveCount(10);
        _ = codes.Should().OnlyHaveUniqueItems();
        _ = codes.Should().AllSatisfy(static c => c.Should().MatchRegex("^[2-9A-HJ-NP-Z]{5}-[2-9A-HJ-NP-Z]{5}$"));
    }

    #endregion

    #region VerifyRecoveryCode

    /// <summary>
    /// Verifies that <see cref="TotpService.VerifyRecoveryCode"/> matches hashes case-insensitively and regardless of hyphens or whitespace.
    /// </summary>
    [Fact]
    [Trait("Member", "VerifyRecoveryCode")]
    public void VerifyRecoveryCodeIsCaseAndHyphenInsensitive()
    {
        TotpService service = new(new FakeTimeProvider());
        IReadOnlyList<string> codes = service.GenerateRecoveryCodes();
        string code = codes[0];
        string hash = service.HashRecoveryCode(code);
        string stripped = code.Replace("-", string.Empty, StringComparison.Ordinal);
        string compactLowercase = $" {string.Concat(stripped.Select(char.ToLowerInvariant))} ";

        _ = service.VerifyRecoveryCode(code, hash).Should().BeTrue();
        _ = service.VerifyRecoveryCode(compactLowercase, hash).Should().BeTrue();
        _ = service.VerifyRecoveryCode("WRONG-CODE1", hash).Should().BeFalse();
        _ = service.VerifyRecoveryCode(string.Empty, hash).Should().BeFalse();
    }

    #endregion
}
