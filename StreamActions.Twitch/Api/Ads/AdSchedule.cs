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

using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using StreamActions.Common.Extensions;

namespace StreamActions.Twitch.Api.Ads;

/// <summary>
/// Sends and represents a response element for a request to get the ad schedule or snooze the next ad.
/// </summary>
public sealed record AdSchedule
{
    /// <summary>
    /// The number of snoozes available for the broadcaster.
    /// </summary>
    [JsonPropertyName("snooze_count")]
    public int? SnoozeCount { get; init; }

    /// <summary>
    /// The UTC timestamp when the broadcaster will gain an additional snooze.
    /// </summary>
    [JsonPropertyName("snooze_refresh_at")]
    public DateTime? SnoozeRefreshAt { get; init; }

    /// <summary>
    /// The UTC timestamp of the broadcaster's next scheduled ad. <see langword="null"/> if the channel has no ad scheduled or is not live.
    /// </summary>
    [JsonPropertyName("next_ad_at")]
    public DateTime? NextAdAt { get; init; }

    /// <summary>
    /// The length in seconds of the scheduled upcoming ad break.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// The length of the scheduled upcoming ad break.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => this.DurationSeconds.HasValue ? TimeSpan.FromSeconds(this.DurationSeconds.Value) : null;

    /// <summary>
    /// The UTC timestamp of the broadcaster's last ad-break. <see langword="null"/> if the channel has not run an ad or is not live.
    /// </summary>
    [JsonPropertyName("last_ad_at")]
    public DateTime? LastAdAt { get; init; }

    /// <summary>
    /// The amount of pre-roll free time remaining for the channel in seconds. Returns 0 if they are currently not pre-roll free.
    /// </summary>
    [JsonPropertyName("preroll_free_time")]
    public int? PrerollFreeTimeSeconds { get; init; }

    /// <summary>
    /// The amount of pre-roll free time remaining for the channel. Returns <see cref="TimeSpan.Zero"/> if they are currently not pre-roll free.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? PrerollFreeTime => this.PrerollFreeTimeSeconds.HasValue ? TimeSpan.FromSeconds(this.PrerollFreeTimeSeconds.Value) : null;
}
