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

namespace StreamActions.Twitch.Api.Schedule;

/// <summary>
/// Represents a scheduled broadcast segment.
/// </summary>
public sealed record ScheduleSegment
{
    /// <summary>
    /// An ID that identifies this broadcast segment.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The UTC date and time of when the broadcast starts.
    /// </summary>
    [JsonPropertyName("start_time")]
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// The UTC date and time of when the broadcast ends.
    /// </summary>
    [JsonPropertyName("end_time")]
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// The broadcast segment's title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Indicates whether the broadcaster canceled this segment of a recurring broadcast.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the broadcaster canceled this segment, this property is set to the same value that's in the <see cref="EndTime"/> property; otherwise, it's <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("canceled_until")]
    public DateTime? CanceledUntil { get; init; }

    /// <summary>
    /// The type of content that the broadcaster plans to stream or <see langword="null"/> if not specified.
    /// </summary>
    [JsonPropertyName("category")]
    public ScheduleCategory? Category { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the broadcast is part of a recurring series that streams at the same time each week or is a one-time broadcast.
    /// </summary>
    [JsonPropertyName("is_recurring")]
    public bool? IsRecurring { get; init; }
}
