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

using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;
using StreamActions.Twitch.EventSub.Channel.Poll.Objects;

namespace StreamActions.Twitch.EventSub.Channel.Poll;

/// <summary>
/// An event that is sent when a poll started on a specified channel.
/// </summary>
public sealed record PollBegin : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.poll.begin";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// ID of the poll.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The requested broadcaster ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The requested broadcaster login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The requested broadcaster display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// Question displayed for the poll.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// An array of choices for the poll.
    /// </summary>
    [JsonPropertyName("choices")]
    public Choice[]? Choices { get; init; }

    /// <summary>
    /// Not supported.
    /// </summary>
    [JsonPropertyName("bits_voting")]
    public BitsVoting? BitsVoting { get; init; }

    /// <summary>
    /// The Channel Points voting settings for the poll.
    /// </summary>
    [JsonPropertyName("channel_points_voting")]
    public ChannelPointsVoting? ChannelPointsVoting { get; init; }

    /// <summary>
    /// The time the poll started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The time the poll will end.
    /// </summary>
    [JsonPropertyName("ends_at")]
    public DateTime? EndsAt { get; init; }
}
