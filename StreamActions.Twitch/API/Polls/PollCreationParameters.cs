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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.API.Polls
{
    /// <summary>
    /// Represents the parameters for <see cref="Poll.CreatePoll(Common.TwitchSession, PollCreationParameters)"/>.
    /// </summary>
    public record PollCreationParameters
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="broadcasterId">The broadcaster running polls. Provided broadcaster_id must match the user_id in the user OAuth token.</param>
        /// <param name="title">Question displayed for the poll. Maximum: 60 characters.</param>
        /// <param name="choices">Array of the poll choices. Minimum: 2 choices.Maximum: 5 choices.</param>
        /// <param name="duration">Total duration for the poll (in seconds). Minimum: 15. Maximum: 1800.</param>
        /// <param name="bitsVotingEnabled">Indicates if Bits can be used for voting. Default: <c>false</c>.</param>
        /// <param name="bitsPerVote">Number of Bits required to vote once with Bits. Minimum: 0. Maximum: 10000.</param>
        /// <param name="channelPointsVotingEnabled">Indicates if Channel Points can be used for voting. Default: <c>false</c>.</param>
        /// <param name="channelPointsPerVote">Number of Channel Points required to vote once with Channel Points. Minimum: 0. Maximum: 1000000.</param>
        /// <exception cref="ArgumentNullException"><paramref name="broadcasterId"/> or <paramref name="title"/> is null, empty, or whitespace; <paramref name="choices"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="choices"/> has less than 2 or more than 5 elements; <paramref name="title"/> is more than 60 characters.</exception>
        public PollCreationParameters(string broadcasterId, string title, IReadOnlyList<PollCreationChoice> choices, int duration, bool bitsVotingEnabled = false,
            int bitsPerVote = 0, bool channelPointsVotingEnabled = false, int channelPointsPerVote = 0)
        {
            if (string.IsNullOrWhiteSpace(broadcasterId)
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentNullException(nameof(title));
            }

            if (title.Length > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(title));
            }

            if (choices is null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            if (choices.Count is < 2 or > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(choices));
            }

            duration = Math.Clamp(duration, 15, 1800);
            bitsPerVote = Math.Clamp(bitsPerVote, 0, 10000);
            channelPointsPerVote = Math.Clamp(channelPointsPerVote, 0, 1000000);

            this.BroadcasterId = broadcasterId;
            this.Title = title;
            this.Choices = choices;
            this.Duration = duration;
            this.BitsVotingEnabled = bitsVotingEnabled;
            this.BitsPerVote = bitsPerVote;
            this.ChannelPointsVotingEnabled = channelPointsVotingEnabled;
            this.ChannelPointsPerVote = channelPointsPerVote;
        }

        /// <summary>
        /// The broadcaster running polls. Provided broadcaster_id must match the user_id in the user OAuth token.
        /// </summary>
        [JsonPropertyName("broadcaster_id")]
        public string BroadcasterId { get; private init; }

        /// <summary>
        /// Question displayed for the poll. Maximum: 60 characters.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; private init; }

        /// <summary>
        /// Array of the poll choices. Minimum: 2 choices.Maximum: 5 choices.
        /// </summary>
        [JsonPropertyName("choices")]
        public IReadOnlyList<PollCreationChoice> Choices { get; private init; }

        /// <summary>
        /// Total duration for the poll (in seconds). Minimum: 15. Maximum: 1800.
        /// </summary>
        [JsonPropertyName("duration")]
        public int Duration { get; private init; }

        /// <summary>
        /// Indicates if Bits can be used for voting. Default: <c>false</c>.
        /// </summary>
        [JsonPropertyName("bits_voting_enabled")]
        public bool BitsVotingEnabled { get; private init; }

        /// <summary>
        /// Number of Bits required to vote once with Bits. Minimum: 0. Maximum: 10000.
        /// </summary>
        [JsonPropertyName("bits_per_vote")]
        public int BitsPerVote { get; private init; }

        /// <summary>
        /// Indicates if Channel Points can be used for voting. Default: <c>false</c>.
        /// </summary>
        [JsonPropertyName("channel_points_voting_enabled")]
        public bool ChannelPointsVotingEnabled { get; private init; }

        /// <summary>
        /// Number of Channel Points required to vote once with Channel Points. Minimum: 0. Maximum: 1000000.
        /// </summary>
        [JsonPropertyName("channel_points_per_vote")]
        public int ChannelPointsPerVote { get; private init; }
    }
}
