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
    /// Represents the parameters for <see cref="Poll.EndPoll(Common.TwitchSession, PollEndParameters)"/>
    /// </summary>
    public record PollEndParameters
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="broadcasterId">The broadcaster running polls. Provided broadcaster_id must match the user_id in the user OAuth token.</param>
        /// <param name="id">ID of the poll.</param>
        /// <param name="status">The poll status to be set. See the remarks for <see cref="Status"/>. Valid values: <c>TERMINATED</c> or <c>ARCHIVED</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="broadcasterId"/>, <paramref name="id"/>, or <paramref name="status"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a valid value.</exception>
        public PollEndParameters(string broadcasterId, string id, string status)
        {
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (status is not "TERMINATED" and not "ARCHIVED")
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            this.BroadcasterId = broadcasterId;
            this.Id = id;
            this.Status = status;
        }

        /// <summary>
        /// The broadcaster running polls. Provided broadcaster_id must match the user_id in the user OAuth token.
        /// </summary>
        [JsonPropertyName("broadcaster_id")]
        public string BroadcasterId { get; private init; }

        /// <summary>
        /// ID of the poll.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; private init; }

        /// <summary>
        /// The poll status to be set.
        /// </summary>
        /// <remarks>
        /// Valid values:
        /// <c>TERMINATED</c>: End the poll manually, but allow it to be viewed publicly.
        /// <c>ARCHIVED</c>: End the poll manually and do not allow it to be viewed publicly.
        /// </remarks>
        [JsonPropertyName("status")]
        public string Status { get; private init; }
    }
}
