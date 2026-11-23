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

using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Converts an enum to/from a JSON string in upper case.
/// </summary>
/// <typeparam name="T">An enum whose values should be converted.</typeparam>
public sealed class JsonUpperCaseEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    #region Static Fields

    /// <summary>
    /// Cached mappings between enum names and enum values.
    /// </summary>
    private static readonly (FrozenDictionary<string, T> StringToEnum, FrozenDictionary<T, string> EnumToString) _maps = BuildMaps();

    /// <summary>
    /// An alternate lookup for <see cref="_maps"/> supporting <see cref="ReadOnlySpan{T}"/> keys.
    /// </summary>
    private static readonly FrozenDictionary<string, T>.AlternateLookup<ReadOnlySpan<char>> _stringToEnumLookup = _maps.StringToEnum.GetAlternateLookup<ReadOnlySpan<char>>();

    #endregion Static Fields

    #region Private Methods

    /// <summary>
    /// Precomputes and caches the mapping between enum values and their uppercase string representations.
    /// </summary>
    /// <returns>A tuple containing the string-to-enum and enum-to-string frozen dictionaries.</returns>
    private static (FrozenDictionary<string, T> StringToEnum, FrozenDictionary<T, string> EnumToString) BuildMaps()
    {
        Dictionary<string, T> stringToEnumMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<T, string> enumToStringMap = [];

        foreach (string name in Enum.GetNames<T>())
        {
            T eval = Enum.Parse<T>(name);
            stringToEnumMap[name] = eval;
            enumToStringMap[eval] = name.ToUpperInvariant();
        }

        return (stringToEnumMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), enumToStringMap.ToFrozenDictionary());
    }

    #endregion Private Methods

    #region Public Properties

    /// <inheritdoc/>
    public override bool HandleNull => true;

    #endregion Public Properties

    #region Public Methods

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType == JsonTokenType.String && !reader.HasValueSequence && reader.ValueSpan.Length <= 128)
        {
            Span<char> charBuffer = stackalloc char[128];
            int charsWritten = reader.CopyString(charBuffer);
            return _stringToEnumLookup.TryGetValue(charBuffer[..charsWritten], out T result) ? result : default;
        }

        return _stringToEnumLookup.TryGetValue(reader.GetString().AsSpan(), out T fallbackResult) ? fallbackResult : default;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer?.WriteStringValue(_maps.EnumToString.TryGetValue(value, out string? result) ? result : Enum.GetName(value)?.ToUpperInvariant());

    #endregion Public Methods
}
