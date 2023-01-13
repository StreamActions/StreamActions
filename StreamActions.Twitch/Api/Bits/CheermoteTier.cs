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
/// Represents a cheermote tier in a <see cref="Cheermote"/>.
/// </summary>
public sealed record CheermoteTier
{
    /// <summary>
    /// Minimum number of bits needed to be used to hit the given tier of emote.
    /// </summary>
    [JsonPropertyName("min_bits")]
    public int? MinBits { get; init; }

    /// <summary>
    /// ID of the emote tier. Possible tiers are: 1, 100, 500, 1000 ,5000, 10k, 100k.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Hex code for the color associated with the bits of that tier. Grey, Purple, Teal, Blue, or Red color to match the base bit type.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>
    /// <see cref="CheermoteImageThemes"/> containing both animated and static image sets, sorted by light and dark.
    /// </summary>
    [JsonPropertyName("images")]
    public CheermoteImageThemes? Images { get; init; }

    /// <summary>
    /// Indicates whether or not emote information is accessible to users.
    /// </summary>
    [JsonPropertyName("can_cheer")]
    public bool? CanCheer { get; init; }

    /// <summary>
    /// Indicates whether or not we hide the emote from the bits card.
    /// </summary>
    [JsonPropertyName("show_in_bits_card")]
    public bool? ShowInBitsCard { get; init; }
}
