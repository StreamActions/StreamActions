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
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Moderation;

/// <summary>
/// Represents a user who has been banned or timed out from a broadcaster's chat room.
/// </summary>
public sealed record BannedUser
{
    /// <summary>
    /// The ID of the banned user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The banned user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The banned user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) of when the timeout expires, or <see langword="null"/> if the user is permanently banned.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) of when the user was banned or put in a timeout.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The reason the user was banned or put in a timeout if the moderator provided one.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// The ID of the moderator that banned the user or put them in a timeout.
    /// </summary>
    [JsonPropertyName("moderator_id")]
    public string? ModeratorId { get; init; }

    /// <summary>
    /// The moderator's login name.
    /// </summary>
    [JsonPropertyName("moderator_login")]
    public string? ModeratorLogin { get; init; }

    /// <summary>
    /// The moderator's display name.
    /// </summary>
    [JsonPropertyName("moderator_name")]
    public string? ModeratorName { get; init; }

    /// <summary>
    /// Gets all users that the broadcaster banned or put in a timeout.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose list of banned users you want to get.</param>
    /// <param name="userIds">A list of user IDs used to filter the results. Maximum of 100 IDs.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor that identifies the page of results to retrieve.</param>
    /// <param name="before">The cursor that identifies the page of results to retrieve.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="BannedUser"/> containing the response, or <see langword="null"/> if the request fails.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="broadcasterId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="userIds"/> contains more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModerationRead"/> or <see cref="Scope.ModeratorManageBannedUsers"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the <see cref="TwitchToken.OAuth"/> in <paramref name="session"/> is an <see cref="TwitchToken.TokenType.App"/> token, this endpoint additionally requires the app to have an authorization from <paramref name="broadcasterId"/> which includes the <see cref="Scope.ModerationRead"/> or <see cref="Scope.ModeratorManageBannedUsers"/> scope.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of banned users.</description>
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
    public static async Task<ResponseData<BannedUser>?> GetBannedUsers(TwitchSession session, string broadcasterId, IEnumerable<string>? userIds = null, int first = 20, string? after = null, string? before = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        List<string> userIdList = userIds?.ToList() ?? [];
        if (userIdList.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(userIds), userIdList.Count, "The total number of user IDs must not exceed 100.").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(after) && !string.IsNullOrWhiteSpace(before))
        {
            throw new InvalidOperationException("can only use one of " + nameof(before) + " or " + nameof(after)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ModerationRead, Scope.ModeratorManageBannedUsers);

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);

        if (userIdList.Count > 0)
        {
            queryParameters.Add("user_id", userIdList);
        }

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        if (!string.IsNullOrWhiteSpace(before))
        {
            queryParameters.Add("before", before);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/moderation/banned"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<BannedUser>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
