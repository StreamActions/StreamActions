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

namespace StreamActions.Twitch.EventSub.Channel.Chat;

/// <summary>
/// Information about the resub event.
/// </summary>
public sealed record Resub
{
    /// <summary>
    /// The total number of months the user has subscribed.
    /// </summary>
    [JsonPropertyName("cumulative_months")]
    public int? CumulativeMonths { get; init; }

    /// <summary>
    /// The number of months the subscription is for.
    /// </summary>
    [JsonPropertyName("duration_months")]
    public int? DurationMonths { get; init; }

    /// <summary>
    /// The total number of months the user has subscribed.
    /// </summary>
    [JsonPropertyName("streak_months")]
    public int? StreakMonths { get; init; }

    /// <summary>
    /// The type of subscription plan being used.
    /// </summary>
    [JsonPropertyName("sub_tier")]
    public string? SubTier { get; init; }

    /// <summary>
    /// Optional. Whether or not this subscription is a Prime subscription.
    /// </summary>
    [JsonPropertyName("is_prime")]
    public bool? IsPrime { get; init; }

    /// <summary>
    /// Whether or not the resub was a result of a gift.
    /// </summary>
    [JsonPropertyName("is_gift")]
    public bool? IsGift { get; init; }

    /// <summary>
    /// Optional. Whether or not the gift was anonymous.
    /// </summary>
    [JsonPropertyName("gifter_is_anonymous")]
    public bool? GifterIsAnonymous { get; init; }

    /// <summary>
    /// The user ID of the subscription gifter. Null if anonymous.
    /// </summary>
    [JsonPropertyName("gifter_user_id")]
    public string? GifterUserId { get; init; }

    /// <summary>
    /// The user name of the subscription gifter. Null if anonymous.
    /// </summary>
    [JsonPropertyName("gifter_user_name")]
    public string? GifterUserName { get; init; }

    /// <summary>
    /// Optional. The user login of the subscription gifter. Null if anonymous.
    /// </summary>
    [JsonPropertyName("gifter_user_login")]
    public string? GifterUserLogin { get; init; }
}
