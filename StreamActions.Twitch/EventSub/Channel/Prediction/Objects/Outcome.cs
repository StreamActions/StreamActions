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

using StreamActions.Common.Json.Serialization;
using System.Text.Json.Serialization;

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
    public Guid? Id { get; init; }

    /// <summary>
    /// The outcome title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The color for the outcome. Valid values are blue and pink.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the Prediction has two outcomes, one is blue and the other is pink. If it has more than two outcomes, all of them are blue.
    /// </para>
    /// </remarks>
    [JsonPropertyName("color")]
    public OutcomeColors? Color { get; init; }

    /// <summary>
    /// The number of users who chose this outcome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This includes viewers who took part without spending Channel Points, which happens where Channel Points can't be used on Predictions.
    /// </para>
    /// </remarks>
    [JsonPropertyName("users")]
    public int? Users { get; init; }

    /// <summary>
    /// The total number of Channel Points used on this outcome.
    /// </summary>
    [JsonPropertyName("channel_points")]
    public int? ChannelPoints { get; init; }

    /// <summary>
    /// An array of the users who used the most Channel Points on this outcome, highest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A viewer who took part without spending Channel Points can appear with a <see cref="TopPredictor.ChannelPointsUsed"/> of 0.
    /// </para>
    /// </remarks>
    [JsonPropertyName("top_predictors")]
    public IReadOnlyList<TopPredictor>? TopPredictors { get; init; }

    /// <summary>
    /// The color for a prediction outcome.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<OutcomeColors>))]
    public enum OutcomeColors
    {
        /// <summary>
        /// Blue.
        /// </summary>
        [JsonCustomEnum("blue")]
        Blue,

        /// <summary>
        /// Pink.
        /// </summary>
        [JsonCustomEnum("pink")]
        Pink
    }
}
