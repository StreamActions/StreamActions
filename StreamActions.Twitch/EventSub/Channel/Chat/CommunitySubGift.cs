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

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// Information about the community gift sub event.
/// </summary>
public sealed record CommunitySubGift
{
    /// <summary>
    /// The ID of the associated community gift.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Number of subscriptions being gifted.
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    /// <summary>
    /// The type of subscription plan being used.
    /// </summary>
    [JsonPropertyName("sub_tier")]
    public string? SubTier { get; init; }

    /// <summary>
    /// Optional. The amount of gifts the gifter has given in this channel. Null if anonymous.
    /// </summary>
    [JsonPropertyName("cumulative_total")]
    public int? CumulativeTotal { get; init; }
}
