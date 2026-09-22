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
/// Any user sends a message to a specific chat room.
/// </summary>
public sealed record Message : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.chat.message";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The broadcaster user ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The user ID of the user that sent the message.
    /// </summary>
    [JsonPropertyName("chatter_user_id")]
    public string? ChatterUserId { get; init; }

    /// <summary>
    /// The user login of the user that sent the message.
    /// </summary>
    [JsonPropertyName("chatter_user_login")]
    public string? ChatterUserLogin { get; init; }

    /// <summary>
    /// The user name of the user that sent the message.
    /// </summary>
    [JsonPropertyName("chatter_user_name")]
    public string? ChatterUserName { get; init; }

    /// <summary>
    /// A UUID that identifies the message.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    /// The structured chat message.
    /// </summary>
    [JsonPropertyName("message")]
    public Common.ChatMessage.Message? ChatMessage { get; init; }

    /// <summary>
    /// The type of message.
    /// </summary>
    [JsonPropertyName("message_type")]
    public MessageType? TypeOfMessage { get; init; }

    /// <summary>
    /// List of chat badges.
    /// </summary>
    [JsonPropertyName("badges")]
    public IEnumerable<Badge>? Badges { get; init; }

    /// <summary>
    /// Optional. Metadata if this message is a cheer.
    /// </summary>
    [JsonPropertyName("cheer")]
    public Cheer? Cheer { get; init; }

    /// <summary>
    /// The color of the user's name in the chat room.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>
    /// Optional. Metadata if this message is a reply.
    /// </summary>
    [JsonPropertyName("reply")]
    public Reply? Reply { get; init; }

    /// <summary>
    /// Optional. The ID of a channel points custom reward that was redeemed.
    /// </summary>
    [JsonPropertyName("channel_points_custom_reward_id")]
    public string? ChannelPointsCustomRewardId { get; init; }

    /// <summary>
    /// Optional. The broadcaster user ID of the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_id")]
    public string? SourceBroadcasterUserId { get; init; }

    /// <summary>
    /// Optional. The login of the broadcaster of the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_login")]
    public string? SourceBroadcasterUserLogin { get; init; }

    /// <summary>
    /// Optional. The user name of the broadcaster of the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_broadcaster_user_name")]
    public string? SourceBroadcasterUserName { get; init; }

    /// <summary>
    /// Optional. The UUID that identifies the source message from the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_message_id")]
    public string? SourceMessageId { get; init; }

    /// <summary>
    /// Optional. The list of chat badges for the chatter in the channel the message was sent from.
    /// </summary>
    [JsonPropertyName("source_badges")]
    public IEnumerable<Badge>? SourceBadges { get; init; }

    /// <summary>
    /// Optional. Determines if a message delivered during a shared chat session is only sent to the source channel.
    /// </summary>
    [JsonPropertyName("is_source_only")]
    public bool? IsSourceOnly { get; init; }

    /// <summary>
    /// The type of message.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<MessageType>))]
    public enum MessageType
    {
        /// <summary>
        /// Text message.
        /// </summary>
        [JsonCustomEnum("text")]
        Text,

        /// <summary>
        /// Channel points highlighted message.
        /// </summary>
        [JsonCustomEnum("channel_points_highlighted")]
        ChannelPointsHighlighted,

        /// <summary>
        /// Channel points sub only message.
        /// </summary>
        [JsonCustomEnum("channel_points_sub_only")]
        ChannelPointsSubOnly,

        /// <summary>
        /// User intro message.
        /// </summary>
        [JsonCustomEnum("user_intro")]
        UserIntro,

        /// <summary>
        /// Power ups message effect message.
        /// </summary>
        [JsonCustomEnum("power_ups_message_effect")]
        PowerUpsMessageEffect,

        /// <summary>
        /// Power ups gigantified emote message.
        /// </summary>
        [JsonCustomEnum("power_ups_gigantified_emote")]
        PowerUpsGigantifiedEmote
    }
}
