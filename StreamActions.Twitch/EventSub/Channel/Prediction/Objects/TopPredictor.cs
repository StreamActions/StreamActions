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
using StreamActions.Twitch.EventSub.Channel.Prediction.Objects;

namespace StreamActions.Twitch.EventSub.Channel.Prediction.Objects;

/// <summary>
/// A user who participated in a Channel Points Prediction.
/// </summary>
public sealed record TopPredictor
{
    /// <summary>
    /// The ID of the user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The login of the user.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The display name of the user.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The number of Channel Points won. This value is always null in the event payload for Prediction progress and Prediction lock. This value is 0 if the outcome did not win or if the Prediction was canceled and Channel Points were refunded.
    /// </summary>
    [JsonPropertyName("channel_points_won")]
    public int? ChannelPointsWon { get; init; }

    /// <summary>
    /// The number of Channel Points used to participate in the Prediction.
    /// </summary>
    [JsonPropertyName("channel_points_used")]
    public int? ChannelPointsUsed { get; init; }
}
