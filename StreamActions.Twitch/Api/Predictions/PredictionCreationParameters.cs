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
/// Represents the parameters for creating a prediction.
/// </summary>
public sealed record PredictionCreationParameters
{
    /// <summary>
    /// The ID of the broadcaster that's running the prediction.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// The question that the broadcaster is asking.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The list of possible outcomes that the viewers may choose from.
    /// </summary>
    [JsonPropertyName("outcomes")]
    public IReadOnlyList<PredictionCreationOutcome>? Outcomes { get; init; }

    /// <summary>
    /// The length of time (in seconds) that the prediction will run for.
    /// </summary>
    [JsonPropertyName("prediction_window")]
    public int? PredictionWindowSeconds { get; init; }
}
