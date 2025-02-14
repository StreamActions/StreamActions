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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Channels;

/// <summary>
/// Sends and represents a response element for a request for a list of channel followers.
/// </summary>
public sealed record ChannelFollower
{
    /// <summary>
    /// An ID that uniquely identifies the user.
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
    /// The UTC timestamp when the user started following the broadcaster.
    /// </summary>
    [JsonPropertyName("followed_at")]
    public DateTime? FollowedAt { get; init; }

    /// <summary>
    /// Gets a list of users that follow the specified broadcaster. You can also use this endpoint to see whether a specific user follows the broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The broadcaster's ID. Returns the list of users that follow this broadcaster. The user ID in <see cref="TwitchSession.Token"/> must match this ID or be a channel moderator.</param>
    /// <param name="userId">A user's ID. Use this parameter to see whether the user follows this broadcaster. If <see langword="null"/>, the response contains all users that follow the broadcaster.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelFollower"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="broadcasterId"/> is <see langword="null"/>; <paramref name="userId"/> or <paramref name="after"/> is not <see langword="null"/>, but is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="userId"/> is specified, the response will contain a single element
    /// in <see cref="ResponseData{TDataType}.Data"/> if <paramref name="userId"/> follows <paramref name="broadcasterId"/>,
    /// containing the follow data; if <paramref name="userId"/> is not following <paramref name="broadcasterId"/>,
    /// <see cref="ResponseData{TDataType}.Data"/> will be an empty list.
    /// </para>
    /// <para>
    /// The list is in descending order by <see cref="FollowedAt"/> (with the most recent follower first).
    /// </para>
    /// <para>
    /// If <paramref name="session"/> does not have the scope <see cref="Scope.ModeratorReadFollowers"/>, only the total follower count will be included in the response.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of followed channels.</description>
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
    public static async Task<ResponseData<ChannelFollower>?> GetChannelFollowers(TwitchSession session, string broadcasterId, string? userId = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (userId is not null && string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        if (after is not null && string.IsNullOrWhiteSpace(after))
        {
            throw new ArgumentNullException(nameof(after)).Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 100);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (userId is not null)
        {
            queryParams.Add("user_id", userId);
        }

        if (after is not null)
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/channels/followers"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChannelFollower>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
