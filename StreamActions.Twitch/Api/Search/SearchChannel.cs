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
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Search;

/// <summary>
/// Represents a channel found in a search.
/// </summary>
public sealed record SearchChannel
{
    /// <summary>
    /// The ISO 639-1 two-letter language code of the language used by the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_language")]
    public string? BroadcasterLanguage { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// The ID of the game that the broadcaster is playing or last played.
    /// </summary>
    [JsonPropertyName("game_id")]
    public string? GameId { get; init; }

    /// <summary>
    /// The name of the game that the broadcaster is playing or last played.
    /// </summary>
    [JsonPropertyName("game_name")]
    public string? GameName { get; init; }

    /// <summary>
    /// An ID that uniquely identifies the channel (this is the broadcaster's ID).
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcaster is streaming live.
    /// </summary>
    [JsonPropertyName("is_live")]
    public bool? IsLive { get; init; }

    /// <summary>
    /// The list of tags that apply to the stream.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: As of February 28, 2023, this field is deprecated and returns only an empty array. If you use this field, please update your code to use the tags field.
    /// </remarks>
    [JsonPropertyName("tag_ids")]
    [Obsolete("Use Tags instead.")]
    public IReadOnlyList<Guid>? TagIds { get; init; }

    /// <summary>
    /// The tags applied to the channel.
    /// </summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// A URL to a thumbnail of the broadcaster's profile image.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    public Uri? ThumbnailUrl { get; init; }

    /// <summary>
    /// The stream's title. Is an empty string if the broadcaster didn't set it.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The UTC date and time of when the broadcaster started streaming. The value is null if the broadcaster is not streaming live.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Gets the channels that match the specified query and have streamed content within the past 6 months.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="query">The URI-encoded search string.</param>
    /// <param name="liveOnly">A Boolean value that determines whether the response includes only channels that are currently streaming live. Default: <see langword="false"/>.</param>
    /// <param name="first">The maximum number of items to return per page in the response. Minimum: 1. Maximum: 100. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="SearchChannel"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of channels that matched the specified query string.</description>
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
    public static async Task<ResponseData<SearchChannel>?> SearchChannels(TwitchSession session, string query, bool liveOnly = false, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentNullException(nameof(query)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];
        queryParameters.Add("query", query);

        if (liveOnly)
        {
            queryParameters.Add("live_only", "true");
        }

        first = Math.Clamp(first, 1, 100);
        queryParameters.Add("first", first.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParameters.Add("after", after);
        }

        Uri requestUri = Util.BuildUri(new("/search/channels"), queryParameters);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, requestUri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<SearchChannel>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
