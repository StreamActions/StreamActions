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
/// Represents a list of markers for a specific video.
/// </summary>
public sealed record VideoMarkers
{
    /// <summary>
    /// An ID that identifies the video.
    /// </summary>
    [JsonPropertyName("video_id")]
    public string? VideoId { get; init; }

    /// <summary>
    /// A list of markers in this video. The list is in ascending order by when the marker was created.
    /// </summary>
    [JsonPropertyName("markers")]
    public IEnumerable<StreamMarker>? Markers { get; init; }
}
