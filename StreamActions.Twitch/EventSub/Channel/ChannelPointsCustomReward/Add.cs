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

namespace StreamActions.Twitch.EventSub.Channel.ChannelPointsCustomReward;

/// <summary>
/// A custom channel points reward has been created for the specified channel.
/// </summary>
public sealed record Add : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.channel_points_custom_reward.add";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The reward identifier.
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
    /// Is the reward currently enabled. If false, the reward won't show up to viewers.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Is the reward currently paused. If true, viewers can't redeem.
    /// </summary>
    [JsonPropertyName("is_paused")]
    public bool? IsPaused { get; init; }

    /// <summary>
    /// Is the reward currently in stock. If false, viewers can't redeem.
    /// </summary>
    [JsonPropertyName("is_in_stock")]
    public bool? IsInStock { get; init; }

    /// <summary>
    /// The reward title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The reward cost.
    /// </summary>
    [JsonPropertyName("cost")]
    public int? Cost { get; init; }

    /// <summary>
    /// The reward description.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>
    /// Does the viewer need to enter information when redeeming the reward.
    /// </summary>
    [JsonPropertyName("is_user_input_required")]
    public bool? IsUserInputRequired { get; init; }

    /// <summary>
    /// Should redemptions be set to FULFILLED status immediately when redeemed and skip the request queue instead of the normal UNFULFILLED status.
    /// </summary>
    [JsonPropertyName("should_redemptions_skip_request_queue")]
    public bool? ShouldRedemptionsSkipRequestQueue { get; init; }

    /// <summary>
    /// Whether a maximum per stream is enabled and what the maximum is.
    /// </summary>
    [JsonPropertyName("max_per_stream")]
    public MaxPerStream? MaxPerStream { get; init; }

    /// <summary>
    /// Whether a maximum per user per stream is enabled and what the maximum is.
    /// </summary>
    [JsonPropertyName("max_per_user_per_stream")]
    public MaxPerUserPerStream? MaxPerUserPerStream { get; init; }

    /// <summary>
    /// Custom background color for the reward.
    /// </summary>
    [JsonPropertyName("background_color")]
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// Set of custom images of 1x, 2x and 4x sizes for the reward. Can be null if no images have been uploaded.
    /// </summary>
    [JsonPropertyName("image")]
    public Image? Image { get; init; }

    /// <summary>
    /// Set of default images of 1x, 2x and 4x sizes for the reward.
    /// </summary>
    [JsonPropertyName("default_image")]
    public Image? DefaultImage { get; init; }

    /// <summary>
    /// Whether a cooldown is enabled and what the cooldown is in seconds.
    /// </summary>
    [JsonPropertyName("global_cooldown")]
    public GlobalCooldown? GlobalCooldown { get; init; }

    /// <summary>
    /// Timestamp of the cooldown expiration. Null if the reward isn't on cooldown.
    /// </summary>
    [JsonPropertyName("cooldown_expires_at")]
    public DateTime? CooldownExpiresAt { get; init; }

    /// <summary>
    /// The number of redemptions redeemed during the current live stream. Counts against the max_per_stream limit. Null if the broadcasters stream isn't live or max_per_stream isn't enabled.
    /// </summary>
    [JsonPropertyName("redemptions_redeemed_current_stream")]
    public int? RedemptionsRedeemedCurrentStream { get; init; }
}
