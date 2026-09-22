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

using StreamActions.Common.Json.Serialization;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// A user is notified if their message's automod status is updated.
/// </summary>
public sealed record UserMessageUpdate : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.chat.user_message_update";

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
    /// The User ID of the message sender.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The message sender's login.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The message sender's user name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The message's status. Possible values are: approved, denied, invalid.
    /// </summary>
    [JsonPropertyName("status")]
    public MessageStatus? Status { get; init; }

    /// <summary>
    /// The ID of the message that was flagged by automod.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    /// The body of the message.
    /// </summary>
    [JsonPropertyName("message")]
    public Common.ChatMessage.Message? Message { get; init; }

    /// <summary>
    /// The message's status.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<MessageStatus>))]
    public enum MessageStatus
    {
        /// <summary>
        /// Approved.
        /// </summary>
        [JsonCustomEnum("approved")]
        Approved,

        /// <summary>
        /// Denied.
        /// </summary>
        [JsonCustomEnum("denied")]
        Denied,

        /// <summary>
        /// Invalid.
        /// </summary>
        [JsonCustomEnum("invalid")]
        Invalid
    }
}
