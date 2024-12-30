/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Extensions;
using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Ingest;

/// <summary>
/// Sends and represents a response element for a request for ingest servers.
/// </summary>
[ETag("Ingest", 72,
    "https://dev.twitch.tv/docs/video-broadcast/reference/", "ECC44F51C6DE913DB8F135D5AB4CDD4171B646A790CBBC2B476BE9259CDFB4E5",
    "2024-06-02T20:15Z", "TwitchReferenceParser", [])]
public sealed record IngestServer
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
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of ingest endpoints.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<IngestResponse?> GetIngestServers(string baseAddress = "https://ingest.twitch.tv/ingests")
    {
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, new(baseAddress), TwitchSession.Empty).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<IngestResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
