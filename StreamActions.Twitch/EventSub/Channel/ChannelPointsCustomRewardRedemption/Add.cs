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
using StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomRewardRedemption.Objects;

namespace StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomRewardRedemption;

/// <summary>
/// A viewer has redeemed a custom channel points reward on the specified channel.
/// </summary>
public sealed record Add : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.channel_points_custom_reward_redemption.add";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The redemption identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

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
    /// User ID of the user that redeemed the reward.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Login of the user that redeemed the reward.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// Display name of the user that redeemed the reward.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The user input provided. Null if not provided.
    /// </summary>
    [JsonPropertyName("user_input")]
    public string? UserInput { get; init; }

    /// <summary>
    /// Defaults to unfulfilled. Possible values are unknown, unfulfilled, fulfilled, and canceled.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    /// Basic information about the reward that was redeemed, at the time it was redeemed.
    /// </summary>
    [JsonPropertyName("reward")]
    public Reward? Reward { get; init; }

    /// <summary>
    /// RFC3339 timestamp of when the reward was redeemed.
    /// </summary>
    [JsonPropertyName("redeemed_at")]
    public DateTime? RedeemedAt { get; init; }
}
