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

namespace StreamActions.Twitch.EventSub.Channel.SharedChat;

/// <summary>
/// An event that is sent when the active shared chat session the channel is in changes.
/// </summary>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.shared_chat.update";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The unique identifier for the shared chat session.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    /// The User ID of the channel in the subscription condition.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The display name of the channel in the subscription condition.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The user login of the channel in the subscription condition.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The User ID of the host channel.
    /// </summary>
    [JsonPropertyName("host_broadcaster_user_id")]
    public string? HostBroadcasterUserId { get; init; }

    /// <summary>
    /// The display name of the host channel.
    /// </summary>
    [JsonPropertyName("host_broadcaster_user_name")]
    public string? HostBroadcasterUserName { get; init; }

    /// <summary>
    /// The user login of the host channel.
    /// </summary>
    [JsonPropertyName("host_broadcaster_user_login")]
    public string? HostBroadcasterUserLogin { get; init; }

    /// <summary>
    /// The list of participants in the session.
    /// </summary>
    [JsonPropertyName("participants")]
    public IReadOnlyList<Participant>? Participants { get; init; }
}
