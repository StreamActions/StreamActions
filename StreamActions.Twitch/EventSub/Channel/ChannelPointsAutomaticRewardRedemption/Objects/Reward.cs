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

namespace StreamActions.Twitch.EventSub.Channel.ChannelPointsAutomaticRewardRedemption.Objects;

/// <summary>
/// An object that describes the reward that was redeemed.
/// </summary>
public sealed record Reward
{
    /// <summary>
    /// The type of reward.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// The reward cost.
    /// </summary>
    [JsonPropertyName("cost")]
    public int Cost { get; init; }

    /// <summary>
    /// An object that contains the unlocked emote if the reward is an unlocked emote.
    /// </summary>
    [JsonPropertyName("unlocked_emote")]
    public UnlockedEmote? UnlockedEmote { get; init; }
}
