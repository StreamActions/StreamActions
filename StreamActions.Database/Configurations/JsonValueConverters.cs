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

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace StreamActions.Database.Configurations;

/// <summary>
/// Shared EF Core value converters and comparers for dictionary properties inside JSON complex types.
/// </summary>
internal static class JsonValueConverters
{
    /// <summary>
    /// Shared JSON serializer options for dictionary value conversion.
    /// </summary>
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Gets a value converter for <see cref="Dictionary{TKey, TValue}"/> of strings.
    /// </summary>
    public static ValueConverter<Dictionary<string, string>, string> StringDictionaryConverter { get; } =
        new(
            static v => JsonSerializer.Serialize(v, _options),
            static v => string.IsNullOrWhiteSpace(v)
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : JsonSerializer.Deserialize<Dictionary<string, string>>(v, _options) ?? new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Gets a value comparer for <see cref="Dictionary{TKey, TValue}"/> of strings.
    /// </summary>
    public static ValueComparer<Dictionary<string, string>> StringDictionaryComparer { get; } =
        new(
            static (a, b) => DictionariesEqual(a, b),
            static v => ComputeHashCode(v),
            static v => new Dictionary<string, string>(v, StringComparer.Ordinal));

    /// <summary>
    /// Compares two string dictionaries for ordinal key and value equality.
    /// </summary>
    /// <param name="left">The first dictionary to compare.</param>
    /// <param name="right">The second dictionary to compare.</param>
    /// <returns><see langword="true"/> if both dictionaries are equal or both are <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
    private static bool DictionariesEqual(Dictionary<string, string>? left, Dictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if ((left is null) || (right is null) || (left.Count != right.Count))
        {
            return false;
        }

        foreach (KeyValuePair<string, string> kvp in left)
        {
            if (!right.TryGetValue(kvp.Key, out string? otherValue) || !string.Equals(kvp.Value, otherValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Computes an order-independent ordinal hash code for a string dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary to hash.</param>
    /// <returns>The computed hash code.</returns>
    private static int ComputeHashCode(Dictionary<string, string> dictionary)
    {
        HashCode hash = new();
        foreach (KeyValuePair<string, string> kvp in dictionary.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key, StringComparer.Ordinal);
            hash.Add(kvp.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
