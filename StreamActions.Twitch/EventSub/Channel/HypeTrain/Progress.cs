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
using StreamActions.Twitch.EventSub.Channel.HypeTrain.Objects;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.HypeTrain;

/// <summary>
/// An event that is sent when a Hype Train makes progress on the specified channel.
/// </summary>
/// <remarks>
/// <para>
/// Notifications are sent periodically while a Hype Train is making progress. EventSub does not make strong assurances about the order of message delivery, so it is possible to receive <see cref="Progress"/> before you receive the corresponding <see cref="Begin"/> notification. When the Hype Train is over, <see cref="HypeTrainEnd"/> is emitted.
/// </para>
/// </remarks>
public sealed record Progress : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.hype_train.progress";

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
    /// <remarks>
    /// <para>
    /// For Bits contributions, each Bit counts as 1 point. For subscription contributions, each subscription (or each month of a multi-month gift) counts as 500, 1000, or 2500 points for a Tier 1, 2, or 3 subscription.
    /// </para>
    /// </remarks>
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    /// <summary>
    /// The number of points contributed to the Hype Train at the current level.
    /// </summary>
    [JsonPropertyName("progress")]
    public int? ProgressAmount { get; init; }

    /// <summary>
    /// The number of points required to reach the next level.
    /// </summary>
    [JsonPropertyName("goal")]
    public int? Goal { get; init; }

    /// <summary>
    /// The top contributor for each contribution type, with at most one entry per type, in no particular order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The array is empty until there is a top contributor. Anonymous contributors are never listed. For a shared Hype Train, the array lists only the top contributors in this broadcaster's channel.
    /// </para>
    /// </remarks>
    [JsonPropertyName("top_contributions")]
    public IReadOnlyList<Contribution>? TopContributions { get; init; }

    /// <summary>
    /// The current level of the Hype Train.
    /// </summary>
    [JsonPropertyName("level")]
    public int? Level { get; init; }

    /// <summary>
    /// Optional. Non-null for a shared Hype Train. Contains the list of broadcasters in the shared Hype Train.
    /// </summary>
    [JsonPropertyName("shared_train_participants")]
    public IReadOnlyList<Participant>? SharedTrainParticipants { get; init; }

    /// <summary>
    /// The time when the Hype Train started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The time when the Hype Train expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; init; }

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
