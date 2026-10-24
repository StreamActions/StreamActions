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
/// Parameters for updating a channel stream schedule segment.
/// </summary>
public sealed record UpdateChannelStreamScheduleSegmentParameters
{
    /// <summary>
    /// The date and time that the broadcast segment starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only partners and affiliates may update a broadcast's start time and only for non-recurring segments.
    /// </para>
    /// </remarks>
    [JsonPropertyName("start_time")]
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// The length of time, in minutes, that the broadcast is scheduled to run. The duration must be in the range 30 through 1380 (23 hours).
    /// </summary>
    [JsonPropertyName("duration")]
    public string? Duration { get; init; }

    /// <summary>
    /// The ID of the category that best represents the broadcast's content.
    /// </summary>
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; init; }

    /// <summary>
    /// The broadcast's title. The title may contain a maximum of 140 characters.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// A Boolean value that indicates whether the broadcast is canceled. Set to <see langword="true"/> to cancel the segment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For recurring segments, the API cancels the first segment after the current UTC date and time and not the specified segment (unless the specified segment is the next segment after the current UTC date and time).
    /// </para>
    /// </remarks>
    [JsonPropertyName("is_canceled")]
    public bool? IsCanceled { get; init; }

    /// <summary>
    /// The time zone where the broadcast takes place. Specify the time zone using IANA time zone database format.
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }
}
