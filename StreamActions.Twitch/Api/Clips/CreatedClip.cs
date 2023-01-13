/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
using StreamActions.Common.Limiters;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Clips;

/// <summary>
/// Sends and represents a response element for a request to create a clip.
/// </summary>
public sealed record CreatedClip
{
    /// <summary>
    /// The rate limiter for <see cref="CreateClip(TwitchSession, string, bool)"/>.
    /// </summary>
    [JsonIgnore]
    public static TokenBucketRateLimiter RateLimiter { get; } = new(1, TimeSpan.FromSeconds(60));

    /// <summary>
    /// ID of the clip that was created.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// URL of the edit page for the clip.
    /// </summary>
    [JsonPropertyName("edit_url")]
    public Uri? EditUrl { get; init; }

    /// <summary>
    /// Creates a clip programmatically.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">ID of the stream from which the clip will be made.</param>
    /// <param name="hasDelay">If false, the clip is captured from the live stream when the API is called; otherwise, a delay is added before the clip is captured (to account for the brief delay between the broadcaster's stream and the viewer's experience of that stream).</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="CreatedClip"/> containing the response. The <see cref="TwitchResponse.Status"/> will be 0 if <see cref="RateLimiter"/> timed out waiting.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ClipsEdit"/>.</exception>
    /// <remarks>
    /// <para>
    /// Clip creation takes time. We recommend that you query <see cref="Clip.GetClips"/>, with the clip ID that is returned here.
    /// If <see cref="Clip.GetClips"/> returns a valid clip, your clip creation was successful. If, after 15 seconds,
    /// you still have not gotten back a valid clip from <see cref="Clip.GetClips"/>, assume that the clip was not created and retry <see cref="CreateClip(TwitchSession, string, bool)"/>.
    /// </para>
    /// <para>
    /// This endpoint has a global rate limit, across all callers. The rate limit is tracked by <see cref="RateLimiter"/>.
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<CreatedClip>?> CreateClip(TwitchSession session, string broadcasterId, bool hasDelay = false)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId));
        }

        session.RequireToken(Scope.ClipsEdit);

        try
        {
            await RateLimiter.WaitForRateLimit(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            Uri uri = Util.BuildUri(new("/clips"), new Dictionary<string, IEnumerable<string>> { { "broadcaster_id", new List<string> { broadcasterId } }, { "has_delay", new List<string> { hasDelay ? "true" : "false" } } });
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session).ConfigureAwait(false);
            RateLimiter.ParseHeaders(response.Headers, "Ratelimit-Helixclipscreation-Limit", "Ratelimit-Helixclipscreation-Remaining", "Ratelimit-Helixclipscreation-Reset");
            return await response.Content.ReadFromJsonAsync<ResponseData<CreatedClip>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException)
        {
            return new() { Status = 0, Error = ex.GetType().Name, Message = ex.Message };
        }
    }
}
