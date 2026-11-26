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
using StreamActions.Twitch.EventSub.Channel.Prediction.Objects;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.Prediction;

/// <summary>
/// An event that is sent when a Prediction is locked on the specified channel, either manually or automatically when its prediction window ends.
/// </summary>
/// <remarks>
/// <para>
/// A Prediction that is resolved or canceled while it's still active is never locked, so no lock notification is sent for it.
/// </para>
/// <para>
/// The <see cref="Outcome.Users"/>, <see cref="Outcome.ChannelPoints"/>, and <see cref="Outcome.TopPredictors"/> values are the totals counted when the Prediction locked, and may not include predictions made just before it locked. The final totals are in the <see cref="PredictionEnd"/> notification.
/// </para>
/// </remarks>
public sealed record PredictionLock : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.prediction.lock";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// Channel Points Prediction ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

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
    /// Title for the Channel Points Prediction.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// An array of outcomes for the Channel Points Prediction.
    /// </summary>
    [JsonPropertyName("outcomes")]
    public IReadOnlyList<Outcome>? Outcomes { get; init; }

    /// <summary>
    /// The time the Channel Points Prediction started.
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// The time the Channel Points Prediction was locked.
    /// </summary>
    [JsonPropertyName("locked_at")]
    public DateTime? LockedAt { get; init; }
}
