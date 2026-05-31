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
    public enum NoAttributeEnum
    {
        FirstValue,
        SecondValue
    }

    [JsonConverter(typeof(JsonUpperCaseEnumConverter<UpperCaseEnum>))]
    public enum UpperCaseEnum
    {
        FirstValue,
        SecondValue
    }

    [JsonConverter(typeof(JsonLowerCaseEnumConverter<LowerCaseEnum>))]
    public enum LowerCaseEnum
    {
        FirstValue,
        SecondValue
    }

    [JsonConverter(typeof(JsonCustomEnumConverter<CustomEnum>))]
    public enum CustomEnum
    {
        [JsonCustomEnum("custom-first")]
        FirstValue,

        [JsonCustomEnum("custom-second")]
        SecondValue,

        MissingAttributeValue
    }

    #region JsonValue

    [Fact]
    [Trait("Member", "JsonValue")]
    public void JsonValue_WithNoAttribute_ReturnsDefaultName()
    {
        // Arrange
        NoAttributeEnum val = NoAttributeEnum.FirstValue;

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
        UpperCaseEnum val = UpperCaseEnum.FirstValue;

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
        LowerCaseEnum val = LowerCaseEnum.FirstValue;

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
        CustomEnum val = CustomEnum.FirstValue;

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
        CustomEnum val = CustomEnum.MissingAttributeValue;

        // Act
        string? result = val.JsonValue();

        // Assert
        result.Should().Be("MissingAttributeValue");
    }

    #endregion JsonValue
}
