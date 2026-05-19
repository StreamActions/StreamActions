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
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents a response from banning or timing out a user.
/// </summary>
public sealed record Ban
{
    /// <summary>
    /// The broadcaster whose chat room the user was banned from chatting in.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The moderator that banned or put the user in the timeout.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// The user that was banned or put in a timeout.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) that the ban or timeout was placed.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) that the timeout will end. Is <see langword="null"/> if the user was banned instead of being put in a timeout.
    /// </summary>
    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Bans a user from participating in a broadcaster's chat room or puts them in a timeout.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose chat room the user is being banned from.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="parameters">The <see cref="BanUserParameters"/> identifying the user and type of ban.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Ban"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentException"><paramref name="parameters"/> data is null or <see cref="BanUserData.UserId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="BanUserData.DurationSeconds"/> is out of range; <see cref="BanUserData.Reason"/> is too long.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageBannedUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageBannedUsers"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully banned the user or placed them in a timeout.</description>
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
    /// <description>The user in <paramref name="moderatorId"/> is not one of the broadcaster's moderators.</description>
    /// </item>
    /// <item>
    /// <term>409 Conflict</term>
    /// <description>The user's ban state is currently being updated by someone else.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description>The app has exceeded the number of requests it may make per minute for this broadcaster.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Ban>?> BanUser(TwitchSession session, string broadcasterId, string moderatorId, BanUserParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Data is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Data.UserId))
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Data.DurationSeconds.HasValue && (parameters.Data.DurationSeconds < 1 || parameters.Data.DurationSeconds > 1209600))
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), parameters.Data.DurationSeconds, "duration must be between 1 and 1,209,600 seconds.").Log(TwitchApi.GetLogger());
        }

        if (parameters.Data.Reason is not null && parameters.Data.Reason.Length > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), parameters.Data.Reason.Length, "reason must be 500 characters or less.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageBannedUsers);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);

        using JsonContent jsonContent = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, Util.BuildUri(new("/moderation/bans"), queryParameters), session, jsonContent).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Ban>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the ban or timeout that was placed on the specified user.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose chat room the user is banned from chatting in.</param>
    /// <param name="moderatorId">The ID of the broadcaster or a user that has permission to moderate the broadcaster's chat room.</param>
    /// <param name="userId">The ID of the user to remove the ban or timeout from.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="moderatorId"/>, or <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorManageBannedUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="moderatorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// App token specific scope or moderator requirements: the app must have an authorization from <paramref name="moderatorId"/> which includes the <see cref="Scope.ModeratorManageBannedUsers"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully removed the ban or timeout.</description>
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
    /// <description>The user in <paramref name="moderatorId"/> is not one of the broadcaster's moderators.</description>
    /// </item>
    /// <item>
    /// <term>409 Conflict</term>
    /// <description>The user's ban state is currently being updated by someone else.</description>
    /// </item>
    /// <item>
    /// <term>429 Too Many Requests</term>
    /// <description>The app has exceeded the number of requests it may make per minute for this broadcaster.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> UnbanUser(TwitchSession session, string broadcasterId, string moderatorId, string userId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ArgumentNullException(nameof(moderatorId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModeratorManageBannedUsers);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);
        queryParameters.Add("moderator_id", moderatorId);
        queryParameters.Add("user_id", userId);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, Util.BuildUri(new("/moderation/bans"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
