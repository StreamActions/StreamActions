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

using StreamActions.Common;
using System.Drawing;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits;

/// <summary>
/// Represents a cheermote tier in a <see cref="Cheermote"/>.
/// </summary>
public sealed record CheermoteTier
{
    /// <summary>
    /// The minimum number of Bits that you must cheer at this tier level. The maximum number of Bits that you can cheer at this level is determined by the required minimum Bits of the next tier level minus 1.
    /// </summary>
    [JsonPropertyName("min_bits")]
    public int? MinBits { get; init; }

    /// <summary>
    /// The tier level. Possible tiers are: 1, 100, 500, 1000 ,5000, 10k, 100k.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The hex code of the color associated with this tier level (for example, <c>#979797</c>), as a string.
    /// </summary>
    [JsonPropertyName("color")]
    public string? ColorString { get; init; }

    /// <summary>
    /// The color associated with this tier level.
    /// </summary>
    [JsonPropertyName("color")]
    public Color? Color => Util.IsValidHexColor(this.ColorString ?? "") ? Util.HexColorToColor(this.ColorString ?? "") : null;

    /// <summary>
    /// The animated and static image sets for the Cheermote.
    /// </summary>
    [JsonPropertyName("images")]
    public CheermoteImageThemes? Images { get; init; }

    /// <summary>
    /// A Boolean value that determines whether users can cheer at this tier level.
    /// </summary>
    [JsonPropertyName("can_cheer")]
    public bool? CanCheer { get; init; }

    /// <summary>
    /// A Boolean value that determines whether this tier level is shown in the Bits card.
    /// </summary>
    [JsonPropertyName("show_in_bits_card")]
    public bool? ShowInBitsCard { get; init; }
}
