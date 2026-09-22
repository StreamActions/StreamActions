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
    /// Length in seconds of the mid-roll ad break requested.
    /// </summary>
    [JsonPropertyName("duration_seconds")]
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The UTC timestamp of when the ad break began.
    /// </summary>
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
    /// The ID of the user that requested the ad. For automatic ads, this will be the ID of the broadcaster.
    /// </summary>
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
