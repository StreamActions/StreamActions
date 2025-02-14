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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Ads;

/// <summary>
/// Sends and represents a response element for a request to get the ad schedule or snooze the next ad.
/// </summary>
public sealed record AdSchedule
{
    /// <summary>
    /// The number of snoozes available for the broadcaster.
    /// </summary>
    [JsonPropertyName("snooze_count")]
    public int? SnoozeCount { get; init; }

    /// <summary>
    /// The UTC timestamp when the broadcaster will gain an additional snooze.
    /// </summary>
    [JsonPropertyName("snooze_refresh_at")]
    public DateTime? SnoozeRefreshAt { get; init; }

    /// <summary>
    /// The UTC timestamp of the broadcaster's next scheduled ad. <see langword="null"/> if the channel has no ad scheduled or is not live.
    /// </summary>
    [JsonPropertyName("next_ad_at")]
    public DateTime? NextAdAt { get; init; }

    /// <summary>
    /// The length in seconds of the scheduled upcoming ad break.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The length of the scheduled upcoming ad break.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// The UTC timestamp of the broadcaster's last ad-break. <see langword="null"/> if the channel has not run an ad or is not live.
    /// </summary>
    [JsonPropertyName("last_ad_at")]
    public DateTime? LastAdAt { get; init; }

    /// <summary>
    /// The amount of pre-roll free time remaining for the channel in seconds. Returns 0 if they are currently not pre-roll free.
    /// </summary>
    [JsonPropertyName("preroll_free_time")]
    public int? PrerollFreeTimeSeconds { get; init; }

    /// <summary>
    /// The amount of pre-roll free time remaining for the channel. Returns <see cref="TimeSpan.Zero"/> if they are currently not pre-roll free.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? PrerollFreeTime => this.PrerollFreeTimeSeconds.HasValue ? TimeSpan.FromSeconds(this.PrerollFreeTimeSeconds.Value) : null;

    /// <summary>
    /// This endpoint returns ad schedule related information, including snooze, when the last ad was run, when the next ad is scheduled, and if the channel is currently in pre-roll free time. Note that a new ad cannot be run until 8 minutes after running a previous ad.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose ad schedule is being retrieved. This ID must match the user ID in the user access token.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="AdSchedule"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadAds"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's ad schedule.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<AdSchedule>?> GetAdSchedule(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelReadAds);

        Uri uri = Util.BuildUri(new("/channels/ads"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<AdSchedule>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// If available, pushes back the timestamp of the upcoming automatic mid-roll ad by 5 minutes. This endpoint duplicates the snooze functionality in the creator dashboard's Ads Manager.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose ads are being snoozed. This ID must match the user ID in the user access token.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="AdSchedule"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageAds"/>.</exception>
    /// <remarks>
    /// <para>
    /// When this endpoint returns, only <see cref="SnoozeCount"/>, <see cref="SnoozeRefreshAt"/>, and <see cref="NextAdAt"/> will be populated.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>User's next ad is successfully snoozed. Their <see cref="SnoozeCount"/> is decremented and <see cref="SnoozeRefreshAt"/> and <see cref="NextAdAt"/> are both updated.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The channel is not live. The channel does not have an upcoming scheduled ad break. The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description>Channel has no snoozes left.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<AdSchedule>?> SnoozeNextAd(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManageAds);

        Uri uri = Util.BuildUri(new("/channels/ads/schedule/snooze"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<AdSchedule>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
