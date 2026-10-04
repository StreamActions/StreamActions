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

using System.Text.Json.Serialization;
using StreamActions.Twitch.EventSub.Channel.Poll.Objects;

namespace StreamActions.Twitch.EventSub.Channel.Poll.Objects;

/// <summary>
/// The Channel Points voting settings for the poll.
/// </summary>
public sealed record ChannelPointsVoting
{
    /// <summary>
    /// Indicates if Channel Points can be used for voting.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Number of Channel Points required to vote once with Channel Points.
    /// </summary>
    [JsonPropertyName("amount_per_vote")]
    public int? AmountPerVote { get; init; }
}
