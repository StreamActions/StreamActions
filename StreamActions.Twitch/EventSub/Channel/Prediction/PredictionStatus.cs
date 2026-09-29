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

namespace StreamActions.Twitch.EventSub.Channel.Prediction;

/// <summary>
/// The status of a Channel Points Prediction.
/// </summary>
[JsonConverter(typeof(JsonCustomEnumConverter<PredictionStatus>))]
public enum PredictionStatus
{
    /// <summary>
    /// The Prediction was resolved.
    /// </summary>
    [JsonCustomEnum("resolved")]
    Resolved,

    /// <summary>
    /// The Prediction was canceled.
    /// </summary>
    [JsonCustomEnum("canceled")]
    Canceled
}
