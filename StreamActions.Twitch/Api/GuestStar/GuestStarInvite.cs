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
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.GuestStar;

/// <summary>
/// Status representing the invited user's join state.
/// </summary>
[JsonConverter(typeof(JsonCustomEnumConverter<GuestStarInviteStatus>))]
public enum GuestStarInviteStatus
{
    /// <summary>
    /// The user has been invited to the session but has not acknowledged it.
    /// </summary>
    [JsonCustomEnum("INVITED")]
    Invited,

    /// <summary>
    /// The invited user has acknowledged the invite and joined the waiting room, but may still be setting up their media devices or otherwise preparing to join the call.
    /// </summary>
    [JsonCustomEnum("ACCEPTED")]
    Accepted,

    /// <summary>
    /// The invited user has signaled they are ready to join the call from the waiting room.
    /// </summary>
    [JsonCustomEnum("READY")]
    Ready
}

/// <summary>
/// Represents a pending invite to a Guest Star session, including the invitee's ready status while joining the waiting room.
/// </summary>
public sealed record GuestStarInvite
{
    /// <summary>
    /// Twitch User ID corresponding to the invited guest.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Timestamp when this user was invited to the session.
    /// </summary>
    [JsonPropertyName("invited_at")]
    public DateTime? InvitedAt { get; init; }

    /// <summary>
    /// Status representing the invited user's join state.
    /// </summary>
    [JsonPropertyName("status")]
    public GuestStarInviteStatus? Status { get; init; }

    /// <summary>
    /// Flag signaling that the invited user has chosen to disable their local video device.
    /// </summary>
    [JsonPropertyName("is_video_enabled")]
    public bool? IsVideoEnabled { get; init; }

    /// <summary>
    /// Flag signaling that the invited user has chosen to disable their local audio device.
    /// </summary>
    [JsonPropertyName("is_audio_enabled")]
    public bool? IsAudioEnabled { get; init; }

    /// <summary>
    /// Flag signaling that the invited user has a video device available for sharing.
    /// </summary>
    [JsonPropertyName("is_video_available")]
    public bool? IsVideoAvailable { get; init; }

    /// <summary>
    /// Flag signaling that the invited user has an audio device available for sharing.
    /// </summary>
    [JsonPropertyName("is_audio_available")]
    public bool? IsAudioAvailable { get; init; }

    /// <summary>
    /// Provides the caller with a list of pending invites to a Guest Star session.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster running the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="sessionId">The session ID to query for invite status.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="GuestStarInvite"/> containing the response, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="sessionId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the invites.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing broadcaster_id or session_id.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<GuestStarInvite>?> GetGuestStarInvites(TwitchSession session, string broadcasterId, string moderatorId, string sessionId)
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

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentNullException(nameof(sessionId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelReadGuestStar, Scope.ChannelManageGuestStar, Scope.ModeratorReadGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "session_id", sessionId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/guest_star/invites"), queryParams), session).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ResponseData<GuestStarInvite>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }

        JsonApiResponse? error = await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        TwitchApi.GetLogger().Warning($"Failed to get guest star invites: {response.StatusCode} {error?.Message}");

        return null;
    }

    /// <summary>
    /// Sends an invite to a specified guest on behalf of the broadcaster for a Guest Star session in progress.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster running the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="sessionId">The session ID for the invite to be sent on behalf of the broadcaster.</param>
    /// <param name="guestId">Twitch User ID for the guest to invite to the Guest Star session.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">One or more string arguments are <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully sent the invite.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing required parameters or invalid session_id.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>Unauthorized guest invited or guest already invited.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> SendGuestStarInvite(TwitchSession session, string broadcasterId, string moderatorId, string sessionId, string guestId)
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

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentNullException(nameof(sessionId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(guestId))
        {
            throw new ArgumentNullException(nameof(guestId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "session_id", sessionId },
            { "guest_id", guestId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/guest_star/invites"), queryParams), session).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new JsonApiResponse { Status = response.StatusCode };
        }

        JsonApiResponse? error = await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        TwitchApi.GetLogger().Warning($"Failed to send guest star invite: {response.StatusCode} {error?.Message}");

        return error ?? new JsonApiResponse { Status = response.StatusCode, Message = "Unknown error" };
    }

    /// <summary>
    /// Revokes a previously sent invite for a Guest Star session.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster running the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="sessionId">The ID of the session for the invite to be revoked on behalf of the broadcaster.</param>
    /// <param name="guestId">Twitch User ID for the guest to revoke the Guest Star session invite from.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">One or more string arguments are <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully revoked the invite.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing required parameters or invalid session_id.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>No invite exists for specified guest_id.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> DeleteGuestStarInvite(TwitchSession session, string broadcasterId, string moderatorId, string sessionId, string guestId)
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

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentNullException(nameof(sessionId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(guestId))
        {
            throw new ArgumentNullException(nameof(guestId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "session_id", sessionId },
            { "guest_id", guestId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/guest_star/invites"), queryParams), session).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new JsonApiResponse { Status = response.StatusCode };
        }

        JsonApiResponse? error = await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        TwitchApi.GetLogger().Warning($"Failed to delete guest star invite: {response.StatusCode} {error?.Message}");

        return error ?? new JsonApiResponse { Status = response.StatusCode, Message = "Unknown error" };
    }
}
