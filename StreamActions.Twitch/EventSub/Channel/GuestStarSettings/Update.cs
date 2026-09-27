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

using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.GuestStarSettings;

/// <summary>
/// A Guest Star settings are updated.
/// </summary>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.guest_star_settings.update";

    /// <inheritdoc/>
    public static string Version => "beta";

    /// <summary>
    /// The ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The user name of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The login of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// Flag determining if Guest Star moderators have access to control whether a guest is live once assigned to a slot.
    /// </summary>
    [JsonPropertyName("is_moderator_send_live_enabled")]
    public bool? IsModeratorSendLiveEnabled { get; init; }

    /// <summary>
    /// Number of slots the Guest Star call interface will allow the host to add to a call.
    /// </summary>
    [JsonPropertyName("slot_count")]
    public int? SlotCount { get; init; }

    /// <summary>
    /// Flag determining if Browser Sources subscribed to sessions on this channel should output audio.
    /// </summary>
    [JsonPropertyName("is_browser_source_audio_enabled")]
    public bool? IsBrowserSourceAudioEnabled { get; init; }

    /// <summary>
    /// This setting determines how the guests are laid out within a browser source.
    /// </summary>
    [JsonPropertyName("group_layout")]
    public string? GroupLayout { get; init; }
}
