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

namespace StreamActions.Twitch.Api.Common;

/// <summary>
/// An image url object.
/// </summary>
public sealed record Images
{
    /// <summary>
    /// A URL to the small version (28px x 28px) of the image.
    /// </summary>
    [JsonPropertyName("url_1x")]
    public Uri? Url1X { get; init; }

    /// <summary>
    /// A URL to the medium version (56px x 56px) of the image.
    /// </summary>
    [JsonPropertyName("url_2x")]
    public Uri? Url2X { get; init; }

    /// <summary>
    /// A URL to the large version (112px x 112px) of the image.
    /// </summary>
    [JsonPropertyName("url_4x")]
    public Uri? Url4X { get; init; }
}
