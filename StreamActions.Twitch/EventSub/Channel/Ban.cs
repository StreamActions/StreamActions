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

namespace StreamActions.Twitch.EventSub.Channel;

/// <summary>
/// An event that is sent when a viewer is timed out or banned from the specified channel.
/// </summary>
public sealed record Ban : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.ban";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The user ID for the user who was banned on the specified channel.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user login for the user who was banned on the specified channel.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user display name for the user who was banned on the specified channel.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The requested broadcaster ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The requested broadcaster login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The requested broadcaster display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The user ID of the issuer of the ban.
    /// </summary>
    [JsonPropertyName("moderator_user_id")]
    public string? ModeratorUserId { get; init; }

    /// <summary>
    /// The user login of the issuer of the ban.
    /// </summary>
    [JsonPropertyName("moderator_user_login")]
    public string? ModeratorUserLogin { get; init; }

    /// <summary>
    /// The user name of the issuer of the ban.
    /// </summary>
    [JsonPropertyName("moderator_user_name")]
    public string? ModeratorUserName { get; init; }

    /// <summary>
    /// The reason behind the ban.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the user was banned or put in a timeout.
    /// </summary>
    [JsonPropertyName("banned_at")]
    public DateTime? BannedAt { get; init; }

    /// <summary>
    /// The UTC timestamp (in RFC3339 format) of when the timeout ends. Is null if the user was banned instead of put in a timeout.
    /// </summary>
    [JsonPropertyName("ends_at")]
    public DateTime? EndsAt { get; init; }

    /// <summary>
    /// Indicates whether the ban is permanent (true) or a timeout (false). If true, ends_at will be null.
    /// </summary>
    [JsonPropertyName("is_permanent")]
    public bool? IsPermanent { get; init; }
}
