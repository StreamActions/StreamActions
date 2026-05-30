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
using System.Globalization;
using System.Net.Http.Json;

namespace StreamActions.Twitch.Api.GuestStar;

/// <summary>
/// Provides methods to assign, update, and remove Guest Star slots.
/// </summary>
public sealed record GuestStarSlot
{
    /// <summary>
    /// Allows a previously invited user to be assigned a slot within the active Guest Star session, once that guest has indicated they are ready to join.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster running the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="sessionId">The ID of the Guest Star session in which to assign the slot.</param>
    /// <param name="guestId">The Twitch User ID corresponding to the guest to assign a slot in the session.</param>
    /// <param name="slotId">The slot assignment to give to the user.</param>
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
    /// <description>Successfully assigned guest to slot.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing required parameters or invalid session_id or slot_id.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>moderator_id is not a guest star moderator.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>Cannot assign host slot, guest not invited, already assigned, or not ready.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> AssignGuestStarSlot(TwitchSession session, string broadcasterId, string moderatorId, string sessionId, string guestId, string slotId)
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

        if (string.IsNullOrWhiteSpace(slotId))
        {
            throw new ArgumentNullException(nameof(slotId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "session_id", sessionId },
            { "guest_id", guestId },
            { "slot_id", slotId }
        };

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/guest_star/slot"), queryParams), session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Allows a user to update the assigned slot for a particular user within the active Guest Star session.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster running the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="sessionId">The ID of the Guest Star session in which to update slot settings.</param>
    /// <param name="sourceSlotId">The slot assignment previously assigned to a user.</param>
    /// <param name="destinationSlotId">The slot to move this user assignment to. If occupied, the user assigned will be swapped into source_slot_id.</param>
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
    /// <description>Successfully updated slot(s).</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing broadcaster_id, session_id, or slot_id.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> UpdateGuestStarSlot(TwitchSession session, string broadcasterId, string moderatorId, string sessionId, string sourceSlotId, string? destinationSlotId = null)
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

        if (string.IsNullOrWhiteSpace(sourceSlotId))
        {
            throw new ArgumentNullException(nameof(sourceSlotId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "session_id", sessionId },
            { "source_slot_id", sourceSlotId }
        };

        if (!string.IsNullOrWhiteSpace(destinationSlotId))
        {
            queryParams.Add("destination_slot_id", destinationSlotId);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, Util.BuildUri(new("/guest_star/slot"), queryParams), session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Allows a caller to remove a slot assignment from a user participating in an active Guest Star session.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster running the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="sessionId">The ID of the Guest Star session in which to remove the slot assignment.</param>
    /// <param name="guestId">The Twitch User ID corresponding to the guest to remove from the session.</param>
    /// <param name="slotId">The slot ID representing the slot assignment to remove from the session.</param>
    /// <param name="shouldReinviteGuest">Flag signaling that the guest should be reinvited to the session, sending them back to the invite queue.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">One or more string arguments are <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> or <see cref="TwitchToken.OAuth"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="TokenTypeException">This ID must match the user ID in the access token.</exception>
    /// <exception cref="TwitchScopeMissingException">Thrown if the token is missing the required scopes.</exception>
    /// <remarks>
    /// <para>This ID must match the user ID in the access token.</para>
    /// <para>This revokes their access to the session immediately and disables their access to publish or subscribe to media within the session.</para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully removed user from slot.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing required parameters or invalid session_id or slot_id.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>moderator_id is not a Guest Star moderator or modifying restricted slot.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>guest_id or slot_id not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> DeleteGuestStarSlot(TwitchSession session, string broadcasterId, string moderatorId, string sessionId, string guestId, string slotId, bool? shouldReinviteGuest = null)
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

        if (string.IsNullOrWhiteSpace(slotId))
        {
            throw new ArgumentNullException(nameof(slotId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "session_id", sessionId },
            { "guest_id", guestId },
            { "slot_id", slotId }
        };

        if (shouldReinviteGuest.HasValue)
        {
            queryParams.Add("should_reinvite_guest", shouldReinviteGuest.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/guest_star/slot"), queryParams), session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Allows a user to update slot settings for a particular guest within a Guest Star session.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster running the Guest Star session.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="sessionId">The ID of the Guest Star session in which to update a slot's settings.</param>
    /// <param name="slotId">The slot assignment that has previously been assigned to a user.</param>
    /// <param name="isAudioEnabled">Flag indicating whether the slot is allowed to share their audio with the rest of the session.</param>
    /// <param name="isVideoEnabled">Flag indicating whether the slot is allowed to share their video with the rest of the session.</param>
    /// <param name="isLive">Flag indicating whether the user assigned to this slot is visible/can be heard from any public subscriptions.</param>
    /// <param name="volume">Value from 0-100 that controls the audio volume for shared views containing the slot.</param>
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
    /// <description>Successfully updated slot settings.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>Missing required parameters or invalid session_id or slot_id.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>moderator_id is not a Guest Star moderator or modifying restricted slot.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> UpdateGuestStarSlotSettings(TwitchSession session, string broadcasterId, string moderatorId, string sessionId, string slotId, bool? isAudioEnabled = null, bool? isVideoEnabled = null, bool? isLive = null, int? volume = null)
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

        if (string.IsNullOrWhiteSpace(slotId))
        {
            throw new ArgumentNullException(nameof(slotId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageGuestStar, Scope.ModeratorManageGuestStar);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "session_id", sessionId },
            { "slot_id", slotId }
        };

        if (isAudioEnabled.HasValue)
        {
            queryParams.Add("is_audio_enabled", isAudioEnabled.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        if (isVideoEnabled.HasValue)
        {
            queryParams.Add("is_video_enabled", isVideoEnabled.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        if (isLive.HasValue)
        {
            queryParams.Add("is_live", isLive.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
        }

        if (volume.HasValue)
        {
            queryParams.Add("volume", volume.Value.ToString(CultureInfo.InvariantCulture));
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, Util.BuildUri(new("/guest_star/slot_settings"), queryParams), session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
