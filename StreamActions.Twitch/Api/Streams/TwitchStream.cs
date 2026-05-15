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
using StreamActions.Common.Json.Serialization;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Streams;

/// <summary>
/// Represents a Twitch stream.
/// </summary>
public sealed record TwitchStream
{
    /// <summary>
    /// An ID that identifies the stream. You can use this ID later to look up the video on demand (VOD).
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The ID of the user that's broadcasting the stream.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The ID of the category or game being played.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// The name of the category or game being played.
    /// </summary>
    [JsonPropertyName("game_name")]
    public string? GameName { get; init; }

    /// <summary>
    /// The type of stream.
    /// </summary>
    [JsonPropertyName("type")]
    public StreamTypes? Type { get; init; }

    /// <summary>
    /// The stream's title. Is an empty string if not set.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The tags applied to the stream.
    /// </summary>
    [JsonPropertyName("tags")]
    public IEnumerable<string>? Tags { get; init; }

    /// <summary>
    /// The number of users watching the stream.
    /// </summary>
    [JsonPropertyName("viewer_count")]
    public int? ViewerCount { get; init; }

    /// <summary>
    /// The UTC date and time of when the broadcast began.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The language that the stream uses. This is an ISO 639-1 two-letter language code or other if the stream uses a language not in the list of supported stream languages.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// A URL to an image of a frame from the last 5 minutes of the stream. Replace the width and height placeholders in the URL ({width}x{height}) with the size of the image you want, in pixels.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    public Uri? ThumbnailUrl { get; init; }

    /// <summary>
    /// The list of tags that apply to the stream.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: As of February 28, 2023, this field is deprecated and returns only an empty array. Use the <see cref="Tags"/> property instead.
    /// </remarks>
    [JsonPropertyName("tag_ids")]
    [Obsolete("Use Tags instead.")]
    public IEnumerable<Guid>? TagIds { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether the stream is meant for mature audiences.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: This field is deprecated and returns only false.
    /// </remarks>
    [JsonPropertyName("is_mature")]
    [Obsolete("This field is deprecated and returns only false.")]
    public bool? IsMature { get; init; }

    /// <summary>
    /// Defines the type of stream.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<StreamTypes>))]
    public enum StreamTypes
    {
        /// <summary>
        /// The stream is live.
        /// </summary>
        [JsonCustomEnum("live")]
        Live,
        /// <summary>
        /// All streams.
        /// </summary>
        [JsonCustomEnum("all")]
        All
    }

    /// <summary>
    /// Gets a list of all streams.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userIds">A user ID used to filter the list of streams. Returns only the streams of those users that are broadcasting. Maximum: 100.</param>
    /// <param name="userLogins">A user login name used to filter the list of streams. Returns only the streams of those users that are broadcasting. Maximum: 100.</param>
    /// <param name="gameIds">A game (category) ID used to filter the list of streams. Returns only the streams that are broadcasting the game (category). Maximum: 100.</param>
    /// <param name="type">The type of stream to filter the list of streams by. Default: <see cref="StreamTypes.All"/>.</param>
    /// <param name="languages">A language code used to filter the list of streams. Returns only streams that broadcast in the specified language. Maximum: 100.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="before">The cursor used to get the previous page of results.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="TwitchStream"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any of the filter lists exceed 100 elements.</exception>
    /// <exception cref="InvalidOperationException">Thrown if both <paramref name="before"/> and <paramref name="after"/> are specified.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of streams.</description>
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
    public static async Task<ResponseData<TwitchStream>?> GetStreams(TwitchSession session, IEnumerable<string>? userIds = null, IEnumerable<string>? userLogins = null, IEnumerable<string>? gameIds = null, StreamTypes type = StreamTypes.All, IEnumerable<string>? languages = null, int first = 20, string? before = null, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        List<string> userIdList = userIds?.ToList() ?? [];
        if (userIdList.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(userIds), userIdList.Count, "The number of user IDs must not exceed 100.").Log(TwitchApi.GetLogger());
        }

        List<string> userLoginList = userLogins?.ToList() ?? [];
        if (userLoginList.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(userLogins), userLoginList.Count, "The number of user login names must not exceed 100.").Log(TwitchApi.GetLogger());
        }

        List<string> gameIdList = gameIds?.ToList() ?? [];
        if (gameIdList.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(gameIds), gameIdList.Count, "The number of game IDs must not exceed 100.").Log(TwitchApi.GetLogger());
        }

        List<string> languageList = languages?.ToList() ?? [];
        if (languageList.Count > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(languages), languageList.Count, "The number of languages must not exceed 100.").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(before) && !string.IsNullOrWhiteSpace(after))
        {
            throw new InvalidOperationException("The before and after parameters are mutually exclusive.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];
        if (userIdList.Count > 0)
        {
            queryParameters.Add("user_id", userIdList);
        }

        if (userLoginList.Count > 0)
        {
            queryParameters.Add("user_login", userLoginList);
        }

        if (gameIdList.Count > 0)
        {
            queryParameters.Add("game_id", gameIdList);
        }

        if (type != StreamTypes.All)
        {
            queryParameters.Add("type", type.JsonValue() ?? "");
        }

        if (languageList.Count > 0)
        {
            queryParameters.Add("language", languageList);
        }

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(before))
        {
            queryParameters.Add("before", before);
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        Uri requestUri = Util.BuildUri(new("/streams"), queryParameters);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, requestUri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<TwitchStream>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the list of broadcasters that the user follows and that are streaming live.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="userId">The ID of the user whose list of followed streams you want to get. This ID must match the user ID in the access token.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 100.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="TwitchStream"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="userId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><paramref name="session"/> is not a <see cref="TwitchToken.TokenType.User"/> token.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.UserReadFollows"/>.</exception>
    /// <remarks>
    /// <para>
    /// This ID must match the user ID in the access token.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of broadcasters that the user follows and that are streaming live.</description>
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
    public static async Task<ResponseData<TwitchStream>?> GetFollowedStreams(TwitchSession session, string userId, int first = 100, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException(nameof(userId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.UserReadFollows);

        NameValueCollection queryParameters = new()
        {
            { "user_id", userId }
        };

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        Uri requestUri = Util.BuildUri(new("/streams/followed"), queryParameters);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, requestUri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<TwitchStream>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
