/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Sends and represents a response for a request to send a message to a Twitch channels chat.
/// </summary>
public sealed record SendChat
{
    /// <summary>
    /// The message id for the message that was sent.
    /// </summary>
    [JsonPropertyName("message_id")]
    public Guid? MessageId { get; init; }

    /// <summary>
    /// If the message passed all checks and was sent.
    /// </summary>
    [JsonPropertyName("is_sent")]
    public bool? IsSent { get; init; }

    /// <summary>
    /// The reason the message was dropped, if any.
    /// </summary>
    [JsonPropertyName("drop_reason")]
    public DropReason? DropReason { get; init; }

    /// <summary>
    /// Sends a message to the broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="SendChatParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="SendChat"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><see cref="SendChatParameters.BroadcasterId"/>, <see cref="SendChatParameters.SenderId"/>, or <see cref="SendChatParameters.Message"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><see cref="SendChatParameters.ReplyParentMessageId"/> is specified and is equal to <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SendChatParameters.Message"/> contains more than 500 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SendChatParameters.ForSourceOnly"/> is not <see langword="null"/> and <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserWriteChat"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires:
    /// <list type="bullet">
    /// <item>The app to have an authorization from <see cref="SendChatParameters.SenderId"/> which includes the <see cref="Scope.UserWriteChat"/> and <see cref="Scope.UserBot"/> scopes.</item>
    /// <item>
    /// One of:
    /// <list type="bullet">
    /// <item>The app to have an authorization from <see cref="SendChatParameters.BroadcasterId"/> which includes the <see cref="Scope.ChannelBot"/> scope.</item>
    /// <item><see cref="SendChatParameters.SenderId"/> to have moderator status from <see cref="SendChatParameters.BroadcasterId"/>.</item>
    /// </list>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully sent the message to chat.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The sender is not permitted to send chat messages to the broadcaster's chat room.</description>
    /// </item>
    /// <item>
    /// <term>422 Unprocessable Entity</term>
    /// <description>The message is too large.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<SendChat>?> SendChatMessage(TwitchSession session, SendChatParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.BroadcasterId))
        {
            throw new ArgumentNullException(nameof(parameters.BroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.SenderId))
        {
            throw new ArgumentNullException(nameof(parameters.SenderId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Message))
        {
            throw new ArgumentNullException(nameof(parameters.Message)).Log(TwitchApi.GetLogger());
        }

        if (parameters.ReplyParentMessageId.HasValue && parameters.ReplyParentMessageId.Value == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(parameters.ReplyParentMessageId)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Message.Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Message), parameters.Message.Length, "length must be < 500").Log(TwitchApi.GetLogger());
        }

        if (parameters.ForSourceOnly.HasValue && session.Token?.Type is TwitchToken.TokenType.User)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.ForSourceOnly), "must be null when using a user token").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.UserWriteChat);

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/chat/messages"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<SendChat>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
