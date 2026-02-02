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

namespace StreamActions.Twitch.Api.Predictions;

/// <summary>
/// Represents the parameters for ending a prediction.
/// </summary>
public sealed record PredictionEndParameters
{
    /// <summary>
    /// The ID of the broadcaster that's running the prediction.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The ID of the prediction to update.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    /// The status to set the prediction to.
    /// </summary>
    [JsonPropertyName("status")]
    public Prediction.PredictionStatus? Status { get; init; }

    /// <summary>
    /// The ID of the winning outcome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Required if <see cref="Status"/> is <see cref="Prediction.PredictionStatus.Resolved"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("winning_outcome_id")]
    public Guid? WinningOutcomeId { get; init; }
}
