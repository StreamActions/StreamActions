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

using StreamActions.Common.Net;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Common;

/// <summary>
/// A Helix response with a data array.
/// </summary>
/// <typeparam name="TDataType">The type representing the returned data.</typeparam>
public record ResponseData<TDataType> : JsonApiResponse
{
    /// <summary>
    /// The data array.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<TDataType>? Data { get; init; }

    /// <summary>
    /// The reporting window's start and end dates.
    /// </summary>
    [JsonPropertyName("date_range")]
    public DateRange? DateRange { get; init; }

    /// <summary>
    /// The pagination cursor information for multi-page responses.
    /// </summary>
    [JsonPropertyName("pagination")]
    public Pagination? Pagination { get; init; }

    /// <summary>
    /// The total number of results returned.
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    /// <summary>
    /// The sum of all subscription costs in a
    /// <see cref="EventSub.EventSubSubscription.GetEventSubSubscriptions(TwitchSession, EventSub.EventSubSubscription.SubscriptionStatus?, string, string, string)"/>
    /// response.
    /// </summary>
    [JsonPropertyName("total_cost")]
    public int? TotalCost { get; init; }

    /// <summary>
    /// The maximum subscription cost that is allowed in a
    /// <see cref="EventSub.EventSubSubscription.GetEventSubSubscriptions(TwitchSession, EventSub.EventSubSubscription.SubscriptionStatus?, string, string, string)"/>
    /// response.
    /// </summary>
    [JsonPropertyName("max_total_cost")]
    public int? MaxTotalCost { get; init; }
}
