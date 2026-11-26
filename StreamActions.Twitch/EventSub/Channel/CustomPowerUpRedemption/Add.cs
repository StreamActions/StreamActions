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

using StreamActions.Common.Json.Serialization;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;
using StreamActions.Twitch.EventSub.Channel.CustomPowerUpRedemption.Objects;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.CustomPowerUpRedemption;

/// <summary>
/// A viewer has redeemed a custom power up on the specified channel.
/// </summary>
/// <remarks>
/// <para>
/// The notification is also sent when broadcasters redeem their own Custom Power-up. They don't spend Bits on their own channel, but custom_power_up.bits still shows the Power-up's price. In that case, <see cref="UserId"/> matches <see cref="BroadcasterUserId"/>.
/// </para>
/// </remarks>
public sealed record Add : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.custom_power_up_redemption.add";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The redemption identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

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
    /// User ID of the user that redeemed the power up.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Login of the user that redeemed the power up.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// Display name of the user that redeemed the power up.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The user input provided. Empty string if not provided.
    /// </summary>
    [JsonPropertyName("user_input")]
    public string? UserInput { get; init; }

    /// <summary>
    /// The redemption status. Custom Power-ups skip the redemption request queue, so this is <see cref="RedemptionStatuses.Fulfilled"/>.
    /// </summary>
    [JsonPropertyName("status")]
    public RedemptionStatuses? Status { get; init; }

    /// <summary>
    /// Basic information about the power up that was redeemed.
    /// </summary>
    [JsonPropertyName("custom_power_up")]
    public CustomPowerUp? CustomPowerUp { get; init; }

    /// <summary>
    /// RFC3339 timestamp of when the power up was redeemed.
    /// </summary>
    [JsonPropertyName("redeemed_at")]
    public DateTime? RedeemedAt { get; init; }

    /// <summary>
    /// The status of the redemption.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<RedemptionStatuses>))]
    public enum RedemptionStatuses
    {
        /// <summary>
        /// Unknown status.
        /// </summary>
        [JsonCustomEnum("unknown")]
        Unknown,

        /// <summary>
        /// Unfulfilled.
        /// </summary>
        [JsonCustomEnum("unfulfilled")]
        Unfulfilled,

        /// <summary>
        /// Fulfilled.
        /// </summary>
        [JsonCustomEnum("fulfilled")]
        Fulfilled,

        /// <summary>
        /// Canceled.
        /// </summary>
        [JsonCustomEnum("canceled")]
        Canceled
    }
}
