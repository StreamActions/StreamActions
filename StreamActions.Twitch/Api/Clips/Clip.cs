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
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Clips;

/// <summary>
/// Sends and represents a response element for a request for clips.
/// </summary>
public sealed record Clip
{
    /// <summary>
    /// An ID that uniquely identifies the clip.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// A URL to the clip.
    /// </summary>
    [JsonPropertyName("url")]
    public Uri? Url { get; init; }

    /// <summary>
    /// A URL that you can use in an iframe to embed the clip.
    /// </summary>
    [JsonPropertyName("embed_url")]
    public Uri? EmbedUrl { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that the video was clipped from.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// An ID that identifies the user that created the clip.
    /// </summary>
    [JsonPropertyName("creator_id")]
    public string? CreatorId { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("creator_name")]
    public string? CreatorName { get; init; }

    /// <summary>
    /// An ID that identifies the video that the clip came from. This field contains an empty string if the video is not available.
    /// </summary>
    [JsonPropertyName("video_id")]
    public string? VideoId { get; init; }

    /// <summary>
    /// The ID of the game that was being played when the clip was created.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// The ISO 639-1 two-letter language code that the broadcaster broadcasts in. For example, <c>"en"</c> for English. The value is <c>"other"</c> if the broadcaster uses a language that Twitch doesn't support.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// The title of the clip.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The number of times the clip has been viewed.
    /// </summary>
    [JsonPropertyName("view_count")]
    public int? ViewCount { get; init; }

    /// <summary>
    /// The date and time of when the clip was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// A URL to a thumbnail image of the clip.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    public Uri? ThumbnailUrl { get; init; }

    /// <summary>
    /// The length of the clip.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// The length of the clip, in seconds. Precision is 0.1.
    /// </summary>
    [JsonPropertyName("duration")]
    public float? DurationSeconds { get; init; }

    /// <summary>
    /// The zero-based offset, in seconds, to where the clip starts in the video (VOD). Is <see langword="null"/> if the video is not available or hasn't been created yet from the live stream.
    /// </summary>
    /// <remarks>
    /// Note that there's a delay between when a clip is created during a broadcast and when the offset is set. During the delay period, VodOffset is <see langword="null"/>. The delay is indeterminate, but is typically minutes long.
    /// </remarks>
    [JsonPropertyName("vod_offset")]
    public int? VodOffset { get; init; }

    /// <summary>
    /// Gets one or more video clips that were captured from streams.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">An ID that identifies the broadcaster whose video clips you want to get. Use this parameter to get clips that were captured from the broadcaster's streams.</param>
    /// <param name="gameId">An ID that identifies the game whose clips you want to get. Use this parameter to get clips that were captured from streams that were playing this game.</param>
    /// <param name="id">An ID that identifies the clip to get. You may specify a maximum of 100 IDs.</param>
    /// <param name="startedAt">The start date used to filter clips. The API returns only clips within the start and end date window.</param>
    /// <param name="endedAt">The end date used to filter clips. If not specified, the time window is the start date plus one week.</param>
    /// <param name="first">The maximum number of clips to return per page in the response. Maximum: 100. Default: 20.</param>
    /// <param name="before">The cursor used to get the previous page of results.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Clip"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; did not specify a valid value for one of <paramref name="id"/>, <paramref name="broadcasterId"/>, or <paramref name="gameId"/>; <paramref name="startedAt"/> is <see langword="null"/> while <paramref name="endedAt"/> is defined.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Specified more than one of the mutually exclusive parameters <paramref name="id"/>, <paramref name="broadcasterId"/>, or <paramref name="gameId"/>; <paramref name="id"/> is defined and has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="after"/> and <paramref name="before"/> were both defined.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// The <paramref name="id"/>, <paramref name="gameId"/>, and <paramref name="broadcasterId"/> parameters are mutually exclusive.
    /// </para>
    /// <para>
    /// For clips returned by <paramref name="gameId"/> or <paramref name="broadcasterId"/>, the list is in descending order by view count. For lists returned by <paramref name="id"/>, the list is in the same order as the input IDs.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully started the commercial.</description>
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
    /// <term>404 Not Found</term>
    /// <description>The specified game does not exist.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Clip>?> GetClips(TwitchSession session, string? broadcasterId = null, string? gameId = null, IEnumerable<string>? id = null, DateTime? startedAt = null,
        DateTime? endedAt = null, int first = 20, string? before = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        if (string.IsNullOrWhiteSpace(broadcasterId) && string.IsNullOrWhiteSpace(gameId) && (id is null || !id.Any()))
        {
            throw new ArgumentNullException(nameof(id) + "," + nameof(broadcasterId) + "," + nameof(gameId), "must provide at least one of these parameters").Log(TwitchApi.GetLogger());
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
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(broadcasterId) + "," + nameof(gameId), "can not mix these parameters").Log(TwitchApi.GetLogger());
            }
            hasId = true;
        }

        if (id is not null && id.Any())
        {
            if (hasId)
            {
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(broadcasterId) + "," + nameof(gameId), "can not mix these parameters").Log(TwitchApi.GetLogger());
            }

            if (id.Count() > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
            }
        }

        if (!string.IsNullOrWhiteSpace(after) && !string.IsNullOrWhiteSpace(before))
        {
            throw new InvalidOperationException("can only use one of " + nameof(before) + " or " + nameof(after)).Log(TwitchApi.GetLogger());
        }

        if (startedAt.HasValue != endedAt.HasValue)
        {
            throw new ArgumentNullException(nameof(startedAt) + "," + nameof(endedAt), "both parameters must be used together").Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 100);

        NameValueCollection queryParams = [];

        if (id is not null && id.Any())
        {
            queryParams.Add("id", id);
        }

        if (!string.IsNullOrWhiteSpace(broadcasterId))
        {
            queryParams.Add("broadcaster_id", broadcasterId);
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            queryParams.Add("game_id", gameId);
        }

        if (id is null || !id.Any())
        {
            queryParams.Add("first", first.ToString(CultureInfo.InvariantCulture));

            if (startedAt.HasValue)
            {
                queryParams.Add("started_at", startedAt.Value.ToRfc3339());
            }

            if (endedAt.HasValue)
            {
                queryParams.Add("ended_at", endedAt.Value.ToRfc3339());
            }

            if (!string.IsNullOrWhiteSpace(after))
            {
                queryParams.Add("after", after);
            }

            if (!string.IsNullOrWhiteSpace(before))
            {
                queryParams.Add("before", before);
            }
        }

        Uri uri = Util.BuildUri(new("/clips"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Clip>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
