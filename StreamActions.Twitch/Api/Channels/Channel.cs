/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using StreamActions.Common;
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Channels
{
    /// <summary>
    /// Sends and represents a response element for a request for channel information.
    /// </summary>
    public record Channel
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
        /// Language of the channel. A language value is either the ISO 639-1 two-letter code for a supported stream language or “other”.
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
        /// <param name="broadcasterId">User ID for the channel.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Channel"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
        public static async Task<ResponseData<Channel>?> GetChannelInformation(TwitchSession session, string broadcasterId)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            Uri uri = Util.BuildUri(new("/channels"), new Dictionary<string, IEnumerable<string>> { { "broadcaster_id", new List<string>() { broadcasterId } } });
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<Channel>>().ConfigureAwait(false);
        }

        /// <summary>
        /// Modifies channel information for users.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="broadcasterId">ID of the channel to be updated.</param>
        /// <param name="parameters">The <see cref="ModifyChannelParameters"/> with the request parameters.</param>
        /// <returns>A <see cref="TwitchResponse"/> with the response code.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>channel:manage:broadcast</c> is not present.</exception>
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

            if (!session.Token?.HasScope("channel:manage:broadcast") ?? false)
            {
                throw new ScopeMissingException("channel:manage:broadcast");
            }

            Uri uri = Util.BuildUri(new("/channels"), new Dictionary<string, IEnumerable<string>> { { "broadcaster_id", new List<string> { broadcasterId } } });
            using JsonContent content = JsonContent.Create(parameters);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, content).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<TwitchResponse>().ConfigureAwait(false);
        }
    }
}
