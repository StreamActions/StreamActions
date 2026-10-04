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

namespace StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomRewardRedemption.Objects;

/// <summary>
/// Basic information about the reward that was redeemed, at the time it was redeemed.
/// </summary>
public sealed record Reward
{
    /// <summary>
    /// The reward identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The reward title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The reward cost.
    /// </summary>
    [JsonPropertyName("cost")]
    public int Cost { get; init; }

    /// <summary>
    /// The reward description.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }
}
