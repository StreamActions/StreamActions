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
using StreamActions.Twitch.API.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.API.Channels
{
    /// <summary>
    /// Sends and represents a response element for a request for the channel editors list.
    /// </summary>
    public record ChannelEditor
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
        /// Date and time the editor was given editor permissions as a string.
        /// </summary>
        [JsonPropertyName("created_at")]
        public string? CreatedAtString { get; init; }

        /// <summary>
        /// Date and time the editor was given editor permissions.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? CreatedAt => Util.ISO8601ToDateTime(this.CreatedAtString ?? "");

        /// <summary>
        /// Gets a list of users who have editor permissions for a specific channel.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="broadcasterId">Broadcaster’s user ID associated with the channel.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelEditor"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="session"/> contains a scope list, and <c>channel:read:editors</c> is not present.</exception>
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

            if (!session.Token?.HasScope("channel:read:editors") ?? false)
            {
                throw new InvalidOperationException("Missing scope channel:read:editors.");
            }

            Uri uri = Util.BuildUri(new("/channels/editors"), new Dictionary<string, string> { { "broadcaster_id", broadcasterId } });
            HttpResponseMessage response = await TwitchAPI.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<ChannelEditor>>().ConfigureAwait(false);
        }
    }
}
