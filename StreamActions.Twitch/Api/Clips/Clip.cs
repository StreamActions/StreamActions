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

using StreamActions.Common;
using StreamActions.Common.Extensions;
using StreamActions.Twitch.Api.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Clips;

/// <summary>
/// Sends and represents a response element for a request for clips.
/// </summary>
public sealed record Clip
{
    /// <summary>
    /// ID of the clip being queried.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// URL where the clip can be viewed.
    /// </summary>
    [JsonPropertyName("url")]
    public Uri? Url { get; init; }

    /// <summary>
    /// URL to embed the clip.
    /// </summary>
    [JsonPropertyName("embed_url")]
    public Uri? EmbedUrl { get; init; }

    /// <summary>
    /// User ID of the stream from which the clip was created.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// Display name corresponding to <see cref="BroadcasterId"/>.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// ID of the user who created the clip.
    /// </summary>
    [JsonPropertyName("creator_id")]
    public string? CreatorId { get; init; }

    /// <summary>
    /// Display name corresponding to <see cref="CreatorId"/>.
    /// </summary>
    [JsonPropertyName("creator_name")]
    public string? CreatorName { get; init; }

    /// <summary>
    /// ID of the video from which the clip was created. This field contains an empty string if the video is not available.
    /// </summary>
    [JsonPropertyName("video_id")]
    public string? VideoId { get; init; }

    /// <summary>
    /// ID of the game assigned to the stream when the clip was created.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// Language of the stream from which the clip was created. A language value is either the ISO 639-1 two-letter code for a supported stream language or "other".
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// Title of the clip.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Number of times the clip has been viewed.
    /// </summary>
    [JsonPropertyName("view_count")]
    public int? ViewCount { get; init; }

    /// <summary>
    /// Date when the clip was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// URL of the clip thumbnail.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    public Uri? ThumbnailUrl { get; init; }

    /// <summary>
    /// Duration of the Clip.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// Duration of the Clip in seconds (up to 0.1 precision).
    /// </summary>
    [JsonPropertyName("duration")]
    public float? DurationSeconds { get; init; }

    /// <summary>
    /// The zero-based offset, in seconds, to where the clip starts in the video (VOD). Is <c>null</c> if the video is not available or hasn't been created yet from the live stream.
    /// </summary>
    /// <remarks>
    /// Note that there's a delay between when a clip is created during a broadcast and when the offset is set. During the delay period, VodOffset is <c>null</c>. The delay is indeterminate, but is typically minutes long.
    /// </remarks>
    [JsonPropertyName("vod_offset")]
    public int? VodOffset { get; init; }

    /// <summary>
    /// Gets clip information by clip ID (one or more), broadcaster ID (one only), or game ID (one only).
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">ID of the clip being queried. Limit: 100. If this is specified, you cannot use any other parameters.</param>
    /// <param name="broadcasterId">ID of the broadcaster for whom clips are returned. Results are ordered by view count. If this is specified, you cannot use <paramref name="id"/> or <paramref name="gameId"/>.</param>
    /// <param name="gameId">ID of the game for which clips are returned. Results are ordered by view count.  If this is specified, you cannot use <paramref name="id"/> or <paramref name="broadcasterId"/>.</param>
    /// <param name="after">Cursor for forward pagination: tells the server where to start fetching the next set of results, in a multi-page response.</param>
    /// <param name="before">Cursor for backward pagination: tells the server where to start fetching the next set of results, in a multi-page response.</param>
    /// <param name="first">Maximum number of objects to return. Maximum: 100. Default: 20.</param>
    /// <param name="startedAt">Starting date/time for returned clips. If this is specified, <paramref name="endedAt"/> also should be specified; otherwise, the <paramref name="endedAt"/> date/time will be 1 week after the <paramref name="startedAt"/> value.</param>
    /// <param name="endedAt">Ending date/time for returned clips. If this is specified, <paramref name="startedAt"/> also must be specified.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Clip"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; did not specify a valid value for one of <paramref name="id"/>, <paramref name="broadcasterId"/>, or <paramref name="gameId"/>; <paramref name="startedAt"/> is null while <paramref name="endedAt"/> is defined.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Specified more than one of the mutually exclusive parameters <paramref name="id"/>, <paramref name="broadcasterId"/>, or <paramref name="gameId"/>; <paramref name="id"/> is defined and has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="after"/> and <paramref name="before"/> were both defined.</exception>
    /// <remarks>
    /// <para>
    /// The clips service returns a maximum of 1000 clips.
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Clip>?> GetClips(TwitchSession session, IEnumerable<string>? id = null, string? broadcasterId = null, string? gameId = null, string? after = null,
        string? before = null, int first = 20, DateTime? startedAt = null, DateTime? endedAt = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(broadcasterId) && string.IsNullOrWhiteSpace(gameId) && (id is null || !id.Any()))
        {
            throw new ArgumentNullException(nameof(id) + "," + nameof(broadcasterId) + "," + nameof(gameId), "Must provide at least one value of id, broadcasterId, or gameId");
        }

        bool hasId = false;
        if (!string.IsNullOrWhiteSpace(broadcasterId))
        {
            hasId = true;
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            if (hasId)
            {
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(broadcasterId) + "," + nameof(gameId), "Can not mix parameters id, broadcasterId, and gameId");
            }
            hasId = true;
        }

        if (id is not null && id.Any())
        {
            if (hasId)
            {
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(broadcasterId) + "," + nameof(gameId), "Can not mix parameters id, broadcasterId, and gameId");
            }

            if (id.Count() > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "must have a count <= 100");
            }
        }

        if (!string.IsNullOrWhiteSpace(after) && !string.IsNullOrWhiteSpace(before))
        {
            throw new InvalidOperationException("Can only use one of before or after");
        }

        if (!startedAt.HasValue && endedAt.HasValue)
        {
            throw new ArgumentNullException(nameof(startedAt), "Must provide startedAt when using endedAt");
        }

        first = Math.Clamp(first, 1, 100);

        Dictionary<string, IEnumerable<string>> queryParams = new();

        if (id is not null && id.Any())
        {
            queryParams.Add("id", id);
        }

        if (!string.IsNullOrWhiteSpace(broadcasterId))
        {
            queryParams.Add("broadcaster_id", new List<string> { broadcasterId });
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            queryParams.Add("game_id", new List<string> { gameId });
        }

        if (id is null || !id.Any())
        {
            queryParams.Add("first", new List<string> { first.ToString(CultureInfo.InvariantCulture) });

            if (startedAt.HasValue)
            {
                queryParams.Add("started_at", new List<string> { startedAt.Value.ToRfc3339() });
            }

            if (endedAt.HasValue)
            {
                queryParams.Add("ended_at", new List<string> { endedAt.Value.ToRfc3339() });
            }

            if (!string.IsNullOrWhiteSpace(after))
            {
                queryParams.Add("after", new List<string> { after });
            }

            if (!string.IsNullOrWhiteSpace(before))
            {
                queryParams.Add("before", new List<string> { before });
            }
        }

        Uri uri = Util.BuildUri(new("/clips"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ResponseData<Clip>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
