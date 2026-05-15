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

namespace StreamActions.Twitch.Api.Streams;

/// <summary>
/// Represents the parameters for creating a stream marker.
/// </summary>
public sealed record CreateStreamMarkerParameters
{
    /// <summary>
    /// The ID of the broadcaster that's streaming content. This ID must match the user ID in the access token or the user in the access token must be one of the broadcaster's editors.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// A short description of the marker to help the user remember why they marked the location. The maximum length of the description is 140 characters.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
