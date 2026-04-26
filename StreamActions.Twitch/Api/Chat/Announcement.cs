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
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Sends requests to transmit an announcement message to Twitch chat.
/// </summary>
public sealed record Announcement
{
    /// <summary>
    /// The announcement to make in the broadcaster's chat room.
    /// </summary>
    /// <remarks>
    /// Announcements are limited to a maximum of 500 characters; announcements longer than 500 characters are truncated.
    /// </remarks>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// The color used to highlight the announcement.
    /// </summary>
    [JsonPropertyName("color")]
    public AnnouncementColor Color { get; init; } = AnnouncementColor.Primary;

    /// <summary>
    /// Determines if the chat announcement is sent only to the source channel (defined by broadcaster_id) during a shared chat session.
    /// This has no effect if the announcement is not sent during a shared chat session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This parameter can only be set when utilizing an <see cref="TwitchToken.TokenType.App"/> Access Token. It cannot be specified when a <see cref="TwitchToken.TokenType.User"/> Access Token is used.
    /// </para>
    /// </remarks>
    [JsonPropertyName("for_source_only")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ForSourceOnly { get; init; }

    /// <summary>
    /// The color used to highlight the announcement.
    /// </summary>
    [JsonConverter(typeof(JsonLowerCaseEnumConverter<AnnouncementColor>))]
    public enum AnnouncementColor
    {
        /// <summary>
        /// The channel's accent color.
        /// </summary>
        /// <remarks>
        /// See <strong>Profile Accent Color</strong> under <em>Profile Settings</em> > <em>Channel and Videos</em> > <em>Brand</em>.
        /// </remarks>
        Primary,
        /// <summary>
        /// Blue.
        /// </summary>
        Blue,
        /// <summary>
        /// Green.
        /// </summary>
        Green,
        /// <summary>
        /// Orange.
        /// </summary>
        Orange,
        /// <summary>
        /// Purple.
        /// </summary>
        Purple
    }

    /// <summary>
    /// Sends an announcement to the broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the chat room to send the announcement to.</param>
    /// <param name="moderatorId">The ID of a user who has permission to moderate the broadcaster's chat room, or the broadcaster's ID if they're sending the announcement.</param>
    /// <param name="message">An <see cref="Announcement"/> containing the message to send and the color to use.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="message"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <see cref="Message"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ForSourceOnly"/> is not <see langword="null"/> and <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageAnnouncements"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires:
    /// <list type="bullet">
    /// <item>The app to have an authorization from <paramref name="broadcasterId"/> which includes the <see cref="Scope.ChannelBot"/> scope.</item>
    /// <item>The app to have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageAnnouncements"/> and <see cref="Scope.UserBot"/> scopes.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully sent the announcement.</description>
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<JsonApiResponse?> SendChatAnnouncement(TwitchSession session, string broadcasterId, string moderatorId, Announcement message)
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

        if (message is null || string.IsNullOrWhiteSpace(message.Message))
        {
            throw new ArgumentNullException(nameof(message)).Log(TwitchApi.GetLogger());
        }

        if (message.ForSourceOnly.HasValue && session.Token?.Type is TwitchToken.TokenType.User)
        {
            throw new ArgumentOutOfRangeException(nameof(message.ForSourceOnly), "must be null when using a user token").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageAnnouncements);

        Uri uri = Util.BuildUri(new("/chat/announcements"), new() { { "broadcaster_id", broadcasterId }, { "moderator_id", moderatorId } });
        using JsonContent jsonContent = JsonContent.Create(message, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
