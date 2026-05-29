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

using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyParentMessageId { get; init; }

    /// <summary>
    /// Determines if the chat message is sent only to the source channel (defined by <see cref="BroadcasterId"/>) during a shared chat session.
    /// This has no effect if the message is not sent during a shared chat session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This parameter is only usable when using an <see cref="TwitchToken.TokenType.App"/> token, it MUST be set to <see langword="null"/> when using a <see cref="TwitchToken.TokenType.User"/> token.
    /// </para>
    /// <para>
    /// If this parameter is not set, the default value when using an App Access Token is <see langword="false"/>. On May 19, 2025 the default value for this parameter will be updated to <see langword="true"/>, and chat messages sent using an App Access Token will only be shared with the source channel by default.
    /// </para>
    /// </remarks>
    [JsonPropertyName("for_source_only")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ForSourceOnly { get; init; }

    /// <summary>
    /// If true, the message will be sent and immediately pinned. Default: false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cannot be combined with <see cref="ReplyParentMessageId"/> or <see cref="ForSourceOnly"/>.
    /// </para>
    /// <para>
    /// When <see cref="Pin"/> is true, additionally requires the <see cref="OAuth.Scope.ModeratorManageChatMessages"/> scope and the sender must be the broadcaster or a moderator.
    /// </para>
    /// <para>
    /// Messages pinned via this endpoint are always pinned for 20 minutes. If the pin fails, the message is not sent.
    /// </para>
    /// </remarks>
    [JsonPropertyName("pin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Pin { get; init; }

    /// <summary>
    /// Converts a given message into a <c>/me</c> message.
    /// </summary>
    /// <param name="message">The message to convert.</param>
    /// <returns>The altered message.</returns>
    public static string ConvertMessageToSlashMe(string message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message)).Log(TwitchApi.GetLogger());
        }

        if (message.StartsWith("/me "))
        {
            message = message.Substring(4);
        }

        return char.ConvertFromUtf32(1) + "ACTION " + message + char.ConvertFromUtf32(1);
    }
}
