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
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.EventSub.Channel.Bits;

/// <summary>
/// A notification sent whenever Bits are used on a channel.
/// </summary>
public sealed record Use : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.bits.use";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The User ID of the channel where the Bits were redeemed.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The login of the channel where the Bits were used.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The display name of the channel where the Bits were used.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The User ID of the redeeming user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The login name of the redeeming user.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The display name of the redeeming user.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    /// <summary>
    /// The number of Bits used.
    /// </summary>
    [JsonPropertyName("bits")]
    public int? Bits { get; init; }

    /// <summary>
    /// Possible values are: cheer, power_up, custom_power_up.
    /// </summary>
    [JsonPropertyName("type")]
    public BitsUseType? UseType { get; init; }

    /// <summary>
    /// Optional. An object that contains the user message and emote information needed to recreate the message.
    /// </summary>
    [JsonPropertyName("message")]
    public Common.ChatMessage.Message? Message { get; init; }

    /// <summary>
    /// Optional. Data about a default (i.e. built-in) Power-up.
    /// </summary>
    [JsonPropertyName("power_up")]
    public PowerUp? PowerUp { get; init; }

    /// <summary>
    /// Optional. Data about a custom Power-up.
    /// </summary>
    [JsonPropertyName("custom_power_up")]
    public CustomPowerUp? CustomPowerUp { get; init; }

    /// <summary>
    /// The type of bits usage.
    /// </summary>
    [JsonConverter(typeof(JsonCustomEnumConverter<BitsUseType>))]
    public enum BitsUseType
    {
        /// <summary>
        /// Cheer.
        /// </summary>
        [JsonCustomEnum("cheer")]
        Cheer,

        /// <summary>
        /// Power up.
        /// </summary>
        [JsonCustomEnum("power_up")]
        PowerUp,

        /// <summary>
        /// Custom power up.
        /// </summary>
        [JsonCustomEnum("custom_power_up")]
        CustomPowerUp
    }
}
