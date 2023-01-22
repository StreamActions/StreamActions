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
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Sends and represents a response element from a request for the bits leaderboard.
/// </summary>
public sealed record Leaderboard
{
    /// <summary>
    /// The user's position on the leaderboard.
    /// </summary>
    [JsonPropertyName("rank")]
    public int? Rank { get; init; }

    /// <summary>
    /// The number of Bits the user has cheered.
    /// </summary>
    [JsonPropertyName("score")]
    public int? Score { get; init; }

    /// <summary>
    /// An ID that identifies a user on the leaderboard.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The time period over which data is aggregated (uses the PST time zone).
    /// </summary>
    public enum LeaderboardPeriod
    {
        /// <summary>
        /// Default. The lifetime of the broadcaster's channel.
        /// </summary>
        All,
        /// <summary>
        /// A day spans from 00:00:00 on the day specified in <c>startedAt</c> and runs through 00:00:00 of the next day.
        /// </summary>
        Day,
        /// <summary>
        /// A week spans from 00:00:00 on the Monday of the week specified in <c>startedAt</c> and runs through 00:00:00 of the next Monday.
        /// </summary>
        Week,
        /// <summary>
        /// A month spans from 00:00:00 on the first day of the month specified in <c>startedAt</c> and runs through 00:00:00 of the first day of the next month.
        /// </summary>
        Month,
        /// <summary>
        /// A year spans from 00:00:00 on the first day of the year specified in <c>startedAt</c> and runs through 00:00:00 of the first day of the next year.
        /// </summary>
        Year
    }

    /// <summary>
    /// Gets the Bits leaderboard for the authenticated broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="count">The number of results to return. Maximum: 100. Default: 10.</param>
    /// <param name="period">The time period over which data is aggregated (uses the PST time zone). This parameter interacts with <paramref name="startedAt"/>.</param>
    /// <param name="startedAt">The start date used for determining the aggregation period. Note that the date is converted to PST before being used. Can not be used with a <paramref name="period"/> of <see cref="LeaderboardPeriod.All"/>.</param>
    /// <param name="userId">An ID that identifies a user that cheered bits in the channel. If <paramref name="count"/> is greater than 1, the response may include users ranked above and below the specified user.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Leaderboard"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.BitsRead"/>.</exception>
    /// <exception cref="InvalidOperationException">Specified <paramref name="startedAt"/> while the value of <paramref name="period"/> was <see cref="LeaderboardPeriod.All"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's Bits leaderboard.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>???</description>
    /// </item>
    /// </list>
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
