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
using StreamActions.Twitch.EventSub.Common.ChatMessage;
using System.Text.Json.Serialization;
using StreamActions.Twitch.EventSub.Channel.SuspiciousUser.Objects;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Twitch.EventSub.Channel.SuspiciousUser;

/// <summary>
/// An event that is sent when a chat message has been sent by a suspicious user.
/// </summary>
public sealed record MessageEvent : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.suspicious_user.message";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The ID of the channel where the treatment for a suspicious user was updated.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The display name of the channel where the treatment for a suspicious user was updated.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The login of the channel where the treatment for a suspicious user was updated.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The user ID of the user that sent the message.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user name of the user that sent the message.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The user login of the user that sent the message.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The status set for the suspicious user. Can be the following: none, active_monitoring, or restricted
    /// </summary>
    [JsonPropertyName("low_trust_status")]
    public LowTrustStatus? LowTrustStatus { get; init; }

    /// <summary>
    /// A list of channel IDs where the suspicious user is also banned.
    /// </summary>
    [JsonPropertyName("shared_ban_channel_ids")]
    public IReadOnlyCollection<string>? SharedBanChannelIds { get; init; }

    /// <summary>
    /// User types (if any) that apply to the suspicious user, can be manually_added, ban_evader, or banned_in_shared_channel.
    /// </summary>
    [JsonPropertyName("types")]
    public IReadOnlyCollection<string>? Types { get; init; }

    /// <summary>
    /// A ban evasion likelihood value (if any) that as been applied to the user automatically by Twitch, can be unknown, possible, or likely.
    /// </summary>
    [JsonPropertyName("ban_evasion_evaluation")]
    public string? BanEvasionEvaluation { get; init; }

    /// <summary>
    /// The structured chat message.
    /// </summary>
    [JsonPropertyName("message")]
    public Message? Message { get; init; }

    /// <summary>
    /// The UUID that identifies the message.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string? MessageId { get; init; }
}
