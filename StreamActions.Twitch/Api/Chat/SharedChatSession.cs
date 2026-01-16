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
using StreamActions.Common.Exceptions;
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat;

/// <summary>
/// A shared chat session.
/// </summary>
public sealed record SharedChatSession
{
    /// <summary>
    /// The unique identifier for the shared chat session.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    /// The User ID of the host channel.
    /// </summary>
    [JsonPropertyName("host_broadcaster_id")]
    public string? HostBroadcasterId { get; init; }

    /// <summary>
    /// The list of participants in the session.
    /// </summary>
    [JsonPropertyName("participants")]
    public IReadOnlyList<SharedChatParticipant>? Participants { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) for when the session was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The UTC date and time (in RFC3339 format) for when the session was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Retrieves the active shared chat session for a channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The User ID of the channel broadcaster.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="SharedChatSession"/> containing the response. Returns an empty array if the broadcaster_id in the request isn’t in a shared chat session.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.App"/> or <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the shared chat session.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The ID in the broadcaster_id query parameter is not valid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<SharedChatSession>?> GetSharedChatSession(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        Uri uri = Util.BuildUri(new("/chat/shared_chat/session"), new() { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<SharedChatSession>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
