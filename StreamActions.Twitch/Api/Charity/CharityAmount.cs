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

namespace StreamActions.Twitch.Api.Charity;

/// <summary>
/// Represents a monetary amount in a charity campaign.
/// </summary>
public sealed record CharityAmount
{
    /// <summary>
    /// The monetary amount. The amount is specified in the currency's minor unit.
    /// For example, the minor units for USD is cents, so if the amount is $5.50 USD, value is set to 550.
    /// </summary>
    [JsonPropertyName("value")]
    public long? Value { get; init; }

    /// <summary>
    /// The number of decimal places used by the currency. For example, USD uses two decimal places.
    /// Use this number to translate value from minor units to major units by using the formula: value / 10^decimal_places
    /// </summary>
    [JsonPropertyName("decimal_places")]
    public int? DecimalPlaces { get; init; }

    /// <summary>
    /// The ISO-4217 three-letter currency code that identifies the type of currency in value.
    /// </summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}
