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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Streams;

/// <summary>
/// Represents markers grouped by user.
/// </summary>
public sealed record UserMarkers
{
    /// <summary>
    /// The ID of the user that created the marker.
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
    /// A list of videos that contain markers.
    /// </summary>
    [JsonPropertyName("videos")]
    public IEnumerable<VideoMarkers>? Videos { get; init; }

    /// <summary>
    /// Gets a list of markers from the user's most recent stream or from the specified VOD/video.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the user whose markers you want to get. Returns markers from this user's most recent video.</param>
    /// <param name="videoId">A video on demand (VOD)/video ID. Returns markers from this VOD/video.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="before">The cursor used to get the previous page of results.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="UserMarkers"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; both <paramref name="userId"/> and <paramref name="videoId"/> are null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if both <paramref name="userId"/> and <paramref name="videoId"/> are specified.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace; both <paramref name="before"/> and <paramref name="after"/> are specified.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserReadBroadcast"/> or <see cref="Scope.ChannelManageBroadcast"/>.</exception>
    /// <remarks>
    /// <para>
    /// The user in the access token must own the video or the user must be one of the broadcaster's editors.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of markers.</description>
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
    /// <description>The user in the access token is not authorized to get the video's markers.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The specified user doesn't have videos.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<UserMarkers>?> GetStreamMarkers(TwitchSession session, string? userId = null, string? videoId = null, int first = 20, string? before = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(videoId))
        {
            throw new ArgumentNullException(nameof(userId) + "," + nameof(videoId), "Either user ID or video ID must be provided.").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(videoId))
        {
            throw new ArgumentOutOfRangeException(nameof(userId) + "," + nameof(videoId), "The user ID and video ID parameters are mutually exclusive.").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(before) && !string.IsNullOrWhiteSpace(after))
        {
            throw new InvalidOperationException("The before and after parameters are mutually exclusive.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.UserReadBroadcast, Scope.ChannelManageBroadcast);

        NameValueCollection queryParameters = [];
        if (!string.IsNullOrWhiteSpace(userId))
        {
            queryParameters.Add("user_id", userId);
        }

        if (!string.IsNullOrWhiteSpace(videoId))
        {
            queryParameters.Add("video_id", videoId);
        }

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(before))
        {
            queryParameters.Add("before", before);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        Uri requestUri = Util.BuildUri(new("/streams/markers"), queryParameters);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, requestUri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<UserMarkers>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
