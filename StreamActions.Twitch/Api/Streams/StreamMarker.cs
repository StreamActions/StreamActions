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

using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Streams;

/// <summary>
/// Represents a marker in a live stream.
/// </summary>
public sealed record StreamMarker
{
    /// <summary>
    /// An ID that identifies the marker.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The UTC date and time of when the user created the marker.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The relative offset (in seconds) of the marker from the beginning of the stream.
    /// </summary>
    [JsonPropertyName("position_seconds")]
    public long? PositionSeconds { get; init; }

    /// <summary>
    /// The relative offset of the marker from the beginning of the stream.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Position => PositionSeconds.HasValue ? TimeSpan.FromSeconds(PositionSeconds.Value) : null;

    /// <summary>
    /// A description that the user gave the marker to help them remember why they marked the location.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// A URL that opens the video in Twitch Highlighter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is only returned by the Get Stream Markers endpoint.
    /// </para>
    /// </remarks>
    [JsonPropertyName("URL")]
    public Uri? Url { get; init; }

    /// <summary>
    /// Adds a marker to a live stream.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The parameters for creating the stream marker.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="StreamMarker"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the description is too long (maximum 140 characters).</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageBroadcast"/>.</exception>
    /// <remarks>
    /// <para>
    /// The user in the access token must own the video or they must be one of the broadcaster's editors.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully created the marker.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// <item>
    /// <term>403 Forbidden</term>
    /// <description>The user in the access token is not authorized to create video markers for the specified broadcaster.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The user is not streaming live or hasn't enabled VOD.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<StreamMarker>?> CreateStreamMarker(TwitchSession session, CreateStreamMarkerParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Description?.Length > 140)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), parameters.Description.Length, "The description must not exceed 140 characters.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelManageBroadcast);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/streams/markers"), session, JsonContent.Create(parameters, options: TwitchApi.SerializerOptions)).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<StreamMarker>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
