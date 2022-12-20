/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Analytics;

/// <summary>
/// Sends and represents a response element for a request for game analytics.
/// </summary>
public sealed record GameAnalytics
{
    /// <summary>
    /// ID of the game whose analytics data is being provided.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// URL to the downloadable CSV file containing analytics data. Valid for 5 minutes.
    /// </summary>
    [JsonPropertyName("URL")]
    public Uri? Url { get; init; }

    /// <summary>
    /// Type of report.
    /// </summary>
    [JsonPropertyName("type")]
    public GameReportType? Type { get; init; }

    /// <summary>
    /// The date range covered by the report.
    /// </summary>
    [JsonPropertyName("date_range")]
    public DateRange? DateRange { get; init; }

    /// <summary>
    /// Game report types.
    /// </summary>
    public enum GameReportType
    {
        /// <summary>
        /// <c>overview_v2</c> report.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "API Definition")]
        Overview_v2
    }

    /// <summary>
    /// Gets a URL that game developers can use to download analytics reports (CSV files) for their games. The URL is valid for 5 minutes.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="gameId">Game ID. If this is specified, the returned URL points to an analytics report for just the specified game. If this is not specified, the response includes multiple URLs (paginated), pointing to separate analytics reports for each of the authenticated user's games.</param>
    /// <param name="after">Cursor for forward pagination: tells the server where to start fetching the next set of results, in a multi-page response. This applies only to queries without <paramref name="gameId"/>.</param>
    /// <param name="first">Maximum number of objects to return. Maximum: 100. Default: 20.</param>
    /// <param name="startedAt">Starting date/time for returned reports. This must be on or after January 31, 2018.</param>
    /// <param name="endedAt">Ending date/time for returned reports. The report covers the entire ending date. This must be on or after January 31, 2018.</param>
    /// <param name="type">Type of analytics report that is returned. Currently, this field has no affect on the response as there is only one report type. If additional types were added, using this field would return only the URL for the specified report.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionAnalytics"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> contains a scope list, and <see cref="Scope.AnalyticsReadGames"/> is not present.</exception>
    /// <exception cref="InvalidOperationException">Specified only one of <paramref name="startedAt"/>/<paramref name="endedAt"/> without specifying the other.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "API Definition")]
    public static async Task<ResponseData<GameAnalytics>?> GetExtensionAnalytics(TwitchSession session, string? gameId = null, string? after = null, int first = 20, DateTime? startedAt = null, DateTime? endedAt = null,
        GameReportType type = GameReportType.Overview_v2)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (!session.Token?.HasScope(Scope.AnalyticsReadGames) ?? false)
        {
            throw new TwitchScopeMissingException(Scope.AnalyticsReadGames);
        }

        first = Math.Clamp(first, 1, 100);

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "first", new List<string> { first.ToString(CultureInfo.InvariantCulture) } },
            { "type", new List<string> { Enum.GetName(type)?.ToLowerInvariant() ?? "" } }
        };

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            queryParams.Add("game_id", new List<string> { gameId });
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", new List<string> { after });
        }

        if ((!startedAt.HasValue && endedAt.HasValue) || (startedAt.HasValue && !endedAt.HasValue))
        {
            throw new InvalidOperationException("If " + nameof(startedAt) + " is specified, " + nameof(endedAt) + " must also be specified (and vice-versa)");
        }

        if (startedAt.HasValue && endedAt.HasValue)
        {
            queryParams.Add("started_at", new List<string> { startedAt.Value.Date.ToRfc3339() });
            queryParams.Add("ended_at", new List<string> { endedAt.Value.Date.ToRfc3339() });
        }

        Uri uri = Util.BuildUri(new("/analytics/games"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResponseData<GameAnalytics>>().ConfigureAwait(false);
    }
}
