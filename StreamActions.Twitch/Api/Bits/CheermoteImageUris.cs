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
/// Represents the uris to the images in a <see cref="CheermoteImageSet"/> in various sizes.
/// </summary>
public sealed record CheermoteImageUris
{
    /// <summary>
    /// The uri to the 1x size image.
    /// </summary>
    [JsonPropertyName("1")]
    public Uri? Uri1X { get; init; }

    /// <summary>
    /// The uri to the 1.5x size image.
    /// </summary>
    [JsonPropertyName("1.5")]
    public Uri? Uri1P5X { get; init; }

    /// <summary>
    /// The uri to the 2x size image.
    /// </summary>
    [JsonPropertyName("2")]
    public Uri? Uri2X { get; init; }

    /// <summary>
    /// The uri to the 3x size image.
    /// </summary>
    [JsonPropertyName("3")]
    public Uri? Uri3X { get; init; }

    /// <summary>
    /// The uri to the 4x size image.
    /// </summary>
    [JsonPropertyName("4")]
    public Uri? Uri4X { get; init; }
}
