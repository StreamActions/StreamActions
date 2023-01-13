/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Channels;

/// <summary>
/// Sends and represents a response element for a request for the channel editors list.
/// </summary>
public sealed record ChannelEditor
{
    /// <summary>
    /// User ID of the editor.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Display name of the editor.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// Date and time the editor was given editor permissions.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// Gets a list of users who have editor permissions for a specific channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">Broadcaster's user ID associated with the channel.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelEditor"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadEditors"/>.</exception>
    public static async Task<ResponseData<ChannelEditor>?> GetChannelEditors(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId));
        }

        session.RequireToken(Scope.ChannelReadEditors);

        Uri uri = Util.BuildUri(new("/channels/editors"), new Dictionary<string, IEnumerable<string>> { { "broadcaster_id", new List<string> { broadcasterId } } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResponseData<ChannelEditor>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
