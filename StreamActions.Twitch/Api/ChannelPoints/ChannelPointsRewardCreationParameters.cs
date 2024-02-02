/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Twitch.Api.ChannelPoints;

/// <summary>
/// The parameters for <see cref="ChannelPointsReward.CreateCustomReward(Common.TwitchSession, string, ChannelPointsRewardCreationParameters)"/>.
/// </summary>
public record ChannelPointsRewardCreationParameters
{
    /// <summary>
    /// The background color to use for the reward, as a string. The color is in Hex format (for example, <c>#00E5CB</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("background_color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the reward is enabled. Disabled rewards aren't shown to the user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("is_enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// The cost of the reward in Channel Points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is required when used in <see cref="ChannelPointsReward.CreateCustomReward(Common.TwitchSession, string, ChannelPointsRewardCreationParameters)"/>.
    /// </para>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("cost")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Cost { get; init; }

    /// <summary>
    /// The title of the reward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is required when used in <see cref="ChannelPointsReward.CreateCustomReward(Common.TwitchSession, string, ChannelPointsRewardCreationParameters)"/>.
    /// </para>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// The prompt/description shown to the viewer when they redeem the reward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("prompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prompt { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the user must enter information when redeeming the reward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("is_user_input_required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsUserInputRequired { get; init; }

    /// <summary>
    /// A Boolean value that determines whether to limit the maximum number of redemptions allowed per live stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field or <see cref="MaxPerStream"/>, use <see langword="null"/>; if updating one of these fields, both must be defined.
    /// </para>
    /// </remarks>
    [JsonPropertyName("is_max_per_stream_enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsMaxPerStreamEnabled { get; init; }

    /// <summary>
    /// The maximum number of redemptions allowed per live stream. The minimum value is 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field or <see cref="IsMaxPerStreamEnabled"/>, use <see langword="null"/>; if updating one of these fields, both must be defined.
    /// </para>
    /// </remarks>
    [JsonPropertyName("max_per_stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxPerStream { get; init; }

    /// <summary>
    /// A Boolean value that determines whether to limit the maximum number of redemptions allowed per user per stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field or <see cref="MaxPerUserPerStream"/>, use <see langword="null"/>; if updating one of these fields, both must be defined.
    /// </para>
    /// </remarks>
    [JsonPropertyName("is_max_per_user_per_stream_enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsMaxPerUserPerStreamEnabled { get; init; }

    /// <summary>
    /// The maximum number of redemptions allowed per user per stream. The minimum value is 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field or <see cref="IsMaxPerUserPerStreamEnabled"/>, use <see langword="null"/>; if updating one of these fields, both must be defined.
    /// </para>
    /// </remarks>
    [JsonPropertyName("max_per_user_per_stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxPerUserPerStream { get; init; }

    /// <summary>
    /// A Boolean value that determines whether to apply a cooldown period between redemptions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field or <see cref="GlobalCooldownSeconds"/>, use <see langword="null"/>; if updating one of these fields, both must be defined.
    /// </para>
    /// </remarks>
    [JsonPropertyName("is_global_cooldown_enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsGlobalCooldownEnabled { get; init; }

    /// <summary>
    /// The cooldown period, in seconds. The minimum value is 1; however, the minimum value is 60 for it to be shown in the Twitch UX.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field or <see cref="IsGlobalCooldownEnabled"/>, use <see langword="null"/>; if updating one of these fields, both must be defined.
    /// </para>
    /// </remarks>
    [JsonPropertyName("global_cooldown_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? GlobalCooldownSeconds { get; init; }

    /// <summary>
    /// A Boolean value that determines whether redemptions should be set to <see cref="ChannelPointsRedemption.RedemptionStatus.Fulfilled"/> status immediately when a reward is redeemed.
    /// If <see langword="false"/>, status is set to <see cref="ChannelPointsRedemption.RedemptionStatus.Unfulfilled"/> and follows the normal request queue process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To not update this field, use <see langword="null"/>.
    /// </para>
    /// </remarks>
    [JsonPropertyName("should_redemptions_skip_request_queue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ShouldRedemptionsSkipRequestQueue { get; init; }
}
