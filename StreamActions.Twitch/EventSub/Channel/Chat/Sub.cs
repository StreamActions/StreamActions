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

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// Information about the sub event.
/// </summary>
public sealed record Sub
{
    /// <summary>
    /// The type of subscription plan being used.
    /// </summary>
    [JsonPropertyName("sub_tier")]
    public string? SubTier { get; init; }

    /// <summary>
    /// Indicates if the subscription was obtained through Amazon Prime.
    /// </summary>
    [JsonPropertyName("is_prime")]
    public bool? IsPrime { get; init; }

    /// <summary>
    /// The number of months the subscription is for.
    /// </summary>
    [JsonPropertyName("duration_months")]
    public int? DurationMonths { get; init; }
}
