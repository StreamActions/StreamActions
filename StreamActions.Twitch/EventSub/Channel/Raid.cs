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

namespace StreamActions.Twitch.EventSub.Channel;

/// <summary>
/// An event that is sent when a broadcaster raids another broadcaster’s channel.
/// </summary>
public sealed record Raid : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(FromToBroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.raid";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The broadcaster ID that created the raid.
    /// </summary>
    [JsonPropertyName("from_broadcaster_user_id")]
    public string? FromBroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster login that created the raid.
    /// </summary>
    [JsonPropertyName("from_broadcaster_user_login")]
    public string? FromBroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster display name that created the raid.
    /// </summary>
    [JsonPropertyName("from_broadcaster_user_name")]
    public string? FromBroadcasterUserName { get; init; }

    /// <summary>
    /// The broadcaster ID that received the raid.
    /// </summary>
    [JsonPropertyName("to_broadcaster_user_id")]
    public string? ToBroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster login that received the raid.
    /// </summary>
    [JsonPropertyName("to_broadcaster_user_login")]
    public string? ToBroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster display name that received the raid.
    /// </summary>
    [JsonPropertyName("to_broadcaster_user_name")]
    public string? ToBroadcasterUserName { get; init; }

    /// <summary>
    /// The number of viewers in the raid.
    /// </summary>
    [JsonPropertyName("viewers")]
    public int? Viewers { get; init; }
}
