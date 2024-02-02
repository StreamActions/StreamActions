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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Converts an enum to/from a JSON string in lower case.
/// </summary>
public sealed class JsonLowerCaseEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    #region Public Methods

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string val = reader.GetString()!;

        foreach (string name in Enum.GetNames<T>())
        {
            if (name.Equals(val, StringComparison.InvariantCultureIgnoreCase))
            {
                return Enum.Parse<T>(name);
            }
        }

        return default;
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Intentional")]
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer?.WriteStringValue(Enum.GetName(value)?.ToLowerInvariant());

    #endregion Public Methods
}
