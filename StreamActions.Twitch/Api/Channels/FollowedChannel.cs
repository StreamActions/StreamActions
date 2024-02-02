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
using System.Net.Http.Json;
using StreamActions.Common.Extensions;
using System.Globalization;
using System.Collections.Specialized;

namespace StreamActions.Twitch.Api.Channels;

/// <summary>
/// Sends and represents a response element for a request for a list of followed channels.
/// </summary>
public sealed record FollowedChannel
{
    /// <summary>
    /// An ID that uniquely identifies the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadasterName { get; init; }

    /// <summary>
    /// The UTC timestamp when the user started following the broadcaster.
    /// </summary>
    [JsonPropertyName("followed_at")]
    public DateTime? FollowedAt { get; init; }

    /// <summary>
    /// Gets a list of broadcasters that the specified user follows. You can also use this endpoint to see whether a user follows a specific broadcaster.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">A user's ID. Returns the list of broadcasters that this user follows. This ID must match the user ID in <see cref="TwitchSession.Token"/>.</param>
    /// <param name="broadcasterId">A broadcaster's ID. Use this parameter to see whether the user follows this broadcaster. If <see langword="null"/>, the response contains all broadcasters that the user follows.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="FollowedChannel"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="userId"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="after"/> is not <see langword="null"/>, but is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserReadFollows"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="broadcasterId"/> is specified, the response will contain a single element
    /// in <see cref="ResponseData{TDataType}.Data"/> if <paramref name="userId"/> follows <paramref name="broadcasterId"/>,
    /// containing the follow data; if <paramref name="userId"/> is not following <paramref name="broadcasterId"/>,
    /// <see cref="ResponseData{TDataType}.Data"/> will be an empty list.
    /// </para>
    /// <para>
    /// The list is in descending order by <see cref="FollowedAt"/> (with the most recently followed broadcaster first).
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
    public static async Task<ResponseData<FollowedChannel>?> GetFollowedChannels(TwitchSession session, string userId, string? broadcasterId = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.UserReadFollows);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        if (broadcasterId is not null && string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (after is not null && string.IsNullOrWhiteSpace(after))
        {
            throw new ArgumentNullException(nameof(after)).Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 100);

        NameValueCollection queryParams = new()
        {
            { "user_id", userId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (broadcasterId is not null)
        {
            queryParams.Add("broadcaster_id", broadcasterId);
        }

        if (after is not null)
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/channels/followed"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<FollowedChannel>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
