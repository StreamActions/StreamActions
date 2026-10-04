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

namespace StreamActions.Twitch.EventSub.Extension.BitsTransaction;

/// <summary>
/// Additional information about a product acquired via a Twitch Extension Bits transaction.
/// </summary>
public sealed record Product
{
    /// <summary>
    /// Product name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Bits involved in the transaction.
    /// </summary>
    [JsonPropertyName("bits")]
    public int? Bits { get; init; }

    /// <summary>
    /// Unique identifier for the product acquired.
    /// </summary>
    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    /// <summary>
    /// Flag indicating if the product is in development.
    /// </summary>
    [JsonPropertyName("in_development")]
    public bool? InDevelopment { get; init; }
}
