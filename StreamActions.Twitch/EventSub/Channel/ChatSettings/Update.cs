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

namespace StreamActions.Twitch.EventSub.Channel.ChatSettings;

/// <summary>
/// A notification for when a broadcaster's chat settings are updated.
/// </summary>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.chat_settings.update";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The ID of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The login of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The user name of the broadcaster specified in the request.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// A Boolean value that determines whether chat messages must contain only emotes.
    /// </summary>
    [JsonPropertyName("emote_mode")]
    public bool? EmoteMode { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster restricts the chat room to followers only.
    /// </summary>
    [JsonPropertyName("follower_mode")]
    public bool? FollowerMode { get; init; }

    /// <summary>
    /// The length of time, in minutes, that the followers must have followed the broadcaster to participate in the chat room.
    /// </summary>
    [JsonPropertyName("follower_mode_duration_minutes")]
    public int? FollowerModeDurationMinutes { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster limits how often users in the chat room are allowed to send messages.
    /// </summary>
    [JsonPropertyName("slow_mode")]
    public bool? SlowMode { get; init; }

    /// <summary>
    /// The amount of time, in seconds, that users need to wait between sending messages.
    /// </summary>
    [JsonPropertyName("slow_mode_wait_time_seconds")]
    public int? SlowModeWaitTimeSeconds { get; init; }

    /// <summary>
    /// A Boolean value that determines whether only users that subscribe to the broadcaster's channel can talk in the chat room.
    /// </summary>
    [JsonPropertyName("subscriber_mode")]
    public bool? SubscriberMode { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster requires users to post only unique messages in the chat room.
    /// </summary>
    [JsonPropertyName("unique_chat_mode")]
    public bool? UniqueChatMode { get; init; }
}
