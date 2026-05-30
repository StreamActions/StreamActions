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
/// Represents a guest currently interacting with the Guest Star session.
/// </summary>
public sealed record GuestStarGuest
{
    /// <summary>
    /// ID representing this guest's slot assignment.
    /// </summary>
    [JsonPropertyName("slot_id")]
    public string? SlotId { get; init; }

    /// <summary>
    /// Flag determining whether or not the guest is visible in the browser source in the host's streaming software.
    /// </summary>
    [JsonPropertyName("is_live")]
    public bool? IsLive { get; init; }

    /// <summary>
    /// User ID of the guest assigned to this slot.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Display name of the guest assigned to this slot.
    /// </summary>
    [JsonPropertyName("user_display_name")]
    public string? UserDisplayName { get; init; }

    /// <summary>
    /// Login of the guest assigned to this slot.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// Value from 0 to 100 representing the host's volume setting for this guest.
    /// </summary>
    [JsonPropertyName("volume")]
    public int? Volume { get; init; }

    /// <summary>
    /// Timestamp when this guest was assigned a slot in the session.
    /// </summary>
    [JsonPropertyName("assigned_at")]
    public DateTime? AssignedAt { get; init; }

    /// <summary>
    /// Information about the guest's audio settings.
    /// </summary>
    [JsonPropertyName("audio_settings")]
    public GuestStarMediaSettings? AudioSettings { get; init; }

    /// <summary>
    /// Information about the guest's video settings.
    /// </summary>
    [JsonPropertyName("video_settings")]
    public GuestStarMediaSettings? VideoSettings { get; init; }
}
