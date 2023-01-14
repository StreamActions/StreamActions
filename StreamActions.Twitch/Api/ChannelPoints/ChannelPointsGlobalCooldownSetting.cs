/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
/// The settings used to determine whether to apply a cooldown period between redemptions and the length of the cooldown in a <see cref="ChannelPointsReward"/>.
/// </summary>
public sealed record ChannelPointsGlobalCooldownSetting
{
    /// <summary>
    /// A Boolean value that determines whether to apply a cooldown period.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// The cooldown period, in seconds.
    /// </summary>
    [JsonPropertyName("global_cooldown_seconds")]
    public int? GlobalCooldownSeconds { get; init; }

    /// <summary>
    /// The cooldown period.
    /// </summary>
    public TimeSpan? GlobalCooldown => this.GlobalCooldownSeconds.HasValue ? TimeSpan.FromSeconds(this.GlobalCooldownSeconds.Value) : null;
}
