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

namespace StreamActions.Twitch.Api.Extensions;

/// <summary>
/// Parameters for <see cref="ExtensionBitsProduct.UpdateExtensionBitsProduct(Common.TwitchSession, UpdateExtensionBitsProductParameters)"/>.
/// </summary>
public sealed record UpdateExtensionBitsProductParameters
{
    /// <summary>
    /// The product's SKU. The SKU must be unique within an extension.
    /// </summary>
    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    /// <summary>
    /// An object that contains the product's cost information.
    /// </summary>
    [JsonPropertyName("cost")]
    public ExtensionProductCost? Cost { get; init; }

    /// <summary>
    /// The product's name as displayed in the extension. The maximum length is 255 characters.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether the product is in development.
    /// </summary>
    [JsonPropertyName("in_development")]
    public bool? InDevelopment { get; init; }

    /// <summary>
    /// The date and time, in RFC3339 format, when the product expires. If not set, the product does not expire.
    /// </summary>
    [JsonPropertyName("expiration")]
    public DateTime? Expiration { get; init; }

    /// <summary>
    /// A Boolean value that determines whether Bits product purchase events are broadcast to all instances of the extension on a channel.
    /// </summary>
    [JsonPropertyName("is_broadcast")]
    public bool? IsBroadcast { get; init; }
}
