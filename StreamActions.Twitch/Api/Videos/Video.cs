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
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Globalization;
using StreamActions.Twitch.Extensions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Videos;

/// <summary>
/// Sends and represents a response element for a request for a broadcasters videos.
/// </summary>
public sealed record Video
{
    /// <summary>
    /// ID of the video.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// ID of the stream that the video originated from if the <see cref="Type"/> is <see cref="VideoType.Archive"/>. Otherwise set to <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("stream_id")]
    public string? StreamId { get; init; }

    /// <summary>
    /// ID of the user who owns the video.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Login of the user who owns the video.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// Display name corresponding to <see cref="UserId"/>.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// Title of the video.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Description of the video.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Date when the video was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// Date when the video was published.
    /// </summary>
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// URL of the video.
    /// </summary>
    [JsonPropertyName("url")]
    public Uri? Url { get; init; }

    /// <summary>
    /// Template URL for the thumbnail of the video.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Template Uri")]
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Indicates whether the video is publicly viewable.
    /// </summary>
    [JsonPropertyName("viewable")]
    public Viewability? Viewable { get; init; }

    /// <summary>
    /// Number of times the video has been viewed.
    /// </summary>
    [JsonPropertyName("view_count")]
    public int? ViewCount { get; init; }

    /// <summary>
    /// Language of the video. A language value is either the ISO 639-1 two-letter code for a supported stream language or <c>other</c>.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// Type of video.
    /// </summary>
    [JsonPropertyName("type")]
    public VideoType? Type { get; init; }

    /// <summary>
    /// Length of the video, as a string in the format <c>1w2d3h4m5s</c>.
    /// </summary>
    [JsonPropertyName("duration")]
    public string? DurationString { get; init; }

    /// <summary>
    /// Length of the video.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationString is not null ? Util.DurationStringToTimeSpan(this.DurationString) : null;

    /// <summary>
    /// Array of muted segments in the video. If there are no muted segments, the value will be <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("muted_segments")]
    public IEnumerable<MutedVideoSegment>? MutedSegments { get; init; }

    /// <summary>
    /// Indicates whether the video is publicly viewable.
    /// </summary>
    public enum Viewability
    {
        /// <summary>
        /// Video is publicly viewable.
        /// </summary>
        Public,
        /// <summary>
        /// Video is private.
        /// </summary>
        Private
    }

    /// <summary>
    /// Type of video.
    /// </summary>
    public enum VideoType
    {
        /// <summary>
        /// All video types.
        /// </summary>
        All,
        /// <summary>
        /// An archived stream.
        /// </summary>
        Archive,
        /// <summary>
        /// An uploaded video.
        /// </summary>
        Upload,
        /// <summary>
        /// A highlight from a stream.
        /// </summary>
        Highlight
    }

    /// <summary>
    /// Time period during which the video was created (PST time zone).
    /// </summary>
    public enum VideoPeriod
    {
        /// <summary>
        /// The lifetime of the broadcaster's channel.
        /// </summary>
        All,
        /// <summary>
        /// 00:00:00 on the current day, through 00:00:00 on the following day.
        /// </summary>
        Day,
        /// <summary>
        /// 00:00:00 on Monday of the current week, through 00:00:00 on the following Monday.
        /// </summary>
        Week,
        /// <summary>
        /// 00:00:00 on the first day of the current month, through 00:00:00 on the first day of the following month.
        /// </summary>
        Month
    }

    /// <summary>
    /// Sort order of the videos.
    /// </summary>
    public enum VideoSort
    {
        /// <summary>
        /// <see cref="CreatedAt"/> timestamp, descending.
        /// </summary>
        Time,
        /// <summary>
        /// What's trending, descending.
        /// </summary>
        Trending,
        /// <summary>
        /// <see cref="ViewCount"/> value, descending.
        /// </summary>
        Views
    }

    /// <summary>
    /// Gets video information by one or more video IDs, user ID, or game ID. For lookup by user or game, several filters are available that can be specified as query parameters. If a game is specified, a maximum of 500 results are available.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">ID of the video being queried. Limit: 100. If this is specified, you cannot use any other parameters.</param>
    /// <param name="userId">ID of the user who owns the video. If this is specified, you cannot use <paramref name="id"/> or <paramref name="gameId"/>.</param>
    /// <param name="gameId">ID of the game the video is of. If this is specified, you cannot use <paramref name="id"/> or <paramref name="userId"/>.</param>
    /// <param name="after">Cursor for forward pagination: tells the server where to start fetching the next set of results, in a multi-page response. The cursor value specified here is from the <see cref="ResponseData{TDataType}.Pagination"/> response field of a prior query.</param>
    /// <param name="before">Cursor for backward pagination: tells the server where to start fetching the next set of results, in a multi-page response. The cursor value specified here is from the <see cref="ResponseData{TDataType}.Pagination"/> response field of a prior query.</param>
    /// <param name="first">Number of values to be returned when getting videos by user or game ID. Limit: 100. Default: 20.</param>
    /// <param name="language">Language of the video being queried. A language value must be either the ISO 639-1 two-letter code for a supported stream language or <c>other</c>.</param>
    /// <param name="period">Period during which the video was created. Default: <see cref="VideoPeriod.All"/>.</param>
    /// <param name="sort">Sort order of the videos. Default: <see cref="VideoSort.Time"/>.</param>
    /// <param name="type">Type of video. Default: <see cref="VideoType.All"/>.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Video"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; did not specify a valid value for one of <paramref name="id"/>, <paramref name="userId"/>, or <paramref name="gameId"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Specified more than one of the mutually exclusive parameters <paramref name="id"/>, <paramref name="userId"/>, or <paramref name="gameId"/>; <paramref name="id"/> is defined and has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="after"/> and <paramref name="before"/> were both defined.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "API Definition")]
    public static async Task<ResponseData<Video>?> GetVideos(TwitchSession session, IEnumerable<string>? id = null, string? userId = null, string? gameId = null, string? after = null,
        string? before = null, int first = 20, string? language = null, VideoPeriod period = VideoPeriod.All, VideoSort sort = VideoSort.Time, VideoType type = VideoType.All)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(gameId) && (id is null || !id.Any()))
        {
            throw new ArgumentNullException(nameof(id) + "," + nameof(userId) + "," + nameof(gameId), "Must provide at least one value of id, userId, or gameId").Log(TwitchApi.GetLogger());
        }

        bool hasId = false;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            hasId = true;
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            if (hasId)
            {
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(userId) + "," + nameof(gameId), "Can not mix parameters id, userId, and gameId").Log(TwitchApi.GetLogger());
            }
            hasId = true;
        }

        if (id is not null && id.Any())
        {
            if (hasId)
            {
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(userId) + "," + nameof(gameId), "Can not mix parameters id, userId, and gameId").Log(TwitchApi.GetLogger());
            }

            if (id.Count() > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
            }
        }

        if (!string.IsNullOrWhiteSpace(after) && !string.IsNullOrWhiteSpace(before))
        {
            throw new InvalidOperationException("Can only use one of before or after").Log(TwitchApi.GetLogger());
        }

        first = Math.Clamp(first, 1, 100);

        Dictionary<string, IEnumerable<string>> queryParams = new();

        if (id is not null && id.Any())
        {
            queryParams.Add("id", id);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            queryParams.Add("user_id", new List<string> { userId });
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            queryParams.Add("game_id", new List<string> { gameId });
        }

        if (id is null || !id.Any())
        {
            queryParams.Add("first", new List<string> { first.ToString(CultureInfo.InvariantCulture) });
            queryParams.Add("period", new List<string> { Enum.GetName(period)?.ToLowerInvariant() ?? "" });
            queryParams.Add("sort", new List<string> { Enum.GetName(sort)?.ToLowerInvariant() ?? "" });
            queryParams.Add("type", new List<string> { Enum.GetName(type)?.ToLowerInvariant() ?? "" });

            if (!string.IsNullOrWhiteSpace(language))
            {
                queryParams.Add("language", new List<string> { language });
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

        Uri uri = Util.BuildUri(new("/videos"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Video>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes one or more videos. Videos are past broadcasts, Highlights, or uploads.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">ID of the video(s) to be deleted. Limit: 5.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with the IDs of the videos that were deleted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="id"/> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> has more than 5 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageVideos"/>.</exception>
    /// <remarks>
    /// <para>
    /// Invalid Video IDs will be ignored (i.e. IDs provided that do not have a video associated with it).
    /// If the OAuth user token does not have permission to delete even one of the valid Video IDs, no videos will be deleted and the response will return a 401.
    /// </para></remarks>
    public static async Task<ResponseData<string>?> DeleteVideos(TwitchSession session, IEnumerable<string> id)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (id is null || !id.Any())
        {
            throw new ArgumentNullException(nameof(id)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManageVideos);

        if (id.Count() > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 5").Log(TwitchApi.GetLogger());
        }

        Uri uri = Util.BuildUri(new("/videos"), new Dictionary<string, IEnumerable<string>> { { "id", id } });
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<string>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
