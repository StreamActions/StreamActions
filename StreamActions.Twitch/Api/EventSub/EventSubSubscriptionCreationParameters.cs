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

using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.EventSub;

/// <summary>
/// Represents the parameters for <see cref="EventSubSubscription.CreateEventSubSubscription(Common.TwitchSession, EventSubSubscriptionCreationParameters)"/>.
/// </summary>
public sealed record EventSubSubscriptionCreationParameters
{
    /// <summary>
    /// The type of subscription to create.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// The version number that identifies the definition of the subscription type that you want the response to use.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// The parameter values that are specific to the specified subscription type.
    /// </summary>
    [JsonPropertyName("condition")]
    public EventSubCondition? Condition { get; init; }

    /// <summary>
    /// The transport details that you want Twitch to use when sending you notifications.
    /// </summary>
    [JsonPropertyName("transport")]
    public EventSubTransport? Transport { get; init; }
}
