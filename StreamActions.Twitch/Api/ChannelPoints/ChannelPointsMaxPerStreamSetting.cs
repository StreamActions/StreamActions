/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.ChannelPoints;

/// <summary>
/// The settings used to determine whether to apply a maximum to the number of redemptions allowed per live stream in a <see cref="ChannelPointsReward"/>.
/// </summary>
public sealed record ChannelPointsMaxPerStreamSetting
{
    /// <summary>
    /// A Boolean value that determines whether the reward applies a limit on the number of redemptions allowed per live stream.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// The maximum number of redemptions allowed per live stream.
    /// </summary>
    [JsonPropertyName("max_per_stream")]
    public int? MaxPerStream { get; init; }
}
