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
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// Sends and represents a response element for a request for a list of users in chat.
/// </summary>
public sealed record Chatter
{
    /// <summary>
    /// The ID of a user that's connected to the broadcaster's chat room.
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
    /// Gets the list of users that are connected to the broadcaster's chat session.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose list of chatters you want to get.</param>
    /// <param name="moderatorId">The ID of the broadcaster or one of the broadcaster's moderators. This ID must match the user ID in the user access token.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 1,000.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Chatter"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> or <paramref name="moderatorId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="after"/> is not null, but is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ModeratorReadChatters"/>.</exception>
    /// <remarks>
    /// <para>
    /// There is a delay between when users join and leave a chat and when the list is updated accordingly.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's list of chatters.</description>
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
    /// <description><paramref name="moderatorId"/> is not a moderator for <paramref name="broadcasterId"/>.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Chatter>?> GetChatters(TwitchSession session, string broadcasterId, string moderatorId, int first = 100, string? after = null)
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

        if (after is not null && string.IsNullOrWhiteSpace(after))
        {
            throw new ArgumentNullException(nameof(after)).Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 1000);

        session.RequireToken(Scope.ModeratorReadChatters);

        NameValueCollection queryParams = new() {
            { "broadcaster_id", broadcasterId },
            { "moderator_id", moderatorId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (after is not null)
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/chat/chatters"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Chatter>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
