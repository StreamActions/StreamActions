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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// Metadata if this message is a reply.
/// </summary>
public sealed record Reply
{
    /// <summary>
    /// An ID that uniquely identifies the parent message that this message is replying to.
    /// </summary>
    [JsonPropertyName("parent_message_id")]
    public string? ParentMessageId { get; init; }

    /// <summary>
    /// The message body of the parent message.
    /// </summary>
    [JsonPropertyName("parent_message_body")]
    public string? ParentMessageBody { get; init; }

    /// <summary>
    /// User ID of the sender of the parent message.
    /// </summary>
    [JsonPropertyName("parent_user_id")]
    public string? ParentUserId { get; init; }

    /// <summary>
    /// User name of the sender of the parent message.
    /// </summary>
    [JsonPropertyName("parent_user_name")]
    public string? ParentUserName { get; init; }

    /// <summary>
    /// User login of the sender of the parent message.
    /// </summary>
    [JsonPropertyName("parent_user_login")]
    public string? ParentUserLogin { get; init; }

    /// <summary>
    /// An ID that identifies the parent message of the reply thread.
    /// </summary>
    [JsonPropertyName("thread_message_id")]
    public string? ThreadMessageId { get; init; }

    /// <summary>
    /// User ID of the sender of the thread's parent message.
    /// </summary>
    [JsonPropertyName("thread_user_id")]
    public string? ThreadUserId { get; init; }

    /// <summary>
    /// User name of the sender of the thread's parent message.
    /// </summary>
    [JsonPropertyName("thread_user_name")]
    public string? ThreadUserName { get; init; }

    /// <summary>
    /// User login of the sender of the thread's parent message.
    /// </summary>
    [JsonPropertyName("thread_user_login")]
    public string? ThreadUserLogin { get; init; }
}
