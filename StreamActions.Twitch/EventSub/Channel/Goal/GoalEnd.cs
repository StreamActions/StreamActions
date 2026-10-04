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
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using StreamActions.Twitch.EventSub.Channel.Goal.Objects;

namespace StreamActions.Twitch.EventSub.Channel.Goal;

/// <summary>
/// An event that is sent when a goal ends.
/// </summary>
public sealed record GoalEnd : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.goal.end";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// An ID that identifies this event.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// An ID that uniquely identifies the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcasters display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The broadcasters user handle.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The type of goal.
    /// </summary>
    [JsonPropertyName("type")]
    public GoalType? GoalType { get; init; }

    /// <summary>
    /// A description of the goal, if specified.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether the broadcaster achieved their goal.
    /// </summary>
    [JsonPropertyName("is_achieved")]
    public bool? IsAchieved { get; init; }

    /// <summary>
    /// The goals current value.
    /// </summary>
    [JsonPropertyName("current_amount")]
    public int? CurrentAmount { get; init; }

    /// <summary>
    /// The goals target value.
    /// </summary>
    [JsonPropertyName("target_amount")]
    public int? TargetAmount { get; init; }

    /// <summary>
    /// The UTC timestamp in RFC 3339 format, which indicates when the broadcaster created the goal.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The UTC timestamp in RFC 3339 format, which indicates when the broadcaster ended the goal.
    /// </summary>
    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; init; }
}
