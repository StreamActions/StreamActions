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
/// Represents a viewer who made a prediction.
/// </summary>
public sealed record PredictionPredictor
{
    /// <summary>
    /// An ID that identifies the viewer.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The viewer's display name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The viewer's login name.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The number of Channel Points the viewer spent.
    /// </summary>
    [JsonPropertyName("channel_points_used")]
    public int? ChannelPointsUsed { get; init; }

    /// <summary>
    /// The number of Channel Points distributed to the viewer.
    /// </summary>
    [JsonPropertyName("channel_points_won")]
    public int? ChannelPointsWon { get; init; }
}
