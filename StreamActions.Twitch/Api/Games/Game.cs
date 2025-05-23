/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions; // Included as per subtask requirements
using StreamActions.Twitch.OAuth; // For TwitchToken, which is part of TwitchSession.Token
using StreamActions.Common.Util; // For Util.BuildUri
using System.Globalization; // For CultureInfo.InvariantCulture

namespace StreamActions.Twitch.Api.Games;

/// <summary>
/// Represents a game on Twitch.
/// </summary>
public sealed record Game
{
    /// <summary>
    /// An ID that uniquely identifies the game.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    /// <summary>
    /// The name of the game.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// A URL to the game’s box art. You must replace the {width}x{height} placeholders with the size of the image you want.
    /// </summary>
    [JsonPropertyName("box_art_url")]
    public string BoxArtUrl { get; init; } = "";

    /// <summary>
    /// The game’s ID on IGDB.
    /// </summary>
    /// <remarks>
    /// Note: The API documentation indicates that this can be an empty string if not available for a game.
    /// </remarks>
    [JsonPropertyName("igdb_id")]
    public string IgdbId { get; init; } = "";

    /// <summary>
    /// Gets information about one or more specified games.
    /// You may get games by ID, name, or IGDB ID. At least one of these parameters must be provided.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="gameIds">A list of game IDs. Maximum of 100 combined IDs/names/IGDB IDs across all parameters.</param>
    /// <param name="gameNames">A list of game names. Names must be an exact match. Maximum of 100 combined IDs/names/IGDB IDs across all parameters.</param>
    /// <param name="igdbIds">A list of IGDB IDs. Maximum of 100 combined IDs/names/IGDB IDs across all parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Game"/> containing the response, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentException">Neither <paramref name="gameIds"/>, <paramref name="gameNames"/>, nor <paramref name="igdbIds"/> were provided, or the total number of IDs/names exceeds 100.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the specified games.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>A query parameter was missing or invalid (e.g., no identifiers provided, or the combined count of identifiers exceeds 100).</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid, missing, or the Client-Id header was invalid or mismatched.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Game>?> GetGames(TwitchSession session, IEnumerable<string>? gameIds = null, IEnumerable<string>? gameNames = null, IEnumerable<string>? igdbIds = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        // Removed explicit token check as session.RequireToken() handles it.

        List<string> gameIdList = gameIds?.ToList() ?? new List<string>();
        List<string> gameNameList = gameNames?.ToList() ?? new List<string>();
        List<string> igdbIdList = igdbIds?.ToList() ?? new List<string>();

        int totalIds = gameIdList.Count + gameNameList.Count + igdbIdList.Count;

        if (totalIds == 0)
        {
            throw new ArgumentNullException(nameof(gameIds) + "," + nameof(gameNames) + "," + nameof(igdbIds), "At least one game ID, game name, or IGDB ID must be provided.").Log(TwitchApi.GetLogger());
        }

        if (totalIds > 100) // MaxIdsPerRequest replaced with 100
        {
            throw new ArgumentOutOfRangeException(nameof(gameIds) + "," + nameof(gameNames) + "," + nameof(igdbIds), totalIds, "The total number of IDs, names, and IGDB IDs must not exceed 100.").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(); // No specific scope needed for Get Games

        Dictionary<string, ICollection<string>> queryParameters = new();
        if (gameIdList.Count > 0)
        {
            queryParameters["id"] = gameIdList;
        }
        if (gameNameList.Count > 0)
        {
            queryParameters["name"] = gameNameList;
        }
        if (igdbIdList.Count > 0)
        {
            queryParameters["igdb_id"] = igdbIdList;
        }

        Uri requestUri = Util.BuildUri("/games", queryParameters);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, requestUri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Game>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        // Removed try-catch block for HTTP and deserialization calls. Exceptions will propagate.
    }

    /// <summary>
    /// Gets a list of games, sorted by the number of current viewers in descending order.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor that identifies the page of results to retrieve.</param>
    /// <param name="before">The cursor that identifies the page of results to retrieve.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Game"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of top games.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>A query parameter was invalid (e.g., 'first' parameter out of range, or an invalid 'after'/'before' cursor).</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid, missing, or the Client-Id header was invalid or mismatched.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Game>?> GetTopGames(TwitchSession session, int first = 20, string? after = null, string? before = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(); // No specific scope needed for Get Top Games

        // ArgumentOutOfRangeException for 'first' removed as per requirements.

        Dictionary<string, ICollection<string>> queryParameters = new();

        // 'first' parameter is no longer nullable, Math.Clamp handles the range.
        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", new[] { first.ToString(CultureInfo.InvariantCulture) });

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", new[] { after });
        }
        if (!string.IsNullOrWhiteSpace(before))
        {
            queryParameters.Add("before", new[] { before });
        }

        Uri requestUri = Util.BuildUri("/games/top", queryParameters);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, requestUri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Game>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
