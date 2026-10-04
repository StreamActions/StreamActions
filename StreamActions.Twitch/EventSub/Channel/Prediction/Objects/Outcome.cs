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
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Twitch.EventSub.Channel.Prediction.Objects;

/// <summary>
/// An outcome for a Channel Points Prediction.
/// </summary>
public sealed record Outcome
{
    /// <summary>
    /// The outcome ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The outcome title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The color for the outcome. Valid values are pink and blue.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>
    /// The number of users who used Channel Points on this outcome.
    /// </summary>
    [JsonPropertyName("users")]
    public int? Users { get; init; }

    /// <summary>
    /// The total number of Channel Points used on this outcome.
    /// </summary>
    [JsonPropertyName("channel_points")]
    public int? ChannelPoints { get; init; }

    /// <summary>
    /// An array of users who used the most Channel Points on this outcome.
    /// </summary>
    [JsonPropertyName("top_predictors")]
    public IReadOnlyCollection<TopPredictor>? TopPredictors { get; init; }
}
