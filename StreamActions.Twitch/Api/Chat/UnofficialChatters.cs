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
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Chat
{
    /// <summary>
    /// Represents a response object containing the unofficial chatter role lists.
    /// </summary>
    public record UnofficialChatters
    {
        /// <summary>
        /// The broadcaster.
        /// </summary>
        [JsonPropertyName("broadcaster")]
        public IReadOnlyList<string>? Broadcaster { get; init; }

        /// <summary>
        /// VIPs.
        /// </summary>
        [JsonPropertyName("vips")]
        public IReadOnlyList<string>? Vips { get; init; }

        /// <summary>
        /// Moderators.
        /// </summary>
        [JsonPropertyName("moderators")]
        public IReadOnlyList<string>? Moderators { get; init; }

        /// <summary>
        /// Twitch staff.
        /// </summary>
        [JsonPropertyName("staff")]
        public IReadOnlyList<string>? Staff { get; init; }

        /// <summary>
        /// Twitch admins.
        /// </summary>
        [JsonPropertyName("admins")]
        public IReadOnlyList<string>? Admins { get; init; }

        /// <summary>
        /// Twitch global moderators.
        /// </summary>
        [JsonPropertyName("global_mods")]
        public IReadOnlyList<string>? GlobalMods { get; init; }

        /// <summary>
        /// Regular viewers.
        /// </summary>
        [JsonPropertyName("viewers")]
        public IReadOnlyList<string>? Viewers { get; init; }

        /// <summary>
        /// Retrieves the list of current chatters from the unofficial endpoint, sorted by role.
        /// </summary>
        /// <param name="channelLogin">The login name of the channel to query.</param>
        /// <param name="baseAddress">The uri to the chatters endpoint. Must include <c>{0}</c> where <paramref name="channelLogin"/> should be inserted.</param>
        /// <returns>A <see cref="UnofficialChattersResponse"/> containing the chatters.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="channelLogin"/> or <paramref name="baseAddress"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="baseAddress"/> does not contain <c>{0}</c>.</exception>
        public static async Task<UnofficialChattersResponse?> GetUnofficialChatters(string channelLogin, string baseAddress = "https://tmi.twitch.tv/group/user/{0}/chatters")
        {
            if (string.IsNullOrWhiteSpace(channelLogin))
            {
                throw new ArgumentNullException(nameof(channelLogin));
            }

            if (string.IsNullOrWhiteSpace(baseAddress))
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }

            if (!baseAddress.Contains("{0}", StringComparison.InvariantCulture))
            {
                throw new InvalidOperationException(nameof(baseAddress) + " must contain {0}");
            }

            Uri uri = Util.BuildUri(new(string.Format(CultureInfo.InvariantCulture, baseAddress, channelLogin)));
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, new() { RateLimiter = new(1, TimeSpan.FromSeconds(1)), Token = new() { OAuth = "__NEW" } }).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<UnofficialChattersResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
    }
}
