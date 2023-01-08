/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Channels;

/// <summary>
/// Sends and represents a response element for a request for channel information.
/// </summary>
public sealed record Channel
{
    /// <summary>
    /// Twitch User ID of this channel owner.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// Broadcaster's user login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// Twitch user display name of this channel owner.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadasterName { get; init; }

    /// <summary>
    /// Name of the game being played on the channel.
    /// </summary>
    [JsonPropertyName("game_name")]
    public string? GameName { get; init; }

    /// <summary>
    /// Current game ID being played on the channel.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// Language of the channel. A language value is either the ISO 639-1 two-letter code for a supported stream language or "other".
    /// </summary>
    [JsonPropertyName("broadcaster_language")]
    public string? BroadcasterLanguage { get; init; }

    /// <summary>
    /// Title of the stream.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Stream delay in seconds.
    /// </summary>
    [JsonPropertyName("delay")]
    public int? Delay { get; init; }

    /// <summary>
    /// Gets channel information for users.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose channel you want to get. You may specify a maximum of 100 IDs.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Channel"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null or has 0 elements.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="broadcasterId"/> has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static async Task<ResponseData<Channel>?> GetChannelInformation(TwitchSession session, IEnumerable<string> broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        session.RequireToken();

        if (broadcasterId is null || !broadcasterId.Any())
        {
            throw new ArgumentNullException(nameof(broadcasterId));
        }

        if (broadcasterId.Count() > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(broadcasterId), "must have a count <= 100");
        }

        Uri uri = Util.BuildUri(new("/channels"), new Dictionary<string, IEnumerable<string>> { { "broadcaster_id", broadcasterId } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResponseData<Channel>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Modifies channel information for users.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">ID of the channel to be updated.</param>
    /// <param name="parameters">The <see cref="ModifyChannelParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="TwitchResponse"/> with the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageBroadcast"/>.</exception>
    public static async Task<TwitchResponse?> ModifyChannelInformation(TwitchSession session, string broadcasterId, ModifyChannelParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId));
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        session.RequireToken(Scope.ChannelManageBroadcast);

        Uri uri = Util.BuildUri(new("/channels"), new Dictionary<string, IEnumerable<string>> { { "broadcaster_id", new List<string> { broadcasterId } } });
        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, content).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TwitchResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
