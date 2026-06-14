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

using System.Collections.Specialized;

namespace StreamActions.Common.Tests;

/// <summary>
/// Performs unit testing on methods in <see cref="Util"/>.
/// </summary>
public class UtilTests
{
    #region IsNullOrDefault

    [Theory]
    [Trait("Member", "IsNullOrDefault")]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("hello", false)]
    public void IsNullOrDefault_WithString_ReturnsCorrectBoolean(string? argument, bool expected) => Util.IsNullOrDefault(argument).Should().Be(expected);

    [Theory]
    [Trait("Member", "IsNullOrDefault")]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    public void IsNullOrDefault_WithInt_ReturnsCorrectBoolean(int argument, bool expected) => Util.IsNullOrDefault(argument).Should().Be(expected);

    [Theory]
    [Trait("Member", "IsNullOrDefault")]
    [MemberData(nameof(ClassTestData))]
    public void IsNullOrDefault_WithClass_ReturnsCorrectBoolean(object? argument, bool expected) => Util.IsNullOrDefault(argument).Should().Be(expected);

    public static IEnumerable<object?[]> ClassTestData =>
    [
        [null, true],
        [new object(), false]
    ];

    [Theory]
    [Trait("Member", "IsNullOrDefault")]
    [MemberData(nameof(StructTestData))]
    public void IsNullOrDefault_WithStruct_ReturnsCorrectBoolean(Guid argument, bool expected) => Util.IsNullOrDefault(argument).Should().Be(expected);

    public static IEnumerable<object[]> StructTestData =>
    [
        [default(Guid), true],
        [Guid.NewGuid(), false]
    ];

    [Theory]
    [Trait("Member", "IsNullOrDefault")]
    [InlineData(null, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void IsNullOrDefault_WithNullableValueType_ReturnsCorrectBoolean(int? argument, bool expected) => Util.IsNullOrDefault(argument).Should().Be(expected);

    [Theory]
    [Trait("Member", "IsNullOrDefault")]
    [MemberData(nameof(ObjectBoxingTestData))]
    public void IsNullOrDefault_WithObjectBoxingValueType_ReturnsCorrectBoolean(object argument, bool expected) => Util.IsNullOrDefault(argument).Should().Be(expected);

    public static IEnumerable<object[]> ObjectBoxingTestData =>
    [
        // IsNullOrDefault has special logic for boxed value types
        // It checks the underlying runtime type and compares to its default instance
        [0, true],
        [1, false]
    ];

    #endregion IsNullOrDefault

    #region IsValidHexColor

    [Theory]
    [Trait("Member", "IsValidHexColor")]
    [InlineData("#000000", true)]
    [InlineData("#123456", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("#FF5733", true)]
    [InlineData("#FFFFFF", true)]
    [InlineData("#aBcDeF", true)]
    [InlineData("", false)]
    [InlineData("#FF573", false)]
    [InlineData("#FF57333", false)]
    [InlineData("#F", false)]
    [InlineData("#FF", false)]
    [InlineData("#FFF", false)]
    [InlineData("#FFFF", false)]
    [InlineData("#FFFFF", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("FF5733", false)]
    [InlineData("The hex color is #FF5733 in the middle of this line.", false)]
    [InlineData("#FF5733 is at the beginning of this line.", false)]
    [InlineData("This line ends with #FF5733.", false)]
    [InlineData(null, false)]
    public void IsValidHexColor_ReturnsCorrectBoolean(string? hexColor, bool expected) => Util.IsValidHexColor(hexColor).Should().Be(expected);

    #endregion IsValidHexColor

    #region DurationStringToTimeSpan

    [Theory]
    [Trait("Member", "DurationStringToTimeSpan")]
    [InlineData("1w2d3h4m5s", "9.03:04:05")]
    [InlineData("1w", "7.00:00:00")]
    [InlineData("2d", "2.00:00:00")]
    [InlineData("3h", "03:00:00")]
    [InlineData("30m", "00:30:00")]
    [InlineData("100s", "00:01:40")]
    [InlineData("0m", "00:00:00")]
    [InlineData("1d1h1m1s", "1.01:01:01")]
    public void DurationStringToTimeSpan_ReturnsExpectedResult(string duration, string expectedTimeSpanString)
    {
        // Arrange
        TimeSpan expected = TimeSpan.Parse(expectedTimeSpanString, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        TimeSpan result = Util.DurationStringToTimeSpan(duration);

        // Assert
        _ = result.Should().Be(expected);
    }

    [Theory]
    [Trait("Member", "DurationStringToTimeSpan")]
    [InlineData("1k")]
    [InlineData("hello")]
    [InlineData("1234")]
    [InlineData("")]
    public void DurationStringToTimeSpan_WithInvalidString_ThrowsArgumentOutOfRangeException(string duration)
    {
        // Act
        Action act = () => Util.DurationStringToTimeSpan(duration);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("duration");
    }

    [Fact]
    [Trait("Member", "DurationStringToTimeSpan")]
    public void DurationStringToTimeSpan_WithUnknownCaptureGroup_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        System.Text.RegularExpressions.Regex regex = new("(?<unknown>1)");
        System.Text.RegularExpressions.Match match = regex.Match("1");
        System.Reflection.MethodInfo? method = typeof(Util).GetMethod("ParseDurationMatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        // Act
        Action act = () =>
        {
            try
            {
                _ = method!.Invoke(null, [match, "1"]);
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                throw ex.InnerException!;
            }
        };

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("duration");
    }

    #endregion DurationStringToTimeSpan

    #region HexColorToColor

    [Fact]
    [Trait("Member", "HexColorToColor")]
    public void HexColorToColor_WithNull_ThrowsArgumentNullException()
    {
        // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        Action act = () => Util.HexColorToColor(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Assert
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [Trait("Member", "HexColorToColor")]
    [InlineData("#000000", 0, 0, 0)]
    [InlineData("#123456", 18, 52, 86)]
    [InlineData("#ABCDEF", 171, 205, 239)]
    [InlineData("#FF5733", 255, 87, 51)]
    [InlineData("#FFFFFF", 255, 255, 255)]
    [InlineData("#aBcDeF", 171, 205, 239)]
    public void HexColorToColor_ValidHexColor_ReturnsCorrectColor(string hexColor, int r, int g, int b)
    {
        System.Drawing.Color expected = System.Drawing.Color.FromArgb(r, g, b);
        Util.HexColorToColor(hexColor).Should().Be(expected);
    }

    [Theory]
    [Trait("Member", "HexColorToColor")]
    [InlineData("")]
    [InlineData("#FF573")]
    [InlineData("#FF57333")]
    [InlineData("#F")]
    [InlineData("#FF")]
    [InlineData("#FFF")]
    [InlineData("#FFFF")]
    [InlineData("#FFFFF")]
    [InlineData("#GGGGGG")]
    [InlineData("FF5733")]
    [InlineData("The hex color is #FF5733 in the middle of this line.")]
    [InlineData("#FF5733 is at the beginning of this line.")]
    [InlineData("This line ends with #FF5733.")]
    public void HexColorToColor_InvalidHexColor_ReturnsEmptyColor(string hexColor)
    {
        Util.HexColorToColor(hexColor).Should().Be(System.Drawing.Color.Empty);
    }

    [Fact]
    [Trait("Member", "HexColorToColor")]
    public void HexColorToColor_WithNonHexCharacters_DoesNotThrowFormatException()
    {
        // Arrange
        string invalidHexColor = "#XXYYZZ";

        // Act
        Action act = () => Util.HexColorToColor(invalidHexColor);

        // Assert
        _ = act.Should().NotThrow<FormatException>();
        Util.HexColorToColor(invalidHexColor).Should().Be(System.Drawing.Color.Empty);
    }

    #endregion HexColorToColor

    #region BuildUri

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithNullBaseUri_ThrowsArgumentNullException()
    {
        // Arrange
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        Uri baseUri = null;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

        // Act
#pragma warning disable CS8604 // Possible null reference argument.
        Action act = () => Util.BuildUri(baseUri);
#pragma warning restore CS8604 // Possible null reference argument.

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("baseUri");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithQueryParams_EscapesAndAppendsQueryParams()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection queryParams = new()
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

        // Act
        Uri result = Util.BuildUri(baseUri, queryParams);

        // Assert
        _ = result.Query.Should().Be("?key1=value1&key2=value2");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithFragmentParams_EscapesAndAppendsFragmentParams()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection fragmentParams = new()
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

        // Act
        Uri result = Util.BuildUri(baseUri, null, fragmentParams);

        // Assert
        _ = result.Fragment.Should().Be("#key1=value1&key2=value2");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithNullValueInQueryParams_AppendsKeyWithoutValue()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection queryParams = new()
            {
                { "key1", null }
            };

        // Act
        Uri result = Util.BuildUri(baseUri, queryParams);

        // Assert
        _ = result.Query.Should().Be("?key1");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithUrlEncodingInQueryParams_EncodesValues()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection queryParams = new()
            {
                { "key1", "value 1" }
            };

        // Act
        Uri result = Util.BuildUri(baseUri, queryParams);

        // Assert
        _ = result.Query.Should().Be("?key1=value%201");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithSingleQueryParam_AppendsSingleQueryParam()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection queryParams = new()
            {
                { "key1", "value1" }
            };

        // Act
        Uri result = Util.BuildUri(baseUri, queryParams);

        // Assert
        _ = result.Query.Should().Be("?key1=value1");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithBaseUriOnly_ReturnsBaseUri()
    {
        // Arrange
        Uri baseUri = new("https://example.com");

        // Act
        Uri result = Util.BuildUri(baseUri);

        // Assert
        _ = result.Should().Be(baseUri);
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithAllParameters_ReturnsExpectedResult()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection queryParams = new()
             {
                 { "key1", "value 1" },
                 { "key2", null }
             };
        NameValueCollection fragmentParams = new()
             {
                 { "key3", "value3" },
                 { "key4", null }
             };

        // Act
        Uri result = Util.BuildUri(baseUri, queryParams, fragmentParams);

        // Assert
        _ = result.Query.Should().Be("?key1=value%201&key2");
        _ = result.Fragment.Should().Be("#key3=value3&key4");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithBaseUriContainingQueryParam_AppendsAdditionalQueryParams()
    {
        // Arrange
        Uri baseUri = new("https://example.com?existingKey=existingValue");
        NameValueCollection queryParams = new()
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

        // Act
        Uri result = Util.BuildUri(baseUri, queryParams);

        // Assert
        _ = result.Query.Should().Be("?existingKey=existingValue&key1=value1&key2=value2");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithBaseUriContainingFragmentParam_AppendsAdditionalFragmentParams()
    {
        // Arrange
        Uri baseUri = new("https://example.com#existingKey=existingValue");
        NameValueCollection fragmentParams = new()
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

        // Act
        Uri result = Util.BuildUri(baseUri, null, fragmentParams);

        // Assert
        _ = result.Fragment.Should().Be("#existingKey=existingValue&key1=value1&key2=value2");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithMultipleValuesForKeyInQueryParams_AppendsAllValuesForKey()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection queryParams = new()
        {
            { "key1", "value1" },
            { "key1", "value2" }
        };

        // Act
        Uri result = Util.BuildUri(baseUri, queryParams);

        // Assert
        _ = result.Query.Should().Be("?key1=value1&key1=value2");
    }

    [Fact]
    [Trait("Member", "BuildUri")]
    public void BuildUri_WithMultipleValuesForKeyInFragmentParams_AppendsAllValuesForKey()
    {
        // Arrange
        Uri baseUri = new("https://example.com");
        NameValueCollection fragmentParams = new()
        {
            { "key1", "value1" },
            { "key1", "value2" }
        };

        // Act
        Uri result = Util.BuildUri(baseUri, null, fragmentParams);

        // Assert
        _ = result.Fragment.Should().Be("#key1=value1&key1=value2");
    }

    #endregion BuildUri

    #region GetDefault

    [Fact]
    [Trait("Member", "GetDefault")]
    public void GetDefault_WithNullArgument_ReturnsNull()
    {
        // Act
        object? result = Util.GetDefault<string>(null!);

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetDefault")]
    public void GetDefault_WithReferenceType_ReturnsNull()
    {
        // Act
        object? result = Util.GetDefault("test");

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetDefault")]
    public void GetDefault_WithValueTypeSameMethodType_ReturnsDefault()
    {
        // Act
        object? result = Util.GetDefault(5);

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetDefault")]
    public void GetDefault_WithValueTypeDifferentMethodType_ReturnsDefaultOfArgumentType()
    {
        // Act
        object? result = Util.GetDefault<object>(5);

        // Assert
        _ = result.Should().Be(0);
        _ = result.Should().BeOfType<int>();
    }

    private struct TestStruct
    {
        public int Value;
    }

    [Fact]
    [Trait("Member", "GetDefault")]
    public void GetDefault_WithCustomStruct_ReturnsDefaultStruct()
    {
        // Arrange
        TestStruct testStruct = new() { Value = 5 };

        // Act
        object? result = Util.GetDefault(testStruct);

        // Assert
        _ = result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetDefault")]
    public void GetDefault_WithCustomStructDifferentMethodType_ReturnsDefaultStruct()
    {
        // Arrange
        TestStruct testStruct = new() { Value = 5 };

        // Act
        object? result = Util.GetDefault<object>(testStruct);

        // Assert
        _ = result.Should().BeOfType<TestStruct>();
        _ = ((TestStruct)result!).Value.Should().Be(0);
    }

    #endregion GetDefault
}
