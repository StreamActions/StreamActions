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

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// A moderator or bot has cleared all messages from a specific user.
/// </summary>
public sealed record ClearUserMessages : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.chat.clear_user_messages";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The broadcaster user ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The broadcaster login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The broadcaster display name.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The ID of the user that was banned or put in a timeout. All of their messages are deleted.
    /// </summary>
    [JsonPropertyName("target_user_id")]
    public string? TargetUserId { get; init; }

    /// <summary>
    /// The user login of the user that was banned or put in a timeout.
    /// </summary>
    [JsonPropertyName("target_user_login")]
    public string? TargetUserLogin { get; init; }

    /// <summary>
    /// The user name of the user that was banned or put in a timeout.
    /// </summary>
    [JsonPropertyName("target_user_name")]
    public string? TargetUserName { get; init; }
}
