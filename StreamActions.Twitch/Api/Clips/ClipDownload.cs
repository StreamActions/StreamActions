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
using System;
using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Clips;

/// <summary>
/// URLs to download the video file(s) for the specified clips.
/// </summary>
public sealed record ClipDownload
{
    /// <summary>
    /// An ID that uniquely identifies the clip.
    /// </summary>
    [JsonPropertyName("clip_id")]
    public string? ClipId { get; init; }

    /// <summary>
    /// The landscape URL to download the clip. This field is <see langword="null"/> if the URL is not available.
    /// </summary>
    [JsonPropertyName("landscape_download_url")]
    public Uri? LandscapeDownloadUrl { get; init; }

    /// <summary>
    /// The portrait URL to download the clip. This field is <see langword="null"/> if the URL is not available.
    /// </summary>
    [JsonPropertyName("portrait_download_url")]
    public Uri? PortraitDownloadUrl { get; init; }

    /// <summary>
    /// Provides URLs to download the video file(s) for the specified clips.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster you want to download clips for.</param>
    /// <param name="clipId">The ID that identifies the clip you want to download. You may specify a maximum of 10 clips.</param>
    /// <param name="editorId">The User ID of the editor for the channel you want to download a clip for. If using the broadcaster's auth token, this is the same as <paramref name="broadcasterId"/>.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ClipDownload"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/>, <paramref name="editorId"/>, or <paramref name="clipId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="clipId"/> has more than 10 elements.</exception>
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
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the clip download URL(s).</description>
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
    /// <description>The user is not an editor for the specified broadcaster.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<ClipDownload>?> GetClipsDownload(TwitchSession session, string broadcasterId, string editorId, IEnumerable<string> clipId)
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

        if (clipId is null || !clipId.Any())
        {
            throw new ArgumentNullException(nameof(clipId)).Log(TwitchApi.GetLogger());
        }

        if (clipId.Count() > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(clipId), clipId.Count(), "must have a count <= 10").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken(Scope.ChannelManageClips, Scope.EditorManageClips);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "editor_id", editorId }
        };
        queryParams.Add("clip_id", clipId);

        Uri uri = Util.BuildUri(new Uri("/clips/downloads", UriKind.Relative), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ClipDownload>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
