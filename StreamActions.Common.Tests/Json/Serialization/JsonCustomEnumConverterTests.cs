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

using System.Text.Json.Serialization;
using System.Text.Json;
using FluentAssertions;
using StreamActions.Common.Json.Serialization;
using Xunit;

namespace StreamActions.Common.Tests.Json.Serialization;

/// <summary>
/// A dummy enum to be used in <see cref="JsonCustomEnumConverterTests"/>.
/// </summary>
public enum TestCustomAttr
{
    [JsonCustomEnum("VALUE_ONE")]
    ValueOne,

    [JsonCustomEnum("another_value")]
    ValueTwo,

    NoAttributeValue
}

/// <summary>
/// A class used for serialization tests.
/// </summary>
public class TestClass
{
    [JsonConverter(typeof(JsonCustomEnumConverter<TestCustomAttr>))]
    public TestCustomAttr EnumValue { get; set; }
}

public class JsonCustomEnumConverterTests
{
    private readonly JsonCustomEnumConverter<TestCustomAttr> _converter;

    public JsonCustomEnumConverterTests()
    {
        _converter = new JsonCustomEnumConverter<TestCustomAttr>();
    }

    #region HandleNull

    [Fact]
    [Trait("Member", "HandleNull")]
    public void HandleNull_ReturnsTrue()
    {
        _converter.HandleNull.Should().BeTrue();
    }

    #endregion HandleNull

    #region Convert(string)

    [Theory]
    [Trait("Member", "Convert")]
    [InlineData("VALUE_ONE", TestCustomAttr.ValueOne)]
    [InlineData("value_one", TestCustomAttr.ValueOne)] // case insensitivity
    [InlineData("another_value", TestCustomAttr.ValueTwo)]
    [InlineData("ANOTHER_VALUE", TestCustomAttr.ValueTwo)]
    [InlineData("NoAttributeValue", default(TestCustomAttr))] // No attribute string won't match Convert(string)
    [InlineData("INVALID_VALUE", default(TestCustomAttr))] // completely unmatched
    public void Convert_StringToEnum_ReturnsExpectedValue(string input, TestCustomAttr expected)
    {
        _converter.Convert(input).Should().Be(expected);
    }

    #endregion Convert(string)

    #region Convert(T)

    [Theory]
    [Trait("Member", "Convert")]
    [InlineData(TestCustomAttr.ValueOne, "VALUE_ONE")]
    [InlineData(TestCustomAttr.ValueTwo, "another_value")]
    [InlineData(TestCustomAttr.NoAttributeValue, "NoAttributeValue")]
    public void Convert_EnumToString_ReturnsExpectedValue(TestCustomAttr input, string expected)
    {
        _converter.Convert(input).Should().Be(expected);
    }

    #endregion Convert(T)

    #region Read

    [Theory]
    [Trait("Member", "Read")]
    [InlineData("{\"EnumValue\":\"VALUE_ONE\"}", TestCustomAttr.ValueOne)]
    [InlineData("{\"EnumValue\":\"another_value\"}", TestCustomAttr.ValueTwo)]
    [InlineData("{\"EnumValue\":\"ANOTHER_VALUE\"}", TestCustomAttr.ValueTwo)]
    [InlineData("{\"EnumValue\":\"NoAttributeValue\"}", default(TestCustomAttr))]
    [InlineData("{\"EnumValue\":\"UNKNOWN\"}", default(TestCustomAttr))]
    public void Read_DeserializeJson_ReturnsExpectedValue(string json, TestCustomAttr expected)
    {
        var result = JsonSerializer.Deserialize<TestClass>(json);
        result.Should().NotBeNull();
        result!.EnumValue.Should().Be(expected);
    }

    #endregion Read

    #region Write

    [Theory]
    [Trait("Member", "Write")]
    [InlineData(TestCustomAttr.ValueOne, "{\"EnumValue\":\"VALUE_ONE\"}")]
    [InlineData(TestCustomAttr.ValueTwo, "{\"EnumValue\":\"another_value\"}")]
    [InlineData(TestCustomAttr.NoAttributeValue, "{\"EnumValue\":\"NoAttributeValue\"}")]
    public void Write_SerializeJson_ReturnsExpectedJson(TestCustomAttr value, string expectedJson)
    {
        var obj = new TestClass { EnumValue = value };
        var json = JsonSerializer.Serialize(obj);
        json.Should().Be(expectedJson);
    }

    #endregion Write
}
