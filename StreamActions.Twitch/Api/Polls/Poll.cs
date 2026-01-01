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

namespace StreamActions.Twitch.Api.Polls;

/// <summary>
/// Sends and represents a response element for a request for poll information.
/// </summary>
public sealed record Poll
{
    /// <summary>
    /// An ID that identifies the poll.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that identifies the broadcaster that created the poll.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The broadcaster's display name.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The broadcaster's login name.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// The question that viewers are voting on.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// A list of choices that viewers can choose from. The list will contain a minimum of two choices and up to a maximum of five choices.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<PollChoice>? Choices { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether viewers may cast additional votes using Channel Points.
    /// </summary>
    [JsonPropertyName("channel_points_voting_enabled")]
    public bool? ChannelPointsVotingEnabled { get; init; }

    /// <summary>
    /// The number of points the viewer must spend to cast one additional vote.
    /// </summary>
    [JsonPropertyName("channel_points_per_vote")]
    public int? ChannelPointsPerVote { get; init; }

    /// <summary>
    /// The poll's status.
    /// </summary>
    [JsonPropertyName("status")]
    public PollStatus? Status { get; init; }

    /// <summary>
    /// The length of time (in seconds) that the poll will run for.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The length of time that the poll will run for.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// The UTC date and time of when the poll began.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The UTC date and time of when the poll ended. If <see cref="Status"/> is <see cref="PollStatus.Active"/>, this field is set to <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; init; }

    /// <summary>
    /// The poll's status.
    /// </summary>
    [JsonConverter(typeof(JsonUpperCaseEnumConverter<PollStatus>))]
    public enum PollStatus
    {
        /// <summary>
        /// Something went wrong while determining the state.
        /// </summary>
        Invalid,
        /// <summary>
        /// The poll is running.
        /// </summary>
        Active,
        /// <summary>
        /// The poll ended on schedule.
        /// </summary>
        Completed,
        /// <summary>
        /// The poll was terminated before its scheduled end.
        /// </summary>
        Terminated,
        /// <summary>
        /// The poll has been archived and is no longer visible on the channel.
        /// </summary>
        Archived,
        /// <summary>
        /// The poll was deleted.
        /// </summary>
        Moderated
    }

    /// <summary>
    /// Gets a list of polls that the broadcaster created.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that created the polls. This ID must match the user ID in the user access token.</param>
    /// <param name="id">A list of IDs that identify the polls to return. Specify this parameter only if you want to filter the list that the request returns. The endpoint ignores duplicate IDs and those not owned by this broadcaster. Maximum: 20</param>
    /// <param name="first">The maximum number of items to return per page in the response. Maximum: 20. Default: 20.</param>
    /// <param name="after">The cursor used to get the next page of results.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is not <see langword="null"/> and has more than 20 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadPolls"/> or <see cref="Scope.ChannelManagePolls"/>.</exception>
    /// <remarks>
    /// <para>
    /// Polls are available for 90 days after they're created.
    /// </para>
    /// <para>
    /// The polls are returned in descending order of start time unless you specify IDs in the request, in which case they're returned in the same order as you passed them in the request.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's polls.</description>
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
    /// <description>The polls with the specified ids do not exist.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public static async Task<ResponseData<Poll>?> GetPolls(TwitchSession session, string broadcasterId, IEnumerable<string>? id = null, int first = 20, string? after = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (id is not null && id.Count() > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 20").Log(TwitchApi.GetLogger());
        }

        session.RequireUserToken(Scope.ChannelReadPolls, Scope.ChannelManagePolls);

        first = Math.Clamp(first, 1, 20);

        NameValueCollection queryParams = new()
        {
            { "broadcaster_id", broadcasterId },
            { "first", first.ToString(CultureInfo.InvariantCulture) }
        };

        if (id is not null && id.Any())
        {
            List<string> ids = [];
            foreach (string pollId in id)
            {
                if (!string.IsNullOrWhiteSpace(pollId))
                {
                    ids.Add(pollId);
                }
            }

            if (ids.Count != 0)
            {
                queryParams.Add("id", ids);
            }
        }

        if (!string.IsNullOrWhiteSpace(after))
        {
            queryParams.Add("after", after);
        }

        Uri uri = Util.BuildUri(new("/polls"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Poll>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a poll that viewers in the broadcaster's channel can vote on.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="PollCreationParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><see cref="PollCreationParameters.BroadcasterId"/>, <see cref="PollCreationParameters.Title"/>, or a <see cref="PollCreationChoice.Title"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><see cref="PollCreationParameters.Duration"/> is <see langword="null"/>; <see cref="PollCreationParameters.Choices"/> is <see langword="null"/> or empty; <see cref="PollCreationParameters.ChannelPointsPerVote"/> is <see langword="null"/> when <see cref="PollCreationParameters.ChannelPointsVotingEnabled"/> is <see langword="true"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="PollCreationParameters.Title"/> has more than 60 characters; <see cref="PollCreationParameters.Choices"/> has less than 2 or more than 5 elements; a <see cref="PollCreationChoice.Title"/> has more than 25 characters.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManagePolls"/>.</exception>
    /// <remarks>
    /// <para>
    /// The poll begins as soon as it's created. You may run only one poll at a time.
    /// </para>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's polls.</description>
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
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

        session.RequireUserToken(Scope.ChannelManagePolls);

        if (string.IsNullOrWhiteSpace(parameters.BroadcasterId))
        {
            throw new ArgumentNullException(nameof(parameters.BroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Title))
        {
            throw new ArgumentNullException(nameof(parameters.Title)).Log(TwitchApi.GetLogger());
        }

        if (!parameters.Duration.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.Duration)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Choices is null || !parameters.Choices.Any())
        {
            throw new ArgumentNullException(nameof(parameters.Choices)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Title.Length > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Title), parameters.Title.Length, "must be 60 characters or less").Log(TwitchApi.GetLogger());
        }

        if (parameters.Choices.Count is < 2 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Choices), parameters.Choices.Count, "must provide 2-5 choices").Log(TwitchApi.GetLogger());
        }

        foreach (PollCreationChoice choice in parameters.Choices)
        {
            if (string.IsNullOrWhiteSpace(choice.Title))
            {
                throw new ArgumentNullException(nameof(parameters.Choices), "a choice may not be null, empty, or whitespace").Log(TwitchApi.GetLogger());
            }
            else if (choice.Title.Length > 25)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters.Choices), choice.Title, "a choices length must be <= 25").Log(TwitchApi.GetLogger());
            }
        }

        if (parameters.ChannelPointsVotingEnabled.HasValue && parameters.ChannelPointsVotingEnabled.Value && !parameters.ChannelPointsPerVote.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.ChannelPointsPerVote), "must be defined if ChannelPointsVotingEnabled is true").Log(TwitchApi.GetLogger());
        }

        if (Math.Clamp(parameters.Duration.Value, 15, 1800) != parameters.Duration.Value)
        {
            parameters = parameters with { Duration = Math.Clamp(parameters.Duration.Value, 15, 1800) };
        }

        if (parameters.ChannelPointsPerVote.HasValue && Math.Clamp(parameters.ChannelPointsPerVote.Value, 1, 1000000) != parameters.ChannelPointsPerVote.Value)
        {
            parameters = parameters with { ChannelPointsPerVote = Math.Clamp(parameters.ChannelPointsPerVote.Value, 1, 1000000) };
        }

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/polls"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Poll>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends an active poll. You have the option to end it or end it and archive it.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="parameters">The <see cref="PollEndParameters"/> with the request parameters.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><see cref="PollEndParameters.BroadcasterId"/>, <see cref="PollEndParameters.Id"/>, or <see cref="PollEndParameters.Status"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="PollEndParameters.Status"/> is not a valid value.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManagePolls"/>.</exception>
    /// <remarks>
    /// <para>
    /// Response Codes:
    /// <list type="table">
    /// <item>
    /// <term>200 OK</term>
    /// <description>Successfully retrieved the broadcaster's polls.</description>
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
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

        session.RequireUserToken(Scope.ChannelManagePolls);

        if (string.IsNullOrWhiteSpace(parameters.BroadcasterId))
        {
            throw new ArgumentNullException(nameof(parameters.BroadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Id))
        {
            throw new ArgumentNullException(nameof(parameters.Id)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Status is null)
        {
            throw new ArgumentNullException(nameof(parameters.Status)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Status is not PollStatus.Terminated or PollStatus.Archived)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Status), parameters.Status, "must be one of Terminated or Archived").Log(TwitchApi.GetLogger());
        }

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, new("/polls"), session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<Poll>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
