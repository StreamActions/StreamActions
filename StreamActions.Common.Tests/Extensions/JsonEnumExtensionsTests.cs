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

using System;
using System.Text.Json.Serialization;
using FluentAssertions;
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;
using Xunit;

namespace StreamActions.Common.Tests.Extensions;

/// <summary>
/// Performs unit testing on methods in <see cref="JsonEnumExtensions"/>.
/// </summary>
public class JsonEnumExtensionsTests
{
    public enum NoAttr
    {
        FirstValue,
        SecondValue
    }

    [JsonConverter(typeof(JsonUpperCaseEnumConverter<UpperCase>))]
    public enum UpperCase
    {
        FirstValue,
        SecondValue
    }

    [JsonConverter(typeof(JsonLowerCaseEnumConverter<LowerCase>))]
    public enum LowerCase
    {
        FirstValue,
        SecondValue
    }

    [JsonConverter(typeof(JsonCustomEnumConverter<Custom>))]
    public enum Custom
    {
        [JsonCustomEnum("custom-first")]
        FirstValue,

        [JsonCustomEnum("custom-second")]
        SecondValue,

        MissingAttributeValue
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OtherConverter
    {
        FirstValue
    }

    [JsonConverter(typeof(JsonCustomEnumConverter<CustomInvalid>))]
    public enum CustomInvalid
    {
        // Missing JsonCustomEnum
        FirstValue,
    }

    [JsonConverter(typeof(JsonCustomEnumConverter<CustomInvalid5>))]
    public enum CustomInvalid5
    {
        [JsonCustomEnum(null!)]
        FirstValue
    }

    [JsonConverter(typeof(JsonConverterAttribute))]
    public enum CustomInvalid4
    {
        FirstValue
    }

    #region JsonValue

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithNoAttribute_ReturnsDefaultName()
    {
        // Arrange
        NoAttr val = NoAttr.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("FirstValue");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithUpperCaseAttribute_ReturnsUpperCaseString()
    {
        // Arrange
        UpperCase val = UpperCase.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("FIRSTVALUE");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithLowerCaseAttribute_ReturnsLowerCaseString()
    {
        // Arrange
        LowerCase val = LowerCase.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("firstvalue");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithCustomAttribute_ReturnsCustomString()
    {
        // Arrange
        Custom val = Custom.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("custom-first");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithCustomAttributeMissingOnValue_ReturnsDefaultName()
    {
        // Arrange
        Custom val = Custom.MissingAttributeValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("MissingAttributeValue");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithOtherConverterAttribute_ReturnsDefaultName()
    {
        // Arrange
        OtherConverter val = OtherConverter.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("FirstValue");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithUpperCaseAttributeAndInvalidValue_ReturnsNull()
    {
        // Arrange
        UpperCase val = (UpperCase)999;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithLowerCaseAttributeAndInvalidValue_ReturnsNull()
    {
        // Arrange
        LowerCase val = (LowerCase)999;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithCustomAttributeAndInvalidValue_ReturnsNull()
    {
        // Arrange
        Custom val = (Custom)999;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithCustomAttributeMissingOnEnumMember_ReturnsDefaultName()
    {
        // Arrange
        CustomInvalid val = CustomInvalid.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("FirstValue");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithCustomAttributeNullValue_ReturnsNull()
    {
        // Arrange
        CustomInvalid5 val = CustomInvalid5.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithConverterTypeNull_ReturnsDefaultName()
    {
        // Arrange
        CustomInvalid4 val = CustomInvalid4.FirstValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("FirstValue");
    }

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithInvalidValue_ReturnsNull()
    {
        // Arrange
        NoAttr val = (NoAttr)999;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().BeNull();
    }

    #endregion JsonValue
}
