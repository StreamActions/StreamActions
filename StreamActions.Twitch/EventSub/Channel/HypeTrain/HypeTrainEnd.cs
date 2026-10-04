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

namespace StreamActions.Twitch.EventSub.Channel.HypeTrain;

/// <summary>
/// An event that is sent when a Hype Train ends.
/// </summary>
public sealed record HypeTrainEnd : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.hype_train.end";

    /// <inheritdoc/>
    public static string Version => "2";

    /// <summary>
    /// The Hype Train ID.
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
    /// Total points contributed to the Hype Train.
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    /// <summary>
    /// The contributors with the most points contributed.
    /// </summary>
    [JsonPropertyName("top_contributions")]
    public Contribution[]? TopContributions { get; init; }

    /// <summary>
    /// The current level of the Hype Train.
    /// </summary>
    [JsonPropertyName("level")]
    public int? Level { get; init; }

    /// <summary>
    /// Optional. Non-null for a shared Hype Train. Contains the list of broadcasters in the shared Hype Train.
    /// </summary>
    [JsonPropertyName("shared_train_participants")]
    public Participant[]? SharedTrainParticipants { get; init; }

    /// <summary>
    /// The time when the Hype Train started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The time when the Hype Train cooldown ends so that the next Hype Train can start.
    /// </summary>
    [JsonPropertyName("cooldown_ends_at")]
    public DateTime? CooldownEndsAt { get; init; }

    /// <summary>
    /// The time when the Hype Train ended.
    /// </summary>
    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; init; }

    /// <summary>
    /// The type of the Hype Train.
    /// </summary>
    [JsonPropertyName("type")]
    public HypeTrainType? HypeTrainType { get; init; }

    /// <summary>
    /// Indicates if the Hype Train is shared.
    /// </summary>
    [JsonPropertyName("is_shared_train")]
    public bool? IsSharedTrain { get; init; }
}
