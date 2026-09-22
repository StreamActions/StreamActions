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
/// Information about the gift sub event.
/// </summary>
public sealed record SubGift
{
    /// <summary>
    /// The number of months the subscription is for.
    /// </summary>
    [JsonPropertyName("duration_months")]
    public int? DurationMonths { get; init; }

    /// <summary>
    /// Optional. The amount of gifts the gifter has given in this channel. Null if anonymous.
    /// </summary>
    [JsonPropertyName("cumulative_total")]
    public int? CumulativeTotal { get; init; }

    /// <summary>
    /// The user ID of the subscription gift recipient.
    /// </summary>
    [JsonPropertyName("recipient_user_id")]
    public string? RecipientUserId { get; init; }

    /// <summary>
    /// The user name of the subscription gift recipient.
    /// </summary>
    [JsonPropertyName("recipient_user_name")]
    public string? RecipientUserName { get; init; }

    /// <summary>
    /// The user login of the subscription gift recipient.
    /// </summary>
    [JsonPropertyName("recipient_user_login")]
    public string? RecipientUserLogin { get; init; }

    /// <summary>
    /// The type of subscription plan being used.
    /// </summary>
    [JsonPropertyName("sub_tier")]
    public string? SubTier { get; init; }

    /// <summary>
    /// Optional. The ID of the associated community gift. Null if not associated with a community gift.
    /// </summary>
    [JsonPropertyName("community_gift_id")]
    public string? CommunityGiftId { get; init; }
}
