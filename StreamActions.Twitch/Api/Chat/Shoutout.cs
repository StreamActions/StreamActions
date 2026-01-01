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
using System.Net.Http.Json;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Sends requests to shoutout another broadcaster.
/// </summary>
public sealed record Shoutout
{
    /// <summary>
    /// Sends a Shoutout to the specified broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="fromBroadcasterId">The ID of the broadcaster that's sending the Shoutout.</param>
    /// <param name="toBroadcasterId">The ID of the broadcaster that's receiving the Shoutout.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that is one of the broadcaster's moderators. This ID must match the user ID in the access token.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="fromBroadcasterId"/>, <paramref name="toBroadcasterId"/>, or <see cref="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageShoutouts"/>.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="fromBroadcasterId"/> must be live and have 1 or more viewers according to Twitch, otherwise, the shoutout fails with <strong>400 Bad Request</strong>.
    /// </para>
    /// <para>
    /// <strong>Rate Limits</strong>: The <paramref name="fromBroadcasterId"/> may send a Shoutout once every 2 minutes.
    /// The <paramref name="fromBroadcasterId"/> may only shoutout the same <paramref name="toBroadcasterId"/> once every 60 minutes.
    /// These rate limits are applied regardless of how a shoutout is triggered, either via API or the Twitch website.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully sent the specified broadcaster a Shoutout.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid; <paramref name="fromBroadcasterId"/> is not live; <paramref name="fromBroadcasterId"/> does not have 1 or more viewers.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description><paramref name="moderatorId"/> is not a moderator for <paramref name="fromBroadcasterId"/>; <paramref name="fromBroadcasterId"/> may not send shoutouts to <paramref name="toBroadcasterId"/>.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description><paramref name="toBroadcasterId"/> was not found.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description><paramref name="fromBroadcasterId"/> has exceeded the rate limit for sending shoutouts; <paramref name="fromBroadcasterId"/> has exceeded the rate limit for sending a shoutout to the same <paramref name="toBroadcasterId"/>.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> SendShoutout(TwitchSession session, string fromBroadcasterId, string toBroadcasterId, string moderatorId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ModeratorManageShoutouts);

        if (string.IsNullOrWhiteSpace(fromBroadcasterId))
        {
            throw new ArgumentNullException(nameof(fromBroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(toBroadcasterId))
        {
            throw new ArgumentNullException(nameof(toBroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        Uri uri = Util.BuildUri(new("/chat/shoutouts"), new() { { "from_user_id", fromBroadcasterId }, { "to_user_id", toBroadcasterId }, { "moderator_id", moderatorId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
