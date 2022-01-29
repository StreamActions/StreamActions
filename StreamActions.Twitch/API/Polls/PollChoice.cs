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

namespace StreamActions.Twitch.Api.Polls
{
    /// <summary>
    /// Represents a choice in <see cref="Poll.Choices"/>.
    /// </summary>
    public record PollChoice
    {
        /// <summary>
        /// ID for the choice.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        /// <summary>
        /// Text displayed for the choice.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        /// <summary>
        /// Total number of votes received for the choice across all methods of voting.
        /// </summary>
        [JsonPropertyName("votes")]
        public int? Votes { get; init; }

        /// <summary>
        /// Number of votes received via Channel Points.
        /// </summary>
        [JsonPropertyName("channel_points_votes")]
        public int? ChannelPointsVotes { get; init; }

        /// <summary>
        /// Number of votes received via Bits.
        /// </summary>
        [JsonPropertyName("bits_votes")]
        public int? BitsVotes { get; init; }
    }
}
