/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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
/// Represents the parameters for <see cref="Poll.CreatePoll(Common.TwitchSession, PollCreationParameters)"/>.
/// </summary>
public sealed record PollCreationParameters
{
    /// <summary>
    /// The ID of the broadcaster that's running the poll. This ID must match the user ID in the user access token.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The question that viewers will vote on. The question may contain a maximum of 60 characters.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// A list of choices that viewers may choose from. The list must contain a minimum of 2 choices and up to a maximum of 5 choices.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<PollCreationChoice>? Choices { get; init; }

    /// <summary>
    /// The length of time (in seconds) that the poll will run for. The minimum is 15 seconds and the maximum is 1800 seconds (30 minutes).
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether viewers may cast additional votes using Channel Points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not set this field, use <see langword="null"/>; if set to <see langword="true"/>, <see cref="ChannelPointsPerVote"/> must be defined.
    /// </para>
    /// </remarks>
    [JsonPropertyName("channel_points_voting_enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ChannelPointsVotingEnabled { get; init; }

    /// <summary>
    /// The number of points that the viewer must spend to cast one additional vote. The minimum is 1 and the maximum is 1mil.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not set this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("channel_points_per_vote")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ChannelPointsPerVote { get; init; }
}
