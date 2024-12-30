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

using StreamActions.Common;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;
using StreamActions.Common.Extensions;
using System.Globalization;

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
    /// <param name="hasDelay">A Boolean value that determines whether the API captures the clip at the moment the viewer requests it or after a delay. If <see langword="false"/> (default), Twitch captures the clip at the moment the viewer requests it (this is the same clip experience as the Twitch UX). If <see langword="true"/>, Twitch adds a delay before capturing the clip (this basically shifts the capture window to the right slightly).</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="CreatedClip"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
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
    public static async Task<ResponseData<CreatedClip>?> CreateClip(TwitchSession session, string broadcasterId, bool hasDelay = false)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ClipsEdit);

        Uri uri = Util.BuildUri(new("/clips"), new() { { "broadcaster_id", broadcasterId }, { "has_delay", hasDelay.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<CreatedClip>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
