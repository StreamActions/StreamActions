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

namespace StreamActions.Twitch.EventSub.Channel.Subscription;

/// <summary>
/// An event that is sent when a user sends a resubscription chat message in a specific channel.
/// </summary>
public sealed record Message : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.subscription.message";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The user ID of the user who sent a resubscription chat message.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user login of the user who sent a resubscription chat message.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user display name of the user who a resubscription chat message.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

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
    /// The tier of the users subscription.
    /// </summary>
    [JsonPropertyName("tier")]
    public string? Tier { get; init; }

    /// <summary>
    /// An object that contains the resubscription message and emote information needed to recreate the message.
    /// </summary>
    [JsonPropertyName("message")]
    public Common.ChatMessage.Message? MessageObject { get; init; }

    /// <summary>
    /// The total number of months the user has been subscribed to the channel.
    /// </summary>
    [JsonPropertyName("cumulative_months")]
    public int? CumulativeMonths { get; init; }

    /// <summary>
    /// The number of consecutive months the users current subscription has been active. This value is null if the user has opted out of sharing this information.
    /// </summary>
    [JsonPropertyName("streak_months")]
    public int? StreakMonths { get; init; }

    /// <summary>
    /// The month duration of the subscription.
    /// </summary>
    [JsonPropertyName("duration_months")]
    public int? DurationMonths { get; init; }
}
