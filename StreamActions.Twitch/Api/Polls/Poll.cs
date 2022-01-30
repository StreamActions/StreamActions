/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using StreamActions.Common;
using StreamActions.Common.Exceptions;
using StreamActions.Twitch.Api.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Polls
{
    /// <summary>
    /// Sends and represents a response element for a request for poll information.
    /// </summary>
    public record Poll
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
        /// Indicates if Bits can be used for voting.
        /// </summary>
        [JsonPropertyName("bits_voting_enabled")]
        public bool? BitsVotingEnabled { get; init; }

        /// <summary>
        /// Number of Bits required to vote once with Bits.
        /// </summary>
        [JsonPropertyName("bits_per_vote")]
        public int? BitsPerVote { get; init; }

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
        /// <remarks>
        /// Valid values are:
        /// <c>ACTIVE</c>: Poll is currently in progress.
        /// <c>COMPLETED</c>: Poll has reached its ended_at time.
        /// <c>TERMINATED</c>: Poll has been manually terminated before its <see cref="EndedAt"/> time.
        /// <c>ARCHIVED</c>: Poll is no longer visible on the channel.
        /// <c>MODERATED</c>: Poll is no longer visible to any user on Twitch.
        /// <c>INVALID</c>: Something went wrong determining the state.
        /// </remarks>
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        /// <summary>
        /// Total duration for the poll (in seconds).
        /// </summary>
        [JsonPropertyName("duration")]
        public int? Duration { get; init; }

        /// <summary>
        /// UTC timestamp for the poll's start time as a string.
        /// </summary>
        [JsonPropertyName("started_at")]
        public string? StartedAtString { get; init; }

        /// <summary>
        /// UTC timestamp for the poll's start time.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? StartedAt => Util.Iso8601ToDateTime(this.StartedAtString ?? "");

        /// <summary>
        /// UTC timestamp for the poll's end time as a string. Set to <c>null</c> if the poll is active.
        /// </summary>
        [JsonPropertyName("ended_at")]
        public string? EndedAtString { get; init; }

        /// <summary>
        /// UTC timestamp for the poll's end time. Set to <c>null</c> if the poll is active.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime? EndedAt => Util.Iso8601ToDateTime(this.EndedAtString ?? "");

        /// <summary>
        /// Get information about all polls or specific polls for a Twitch channel. Poll information is available for 90 days.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="broadcasterId">The broadcaster running polls. Provided broadcaster_id must match the user_id in the user OAuth token.</param>
        /// <param name="id">ID of a poll. Filters results to one or more specific polls. Not providing one or more IDs will return the full list of polls for the authenticated channel. Maximum: 100</param>
        /// <param name="after">Cursor for forward pagination: tells the server where to start fetching the next set of results in a multi-page response. The cursor value specified here is from the pagination response field of a prior query.</param>
        /// <param name="first">Maximum number of objects to return. Maximum: 20. Default: 20.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is null, empty, or whitespace; <paramref name="after"/> is not null, but is empty or whitespace.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="id"/> is not null and has more than 100 elements.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>channel:read:polls</c> is not present.</exception>
        public static async Task<ResponseData<Poll>?> GetPolls(TwitchSession session, string broadcasterId, IEnumerable<string>? id = null, string? after = null, int first = 20)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (id is not null && id.Count() > 100)
            {
                throw new InvalidOperationException(nameof(id) + " must have a count <= 100");
            }

            if (after is not null && string.IsNullOrWhiteSpace(after))
            {
                throw new ArgumentNullException(nameof(after));
            }

            if (!session.Token?.HasScope("channel:read:polls") ?? false)
            {
                throw new ScopeMissingException("channel:read:polls");
            }

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
                    ids.Add(pollId);
                }

                queryParams.Add("id", ids);
            }

            if (after is not null)
            {
                queryParams.Add("after", new List<string> { after });
            }

            Uri uri = Util.BuildUri(new("/polls"), queryParams);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<Poll>>().ConfigureAwait(false);
        }

        /// <summary>
        /// Create a poll for a specific Twitch channel.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="parameters">The <see cref="PollCreationParameters"/> with the request parameters.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is null.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>channel:manage:polls</c> is not present.</exception>
        public static async Task<ResponseData<Poll>?> CreatePoll(TwitchSession session, PollCreationParameters parameters)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!session.Token?.HasScope("channel:manage:polls") ?? false)
            {
                throw new ScopeMissingException("channel:manage:polls");
            }

            using JsonContent content = JsonContent.Create(parameters);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, new("/polls"), session, content).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<Poll>>().ConfigureAwait(false);
        }

        /// <summary>
        /// End a poll that is currently active.
        /// </summary>
        /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
        /// <param name="parameters">The <see cref="PollEndParameters"/> with the request parameters.</param>
        /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="Poll"/> containing the response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is null.</exception>
        /// <exception cref="ScopeMissingException"><paramref name="session"/> contains a scope list, and <c>channel:manage:polls</c> is not present.</exception>
        public static async Task<ResponseData<Poll>?> EndPoll(TwitchSession session, PollEndParameters parameters)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!session.Token?.HasScope("channel:manage:polls") ?? false)
            {
                throw new ScopeMissingException("channel:manage:polls");
            }

            using JsonContent content = JsonContent.Create(parameters);
            HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, new("/polls"), session, content).ConfigureAwait(false);
            return await response.Content.ReadFromJsonAsync<ResponseData<Poll>>().ConfigureAwait(false);
        }
    }
}
