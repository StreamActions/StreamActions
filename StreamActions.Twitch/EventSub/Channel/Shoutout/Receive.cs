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

namespace StreamActions.Twitch.EventSub.Channel.Shoutout;

/// <summary>
/// A notification sent when the specified broadcaster receives a Shoutout.
/// </summary>
public sealed record Receive : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.shoutout.receive";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// An ID that identifies the broadcaster that received the Shoutout.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that sent the Shoutout.
    /// </summary>
    [JsonPropertyName("from_broadcaster_user_id")]
    public string? FromBroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("from_broadcaster_user_login")]
    public string? FromBroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("from_broadcaster_user_name")]
    public string? FromBroadcasterUserName { get; init; }

    /// <summary>
    /// The number of users that were watching the from-broadcaster's stream at the time of the Shoutout.
    /// </summary>
    [JsonPropertyName("viewer_count")]
    public int? ViewerCount { get; init; }

    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the moderator sent the Shoutout.
    /// </summary>
    [JsonPropertyName("started_at")]
    public string? StartedAt { get; init; }
}
