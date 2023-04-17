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
using System.Text.Json.Serialization;
using StreamActions.Common.Extensions;
using StreamActions.Common.Json.Serialization;

namespace StreamActions.Twitch.Api.Videos;

/// <summary>
/// Sends and represents a response element for a request for a broadcasters videos.
/// </summary>
public sealed record Video
{
    /// <summary>
    /// An ID that identifies the video.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The ID of the stream that the video originated from if the video's <see cref="Type"/> is <see cref="VideoType.Archive"/>; otherwise, <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("stream_id")]
    public string? StreamId { get; init; }

    /// <summary>
    /// The ID of the broadcaster that owns the video.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The video's title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The video's description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The date and time, in UTC, of when the video was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The date and time, in UTC, of when the video was published.
    /// </summary>
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// The video's URL.
    /// </summary>
    [JsonPropertyName("url")]
    public Uri? Url { get; init; }

    /// <summary>
    /// A URL to a thumbnail image of the video.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before using the URL, you must replace the <c>%{width}</c> and <c>%{height}</c> placeholders with the width and height of the thumbnail you want returned.
    /// Specify the width and height in pixels. Because the CDN preserves the thumbnail's ratio, the thumbnail may not be the exact size you requested.
    /// </para>
    /// </remarks>
    [JsonPropertyName("thumbnail_url")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Template Uri")]
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// The video's viewable state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Always set to <see cref="Viewability.Public"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("viewable")]
    public Viewability? Viewable { get; init; }

    /// <summary>
    /// The number of times that users have watched the video.
    /// </summary>
    [JsonPropertyName("view_count")]
    public int? ViewCount { get; init; }

    /// <summary>
    /// The ISO 639-1 two-letter language code that the video was broadcast in. The language value is <c>"other"</c> if the video was broadcast in a language not in the list of supported languages.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// The video's type.
    /// </summary>
    [JsonPropertyName("type")]
    public VideoType? Type { get; init; }

    /// <summary>
    /// The video's length in ISO 8601 duration format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For example, 3m21s represents 3 minutes, 21 seconds.
    /// </para>
    /// </remarks>
    [JsonPropertyName("duration")]
    public string? DurationString { get; init; }

    /// <summary>
    /// The video's length.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationString is not null ? Util.DurationStringToTimeSpan(this.DurationString) : null;

    /// <summary>
    /// The segments that Twitch Audio Recognition muted; otherwise, <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("muted_segments")]
    public IEnumerable<MutedVideoSegment>? MutedSegments { get; init; }

    /// <summary>
    /// The video's viewable state.
    /// </summary>
    [JsonConverter(typeof(JsonLowerCaseEnumConverter<Viewability>))]
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
    /// The video's type.
    /// </summary>
    [JsonConverter(typeof(JsonLowerCaseEnumConverter<VideoType>))]
    public enum VideoType
    {
        /// <summary>
        /// All video types.
        /// </summary>
        All,
        /// <summary>
        /// An on-demand video (VOD) of one of the broadcaster's past streams.
        /// </summary>
        Archive,
        /// <summary>
        /// A video that the broadcaster uploaded to their video library.
        /// </summary>
        Upload,
        /// <summary>
        /// A highlight reel of one of the broadcaster's past streams.
        /// </summary>
        Highlight
    }

    /// <summary>
    /// A filter used to filter the list of videos by when they were published.
    /// </summary>
    [JsonConverter(typeof(JsonLowerCaseEnumConverter<VideoPeriod>))]
    public enum VideoPeriod
    {
        /// <summary>
        /// Default. The lifetime of the broadcaster's channel.
        /// </summary>
        All,
        /// <summary>
        /// A day spans from 00:00:00 on the day specified in <c>startedAt</c> and runs through 00:00:00 of the next day.
        /// </summary>
        Day,
        /// <summary>
        /// A week spans from 00:00:00 on the Monday of the week specified in <c>startedAt</c> and runs through 00:00:00 of the next Monday.
        /// </summary>
        Week,
        /// <summary>
        /// A month spans from 00:00:00 on the first day of the month specified in <c>startedAt</c> and runs through 00:00:00 of the first day of the next month.
        /// </summary>
        Month
    }

    /// <summary>
    /// The order to sort the returned videos in.
    /// </summary>
    [JsonConverter(typeof(JsonLowerCaseEnumConverter<VideoSort>))]
    public enum VideoSort
    {
        /// <summary>
        /// Sort the results in descending order by when they were created (i.e., latest video first).
        /// </summary>
        Time,
        /// <summary>
        /// Sort the results in descending order by biggest gains in viewership (i.e., highest trending video first).
        /// </summary>
        Trending,
        /// <summary>
        ///  Sort the results in descending order by most views (i.e., highest number of views first).
        /// </summary>
        Views
    }

    /// <summary>
    /// Gets information about one or more published videos. You may get videos by ID, by user, or by game/category.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">A list of IDs that identify the videos you want to get. You may specify a maximum of 100 IDs.</param>
    /// <param name="userId">The ID of the user whose list of videos you want to get.</param>
    /// <param name="gameId">A category or game ID. The response contains a maximum of 500 videos that show this content.</param>
    /// <param name="language">A filter used to filter the list of videos by the language that the video owner broadcasts in. For supported languages, set this parameter to the ISO 639-1 two-letter code for the language. If the language is not supported, use <c>"other"</c>.</param>
    /// <param name="period">A filter used to filter the list of videos by when they were published. Default: <see cref="VideoPeriod.All"/>.</param>
    /// <param name="sort">The order to sort the returned videos in. Default: <see cref="VideoSort.Time"/>.</param>
    /// <param name="type">A filter used to filter the list of videos by the video's type. Default: <see cref="VideoType.All"/>.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Limit: 100. Default: 20.</param>
    /// <param name="before">The cursor used to get the next page of results.</param>
    /// <param name="after">The cursor used to get the previous page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Video"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; did not specify one of <paramref name="id"/>, <paramref name="userId"/>, or <paramref name="gameId"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Specified more than one of the mutually exclusive parameters <paramref name="id"/>, <paramref name="userId"/>, or <paramref name="gameId"/>; <paramref name="id"/> is defined and has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="after"/> and <paramref name="before"/> were both defined.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// The <paramref name="id"/>, <paramref name="userId"/>, and <paramref name="gameId"/> parameters are mutually exclusive.
    /// </para>
    /// <para>
    /// You may apply several filters to get a subset of the videos. The filters are applied as an AND operation to each video.
    /// For example, if <paramref name="language"/> is set to <c>de</c> and <paramref name="gameId"/> is set to <c>21779</c>, the response includes only videos that show playing League of Legends by users that stream in German.
    /// The filters apply only if you get videos by user ID or game ID.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of videos.</description>
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
    /// <description>The specified game does not exist; all specified ids do not exist.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Video>?> GetVideos(TwitchSession session, IEnumerable<string>? id = null, string? userId = null, string? gameId = null, string? language = null,
        VideoPeriod period = VideoPeriod.All, VideoSort sort = VideoSort.Time, VideoType type = VideoType.All, int first = 20, string? before = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(gameId) && (id is null || !id.Any()))
        {
            throw new ArgumentNullException(nameof(id) + "," + nameof(userId) + "," + nameof(gameId), "must provide at least one of these parameters").Log(TwitchApi.GetLogger());
        }

        session.RequireToken();

        bool hasId = false;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            hasId = true;
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            if (hasId)
            {
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(userId) + "," + nameof(gameId), "can not mix these parameters").Log(TwitchApi.GetLogger());
            }
            hasId = true;
        }

        if (id is not null && id.Any())
        {
            if (hasId)
            {
                throw new ArgumentOutOfRangeException(nameof(id) + "," + nameof(userId) + "," + nameof(gameId), "can not mix these parameters").Log(TwitchApi.GetLogger());
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
            queryParams.Add("period", new List<string> { period.JsonValue() ?? "" });
            queryParams.Add("sort", new List<string> { sort.JsonValue() ?? "" });
            queryParams.Add("type", new List<string> { type.JsonValue() ?? "" });

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
    /// Deletes one or more videos. You may delete past broadcasts, highlights, or uploads.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="id">The list of videos to delete. You can delete a maximum of 5 videos per request.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with the IDs of the videos that were deleted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="id"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> has more than 5 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageVideos"/>.</exception>
    /// <remarks>
    /// <para>
    /// If the user doesn't have permission to delete one of the videos in the list, none of the videos are deleted.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully deleted the list of videos.</description>
    /// </item>
    /// <item>
    /// <term>400 Bad Request</term>
    /// <description>The described parameter was missing or invalid.</description>
    /// </item>
    /// <item>
    /// <term>401 Unauthorized</term>
    /// <description>The OAuth token was invalid for this request due to the specified reason.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
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
