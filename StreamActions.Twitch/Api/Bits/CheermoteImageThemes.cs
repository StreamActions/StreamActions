﻿/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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
/// Represents the image themes in a <see cref="CheermoteTier"/>.
/// </summary>
public sealed record CheermoteImageThemes
{
    /// <summary>
    /// The <see cref="CheermoteImageSet"/> for the dark theme.
    /// </summary>
    [JsonPropertyName("dark")]
    public CheermoteImageSet? Dark { get; init; }

    /// <summary>
    /// The <see cref="CheermoteImageSet"/> for the light theme.
    /// </summary>
    [JsonPropertyName("light")]
    public CheermoteImageSet? Light { get; init; }
}
