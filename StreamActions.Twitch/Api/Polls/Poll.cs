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
using System.Net.Http.Json;
using StreamActions.Common.Extensions;

namespace StreamActions.Twitch.Api.Polls;

/// <summary>
/// Sends and represents a response element for a request for poll information.
/// </summary>
public sealed record Poll
{
    /// <summary>
    /// ID of the poll.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    ///  	Name of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// Login of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// Question displayed for the poll.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Array of the poll choices.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<PollChoice>? Choices { get; init; }

    /// <summary>
    /// Indicates if Channel Points can be used for voting.
    /// </summary>
    [JsonPropertyName("channel_points_voting_enabled")]
    public bool? ChannelPointsVotingEnabled { get; init; }

    /// <summary>
    /// Number of Channel Points required to vote once with Channel Points.
    /// </summary>
    [JsonPropertyName("channel_points_per_vote")]
    public int? ChannelPointsPerVote { get; init; }

    /// <summary>
    /// Poll status.
    /// </summary>
    [JsonPropertyName("status")]
    public PollStatus? Status { get; init; }

    /// <summary>
    /// Total duration for the poll (in seconds).
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; init; }

    /// <summary>
    /// UTC timestamp for the poll's start time.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// UTC timestamp for the poll's end time. Set to <see langword="null"/> if the poll is active.
    /// </summary>
    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; init; }

    /// <summary>
    /// Poll status.
    /// </summary>
    public enum PollStatus
    {
        /// <summary>
        /// Something went wrong determining the state.
        /// </summary>
        INVALID,
        /// <summary>
        /// Poll is currently in progress.
        /// </summary>
        ACTIVE,
        /// <summary>
        /// Poll has reached its <see cref="EndedAt"/> time.
        /// </summary>
        COMPLETED,
        /// <summary>
        /// Poll has been manually terminated before its <see cref="EndedAt"/> time.
        /// </summary>
        TERMINATED,
        /// <summary>
        /// Poll is no longer visible on the channel.
        /// </summary>
        ARCHIVED,
        /// <summary>
        /// Poll is no longer visible to any user on Twitch.
        /// </summary>
        MODERATED
    }

    /// <summary>
    /// Get information about all polls or specific polls for a Twitch channel. Poll information is available for 90 days.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The broadcaster running polls. Provided broadcaster_id must match the user_id in the user OAuth token.</param>
    /// <param name="id">ID of a poll. Filters results to one or more specific polls. Not providing one or more IDs will return the full list of polls for the authenticated channel. Maximum: 100</param>
    /// <param name="after">Cursor for forward pagination: tells the server where to start fetching the next set of results in a multi-page response. The cursor value specified here is from the pagination response field of a prior query.</param>
    /// <param name="first">Maximum number of objects to return. Maximum: 20. Default: 20.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not <see langword="null"/> and has more than 100 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadPolls"/>.</exception>
    public static async Task<ResponseData<Poll>?> GetPolls(TwitchSession session, string broadcasterId, IEnumerable<string>? id = null, string? after = null, int first = 20)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (id is not null && id.Count() > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 100").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelReadPolls);

        first = Math.Clamp(first, 1, 20);

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "broadcaster_id", new List<string> { broadcasterId } },
            { "first", new List<string> { first.ToString(CultureInfo.InvariantCulture) } }
        };

        if (id is not null && id.Any())
        {
            List<string> ids = new();
            foreach (string pollId in id)
            {
                if (!string.IsNullOrWhiteSpace(pollId))
                {
                    ids.Add(pollId);
                }
            }

            if (ids.Any())
            {
                queryParams.Add("id", ids);
            }
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", new List<string> { after });
        }

        Uri uri = Util.BuildUri(new("/polls"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Poll>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Create a poll for a specific Twitch channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="PollCreationParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManagePolls"/>.</exception>
    public static async Task<ResponseData<Poll>?> CreatePoll(TwitchSession session, PollCreationParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManagePolls);

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/polls"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Poll>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// End a poll that is currently active.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="PollEndParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManagePolls"/>.</exception>
    public static async Task<ResponseData<Poll>?> EndPoll(TwitchSession session, PollEndParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManagePolls);

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, new("/polls"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Poll>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
