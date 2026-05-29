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
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Teams;

/// <summary>
/// Represents a Twitch team.
/// </summary>
public sealed record Team
{
    /// <summary>
    /// An ID that identifies the broadcaster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is only available when calling <see cref="GetChannelTeams"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is only available when calling <see cref="GetChannelTeams"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is only available when calling <see cref="GetChannelTeams"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// A URL to the team's background image.
    /// </summary>
    [JsonPropertyName("background_image_url")]
    public Uri? BackgroundImageUrl { get; init; }

    /// <summary>
    /// A URL to the team's banner.
    /// </summary>
    [JsonPropertyName("banner")]
    public Uri? Banner { get; init; }

    /// <summary>
    /// The UTC date and time of when the team was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// The UTC date and time of the last time the team was updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// The team's description. The description may contain formatting such as Markdown, HTML, newline (\n) characters, etc.
    /// </summary>
    [JsonPropertyName("info")]
    public string? Info { get; init; }

    /// <summary>
    /// A URL to a thumbnail image of the team's logo.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    public Uri? ThumbnailUrl { get; init; }

    /// <summary>
    /// The team's name.
    /// </summary>
    [JsonPropertyName("team_name")]
    public string? TeamName { get; init; }

    /// <summary>
    /// The team's display name.
    /// </summary>
    [JsonPropertyName("team_display_name")]
    public string? TeamDisplayName { get; init; }

    /// <summary>
    /// An ID that identifies the team.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The list of team members.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is only available when calling <see cref="GetTeams"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("users")]
    public IReadOnlyList<TeamMember>? Users { get; init; }

    /// <summary>
    /// Gets the list of Twitch teams that the broadcaster is a member of.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose teams you want to get.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Team"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// <paramref name="broadcasterId"/> is <see langword="null"/> or whitespace.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the list of teams.</description>
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
    public static async Task<ResponseData<Team>?> GetChannelTeams(TwitchSession session, string broadcasterId)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];
        queryParameters.Add("broadcaster_id", broadcasterId);

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/teams/channel"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Team>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets information about the specified Twitch team.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="teamId">The ID of the team to get. This parameter and <paramref name="teamName"/> are mutually exclusive.</param>
    /// <param name="teamName">The name of the team to get. This parameter and <paramref name="teamId"/> are mutually exclusive.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Team"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="session"/> is <see langword="null"/>.
    /// </para>
    /// <para>-or-</para>
    /// <para>
    /// Neither <paramref name="teamId"/> nor <paramref name="teamName"/> were provided.
    /// </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <para>
    /// Both <paramref name="teamId"/> and <paramref name="teamName"/> were provided.
    /// </para>
    /// </exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the team's information.</description>
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
    public static async Task<ResponseData<Team>?> GetTeams(TwitchSession session, string? teamId = null, string? teamName = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(teamId) && string.IsNullOrWhiteSpace(teamName))
        {
            throw new ArgumentNullException(nameof(teamId) + "," + nameof(teamName), "Either a team ID or team name must be provided.").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(teamId) && !string.IsNullOrWhiteSpace(teamName))
        {
            throw new ArgumentOutOfRangeException(nameof(teamId) + "," + nameof(teamName), "The team ID and team name parameters are mutually exclusive.").Log(TwitchApi.GetLogger());
        }

        session.RequireUserOrAppToken();

        NameValueCollection queryParameters = [];
        if (!string.IsNullOrWhiteSpace(teamId))
        {
            queryParameters.Add("id", teamId);
        }
        else
        {
            queryParameters.Add("name", teamName);
        }

        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, Util.BuildUri(new("/teams"), queryParameters), session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Team>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
