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
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Users;

/// <summary>
/// Represents authorization scopes that a user has granted the application.
/// </summary>
public sealed record UserAuthorization
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// An array of all the scopes the user has granted to the client ID.
    /// </summary>
    [JsonPropertyName("scopes")]
    public IReadOnlyList<Scope>? Scopes { get; init; }

    /// <summary>
    /// Gets the authorization scopes that the specified user(s) have granted the application.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the user(s) you want to check authorization for. You may specify a maximum of 10 IDs.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="UserAuthorization"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="userId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="userId"/> has more than 10 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// This endpoint requires an <see cref="TwitchToken.TokenType.App"/> token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved user authorization.</description>
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
    public static async Task<ResponseData<UserAuthorization>?> GetAuthorizationByUser(TwitchSession session, IEnumerable<string> userId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (userId is null || !userId.Any())
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        if (userId.Count() > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), userId.Count(), "must have a count <= 10").Log(TwitchApi.GetLogger());
        }

        session.RequireAppToken();

        NameValueCollection queryParams = [];
        queryParams.Add("user_id", userId);

        Uri uri = Util.BuildUri(new("/authorization/users"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<UserAuthorization>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
