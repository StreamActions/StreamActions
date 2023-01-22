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

namespace StreamActions.Twitch.Api.Videos;

/// <summary>
/// Represents a muted video segment in a <see cref="Video"/>.
/// </summary>
public sealed record MutedVideoSegment
{
    /// <summary>
    /// The duration of the muted segment, in seconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The duration of the muted segment.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// The offset, in seconds, from the beginning of the video to where the muted segment begins.
    /// </summary>
    [JsonPropertyName("offset")]
    public int? OffsetSeconds { get; init; }

    /// <summary>
    /// The offset from the beginning of the video to where the muted segment begins.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Offset => this.OffsetSeconds.HasValue ? TimeSpan.FromSeconds(this.OffsetSeconds.Value) : null;
}
