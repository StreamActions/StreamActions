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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Tests.Json.Serialization;

/// <summary>
/// Performs unit testing on methods in <see cref="JsonCustomConverter{T}"/>.
/// </summary>
public sealed class JsonCustomConverterTests
{
    /// <summary>
    /// Test model for <see cref="JsonCustomConverter{T}"/> unit tests.
    /// </summary>
    private sealed class TestModel
    {
        /// <summary>
        /// Gets or sets the mapped name.
        /// </summary>
        [JsonPropertyName("custom_name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the integer property ignored when default.
        /// </summary>
        [JsonPropertyName("default_int")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int DefaultInt { get; set; }

        /// <summary>
        /// Gets or sets the boolean property ignored when default.
        /// </summary>
        [JsonPropertyName("default_bool")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DefaultBool { get; set; }

        /// <summary>
        /// Gets or sets the string property ignored when null.
        /// </summary>
        [JsonPropertyName("nullable_text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NullableText { get; set; }

        /// <summary>
        /// Gets or sets the always-ignored property.
        /// </summary>
        [JsonPropertyName("always_ignored")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string? AlwaysIgnored { get; set; }

        /// <summary>
        /// Gets or sets the custom-handled property.
        /// </summary>
        [JsonPropertyName("custom_handled")]
        public string? CustomHandled { get; set; }
    }

    /// <summary>
    /// Test converter implementation of <see cref="JsonCustomConverter{T}"/>.
    /// </summary>
    private sealed class TestModelConverter : JsonCustomConverter<TestModel>
    {
        /// <inheritdoc/>
        public override bool HandleNull => true;

        /// <inheritdoc/>
        protected override bool ShouldUseDefaultConversion(string propertyName, string jsonPropertyName)
            => propertyName != nameof(TestModel.CustomHandled);

        /// <inheritdoc/>
        protected override void Read(ref Utf8JsonReader reader, Utf8JsonReader newReader, Type typeToConvert, JsonSerializerOptions options, TestModel obj, string propertyName, string jsonPropertyName)
        {
            if (propertyName == nameof(TestModel.CustomHandled))
            {
                obj.CustomHandled = $"custom:{reader.GetString()}";
            }
        }

        /// <inheritdoc/>
        protected override void Write(Utf8JsonWriter writer, TestModel obj, JsonSerializerOptions options, string propertyName, string jsonPropertyName, object? value)
        {
            if (propertyName == nameof(TestModel.CustomHandled))
            {
                writer.WriteString(jsonPropertyName, $"written:{value}");
            }
        }
    }

    /// <summary>
    /// Creates serializer options configured with <see cref="TestModelConverter"/>.
    /// </summary>
    /// <returns>Configured <see cref="JsonSerializerOptions"/>.</returns>
    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new();
        options.Converters.Add(new TestModelConverter());
        return options;
    }

    #region Read

    [Fact]
    [Trait("Member", "Read")]
    public void Read_DeserializesMappedAndCustomProperties()
    {
        JsonSerializerOptions options = CreateOptions();
        string json = "{\"custom_name\":\"alice\",\"default_int\":42,\"default_bool\":true,\"nullable_text\":\"hello\",\"custom_handled\":\"raw\"}";

        TestModel? result = JsonSerializer.Deserialize<TestModel>(json, options);

        _ = result.Should().NotBeNull();
        _ = result!.Name.Should().Be("alice");
        _ = result.DefaultInt.Should().Be(42);
        _ = result.DefaultBool.Should().BeTrue();
        _ = result.NullableText.Should().Be("hello");
        _ = result.CustomHandled.Should().Be("custom:raw");
    }

    #endregion Read

    #region Write

    [Fact]
    [Trait("Member", "Write")]
    public void Write_SerializesValidJsonObjectAndAppliesIgnoreConditions()
    {
        JsonSerializerOptions options = CreateOptions();
        TestModel model = new()
        {
            Name = "bob",
            DefaultInt = 0,
            DefaultBool = false,
            NullableText = null,
            AlwaysIgnored = "secret",
            CustomHandled = "val",
        };

        string json = JsonSerializer.Serialize(model, options);
        using JsonDocument doc = JsonDocument.Parse(json);

        _ = doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        _ = doc.RootElement.GetProperty("custom_name").GetString().Should().Be("bob");
        _ = doc.RootElement.TryGetProperty("default_int", out _).Should().BeFalse();
        _ = doc.RootElement.TryGetProperty("default_bool", out _).Should().BeFalse();
        _ = doc.RootElement.TryGetProperty("nullable_text", out _).Should().BeFalse();
        _ = doc.RootElement.TryGetProperty("always_ignored", out _).Should().BeFalse();
        _ = doc.RootElement.GetProperty("custom_handled").GetString().Should().Be("written:val");
    }

    [Fact]
    [Trait("Member", "Write")]
    public void Write_SerializesNonDefaultValueTypes()
    {
        JsonSerializerOptions options = CreateOptions();
        TestModel model = new()
        {
            Name = "carol",
            DefaultInt = 7,
            DefaultBool = true,
            NullableText = "present",
            CustomHandled = "ok",
        };

        string json = JsonSerializer.Serialize(model, options);
        using JsonDocument doc = JsonDocument.Parse(json);

        _ = doc.RootElement.GetProperty("default_int").GetInt32().Should().Be(7);
        _ = doc.RootElement.GetProperty("default_bool").GetBoolean().Should().BeTrue();
        _ = doc.RootElement.GetProperty("nullable_text").GetString().Should().Be("present");
    }

    [Fact]
    [Trait("Member", "Write")]
    public void Write_WhenValueIsNull_WritesJsonNull()
    {
        JsonSerializerOptions options = CreateOptions();
        TestModel? model = null;

        string json = JsonSerializer.Serialize(model, options);

        _ = json.Should().Be("null");
    }

    #endregion Write
}
