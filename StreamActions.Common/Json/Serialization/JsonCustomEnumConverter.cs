/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Converts an enum to/from a JSON string as defined by an associated <see cref="JsonCustomEnumAttribute"/>.
/// </summary>
public sealed class JsonCustomEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    #region Public Methods

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => this.Convert(reader.GetString()!);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer?.WriteStringValue(this.Convert(value));

    /// <summary>
    /// Converts the given string into an enum value.
    /// </summary>
    /// <remarks>
    /// The string comparison is performed using <see cref="StringComparison.InvariantCultureIgnoreCase"/>.
    /// </remarks>
    /// <param name="value">The string value to convert.</param>
    /// <returns>The enum value of type <typeparamref name="T"/> that has a matching <see cref="JsonCustomEnumAttribute"/>; <see langword="default"/> if there is no match.</returns>
    public T Convert(string value)
    {
        foreach (string eval in Enum.GetNames<T>())
        {
            MemberInfo? memberInfo = typeof(T).GetMember(eval).FirstOrDefault();
            if (memberInfo is not null)
            {
                Attribute? attribute = Attribute.GetCustomAttribute(memberInfo, typeof(JsonCustomEnumAttribute));
                if (attribute is not null && ((attribute as JsonCustomEnumAttribute)?.Value.Equals(value, StringComparison.InvariantCultureIgnoreCase) ?? false))
                {
                    return Enum.Parse<T>(eval);
                }
            }
        }

        return default;
    }

    /// <summary>
    /// Converts the given enum value into a string.
    /// </summary>
    /// <param name="value">The enum value to convert.</param>
    /// <returns>The string value defined in the <see cref="JsonCustomEnumAttribute"/> applied to the enum value; <see cref="Enum.GetName{TEnum}(TEnum)"/> if the attribute is not present.</returns>
    public string? Convert(T value)
    {
        MemberInfo? memberInfo = typeof(T).GetMember(Enum.GetName(typeof(T), value) ?? "__").FirstOrDefault();
        if (memberInfo is not null)
        {
            Attribute? attribute = Attribute.GetCustomAttribute(memberInfo, typeof(JsonCustomEnumAttribute));
            if (attribute is not null)
            {
                return (attribute as JsonCustomEnumAttribute)?.Value;
            }
        }

        return Enum.GetName(value);
    }

    #endregion Public Methods
}
