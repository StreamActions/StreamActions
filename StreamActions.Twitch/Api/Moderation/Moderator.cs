﻿/*
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
/// Sends and represents a response element for a request to update or retrieve a list of channel moderators.
/// </summary>
public sealed record Moderator
{
    /// <summary>
    /// The ID of the user that has permission to moderate the broadcaster's channel.
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
    /// Gets all users allowed to moderate the broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose list of moderators you want to get. This ID must match the user ID in the access token.</param>
    /// <param name="userId">A list of user IDs used to filter the results. Limit: 100.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Moderator"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="after"/> is not null, but is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModerationRead"/> or <see cref="Scope.ChannelManageModerators"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of moderators.</description>
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
    public static async Task<ResponseData<Moderator>?> GetModerators(TwitchSession session, string broadcasterId, IEnumerable<string>? userId = null, int first = 20, string? after = null)
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

        session.RequireToken(Scope.ModerationRead, Scope.ChannelManageModerators);

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

        Uri uri = Util.BuildUri(new("/moderation/moderators"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Moderator>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a moderator to the broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the chat room. This ID must match the user ID in the access token.</param>
    /// <param name="userId">The ID of the user to add as a moderator in the broadcaster's chat room.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageModerators"/>.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Rate Limits</strong>: The broadcaster may add a maximum of 10 moderators within a 10-second window.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully added the moderator.</description>
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
    /// <term>422 Unprocessable Entity</term>
    /// <description><paramref name="userId"/> is a VIP. To make them a moderator, you must first remove them as a VIP.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description><paramref name="broadcasterId"/> has exceeded the rate limit for this endpoint.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> AddChannelModerator(TwitchSession session, string broadcasterId, string userId)
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


        session.RequireToken(Scope.ChannelManageModerators);

        NameValueCollection queryParams = new() {
            { "broadcaster_id", broadcasterId },
            { "user_id", userId }
        };

        Uri uri = Util.BuildUri(new("/moderation/moderators"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a moderator to the broadcaster's chat room.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that owns the chat room. This ID must match the user ID in the access token.</param>
    /// <param name="userId">The ID of the user to remove as a moderator in the broadcaster's chat room.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageModerators"/>.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Rate Limits</strong>: The broadcaster may remove a maximum of 10 moderators within a 10-second window.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully removed the moderator.</description>
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
    /// <term>429 Too Many Requests</term>
    /// <description><paramref name="broadcasterId"/> has exceeded the rate limit for this endpoint.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> RemoveChannelModerator(TwitchSession session, string broadcasterId, string userId)
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


        session.RequireToken(Scope.ChannelManageModerators);

        NameValueCollection queryParams = new() {
            { "broadcaster_id", broadcasterId },
            { "user_id", userId }
        };

        Uri uri = Util.BuildUri(new("/moderation/moderators"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
