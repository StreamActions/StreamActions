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

namespace StreamActions.Twitch.EventSub.Channel.Subscription;

/// <summary>
/// An event that is sent when a user gives one or more gifted subscriptions in a channel.
/// </summary>
public sealed record Gift : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.subscription.gift";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The user ID of the user who sent the subscription gift. Set to null if it was an anonymous subscription gift.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The user login of the user who sent the gift. Set to null if it was an anonymous subscription gift.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user display name of the user who sent the gift. Set to null if it was an anonymous subscription gift.
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
    /// The number of subscriptions in the subscription gift.
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    /// <summary>
    /// The tier of subscriptions in the subscription gift.
    /// </summary>
    [JsonPropertyName("tier")]
    public string? Tier { get; init; }

    /// <summary>
    /// The number of subscriptions gifted by this user in the channel. This value is null for anonymous gifts or if the gifter has opted out of sharing this information.
    /// </summary>
    [JsonPropertyName("cumulative_total")]
    public int? CumulativeTotal { get; init; }

    /// <summary>
    /// Whether the subscription gift was anonymous.
    /// </summary>
    [JsonPropertyName("is_anonymous")]
    public bool? IsAnonymous { get; init; }
}
