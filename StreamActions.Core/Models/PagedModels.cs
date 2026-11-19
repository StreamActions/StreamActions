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

using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Models;

/// <summary>
/// Provides helper methods for projecting sequences into <see cref="PagedResult{T}"/> instances.
/// </summary>
public static class PagedResult
{
    /// <summary>
    /// Projects an in-memory sequence into a <see cref="PagedResult{T}"/> by applying an optional search predicate and slicing according to <paramref name="request"/>.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source item sequence.</param>
    /// <param name="request">The paging and search request parameters.</param>
    /// <param name="searchPredicate">An optional predicate that evaluates whether an item matches a non-empty trimmed search query.</param>
    /// <param name="totalUnfilteredCountOverride">An optional explicit unfiltered item count if <paramref name="source"/> was pre-filtered.</param>
    /// <returns>A <see cref="PagedResult{T}"/> containing the sliced items and count metadata.</returns>
    public static PagedResult<T> FromEnumerable<T>(
        IEnumerable<T> source,
        PagedRequest request,
        Func<T, string, bool>? searchPredicate = null,
        int? totalUnfilteredCountOverride = null) =>
        PagedResult<T>.FromEnumerable(source, request, searchPredicate, totalUnfilteredCountOverride);
}

/// <summary>
/// Represents a paging, search, and sorting request for data table queries.
/// </summary>
/// <param name="PageNumber">The 1-based page number to retrieve when not in infinite-scroll mode.</param>
/// <param name="PageSize">
/// The maximum number of items per page (for example, <c>10</c>, <c>25</c>, <c>50</c>, <c>100</c>),
/// or <see cref="ShowAllPageSize"/> (<c>-1</c>) for virtualized infinite-scroll mode.
/// </param>
/// <param name="SearchQuery">An optional search or filter query string.</param>
/// <param name="SortColumn">An optional column identifier to sort by.</param>
/// <param name="SortDescending">A value indicating whether sorting should be in descending order.</param>
public sealed record PagedRequest(
    int PageNumber = 1,
    int PageSize = 25,
    string? SearchQuery = null,
    string? SortColumn = null,
    bool SortDescending = false)
{
    /// <summary>
    /// Sentinel <see cref="PageSize"/> value (<c>-1</c>) indicating that all matching items should be rendered in virtualized infinite-scroll mode.
    /// </summary>
    public const int ShowAllPageSize = -1;

    /// <summary>
    /// Gets a value indicating whether the request specifies virtualized infinite-scroll mode (<see cref="PageSize"/> equals <see cref="ShowAllPageSize"/>).
    /// </summary>
    public bool IsInfiniteScroll => this.PageSize == ShowAllPageSize;
}

/// <summary>
/// Represents a paged or filtered data table result set alongside unfiltered and filtered item counts.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items for the requested page (or all filtered items when in infinite-scroll mode).</param>
/// <param name="TotalUnfilteredCount">The total number of items before applying the search filter.</param>
/// <param name="TotalFilteredCount">The total number of items matching the search filter across all pages.</param>
/// <param name="PageNumber">The resolved 1-based page number.</param>
/// <param name="PageSize">The requested page size, or <see cref="PagedRequest.ShowAllPageSize"/> (<c>-1</c>).</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalUnfilteredCount,
    int TotalFilteredCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>
    /// Projects an in-memory sequence into a <see cref="PagedResult{T}"/> by applying an optional search predicate and slicing according to <paramref name="request"/>.
    /// </summary>
    /// <param name="source">The source item sequence.</param>
    /// <param name="request">The paging and search request parameters.</param>
    /// <param name="searchPredicate">An optional predicate that evaluates whether an item matches a non-empty trimmed search query.</param>
    /// <param name="totalUnfilteredCountOverride">An optional explicit unfiltered item count if <paramref name="source"/> was pre-filtered.</param>
    /// <returns>A <see cref="PagedResult{T}"/> containing the sliced items and count metadata.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory helper on PagedResult<T> alongside non-generic PagedResult.FromEnumerable.")]
    public static PagedResult<T> FromEnumerable(
        IEnumerable<T> source,
        PagedRequest request,
        Func<T, string, bool>? searchPredicate = null,
        int? totalUnfilteredCountOverride = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<T> allItems = source as IReadOnlyList<T> ?? [.. source];
        int totalUnfiltered = totalUnfilteredCountOverride ?? allItems.Count;

        IReadOnlyList<T> filteredItems;
        if (!string.IsNullOrWhiteSpace(request.SearchQuery) && searchPredicate is not null)
        {
            string trimmedQuery = request.SearchQuery.Trim();
            filteredItems = [.. allItems.Where(item => searchPredicate(item, trimmedQuery))];
        }
        else
        {
            filteredItems = allItems;
        }

        int totalFiltered = filteredItems.Count;
        if (request.IsInfiniteScroll || request.PageSize <= 0)
        {
            return new(
                Items: filteredItems,
                TotalUnfilteredCount: totalUnfiltered,
                TotalFilteredCount: totalFiltered,
                PageNumber: 1,
                PageSize: PagedRequest.ShowAllPageSize);
        }

        int totalPages = totalFiltered == 0
            ? 1
            : (int)Math.Ceiling((double)totalFiltered / request.PageSize);
        int clampedPage = Math.Clamp(request.PageNumber, 1, totalPages);
        int skip = (clampedPage - 1) * request.PageSize;
        List<T> pageItems = [.. filteredItems.Skip(skip).Take(request.PageSize)];

        return new(
            Items: pageItems,
            TotalUnfilteredCount: totalUnfiltered,
            TotalFilteredCount: totalFiltered,
            PageNumber: clampedPage,
            PageSize: request.PageSize);
    }
}
