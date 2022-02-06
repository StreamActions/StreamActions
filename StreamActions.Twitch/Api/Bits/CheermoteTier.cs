/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Text.Json.Serialization;

namespace StreamActions.Twitch.Api.Bits
{
    /// <summary>
    /// Represents a cheermote tier in a <see cref="Cheermote"/>.
    /// </summary>
    public record CheermoteTier
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
}
