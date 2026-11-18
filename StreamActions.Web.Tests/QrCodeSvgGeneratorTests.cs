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
using StreamActions.Web.Security;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace StreamActions.Web.Tests;

/// <summary>
/// Unit tests for <see cref="QrCodeSvgGenerator"/>.
/// </summary>
public sealed partial class QrCodeSvgGeneratorTests
{
    #region GenerateSvg

    /// <summary>
    /// Verifies that <see cref="QrCodeSvgGenerator.GenerateSvg"/> produces deterministic, valid, CSP-safe SVG markup for an <c>otpauth://</c> URI.
    /// </summary>
    [Fact]
    [Trait("Member", "GenerateSvg")]
    public void GenerateSvg_WithValidOtpAuthUri_ReturnsDeterministicValidSvg()
    {
        const string uri = "otpauth://totp/My%20Panel:streamer?secret=JBSWY3DPEHPK3PXP&issuer=My%20Panel&algorithm=SHA1&digits=6&period=30";

        string firstSvg = QrCodeSvgGenerator.GenerateSvg(uri);
        string secondSvg = QrCodeSvgGenerator.GenerateSvg(uri);

        _ = firstSvg.Should().Be(secondSvg);
        _ = firstSvg.Should().StartWith("<svg ");
        _ = firstSvg.Should().EndWith("</svg>");
        _ = firstSvg.Should().Contain("<path d=\"");
        _ = firstSvg.Should().NotContain("<script");
        _ = firstSvg.Should().NotContain("style=");

        XDocument document = XDocument.Parse(firstSvg);
        _ = document.Root.Should().NotBeNull();
        _ = document.Root.Name.LocalName.Should().Be("svg");

        string? viewBox = document.Root.Attribute("viewBox")?.Value;
        _ = viewBox.Should().NotBeNullOrWhiteSpace();
        string[] parts = viewBox.Split(' ');
        _ = parts.Should().HaveCount(4);
        int totalSize = int.Parse(parts[2], CultureInfo.InvariantCulture);
        int symbolSize = totalSize - (QrCodeSvgGenerator.DefaultQuietZoneModules * 2);
        _ = ((symbolSize - 17) % 4).Should().Be(0);
    }

    /// <summary>
    /// Verifies that custom quiet-zone sizes adjust the SVG <c>viewBox</c> and shift dark module coordinates accordingly.
    /// </summary>
    [Fact]
    [Trait("Member", "GenerateSvg")]
    public void GenerateSvg_WithCustomQuietZone_AdjustsViewBoxAndPathOffset()
    {
        const string payload = "otpauth://totp/StreamActions:user?secret=ABCDEF234567";

        string defaultSvg = QrCodeSvgGenerator.GenerateSvg(payload, quietZoneModules: 4);
        string zeroQuietSvg = QrCodeSvgGenerator.GenerateSvg(payload, quietZoneModules: 0);

        Match defaultViewBox = ViewBoxRegex().Match(defaultSvg);
        Match zeroViewBox = ViewBoxRegex().Match(zeroQuietSvg);

        _ = defaultViewBox.Success.Should().BeTrue();
        _ = zeroViewBox.Success.Should().BeTrue();

        int defaultSize = int.Parse(defaultViewBox.Groups[1].Value, CultureInfo.InvariantCulture);
        int zeroSize = int.Parse(zeroViewBox.Groups[1].Value, CultureInfo.InvariantCulture);

        _ = (defaultSize - zeroSize).Should().Be(8);
        // Top-left finder corner starts at (0,0) when quietZone=0 and (4,4) when quietZone=4.
        _ = zeroQuietSvg.Should().Contain("d=\"M0,0h7v1h-7z");
        _ = defaultSvg.Should().Contain("d=\"M4,4h7v1h-7z");
    }

    /// <summary>
    /// Verifies that payloads spanning Versions 1–12 across ECC Levels M and L (including UTF-8 characters) generate valid SVG symbols.
    /// </summary>
    /// <param name="length">The ASCII payload length in bytes.</param>
    /// <param name="expectedSymbolSize">The expected QR matrix dimension in modules.</param>
    [Theory]
    [Trait("Member", "GenerateSvg")]
    [InlineData(10, 21)]
    [InlineData(20, 25)]
    [InlineData(40, 29)]
    [InlineData(60, 33)]
    [InlineData(100, 41)]
    [InlineData(150, 49)]
    [InlineData(200, 57)]
    [InlineData(287, 65)]
    [InlineData(367, 65)]
    public void GenerateSvg_WithPayloadLengthsAcrossVersions1To12_ProducesExpectedMatrixDimensions(
        int length,
        int expectedSymbolSize)
    {
        string payload = new('A', length);

        string svg = QrCodeSvgGenerator.GenerateSvg(payload);
        XDocument document = XDocument.Parse(svg);

        string[] parts = document.Root!.Attribute("viewBox")!.Value.Split(' ');
        int totalSize = int.Parse(parts[2], CultureInfo.InvariantCulture);
        _ = (totalSize - (QrCodeSvgGenerator.DefaultQuietZoneModules * 2)).Should().Be(expectedSymbolSize);
    }

    /// <summary>
    /// Verifies that null or whitespace payloads throw <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="invalidPayload">The invalid payload string.</param>
    [Theory]
    [Trait("Member", "GenerateSvg")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateSvg_WithNullOrWhitespacePayload_ThrowsArgumentException(string? invalidPayload)
    {
        Action act = () => _ = QrCodeSvgGenerator.GenerateSvg(invalidPayload!);

        _ = act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that payloads exceeding Version 12 ECC Level L capacity (367 UTF-8 bytes) throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "GenerateSvg")]
    public void GenerateSvg_WithOversizedPayload_ThrowsArgumentException()
    {
        string oversized = new('X', 368);

        Action act = () => _ = QrCodeSvgGenerator.GenerateSvg(oversized);

        _ = act.Should().Throw<ArgumentException>()
            .WithParameterName("payload");
    }

    /// <summary>
    /// Verifies that a negative quiet-zone width throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "GenerateSvg")]
    public void GenerateSvg_WithNegativeQuietZone_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => _ = QrCodeSvgGenerator.GenerateSvg("otpauth://totp/test", quietZoneModules: -1);

        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("quietZoneModules");
    }

    /// <summary>
    /// Matches the width component of an SVG <c>viewBox="0 0 N N"</c> attribute.
    /// </summary>
    [GeneratedRegex("viewBox=\"0 0 (\\d+) \\d+\"", RegexOptions.CultureInvariant)]
    private static partial Regex ViewBoxRegex();

    #endregion
}

