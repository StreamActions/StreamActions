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
}
