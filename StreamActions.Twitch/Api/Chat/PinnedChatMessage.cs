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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Represents a pinned chat message.
/// </summary>
public sealed record PinnedChatMessage
{
    /// <summary>
    /// The ID of the pinned chat message.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }

    /// <summary>
    /// The ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The ID of the user who sent the pinned message.
    /// </summary>
    [JsonPropertyName("sender_user_id")]
    public string? SenderUserId { get; init; }

    /// <summary>
    /// The login of the user who sent the pinned message.
    /// </summary>
    [JsonPropertyName("sender_user_login")]
    public string? SenderUserLogin { get; init; }

    /// <summary>
    /// The display name of the user who sent the pinned message.
    /// </summary>
    [JsonPropertyName("sender_user_name")]
    public string? SenderUserName { get; init; }

    /// <summary>
    /// The ID of the user who pinned the message.
    /// </summary>
    [JsonPropertyName("pinned_by_user_id")]
    public string? PinnedByUserId { get; init; }

    /// <summary>
    /// The login of the user who pinned the message.
    /// </summary>
    [JsonPropertyName("pinned_by_user_login")]
    public string? PinnedByUserLogin { get; init; }

    /// <summary>
    /// The display name of the user who pinned the message.
    /// </summary>
    [JsonPropertyName("pinned_by_user_name")]
    public string? PinnedByUserName { get; init; }

    /// <summary>
    /// The pinned message content.
    /// </summary>
    [JsonPropertyName("message")]
    public PinnedChatMessageContent? Message { get; init; }

    /// <summary>
    /// The UTC timestamp of when the message was pinned.
    /// </summary>
    [JsonPropertyName("starts_at")]
    public DateTime? StartsAt { get; init; }

    /// <summary>
    /// The UTC expiry timestamp. Null if pinned until stream ends.
    /// </summary>
    [JsonPropertyName("ends_at")]
    public DateTime? EndsAt { get; init; }

    /// <summary>
    /// The UTC timestamp of last update.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the currently pinned message for the broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the chat room.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="PinnedChatMessage"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; or <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageChatMessages"/> or <see cref="Scope.ModeratorReadChatMessages"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires:
    /// <list type="bullet">
    /// <item>The app to have an authorization from <paramref name="broadcasterId"/> which includes the <see cref="Scope.ChannelBot"/> scope.</item>
    /// <item>The app to have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageChatMessages"/> or <see cref="Scope.ModeratorReadChatMessages"/>, and the <see cref="Scope.UserBot"/> scopes.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved pinned message(s).</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<PinnedChatMessage>?> GetPinnedChatMessage(TwitchSession session, string broadcasterId, string moderatorId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageChatMessages, Scope.ModeratorReadChatMessages);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/chat/pins"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<PinnedChatMessage>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Pins a chat message to the specified broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the chat room.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="messageId">The ID of the message to pin.</param>
    /// <param name="durationSeconds">The number of seconds the message should be pinned for. Minimum: 30. Maximum: 1800. If not specified, the message will be pinned until the stream ends.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; or <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="messageId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageChatMessages"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires:
    /// <list type="bullet">
    /// <item>The app to have an authorization from <paramref name="broadcasterId"/> which includes the <see cref="Scope.ChannelBot"/> scope.</item>
    /// <item>The app to have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageChatMessages"/> and <see cref="Scope.UserBot"/> scopes.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully pinned the message.</description>
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
    /// <description>The user does not have permission to pin messages in this channel.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> PinChatMessage(TwitchSession session, string broadcasterId, string moderatorId, string messageId, int? durationSeconds = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentNullException(nameof(messageId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageChatMessages);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);
        queryParameters.Add("message_id", messageId);

        if (durationSeconds.HasValue)
        {
            queryParameters.Add("duration_seconds", durationSeconds.Value.ToString(CultureInfo.InvariantCulture));
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, Util.BuildUri(new("/chat/pins"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the duration of an existing pinned chat message.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the chat room.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="messageId">The ID of the pinned message to update.</param>
    /// <param name="durationSeconds">The new number of seconds the message should remain pinned, starting from now. Minimum: 30. Maximum: 1800. If not specified, the message will be pinned until the stream ends.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; or <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="messageId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageChatMessages"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires:
    /// <list type="bullet">
    /// <item>The app to have an authorization from <paramref name="broadcasterId"/> which includes the <see cref="Scope.ChannelBot"/> scope.</item>
    /// <item>The app to have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageChatMessages"/> and <see cref="Scope.UserBot"/> scopes.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully updated the pinned message.</description>
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
    /// <description>The user does not have permission to update pinned messages in this channel.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> UpdatePinnedChatMessage(TwitchSession session, string broadcasterId, string moderatorId, string messageId, int? durationSeconds = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentNullException(nameof(messageId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageChatMessages);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);
        queryParameters.Add("message_id", messageId);

        if (durationSeconds.HasValue)
        {
            queryParameters.Add("duration_seconds", durationSeconds.Value.ToString(CultureInfo.InvariantCulture));
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, Util.BuildUri(new("/chat/pins"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Unpins a pinned chat message from the specified broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the chat room.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="messageId">The ID of the message to unpin.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; or <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="messageId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageChatMessages"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires:
    /// <list type="bullet">
    /// <item>The app to have an authorization from <paramref name="broadcasterId"/> which includes the <see cref="Scope.ChannelBot"/> scope.</item>
    /// <item>The app to have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageChatMessages"/> and <see cref="Scope.UserBot"/> scopes.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully unpinned the message.</description>
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
    /// <description>The user does not have permission to unpin messages in this channel.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> UnpinChatMessage(TwitchSession session, string broadcasterId, string moderatorId, string messageId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentNullException(nameof(messageId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageChatMessages);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);
        queryParameters.Add("message_id", messageId);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/chat/pins"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
