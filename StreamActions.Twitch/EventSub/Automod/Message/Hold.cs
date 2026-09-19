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

namespace StreamActions.Twitch.EventSub.Automod.Message;

/// <summary>
/// An event that is sent when a message is caught by automod for review.
/// </summary>
public sealed record Hold : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "automod.message.hold";

    /// <inheritdoc/>
    public static string Version => "2";

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
    /// The message senders user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The message senders login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The message senders display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The ID of the held message.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    /// The body of the message.
    /// </summary>
    [JsonPropertyName("message")]
    public Common.ChatMessage.Message? Message { get; init; }

    /// <summary>
    /// The timestamp of when automod saved the message.
    /// </summary>
    [JsonPropertyName("held_at")]
    public DateTime? HeldAt { get; init; }

    /// <summary>
    /// The reason that the message was caught.
    /// </summary>
    [JsonPropertyName("reason")]
    public HoldReason? Reason { get; init; }

    /// <summary>
    /// Optional. If the message was caught by automod, this will be populated.
    /// </summary>
    [JsonPropertyName("automod")]
    public Automod? Automod { get; init; }

    /// <summary>
    /// Optional. If the message was caught due to a blocked term or a blocked link, this will be populated.
    /// </summary>
    [JsonPropertyName("blocked_term")]
    public BlockedTerm? BlockedTerm { get; init; }

    /// <summary>
    /// The reason why the message was caught.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<HoldReason>))]
    public enum HoldReason
    {
        /// <summary>
        /// The message was caught by automod.
        /// </summary>
        [JsonCustomEnum("automod")]
        Automod,

        /// <summary>
        /// The message was caught due to a blocked term.
        /// </summary>
        [JsonCustomEnum("blocked_term")]
        BlockedTerm,

        /// <summary>
        /// The message was caught due to a blocked link.
        /// </summary>
        [JsonCustomEnum("blocked_link")]
        BlockedLink
    }
}
