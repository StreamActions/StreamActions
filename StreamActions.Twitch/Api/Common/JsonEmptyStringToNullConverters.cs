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

namespace StreamActions.Twitch.Api.Common;

/// <summary>
/// Converts a JSON string to a nullable <see cref="Guid"/>, treating empty or whitespace strings as <see langword="null"/>.
/// </summary>
internal sealed class JsonEmptyStringToNullGuidConverter : JsonConverter<Guid?>
{
    /// <inheritdoc/>
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            return string.IsNullOrWhiteSpace(value)
                ? null
                : Guid.TryParse(value, out Guid result)
                ? result
                : throw new JsonException($"Unable to convert \"{value}\" to {typeof(Guid)}.");
        }

        throw new JsonException($"Unexpected token parsing {typeof(Guid?)}. Expected String or Null, got {reader.TokenType}.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>
/// Converts a JSON string to a nullable <see cref="Uri"/>, treating empty or whitespace strings as <see langword="null"/>.
/// </summary>
internal sealed class JsonEmptyStringToNullUriConverter : JsonConverter<Uri?>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override Uri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            return string.IsNullOrWhiteSpace(value)
                ? null
                : Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out Uri? result)
                ? result
                : throw new JsonException($"Unable to convert \"{value}\" to {typeof(Uri)}.");
        }

        throw new JsonException($"Unexpected token parsing {typeof(Uri)}. Expected String or Null, got {reader.TokenType}.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Uri? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is not null)
        {
            writer.WriteStringValue(value.OriginalString);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
