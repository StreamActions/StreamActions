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

using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using StreamActions.Common.Json.Serialization;
using Xunit;

namespace StreamActions.Common.Tests.Json.Serialization;

/// <summary>
/// Performs unit testing on methods in <see cref="JsonUpperCaseEnumConverter{T}"/>.
/// </summary>
public class JsonUpperCaseEnumConverterTests
{
    public enum TestEnumValue
    {
        FirstValue,
        SecondValue
    }

    private class TestClass
    {
        [JsonConverter(typeof(JsonUpperCaseEnumConverter<TestEnumValue>))]
        public TestEnumValue EnumValue { get; set; }
    }

    #region HandleNull

    [Fact]
    [Trait("Member", "HandleNull")]
    public void HandleNull_ReturnsTrue()
    {
        // Arrange
        JsonUpperCaseEnumConverter<TestEnumValue> converter = new();

        // Act & Assert
        converter.HandleNull.Should().BeTrue();
    }

    #endregion HandleNull

    #region Write

    [Theory]
    [Trait("Member", "Write")]
    [InlineData(TestEnumValue.FirstValue, "{\"EnumValue\":\"FIRSTVALUE\"}")]
    [InlineData(TestEnumValue.SecondValue, "{\"EnumValue\":\"SECONDVALUE\"}")]
    public void Write_WithValidEnum_WritesUpperCasedString(TestEnumValue enumValue, string expectedJson)
    {
        // Arrange
        TestClass testObject = new() { EnumValue = enumValue };

        // Act
        string json = JsonSerializer.Serialize(testObject);

        // Assert
        json.Should().Be(expectedJson);
    }

    #endregion Write

    #region Read

    [Theory]
    [Trait("Member", "Read")]
    [InlineData("{\"EnumValue\":\"FIRSTVALUE\"}", TestEnumValue.FirstValue)]
    [InlineData("{\"EnumValue\":\"SECONDVALUE\"}", TestEnumValue.SecondValue)]
    public void Read_WithValidUpperCasedString_ReturnsCorrectEnum(string json, TestEnumValue expectedEnum)
    {
        // Act
        TestClass? result = JsonSerializer.Deserialize<TestClass>(json);

        // Assert
        result.Should().NotBeNull();
        result!.EnumValue.Should().Be(expectedEnum);
    }

    [Theory]
    [Trait("Member", "Read")]
    [InlineData("{\"EnumValue\":\"FirstValue\"}", TestEnumValue.FirstValue)]
    [InlineData("{\"EnumValue\":\"secondvalue\"}", TestEnumValue.SecondValue)]
    public void Read_WithValidMixedCaseString_ReturnsCorrectEnum(string json, TestEnumValue expectedEnum)
    {
        // Act
        TestClass? result = JsonSerializer.Deserialize<TestClass>(json);

        // Assert
        result.Should().NotBeNull();
        result!.EnumValue.Should().Be(expectedEnum);
    }

    [Theory]
    [Trait("Member", "Read")]
    [InlineData("{\"EnumValue\":\"INVALID\"}")]
    [InlineData("{\"EnumValue\":\"\"}")]
    public void Read_WithInvalidString_ReturnsDefaultEnum(string json)
    {
        // Act
        TestClass? result = JsonSerializer.Deserialize<TestClass>(json);

        // Assert
        result.Should().NotBeNull();
        result!.EnumValue.Should().Be(default(TestEnumValue));
    }

    #endregion Read
}
