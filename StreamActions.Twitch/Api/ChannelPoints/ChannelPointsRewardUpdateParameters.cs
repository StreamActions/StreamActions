﻿/*
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
/// The parameters for <see cref="ChannelPointsReward.UpdateCustomReward(Common.TwitchSession, string, Guid, ChannelPointsRewardUpdateParameters)"/>
/// </summary>
public sealed record ChannelPointsRewardUpdateParameters : ChannelPointsRewardCreationParameters
{
    /// <summary>
    /// A Boolean value that determines whether the reward is currently paused. Viewers can't redeem paused rewards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("is_paused")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsPaused { get; init; }
}
