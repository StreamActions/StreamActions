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
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Converts an enum to/from a JSON string as defined by an associated <see cref="JsonCustomEnumAttribute"/>.
/// </summary>
/// <typeparam name="T">An enum whose values are tagged with <see cref="JsonCustomEnumAttribute"/>.</typeparam>
public sealed class JsonCustomEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    #region Static Fields

    /// <summary>
    /// Cached mappings between custom attribute strings and enum values.
    /// </summary>
    private static readonly (FrozenDictionary<string, T> StringToEnum, FrozenDictionary<T, string> EnumToString) _maps = BuildMaps();

    /// <summary>
    /// An alternate lookup for <see cref="_maps"/> supporting <see cref="ReadOnlySpan{T}"/> keys.
    /// </summary>
    private static readonly FrozenDictionary<string, T>.AlternateLookup<ReadOnlySpan<char>> _stringToEnumLookup = _maps.StringToEnum.GetAlternateLookup<ReadOnlySpan<char>>();

    #endregion Static Fields

    #region Private Methods

    /// <summary>
    /// Precomputes and caches the mapping between enum values and their associated custom string attributes.
    /// </summary>
    /// <returns>A tuple containing the string-to-enum and enum-to-string frozen dictionaries.</returns>
    private static (FrozenDictionary<string, T> StringToEnum, FrozenDictionary<T, string> EnumToString) BuildMaps()
    {
        Dictionary<string, T> stringToEnumMap = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<T, string> enumToStringMap = [];

        foreach (string name in Enum.GetNames<T>())
        {
            MemberInfo? memberInfo = typeof(T).GetMember(name).FirstOrDefault();
            if (memberInfo is not null)
            {
                Attribute? attribute = Attribute.GetCustomAttribute(memberInfo, typeof(JsonCustomEnumAttribute));
                if (attribute is JsonCustomEnumAttribute customAttr && customAttr.Value is not null)
                {
                    T eval = Enum.Parse<T>(name);
                    stringToEnumMap[customAttr.Value] = eval;
                    enumToStringMap[eval] = customAttr.Value;
                }
            }
        }

        return (stringToEnumMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), enumToStringMap.ToFrozenDictionary());
    }

    #endregion Private Methods

    #region Public Properties

    /// <inheritdoc/>
    public override bool HandleNull => true;

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// Converts the given string into an enum value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The string comparison is performed using <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </para>
    /// </remarks>
    /// <param name="value">The string value to convert.</param>
    /// <returns>The enum value of type <typeparamref name="T"/> that has a matching <see cref="JsonCustomEnumAttribute"/>; <see langword="default"/> if there is no match.</returns>
    public T Convert(string value) => value is not null ? this.Convert(value.AsSpan()) : default;

    /// <summary>
    /// Converts the given character span into an enum value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The comparison is performed using <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </para>
    /// </remarks>
    /// <param name="value">The character span to convert.</param>
    /// <returns>The enum value of type <typeparamref name="T"/> that has a matching <see cref="JsonCustomEnumAttribute"/>; <see langword="default"/> if there is no match.</returns>
    public T Convert(ReadOnlySpan<char> value) => _stringToEnumLookup.TryGetValue(value, out T result) ? result : default;

    /// <summary>
    /// Converts the given enum value into a string.
    /// </summary>
    /// <param name="value">The enum value to convert.</param>
    /// <returns>The string value defined in the <see cref="JsonCustomEnumAttribute"/> applied to the enum value; <see cref="Enum.GetName{TEnum}(TEnum)"/> if the attribute is not present.</returns>
    public string? Convert(T value) => _maps.EnumToString.TryGetValue(value, out string? result) ? result : Enum.GetName(value);

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
            return this.Convert(charBuffer[..charsWritten]);
        }

        return this.Convert(reader.GetString().AsSpan());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer?.WriteStringValue(this.Convert(value));

    #endregion Public Methods
}
