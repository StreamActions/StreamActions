/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
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
/// The parameters for <see cref="ChannelPointsReward.CreateCustomReward(Common.TwitchSession, string, ChannelPointsRewardCreationParameters)"/>
/// </summary>
public record ChannelPointsRewardCreationParameters
{
    /// <summary>
    /// The background color to use for the reward, as a string. The color is in Hex format (for example, <c>#00E5CB</c>).
    /// </summary>
    [JsonPropertyName("background_color")]
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the reward is enabled. Disabled rewards aren't shown to the user.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// The cost of the reward in Channel Points.
    /// </summary>
    [JsonPropertyName("cost")]
    public int? Cost { get; init; }

    /// <summary>
    /// The title of the reward.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The prompt/description shown to the viewer when they redeem the reward.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the user must enter information when redeeming the reward.
    /// </summary>
    [JsonPropertyName("is_user_input_required")]
    public bool? IsUserInputRequired { get; init; }

    /// <summary>
    /// A Boolean value that determines whether to limit the maximum number of redemptions allowed per live stream.
    /// </summary>
    [JsonPropertyName("is_max_per_stream_enabled")]
    public bool? IsMaxPerStreamEnabled { get; init; }

    /// <summary>
    /// The maximum number of redemptions allowed per live stream. The minimum value is 1.
    /// </summary>
    [JsonPropertyName("max_per_stream")]
    public int? MaxPerStream { get; init; }

    /// <summary>
    /// A Boolean value that determines whether to limit the maximum number of redemptions allowed per user per stream.
    /// </summary>
    [JsonPropertyName("is_max_per_user_per_stream_enabled")]
    public bool? IsMaxPerUserPerStreamEnabled { get; init; }

    /// <summary>
    /// The maximum number of redemptions allowed per user per stream. The minimum value is 1.
    /// </summary>
    [JsonPropertyName("max_per_user_per_stream")]
    public int? MaxPerUserPerStream { get; init; }

    /// <summary>
    /// A Boolean value that determines whether to apply a cooldown period between redemptions.
    /// </summary>
    [JsonPropertyName("is_global_cooldown_enabled")]
    public bool? IsGlobalCooldownEnabled { get; init; }

    /// <summary>
    /// The cooldown period, in seconds. The minimum value is 1; however, the minimum value is 60 for it to be shown in the Twitch UX.
    /// </summary>
    [JsonPropertyName("global_cooldown_seconds")]
    public int? GlobalCooldownSeconds { get; init; }

    /// <summary>
    /// A Boolean value that determines whether redemptions should be set to <c>FULFILLED</c> status immediately when a reward is redeemed.
    /// If <see langword="false"/>, status is set to <c>UNFULFILLED</c> and follows the normal request queue process.
    /// </summary>
    [JsonPropertyName("should_redemptions_skip_request_queue")]
    public bool? ShouldRedemptionsSkipRequestQueue { get; init; }
}
