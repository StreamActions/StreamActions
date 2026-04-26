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
using StreamActions.Common.Extensions;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Clips;

/// <summary>
/// Sends and represents a response element for a request to create a clip.
/// </summary>
public sealed record CreatedClip
{
    /// <summary>
    /// An ID that uniquely identifies the clip.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// A URL that you can use to edit the clip's title, identify the part of the clip to publish, and publish the clip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The URL is valid for up to 24 hours or until the clip is published, whichever comes first.
    /// </para>
    /// </remarks>
    [JsonPropertyName("edit_url")]
    public Uri? EditUrl { get; init; }

    /// <summary>
    /// Creates a clip from the broadcaster's stream.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose stream you want to create a clip from.</param>
    /// <param name="title">The title of the clip.</param>
    /// <param name="duration">The length of the clip in seconds. Possible values range from 5 to 60 inclusively with a precision of 0.1. The default is 30.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="CreatedClip"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is specified and is not between 5 and 60.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ClipsEdit"/>.</exception>
    /// <remarks>
    /// <para>
    /// This API captures up to 90 seconds of the broadcaster's stream. The 90 seconds spans the point in the stream from when you called the API.
    /// For example, if you call the API at the 4:00 minute mark, the API captures from approximately the 3:35 mark to approximately the 4:05 minute mark. Twitch tries its best to capture 90 seconds of the stream, but the actual length may be less. This may occur if you begin capturing the clip near the beginning or end of the stream.
    /// </para>
    /// <para>
    /// By default, Twitch publishes up to the last 30 seconds of the 90 seconds window and provides a default title for the clip. To specify the title and the portion of the 90 seconds window that's used for the clip, use the URL in the response's <see cref="EditUrl"/> field.
    /// You can specify a clip that's from 5 seconds to 60 seconds in length. The URL is valid for up to 24 hours or until the clip is published, whichever comes first.
    /// </para>
    /// <para>
    /// Creating a clip is an asynchronous process that can take a short amount of time to complete. To determine whether the clip was successfully created, call <see cref="Clip.GetClips(TwitchSession, IEnumerable{string}?, string?, string?, string?, string?, int, DateTime?, DateTime?)"/> using the <see cref="Id"/> that this request returned.
    /// If Get Clips returns the clip, the clip was successfully created. If after 15 seconds Get Clips hasn't returned the clip, assume it failed.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>202 Accepted</term>
    /// <description>Successfully started the clip process.</description>
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
    /// <description>The specified broadcaster has disabled or restricted the ability to capture clips.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The specified broadcaster is not live.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "API Definition")]
    public static async Task<ResponseData<CreatedClip>?> CreateClip(TwitchSession session, string broadcasterId, string? title = null, float? duration = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (duration.HasValue && (duration.Value < 5 || duration.Value > 60))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration.Value, "must be between 5 and 60").Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ClipsEdit);

        NameValueCollection queryParams = new() { { "broadcaster_id", broadcasterId } };

        if (!string.IsNullOrWhiteSpace(title))
        {
            queryParams.Add("title", title);
        }

        if (duration.HasValue)
        {
            queryParams.Add("duration", duration.Value.ToString(CultureInfo.InvariantCulture));
        }

        Uri uri = Util.BuildUri(new Uri("/clips", UriKind.Relative), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<CreatedClip>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a clip from a broadcaster's VOD on behalf of the broadcaster or an editor of the channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The user ID for the channel you want to create a clip for.</param>
    /// <param name="editorId">The user ID of the editor for the channel you want to create a clip for. If using the broadcaster's auth token, this is the same as <paramref name="broadcasterId"/>.</param>
    /// <param name="vodId">ID of the VOD the user wants to clip.</param>
    /// <param name="vodOffset">Offset in the VOD to create the clip, in seconds.</param>
    /// <param name="title">The title of the clip.</param>
    /// <param name="duration">The length of the clip, in seconds. Precision is 0.1. Defaults to 30. Min: 5 seconds, Max: 60 seconds.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="CreatedClip"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="editorId"/>, <paramref name="vodId"/>, or <paramref name="title"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is specified and is not between 5 and 60.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageClips"/> or <see cref="Scope.EditorManageClips"/>.</exception>
    /// <remarks>
    /// <para>
    /// If <paramref name="session"/> is a <see cref="TwitchToken.TokenType.User"/> token, <paramref name="editorId"/> must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>202 Accepted</term>
    /// <description>Successfully started the clip process.</description>
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
    /// <description>The specified broadcaster has disabled or restricted the ability to capture clips, or the editor is not authorized.</description>
    /// </item>
    /// <item>
    /// <term>404 Not Found</term>
    /// <description>The VOD, broadcaster, or editor was not found.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<CreatedClip>?> CreateClipFromVod(TwitchSession session, string broadcasterId, string editorId, string vodId, int vodOffset, string title, float? duration = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(editorId))
        {
            throw new ArgumentNullException(nameof(editorId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(vodId))
        {
            throw new ArgumentNullException(nameof(vodId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentNullException(nameof(title)).Log(TwitchApi.GetLogger());
        }

        if (duration.HasValue && (duration.Value < 5 || duration.Value > 60))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration.Value, "must be between 5 and 60").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ChannelManageClips, Scope.EditorManageClips);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "editor_id", editorId },
            { "vod_id", vodId },
            { "vod_offset", vodOffset.ToString(CultureInfo.InvariantCulture) },
            { "title", title }
        };

        if (duration.HasValue)
        {
            queryParams.Add("duration", duration.Value.ToString(CultureInfo.InvariantCulture));
        }

        Uri uri = Util.BuildUri(new Uri("/videos/clips", UriKind.Relative), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<CreatedClip>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
