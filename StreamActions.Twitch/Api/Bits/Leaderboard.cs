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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Globalization;
using StreamActions.Twitch.Extensions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Sends and represents a response element from a request for the bits leaderboard.
/// </summary>
public sealed record Leaderboard
{
    /// <summary>
    /// Leaderboard rank of the user.
    /// </summary>
    [JsonPropertyName("rank")]
    public int? Rank { get; init; }

    /// <summary>
    /// Leaderboard score (number of Bits) of the user.
    /// </summary>
    [JsonPropertyName("score")]
    public int? Score { get; init; }

    /// <summary>
    /// ID of the user (viewer) in the leaderboard entry.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// User login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// Display name corresponding to <see cref="UserId"/>.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// Time period over which data is aggregated (PST time zone).
    /// </summary>
    public enum LeaderboardPeriod
    {
        /// <summary>
        /// The lifetime of the broadcaster's channel.
        /// </summary>
        All,
        /// <summary>
        /// 00:00:00 on the day specified in <paramref name="startedAt"/>, through 00:00:00 on the following day.
        /// </summary>
        Day,
        /// <summary>
        /// 00:00:00 on Monday of the week specified in <paramref name="startedAt"/>, through 00:00:00 on the following Monday.
        /// </summary>
        Week,
        /// <summary>
        /// 00:00:00 on the first day of the month specified in <paramref name="startedAt"/>, through 00:00:00 on the first day of the following month.
        /// </summary>
        Month,
        /// <summary>
        /// 00:00:00 on the first day of the year specified in <paramref name="startedAt"/>, through 00:00:00 on the first day of the following year.
        /// </summary>
        Year
    }

    /// <summary>
    /// Gets a ranked list of Bits leaderboard information for an authorized broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="count">Number of results to be returned. Maximum: 100. Default: 10.</param>
    /// <param name="period">Time period over which data is aggregated (PST time zone). This parameter interacts with <paramref name="startedAt"/>.</param>
    /// <param name="startedAt">Timestamp for the period over which the returned data is aggregated. Must be in RFC 3339 format. If this is not provided, data is aggregated over the current period; e.g., the current day/week/month/year. Can not be used with a <paramref name="period"/> of <see cref="LeaderboardPeriod.All"/>.</param>
    /// <param name="userId">ID of the user whose results are returned; i.e., the person who paid for the Bits.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Leaderboard"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.BitsRead"/>.</exception>
    /// <exception cref="InvalidOperationException">Specified <paramref name="startedAt"/> while the value of <paramref name="period"/> was <see cref="LeaderboardPeriod.All"/>.</exception>
    /// <remarks>
    /// <para>
    /// Note for <paramref name="startedAt"/>:
    /// Currently, the HH:MM:SS part of this value is used only to identify a given day in PST and otherwise ignored. For example, if the <paramref name="startedAt"/> value resolves to 5PM PST yesterday and <paramref name="period"/> is <see cref="LeaderboardPeriod.Day"/>, data is returned for all of yesterday.
    /// </para>
    /// <para>
    /// Note for <paramref name="userId"/>:
    /// As long as <paramref name="count"/> is greater than 1, the returned data includes additional users, with Bits amounts above and below the user specified by <paramref name="userId"/>.
    /// If <paramref name="userId"/> is not provided, the endpoint returns the Bits leaderboard data across top users (subject to the value of <paramref name="count"/>).
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "API Definition")]
    public static async Task<ResponseData<Leaderboard>?> GetBitsLeaderboard(TwitchSession session, int count = 10, LeaderboardPeriod period = LeaderboardPeriod.All, DateTime? startedAt = null, string? userId = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.BitsRead);

        count = Math.Clamp(count, 1, 100);

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "count", new List<string> { count.ToString(CultureInfo.InvariantCulture) } },
            { "period", new List<string> { Enum.GetName(period)?.ToLowerInvariant() ?? "" } }
        };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            queryParams.Add("user_id", new List<string> { userId });
        }

        if (startedAt.HasValue)
        {
            if (period == LeaderboardPeriod.All)
            {
                throw new InvalidOperationException("Can not use " + nameof(startedAt) + " while " + nameof(period) + " is set to all").Log(TwitchApi.GetLogger());
            }
            else
            {
                queryParams.Add("started_at", new List<string> { startedAt.Value.Date.ToRfc3339() });
            }
        }

        Uri uri = Util.BuildUri(new("/bits/leaderboard"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Leaderboard>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
