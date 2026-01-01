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
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Raids;

/// <summary>
/// Sends requests to start or cancel a Raid.
/// </summary>
public sealed record Raid
{
    /// <summary>
    /// The UTC date and time of when the raid was requested.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether the channel being raided contains mature content.
    /// </summary>
    public bool? IsMature { get; init; }

    /// <summary>
    /// Raid another channel by sending the broadcaster's viewers to the targeted channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="fromBroadcasterId">The ID of the broadcaster that's sending the raiding party. This ID must match the user ID in the user access token.</param>
    /// <param name="toBroadcasterId">The ID of the broadcaster to raid.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with an element of type <see cref="Raid"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="fromBroadcasterId"/> or <paramref name="toBroadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageRaids"/>.</exception>
    /// <remarks>
    /// <para>
    /// When you call the API from a chat bot or extension, the Twitch UX pops up a window at the top of the chat room that identifies the number of viewers in the raid.
    /// The raid occurs when the broadcaster clicks <strong>Raid Now</strong> or after the 90-second countdown expires.
    /// </para>
    /// <para>
    /// To determine whether the raid successfully occurred, you must subscribe to the <em>Channel Raid</em> event on EventSub.
    /// </para>
    /// <para>
    /// <strong>Rate Limits</strong>: The limit is 10 requests within a 10-minute window.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully requested to start a raid.
    /// To determine whether the raid successfully occurred (that is, the broadcaster clicked <strong>Raid Now</strong> or the countdown expired),
    /// you must subscribe to the <em>Channel Raid</em> event on EventSub.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description><list type="bullet">
    /// <item><paramref name="toBroadcasterId"/> has blocked <paramref name="fromBroadcasterId"/>.</item>
    /// <item><paramref name="toBroadcasterId"/> is not accepting raids from <paramref name="fromBroadcasterId"/>.</item>
    /// <item>There are too many viewers to start a raid.</item>
    /// <item>The described parameter was missing or invalid.</item>
    /// </list>
    /// </description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description><paramref name="toBroadcasterId"/> was not found.</description>
    /// </item>
    /// <item>
    /// <term>409 Conflict</term>
    /// <description><paramref name="fromBroadcasterId"/> is already in the process of raiding another channel.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description><paramref name="fromBroadcasterId"/> exceeded the number of raid requests that they may make.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Raid>?> StartARaid(TwitchSession session, string fromBroadcasterId, string toBroadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageRaids);

        if (string.IsNullOrWhiteSpace(fromBroadcasterId))
        {
            throw new ArgumentNullException(nameof(fromBroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(toBroadcasterId))
        {
            throw new ArgumentNullException(nameof(toBroadcasterId)).Log(TwitchApi.GetLogger());
        }

        Uri uri = Util.BuildUri(new("/raids"), new() { { "from_broadcaster_id", fromBroadcasterId }, { "to_broadcaster_id", toBroadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Raid>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel a pending raid.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that initiated the raid. This ID must match the user ID in the user access token.</param>
    /// <returns>A <see cref="JsonApiResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageRaids"/>.</exception>
    /// <remarks>
    /// <para>
    /// You can cancel a raid at any point up until the broadcaster clicks <strong>Raid Now</strong> in the Twitch UX or the 90-second countdown expires.
    /// </para>
    /// <para>
    /// <strong>Rate Limits</strong>: The limit is 10 requests within a 10-minute window.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>The pending raid was successfully canceled.</description>
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
    /// <term>404 Not Found</term>
    /// <description><paramref name="broadcasterId"/> doesn't have a pending raid to cancel.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description><paramref name="broadcasterId"/> exceeded the number of raid requests that they may make.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> CancelARaid(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageRaids);

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        Uri uri = Util.BuildUri(new("/raids"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
