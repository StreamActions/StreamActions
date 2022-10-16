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

namespace StreamActions.Twitch.Api.Polls;

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
}
