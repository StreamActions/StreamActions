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

namespace StreamActions.Twitch.Api.GuestStar;

/// <summary>
/// Information about a guest's media settings.
/// </summary>
public sealed record GuestStarMediaSettings
{
    /// <summary>
    /// Flag determining whether the host is allowing the guest's media to be seen or heard within the session.
    /// </summary>
    [JsonPropertyName("is_host_enabled")]
    public bool? IsHostEnabled { get; init; }

    /// <summary>
    /// Flag determining whether the guest is allowing their media to be transmitted to the session.
    /// </summary>
    [JsonPropertyName("is_guest_enabled")]
    public bool? IsGuestEnabled { get; init; }

    /// <summary>
    /// Flag determining whether the guest has an appropriate media device available to be transmitted to the session.
    /// </summary>
    [JsonPropertyName("is_available")]
    public bool? IsAvailable { get; init; }
}
