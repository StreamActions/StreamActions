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
/// Represents the parameters for <see cref="Poll.CreatePoll(Common.TwitchSession, PollCreationParameters)"/>.
/// </summary>
public sealed record PollCreationParameters
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="broadcasterId">The broadcaster running polls. Provided broadcaster_id must match the user_id in the user OAuth token.</param>
    /// <param name="title">Question displayed for the poll. Maximum: 60 characters.</param>
    /// <param name="choices">Array of the poll choices. Minimum: 2 choices.Maximum: 5 choices.</param>
    /// <param name="duration">Total duration for the poll (in seconds). Minimum: 15. Maximum: 1800.</param>
    /// <param name="channelPointsVotingEnabled">Indicates if Channel Points can be used for voting. Default: <see langword="false"/>.</param>
    /// <param name="channelPointsPerVote">Number of Channel Points required to vote once with Channel Points. Minimum: 0. Maximum: 1000000.</param>
    /// <exception cref="ArgumentNullException"><paramref name="broadcasterId"/> or <paramref name="title"/> is null, empty, or whitespace; <paramref name="choices"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="choices"/> has less than 2 or more than 5 elements; <paramref name="title"/> is more than 60 characters.</exception>
    public PollCreationParameters(string broadcasterId, string title, IReadOnlyList<PollCreationChoice> choices, int duration, bool channelPointsVotingEnabled = false, int channelPointsPerVote = 0)
    {
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentNullException(nameof(title));
        }

        if (title.Length > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(title), title.Length, "Must be 60 characters or less");
        }

        if (choices is null)
        {
            throw new ArgumentNullException(nameof(choices));
        }

        if (choices.Count is < 2 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(choices), choices.Count, "Must provide 2-5 choices.");
        }

        duration = Math.Clamp(duration, 15, 1800);
        channelPointsPerVote = Math.Clamp(channelPointsPerVote, 0, 1000000);

        this.BroadcasterId = broadcasterId;
        this.Title = title;
        this.Choices = choices;
        this.Duration = duration;
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
    /// Indicates if Channel Points can be used for voting. Default: <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("channel_points_voting_enabled")]
    public bool ChannelPointsVotingEnabled { get; private init; }

    /// <summary>
    /// Number of Channel Points required to vote once with Channel Points. Minimum: 0. Maximum: 1000000.
    /// </summary>
    [JsonPropertyName("channel_points_per_vote")]
    public int ChannelPointsPerVote { get; private init; }
}
