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

using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Ingest
{
    /// <summary>
    /// Sends and represents a response element for a request for ingest servers.
    /// </summary>
    public record Ingest
    {
        /// <summary>
        /// Sequential identifier of ingest server.
        /// </summary>
        [JsonPropertyName("_id")]
        public int? Id { get; init; }

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        [JsonPropertyName("availability")]
        public float? Availability { get; init; }

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        [JsonPropertyName("default")]
        public bool? IsDefault { get; init; }

        /// <summary>
        /// Descriptive name of ingest server.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        /// <summary>
        /// RTMP URL template for ingest server. Replace <c>{stream_key}</c> with the stream key.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "API Definition")]
        [JsonPropertyName("url_template")]
        public string? UrlTemplate { get; init; }

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        [JsonPropertyName("priority")]
        public int? Priority { get; init; }

        /// <summary>
        /// Get Ingest Servers returns a list of endpoints for ingesting live video into Twitch.
        /// </summary>
        /// <param name="baseAddress">The uri to the Ingests endpoint.</param>
        /// <returns>A <see cref="IngestResponse"/> containing the response data.</returns>
        public static async Task<IngestResponse?> GetIngestServers(string baseAddress = "https://ingest.twitch.tv/ingests")
        {
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new(baseAddress), new() { RateLimiter = new(1, 1), Token = new() { OAuth = "__NEW" } }).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<IngestResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
    }
}
