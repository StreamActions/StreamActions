/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Parameters for <see cref="SendChat.SendChatMessage(StreamActions.Twitch.Api.Common.TwitchSession, StreamActions.Twitch.Api.Chat.SendChatParameters)"/>
/// </summary>
public sealed record SendChatParameters
{
    /// <summary>
    /// The ID of the broadcaster whose chat room the message will be sent to.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The ID of the user sending the message.
    /// </summary>
    [JsonPropertyName("sender_id")]
    public string? SenderId { get; init; }

    /// <summary>
    /// The message to send. The message is limited to a maximum of 500 characters. Chat messages can also include emoticons.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// If replying to an existing chat message, the ID of the chat message being replied to.
    /// </summary>
    [JsonPropertyName("reply_parent_message_id")]
    public Guid? ReplyParentMessageId { get; init; }
}
