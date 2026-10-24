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

using StreamActions.Common.Json.Serialization;
using System.Text;
using System.Text.Json;

namespace StreamActions.Common.Tests.Json.Serialization;

public class JsonLowerCaseEnumConverterTests
{
    private enum TestEnum
    {
        FirstValue,
        SecondValue,
        Third_Value
    }

    private sealed class TestModel
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonLowerCaseEnumConverter<TestEnum>))]
        public TestEnum EnumValue { get; set; }
    }

    #region HandleNull

    [Fact]
    [Trait("Member", "HandleNull")]
    public void HandleNull_ShouldReturnTrue()
    {
        JsonLowerCaseEnumConverter<TestEnum> converter = new();
        _ = converter.HandleNull.Should().BeTrue();
    }

    #endregion HandleNull

    #region Read

    [Fact]
    [Trait("Member", "Read")]
    public void Read_WithLowerCaseString_ShouldParseToEnum()
    {
        string json = "{\"EnumValue\":\"firstvalue\"}";
        TestModel? result = JsonSerializer.Deserialize<TestModel>(json);

        _ = result.Should().NotBeNull();
        _ = result!.EnumValue.Should().Be(TestEnum.FirstValue);
    }

    [Fact]
    [Trait("Member", "Read")]
    public void Read_WithMixedCaseString_ShouldParseToEnum()
    {
        string json = "{\"EnumValue\":\"SeCondVaLue\"}";
        TestModel? result = JsonSerializer.Deserialize<TestModel>(json);

        _ = result.Should().NotBeNull();
        _ = result!.EnumValue.Should().Be(TestEnum.SecondValue);
    }

    [Fact]
    [Trait("Member", "Read")]
    public void Read_WithUpperCaseString_ShouldParseToEnum()
    {
        string json = "{\"EnumValue\":\"THIRD_VALUE\"}";
        TestModel? result = JsonSerializer.Deserialize<TestModel>(json);

        _ = result.Should().NotBeNull();
        _ = result!.EnumValue.Should().Be(TestEnum.Third_Value);
    }

    [Fact]
    [Trait("Member", "Read")]
    public void Read_WithInvalidString_ShouldReturnDefaultValue()
    {
        string json = "{\"EnumValue\":\"invalid\"}";
        TestModel? result = JsonSerializer.Deserialize<TestModel>(json);

        _ = result.Should().NotBeNull();
        _ = result!.EnumValue.Should().Be(default);
    }

    [Fact]
    [Trait("Member", "Read")]
    public void Read_WithDirectReaderCall_ShouldReturnExpectedResult()
    {
        JsonLowerCaseEnumConverter<TestEnum> converter = new();
        byte[] jsonUtf8Bytes = Encoding.UTF8.GetBytes("\"secondvalue\"");
        Utf8JsonReader reader = new(jsonUtf8Bytes);
        _ = reader.Read();

        TestEnum result = converter.Read(ref reader, typeof(TestEnum), new JsonSerializerOptions());

        _ = result.Should().Be(TestEnum.SecondValue);
    }

    #endregion Read

    #region Write

    [Fact]
    [Trait("Member", "Write")]
    public void Write_ShouldSerializeToLowerCaseString()
    {
        TestModel model = new() { EnumValue = TestEnum.SecondValue };
        string json = JsonSerializer.Serialize(model);

        _ = json.Should().Contain("\"secondvalue\"");
        _ = json.Should().NotContain("\"SecondValue\"");
    }

    [Fact]
    [Trait("Member", "Write")]
    public void Write_WithUnderscore_ShouldSerializeToLowerCaseString()
    {
        TestModel model = new() { EnumValue = TestEnum.Third_Value };
        string json = JsonSerializer.Serialize(model);

        _ = json.Should().Contain("\"third_value\"");
        _ = json.Should().NotContain("\"Third_Value\"");
    }

    [Fact]
    [Trait("Member", "Write")]
    public void Write_WithDirectWriterCall_ShouldWriteLowerCase()
    {
        JsonLowerCaseEnumConverter<TestEnum> converter = new();
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream);

        converter.Write(writer, TestEnum.FirstValue, new JsonSerializerOptions());
        writer.Flush();

        string json = Encoding.UTF8.GetString(stream.ToArray());
        _ = json.Should().Be("\"firstvalue\"");
    }

    #endregion Write
}
