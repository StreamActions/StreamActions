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

namespace StreamActions.Twitch.EventSub.Channel.GuestStarGuest;

/// <summary>
/// A Guest Star guest is updated.
/// </summary>
public sealed record Update : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.guest_star_guest.update";

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
    /// ID associated with the session.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    /// The ID of the moderator.
    /// </summary>
    [JsonPropertyName("moderator_user_id")]
    public string? ModeratorUserId { get; init; }

    /// <summary>
    /// The user name of the moderator.
    /// </summary>
    [JsonPropertyName("moderator_user_name")]
    public string? ModeratorUserName { get; init; }

    /// <summary>
    /// The login of the moderator.
    /// </summary>
    [JsonPropertyName("moderator_user_login")]
    public string? ModeratorUserLogin { get; init; }

    /// <summary>
    /// The ID of the guest.
    /// </summary>
    [JsonPropertyName("guest_user_id")]
    public string? GuestUserId { get; init; }

    /// <summary>
    /// The user name of the guest.
    /// </summary>
    [JsonPropertyName("guest_user_name")]
    public string? GuestUserName { get; init; }

    /// <summary>
    /// The login of the guest.
    /// </summary>
    [JsonPropertyName("guest_user_login")]
    public string? GuestUserLogin { get; init; }

    /// <summary>
    /// ID associated with the slot the guest is assigned to.
    /// </summary>
    [JsonPropertyName("slot_id")]
    public string? SlotId { get; init; }

    /// <summary>
    /// Current state of the guest.
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>
    /// User ID of the host.
    /// </summary>
    [JsonPropertyName("host_user_id")]
    public string? HostUserId { get; init; }

    /// <summary>
    /// User name of the host.
    /// </summary>
    [JsonPropertyName("host_user_name")]
    public string? HostUserName { get; init; }

    /// <summary>
    /// Login of the host.
    /// </summary>
    [JsonPropertyName("host_user_login")]
    public string? HostUserLogin { get; init; }

    /// <summary>
    /// Flag determining whether the host is allowing the guest's video to be seen.
    /// </summary>
    [JsonPropertyName("host_video_enabled")]
    public bool? HostVideoEnabled { get; init; }

    /// <summary>
    /// Flag determining whether the host is allowing the guest's audio to be heard.
    /// </summary>
    [JsonPropertyName("host_audio_enabled")]
    public bool? HostAudioEnabled { get; init; }

    /// <summary>
    /// Value between 0 and 100 representing the host's volume setting for the guest.
    /// </summary>
    [JsonPropertyName("host_volume")]
    public int? HostVolume { get; init; }
}
