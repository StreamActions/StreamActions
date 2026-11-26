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

using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.AdBreak;

/// <summary>
/// A midroll commercial break has started running.
/// </summary>
public sealed record Begin : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.ad_break.begin";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The length of the ad break, in seconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This can differ from the length that was requested; for example, Twitch shortens a request that's longer than the maximum allowed for the channel.
    /// </para>
    /// </remarks>
    [JsonPropertyName("duration_seconds")]
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The length of the ad break.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This can differ from the length that was requested; for example, Twitch shortens a request that's longer than the maximum allowed for the channel.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// The UTC date and time (in RFC3339 format) of when the ad break began.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This can be several seconds after the ad break was requested, and viewers may start seeing ads later than this time.
    /// </para>
    /// </remarks>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Indicates if the ad was automatically scheduled via Ads Manager.
    /// </summary>
    [JsonPropertyName("is_automatic")]
    public bool? IsAutomatic { get; init; }

    /// <summary>
    /// The broadcaster's user ID for the channel the ad was run on.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster's user login for the channel the ad was run on.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster's user display name for the channel the ad was run on.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The ID of the user that requested the ad. For automatic ads, this is the ID of the broadcaster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the ad break was synchronized from another channel during a co-streamed event, this is the ID of the channel that ran the original ad break, and <see cref="IsAutomatic"/> reflects that ad break.
    /// </para>
    /// </remarks>
    [JsonPropertyName("requester_user_id")]
    public string? RequesterUserId { get; init; }

    /// <summary>
    /// The login of the user that requested the ad.
    /// </summary>
    [JsonPropertyName("requester_user_login")]
    public string? RequesterUserLogin { get; init; }

    /// <summary>
    /// The display name of the user that requested the ad.
    /// </summary>
    [JsonPropertyName("requester_user_name")]
    public string? RequesterUserName { get; init; }
}
