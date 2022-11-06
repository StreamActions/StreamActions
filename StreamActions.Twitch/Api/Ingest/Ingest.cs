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

using StreamActions.Common.Attributes;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Ingest;

/// <summary>
/// Sends and represents a response element for a request for ingest servers.
/// </summary>
[ETag("https://dev.twitch.tv/docs/video-broadcast/reference", "6624802d54891a42b2c19effa0b96885", "2022-10-16T20:11Z", new string[] {
    "-stripblank", "-strip", "-findfirst", "'<div class=\"main\">'", "-findlast", "'<div class=\"subscribe-footer\">'", "-remre",
    "'cloudcannon[^\"]*'" })]
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
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new(baseAddress), new() { RateLimiter = new(1, TimeSpan.FromSeconds(1)), Token = new() { OAuth = "__NEW" } }).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<IngestResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
