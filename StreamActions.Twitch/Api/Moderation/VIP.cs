/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;
using StreamActions.Common.Extensions;
using System.Globalization;
using System.Collections.Specialized;
using StreamActions.Common.Net;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Sends and represents a response element for a request to update or retrieve a list of channel VIPs.
/// </summary>
public sealed record VIP
{
    /// <summary>
    /// An ID that uniquely identifies the VIP user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// Gets a list of the broadcaster's VIPs.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose list of VIPs you want to get. This ID must match the user ID in the access token.</param>
    /// <param name="userId">Filters the list for specific VIPs. Limit: 100.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="VIP"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="after"/> is not null, but is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadVips"/> or <see cref="Scope.ChannelManageVideos"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's list of VIPs.</description>
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
    public static async Task<ResponseData<VIP>?> GetVIPs(TwitchSession session, string broadcasterId, IEnumerable<string>? userId = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (userId is not null && userId.Count() > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), userId.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
        }

        if (after is not null && string.IsNullOrWhiteSpace(after))
        {
            throw new ArgumentNullException(nameof(after)).Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 100);

        session.RequireToken(Scope.ChannelReadVips, Scope.ChannelManageVips);

        NameValueCollection queryParams = new() {
            { "broadcaster_id", broadcasterId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (userId is not null && userId.Any())
        {
            queryParams.Add("user_id", userId);
        }

        if (after is not null)
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/channels/vips"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<VIP>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the specified user as a VIP in the broadcaster's channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that's adding the user as a VIP. This ID must match the user ID in the access token.</param>
    /// <param name="userId">The ID of the user to give VIP status to.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageVips"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Rate Limits</b>: The broadcaster may add a maximum of 10 VIPs within a 10-second window.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully added the VIP.</description>
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
    /// <description><paramref name="broadcasterId"/> or <paramref name="userId"/> does not exist.</description>
    /// </item>
    /// <item>
    /// <term>409 Conflict</term>
    /// <description>The broadcaster doesn't have available VIP slots.</description>
    /// </item>
    /// <item>
    /// <term>422 Unprocessable Entity</term>
    /// <description><paramref name="userId"/> can not be added as a VIP because they are already a VIP or channel moderator.</description>
    /// </item>
    /// <item>
    /// <term>425 Too Early</term>
    /// <description><paramref name="broadcasterId"/> has not completed the <i>Build a Community</i> achievement in the Creator Dashboard.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description><paramref name="broadcasterId"/> has exceeded the rate limit for this endpoint.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> AddChannelVIP(TwitchSession session, string broadcasterId, string userId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }


        session.RequireToken(Scope.ChannelManageVips);

        NameValueCollection queryParams = new() {
            { "broadcaster_id", broadcasterId },
            { "user_id", userId }
        };

        Uri uri = Util.BuildUri(new("/channels/vips"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the specified user as a VIP in the broadcaster's channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster who owns the channel where the user has VIP status.</param>
    /// <param name="userId">The ID of the user to remove VIP status from.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageVips"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the broadcaster is removing the user's VIP status, the ID in the <paramref name="broadcasterId"/> parameter must match the user ID in the access token;
    /// otherwise, if the user is removing their VIP status themselves, the ID in the <paramref name="userId"/> parameter must match the user ID in the access token.
    /// </para>
    /// <para>
    /// <b>Rate Limits</b>: The broadcaster may remove a maximum of 10 VIPs within a 10-second window.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully removed the VIP status from the user.</description>
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
    /// <description>The <paramref name="broadcasterId"/> does not have permission to remove VIP status from <paramref name="userId"/>.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description><paramref name="broadcasterId"/> or <paramref name="userId"/> does not exist.</description>
    /// </item>
    /// <item>
    /// <term>422 Unprocessable Entity</term>
    /// <description><paramref name="userId"/> is not a VIP in the broadcaster's channel.</description>
    /// </item>
    /// <item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description><paramref name="broadcasterId"/> has exceeded the rate limit for this endpoint.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> RemoveChannelVIP(TwitchSession session, string broadcasterId, string userId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }


        session.RequireToken(Scope.ChannelManageVips);

        NameValueCollection queryParams = new() {
            { "broadcaster_id", broadcasterId },
            { "user_id", userId }
        };

        Uri uri = Util.BuildUri(new("/channels/vips"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
