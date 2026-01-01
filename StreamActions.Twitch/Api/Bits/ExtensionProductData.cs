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

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Contains details about the digital product in a <see cref="ExtensionTransaction"/>.
/// </summary>
public sealed record ExtensionProductData
{
    /// <summary>
    /// Set to <c>twitch.ext.</c> + <c>the extension's ID</c>.
    /// </summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    /// <summary>
    /// An ID that identifies the digital product.
    /// </summary>
    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    /// <summary>
    /// Contains details about the digital product's cost.
    /// </summary>
    [JsonPropertyName("cost")]
    public ExtensionProductCost? Cost { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the product is in development. Is <see langword="true"/> if the digital product is in development and cannot be exchanged.
    /// </summary>
    [JsonPropertyName("inDevelopment")]
    public bool? InDevelopment { get; init; }

    /// <summary>
    /// The name of the digital product.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// This field is always empty since you may purchase only unexpired products.
    /// </summary>
    [JsonPropertyName("expiration")]
    public DateTime? Expiration { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the data was broadcast to all instances of the extension.
    /// </summary>
    [JsonPropertyName("broadcast")]
    public bool? Broadcast { get; init; }
}
