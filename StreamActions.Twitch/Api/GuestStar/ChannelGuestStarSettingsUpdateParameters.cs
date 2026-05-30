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
/// Parameters for updating the channel settings for configuration of the Guest Star feature.
/// </summary>
public sealed record ChannelGuestStarSettingsUpdateParameters
{
    /// <summary>
    /// Flag determining if Guest Star moderators have access to control whether a guest is live once assigned to a slot.
    /// </summary>
    [JsonPropertyName("is_moderator_send_live_enabled")]
    public bool? IsModeratorSendLiveEnabled { get; init; }

    /// <summary>
    /// Number of slots the Guest Star call interface will allow the host to add to a call. Required to be between 1 and 6.
    /// </summary>
    [JsonPropertyName("slot_count")]
    public int? SlotCount { get; init; }

    /// <summary>
    /// Flag determining if Browser Sources subscribed to sessions on this channel should output audio.
    /// </summary>
    [JsonPropertyName("is_browser_source_audio_enabled")]
    public bool? IsBrowserSourceAudioEnabled { get; init; }

    /// <summary>
    /// This setting determines how the guests within a session should be laid out within the browser source.
    /// </summary>
    [JsonPropertyName("group_layout")]
    public GuestStarGroupLayout? GroupLayout { get; init; }

    /// <summary>
    /// Flag determining if Guest Star should regenerate the auth token associated with the channel’s browser sources.
    /// </summary>
    [JsonPropertyName("regenerate_browser_sources")]
    public bool? RegenerateBrowserSources { get; init; }
}
