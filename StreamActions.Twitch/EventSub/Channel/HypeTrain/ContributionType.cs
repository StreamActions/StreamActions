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

namespace StreamActions.Twitch.EventSub.Channel.HypeTrain;

/// <summary>
/// The contribution method used.
/// </summary>
[JsonConverter(typeof(JsonCustomEnumConverter<ContributionType>))]
public enum ContributionType
{
    /// <summary>
    /// Bits contributions with Cheering, Power-ups, and Extensions.
    /// </summary>
    [JsonCustomEnum("bits")]
    Bits,

    /// <summary>
    /// Subscription activity like subscribing or gifting subscriptions.
    /// </summary>
    [JsonCustomEnum("subscription")]
    Subscription,

    /// <summary>
    /// Covers other contribution methods not listed.
    /// </summary>
    [JsonCustomEnum("other")]
    Other
}
