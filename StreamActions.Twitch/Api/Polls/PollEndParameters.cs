/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Polls
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
        /// <param name="status">The poll status to be set. Valid values: <see cref="Poll.PollStatus.TERMINATED"/> or <see cref="Poll.PollStatus.ARCHIVED"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="broadcasterId"/> or <paramref name="id"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a valid value.</exception>
        public PollEndParameters(string broadcasterId, string id, Poll.PollStatus status)
        {
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                throw new ArgumentNullException(nameof(broadcasterId));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (status is not Poll.PollStatus.TERMINATED and not Poll.PollStatus.ARCHIVED)
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "Valid values: TERMINATED or ARCHIVED");
            }

            this.BroadcasterId = broadcasterId;
            this.Id = id;
            this.Status = Enum.GetName(status) ?? "";
        }

        /// <summary>
        /// The broadcaster running polls. Provided <see cref="BroadcasterId"/> must match the user_id authorized in <see cref="Common.TwitchSession.Token"/>.
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
