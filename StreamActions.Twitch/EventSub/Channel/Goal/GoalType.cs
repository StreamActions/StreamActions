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

namespace StreamActions.Twitch.EventSub.Channel.Goal;

/// <summary>
/// The type of goal.
/// </summary>
[JsonConverter(typeof(JsonCustomEnumConverter<GoalType>))]
public enum GoalType
{
    /// <summary>
    /// The goal is to increase followers.
    /// </summary>
    [JsonCustomEnum("follow")]
    Follow,

    /// <summary>
    /// The goal is to increase subscriptions.
    /// </summary>
    [JsonCustomEnum("subscription")]
    Subscription,

    /// <summary>
    /// The goal is to increase the number of subscriptions.
    /// </summary>
    [JsonCustomEnum("subscription_count")]
    SubscriptionCount,

    /// <summary>
    /// The goal is to increase the tier points associated with new subscriptions.
    /// </summary>
    [JsonCustomEnum("new_subscription")]
    NewSubscription,

    /// <summary>
    /// The goal is to increase the number of new subscriptions.
    /// </summary>
    [JsonCustomEnum("new_subscription_count")]
    NewSubscriptionCount,

    /// <summary>
    /// The goal is to increase the amount of Bits used on the channel.
    /// </summary>
    [JsonCustomEnum("new_bit")]
    NewBit,

    /// <summary>
    /// The goal is to increase the number of unique Cheerers to Cheer on the channel.
    /// </summary>
    [JsonCustomEnum("new_cheerer")]
    NewCheerer
}
