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

namespace StreamActions.Common.Extensions;

/// <summary>
/// Contains extension members to <see cref="List{T}"/>.
/// </summary>
public static class ListExtensions
{
    #region Public Methods

    /// <summary>
    /// Returns the specified element, or <paramref name="defVal"/> if <paramref name="index"/> is out of range.
    /// </summary>
    /// <typeparam name="T">The type stored in <paramref name="source"/></typeparam>
    /// <param name="source">A List of <typeparamref name="T"/> values.</param>
    /// <param name="index">The index to attempt to retrieve.</param>
    /// <param name="defVal">The default value to return, if <paramref name="index"/> is out of range.</param>
    /// <returns>The value at <paramref name="index"/>; <paramref name="defVal"/> if index is out of range.</returns>
    public static T? GetElementAtOrDefault<T>(this List<T> source, int index, T? defVal = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        try
        {
            return source[index];
        }
        catch (IndexOutOfRangeException)
        {
            return defVal;
        }
    }

    /// <summary>
    /// Determines if the string at the specified index of the <c>List&lt;string></c> is <see langword="null"/> or an empty string (""), or if the index is out of range.
    /// </summary>
    /// <param name="source">The <c>List&lt;string></c> to check.</param>
    /// <param name="index">The index of the desired entry in the <c>List&lt;string></c>.</param>
    /// <returns><see langword="true"/> if the string at the specified index is <see langword="null"/>, empty, or the index is out of range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static bool IsNullEmptyOrOutOfRange(this List<string> source, int index)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        try
        {
            return string.IsNullOrEmpty(source[index]);
        }
        catch (IndexOutOfRangeException)
        {
            return true;
        }
    }

    /// <summary>
    /// Determines if the string at the specified index of the <c>List&lt;string&rt;</c> is <see langword="null"/>, an empty string (""), or only consists of white-space characters, or if the index is out of range.
    /// </summary>
    /// <param name="source">The <c>List&lt;string&rt;</c> to check.</param>
    /// <param name="index">The index of the desired entry in the <c>List&lt;string&rt;</c>.</param>
    /// <returns><see langword="true"/> if the string at the specified index is <see langword="null"/>, empty, contains only white-space characters, or the index is out of range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static bool IsNullWhiteSpaceOrOutOfRange(this List<string?> source, int index)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        try
        {
            return string.IsNullOrWhiteSpace(source[index]);
        }
        catch (IndexOutOfRangeException)
        {
            return true;
        }
    }

    #endregion Public Methods
}
