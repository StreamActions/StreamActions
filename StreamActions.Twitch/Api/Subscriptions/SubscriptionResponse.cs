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

using StreamActions.Twitch.Api.Common;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Subscriptions;

/// <summary>
/// A <see cref="ResponseData{TDataType}"/> with the subscription <see cref="Points"/> field.
/// </summary>
public sealed record SubscriptionResponse : ResponseData<Subscription>
{
    /// <summary>
    /// The current number of subscriber points earned by this broadcaster.
    /// </summary>
    /// <remarks>
    /// Points are based on the subscription tier of each user that subscribes to this broadcaster.
    /// For example, a Tier 1 subscription is worth 1 point, Tier 2 is worth 2 points, and Tier 3 is worth 6 points.
    /// The number of points determines the number of emote slots that are unlocked for the broadcaster.
    /// </remarks>
    [JsonPropertyName("points")]
    public int? Points { get; init; }
}
