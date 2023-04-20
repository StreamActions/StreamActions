/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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

using System.Collections.Specialized;

namespace StreamActions.Common.Extensions;

/// <summary>
/// Extensions for <see cref="NameValueCollection"/>.
/// </summary>
public static class NameValueCollectionExtensions
{
    /// <summary>
    /// Adds multiple values with the specified name to the <see cref="NameValueCollection"/>.
    /// </summary>
    /// <param name="collection">The collection to add the values to.</param>
    /// <param name="name">The String key of the entries to add. The key can be <see langword="null"/>.</param>
    /// <param name="values">An enumerable of the String values of the entries to add. The values can be <see langword="null"/>.</param>
    /// <exception cref="NotSupportedException">The collection is read-only.</exception>
    public static void Add(this NameValueCollection collection, string? name, IEnumerable<string?> values)
    {
        if (collection is null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        foreach (string? value in values)
        {
            collection.Add(name, value);
        }
    }
}
