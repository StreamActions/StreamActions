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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Search;

/// <summary>
/// Represents a game or category found in a search.
/// </summary>
public sealed record SearchCategory
{
    /// <summary>
    /// A URL to an image of the game's box art or streaming category.
    /// </summary>
    [JsonPropertyName("box_art_url")]
    public Uri? BoxArtUrl { get; init; }

    /// <summary>
    /// The name of the game or category.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// An ID that uniquely identifies the game or category.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Gets the games or categories that match the specified query.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="query">The URI-encoded search string.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="SearchCategory"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// To match, the category's name must contain all parts of the query string. The comparison is case insensitive.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of category names that matched the specified query string.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<SearchCategory>?> SearchCategories(TwitchSession session, string query, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentNullException(nameof(query)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];
        queryParameters.Add("query", query);

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        Uri requestUri = Util.BuildUri(new("/search/categories"), queryParameters);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, requestUri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<SearchCategory>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
