/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
/// Contains details about the digital product's cost in a <see cref="ExtensionProductData"/>.
/// </summary>
public sealed record ExtensionProductCost
{
    /// <summary>
    /// The amount exchanged for the digital product.
    /// </summary>
    [JsonPropertyName("amount")]
    public int? Amount { get; init; }

    /// <summary>
    /// The type of currency exchanged.
    /// </summary>
    [JsonPropertyName("type")]
    public ExtensionProductCostType? Type { get; init; }

    /// <summary>
    /// The type of currency exchanged.
    /// </summary>
    public enum ExtensionProductCostType
    {
        /// <summary>
        /// Purchase via bits.
        /// </summary>
        Bits
    }
}
