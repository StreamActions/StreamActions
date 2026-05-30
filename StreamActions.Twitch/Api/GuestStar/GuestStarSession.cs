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
using StreamActions.Common.Net;

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.GuestStar;

/// <summary>
/// Summary of the Guest Star session details.
/// </summary>
public sealed record GuestStarSession
{
    /// <summary>
    /// ID uniquely representing the Guest Star session.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// List of guests currently interacting with the Guest Star session.
    /// </summary>
    [JsonPropertyName("guests")]
    public IReadOnlyList<GuestStarGuest>? Guests { get; init; }

    /// <summary>
    /// Gets information about an ongoing Guest Star session for a particular channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">ID for the user hosting the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="GuestStarSession"/> containing the response, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>Guests must be either invited or assigned a slot within the session.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the session.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing broadcaster_id or moderator_id.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>moderator_id and user token do not match.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<GuestStarSession>?> GetGuestStarSession(TwitchSession session, string broadcasterId, string moderatorId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelReadGuestStar, Scope.ChannelManageGuestStar, Scope.ModeratorReadGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/guest_star/session"), queryParams), session).ConfigureAwait(false);

            return await response.Content.ReadFromJsonAsync<ResponseData<GuestStarSession>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Programmatically creates a Guest Star session on behalf of the broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster you want to create a Guest Star session for.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="GuestStarSession"/> containing the response, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>Requires the broadcaster to be present in the call interface, or the call will be ended automatically.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully created the session.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing broadcaster_id or session limit reached (1 active call).</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>Phone verification missing.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>Insufficient authorization for creating session.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<GuestStarSession>?> CreateGuestStarSession(TwitchSession session, string broadcasterId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/guest_star/session"), queryParams), session).ConfigureAwait(false);

            return await response.Content.ReadFromJsonAsync<ResponseData<GuestStarSession>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Programmatically ends a Guest Star session on behalf of the broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster you want to end a Guest Star session for.</param>
    /// <param name="sessionId">ID for the session to end on behalf of the broadcaster.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="GuestStarSession"/> containing the response, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/> or <paramref name="sessionId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>Performs the same action as if the host clicked the "End Call" button in the Guest Star UI.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully ended the session.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing or invalid broadcaster_id, missing or invalid session_id, or session has already been ended.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>Insufficient authorization for ending session.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<GuestStarSession>?> EndGuestStarSession(TwitchSession session, string broadcasterId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentNullException(nameof(sessionId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "session_id", sessionId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/guest_star/session"), queryParams), session).ConfigureAwait(false);

            return await response.Content.ReadFromJsonAsync<ResponseData<GuestStarSession>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
