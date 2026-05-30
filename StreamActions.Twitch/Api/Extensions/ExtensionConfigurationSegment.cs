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
using StreamActions.Common.Net;
using StreamActions.Twitch.Api.Common;
using StreamActions.Common.Logger;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Contains extension configuration segment data and methods to get and set segments.
/// </summary>
public sealed record ExtensionConfigurationSegment
{
    /// <summary>
    /// The type of segment. Possible values are: broadcaster, developer, global.
    /// </summary>
    [JsonPropertyName("segment")]
    public string? Segment { get; init; }

    /// <summary>
    /// The ID of the broadcaster that installed the extension. The object includes this field only if the segment query parameter is set to developer or broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The contents of the segment. This string may be a plain-text string or a string-encoded JSON object.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// The version number that identifies this definition of the segment’s data.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Gets the specified configuration segment from the specified extension.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Requires a signed JSON Web Token (JWT) created by an Extension Backend Service (EBS).</param>
    /// <param name="extensionId">The ID of the extension that contains the configuration segment you want to get.</param>
    /// <param name="segment">The type of configuration segment to get. Possible case-sensitive values are: broadcaster, developer, global. You may specify one or more segments.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that installed the extension. This parameter is required if you set the segment parameter to broadcaster or developer.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ExtensionConfigurationSegment"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="extensionId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="segment"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentException"><paramref name="broadcasterId"/> is required when <paramref name="segment"/> contains broadcaster or developer.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.Jwt"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the configurations.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The JWT token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>429 Too many requests</term>
    /// <description>The app exceeded the number of requests that it may make per minute.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ExtensionConfigurationSegment>?> GetExtensionConfigurationSegment(TwitchSession session, string extensionId, IEnumerable<string> segment, string? broadcasterId = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireJwtToken();

        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentNullException(nameof(extensionId)).Log(TwitchApi.GetLogger());
        }

        if (segment is null || !segment.Any())
        {
            throw new ArgumentNullException(nameof(segment)).Log(TwitchApi.GetLogger());
        }

        List<string> segments = [];
        foreach (string s in segment)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                segments.Add(s);
            }
        }

        if (segments.Contains("broadcaster") || segments.Contains("developer"))
        {
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId), "broadcaster_id is required if segment is broadcaster or developer.").Log(TwitchApi.GetLogger());
            }
        }

        NameValueCollection queryParams = new()
        {
            { "extension_id", extensionId },
            { "segment", segments }
        };

        if (!string.IsNullOrWhiteSpace(broadcasterId))
        {
            queryParams.Add("broadcaster_id", broadcasterId);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/extensions/configurations"), queryParams), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ExtensionConfigurationSegment>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a configuration segment. The segment is limited to 5 KB.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request. Requires a signed JSON Web Token (JWT) created by an Extension Backend Service (EBS).</param>
    /// <param name="parameters">The <see cref="SetExtensionConfigurationSegmentParameters"/> for the request.</param>
    /// <returns>A <see cref="JsonApiResponse"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.Jwt"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>204 No Content</term>
    /// <description>Successfully updated the configuration.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The JWT token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<JsonApiResponse?> SetExtensionConfigurationSegment(TwitchSession session, SetExtensionConfigurationSegmentParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireJwtToken();

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Put, new("/extensions/configurations"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<JsonApiResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
