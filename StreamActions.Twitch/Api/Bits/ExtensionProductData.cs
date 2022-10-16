/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Represents the product acquired, as it looked at the time of the <see cref="ExtensionTransaction"/>.
/// </summary>
public record ExtensionProductData
{
    /// <summary>
    /// Set to twitch.ext + your Extension ID.
    /// </summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    /// <summary>
    /// Unique identifier for the product across the Extension
    /// </summary>
    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    /// <summary>
    /// A <see cref="ExtensionProductCost"/> representing the cost to acquire the product.
    /// </summary>
    [JsonPropertyName("cost")]
    public ExtensionProductCost? Cost { get; init; }

    /// <summary>
    /// Indicates if the product is in development
    /// </summary>
    [JsonPropertyName("inDevelopment")]
    public bool? InDevelopment { get; init; }

    /// <summary>
    /// Display name of the product.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// The expiration timestamp for the product. Currently always empty since only unexpired products can be purchased.
    /// </summary>
    [JsonPropertyName("expiration")]
    public DateTime? Expiration { get; init; }

    /// <summary>
    /// Indicates whether or not the data was sent over the Extension PubSub to all instances of the Extension.
    /// </summary>
    [JsonPropertyName("broadcast")]
    public bool? Broadcast { get; init; }
}
