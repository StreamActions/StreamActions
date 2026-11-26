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
using StreamActions.Twitch.EventSub.Automod.Message.Objects;
using System.Text.Json.Serialization;
using static StreamActions.Twitch.EventSub.Automod.Message.Hold;

namespace StreamActions.Twitch.EventSub.Automod.Message;

/// <summary>
/// An event that is sent when the broadcaster or a moderator approves or denies a message that AutoMod held for review.
/// </summary>
/// <remarks>
/// <para>
/// No notification is sent when a held message expires. A message can still be denied after it expires, and that sends a notification with <see cref="Status"/> set to <see cref="MessageStatus.Denied"/>.
/// </para>
/// <para>
/// Messages that match any of the channel's private blocked terms or blocked links don't trigger a notification, even if they also match public ones.
/// </para>
/// </remarks>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "automod.message.update";

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
    /// The message sender's user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The message sender's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The message sender's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The ID of the user who approved or denied the message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the broadcaster or one of the broadcaster's moderators, and may differ from the moderator_user_id in the subscription's condition.
    /// </para>
    /// </remarks>
    [JsonPropertyName("moderator_user_id")]
    public string? ModeratorUserId { get; init; }

    /// <summary>
    /// The display name of the user who approved or denied the message.
    /// </summary>
    [JsonPropertyName("moderator_user_name")]
    public string? ModeratorUserName { get; init; }

    /// <summary>
    /// The login of the user who approved or denied the message.
    /// </summary>
    [JsonPropertyName("moderator_user_login")]
    public string? ModeratorUserLogin { get; init; }

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
    [JsonPropertyName("status")]
    public MessageStatus? Status { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) of when Twitch received the chat message. It doesn't change when the message is approved or denied.
    /// </summary>
    [JsonPropertyName("held_at")]
    public DateTime? HeldAt { get; init; }

    /// <summary>
    /// The reason why the message was caught.
    /// </summary>
    [JsonPropertyName("reason")]
    public HoldReason? Reason { get; init; }

    /// <summary>
    /// Optional. The details of the AutoMod check that caught the message. Is null unless <see cref="Reason"/> is <see cref="HoldReason.Automod"/>.
    /// </summary>
    [JsonPropertyName("automod")]
    public Objects.Automod? Automod { get; init; }

    /// <summary>
    /// Optional. The details of the blocked terms or blocked links that caught the message. Is null unless <see cref="Reason"/> is <see cref="HoldReason.BlockedTerm"/> or <see cref="HoldReason.BlockedLink"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At most one of <see cref="Automod"/> and <see cref="BlockedTerm"/> is populated, even if the message also matched the other check.
    /// </para>
    /// </remarks>
    [JsonPropertyName("blocked_term")]
    public BlockedTerm? BlockedTerm { get; init; }

    /// <summary>
    /// The status of the message.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<MessageStatus>))]
    public enum MessageStatus
    {
        /// <summary>
        /// The message was approved.
        /// </summary>
        [JsonCustomEnum("approved")]
        Approved,

        /// <summary>
        /// The message was denied.
        /// </summary>
        [JsonCustomEnum("denied")]
        Denied
    }
}
