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

namespace StreamActions.Twitch.OAuth;

public sealed partial record Scope
{
    /// <summary>
    /// View Bits information for a channel.
    /// </summary>
    /// <value>bits:read</value>
    public static readonly Scope BitsRead = new("bits:read", "View Bits information for a channel.");

    /// <summary>
    /// Join your channel's chatroom as a bot user, and perform chat-related actions as that user.
    /// </summary>
    /// <remarks>
    /// This scope is used on a broadcaster to allow access to the EventSub Chat subscriptions and sending chat messages when using an App Access Token. Alternatively, the bot user who was authorized with <see cref="UserBot"/> can be made a moderator.
    /// </remarks>
    /// <value>channel:bot</value>
    public static readonly Scope ChannelBot = new("channel:bot", "Join your channel's chatroom as a bot user, and perform chat-related actions as that user.");

    /// <summary>
    /// Run commercials on a channel.
    /// </summary>
    /// <value>channel:edit:commercial</value>
    public static readonly Scope ChannelEditCommercial = new("channel:edit:commercial", "Run commercials on a channel.");

    /// <summary>
    /// Manage ads schedule on a channel.
    /// </summary>
    /// <value>channel:manage:ads</value>
    public static readonly Scope ChannelManageAds = new("channel:manage:ads", "Manage ads schedule on a channel.");

    /// <summary>
    /// Manage a channel's broadcast configuration, including updating channel configuration and managing stream markers and stream tags.
    /// </summary>
    /// <value>channel:manage:broadcast</value>
    public static readonly Scope ChannelManageBroadcast = new("channel:manage:broadcast", "Manage a channel's broadcast configuration, including updating channel configuration and managing stream markers and stream tags.");

    /// <summary>
    /// Manage a channel's Extension configuration, including activating Extensions.
    /// </summary>
    /// <value>channel:manage:extensions</value>
    public static readonly Scope ChannelManageExtensions = new("channel:manage:extensions", "Manage a channel's Extension configuration, including activating Extensions.");

    /// <summary>
    /// Manage Guest Star for your channel.
    /// </summary>
    /// <value>channel:manage:guest_star</value>
    public static readonly Scope ChannelManageGuestStar = new("channel:manage:guest_star", "Manage Guest Star for your channel.");

    /// <summary>
    /// Add or remove the moderator role from users in your channel.
    /// </summary>
    /// <value>channel:manage:moderators</value>
    public static readonly Scope ChannelManageModerators = new("channel:manage:moderators", "Add or remove the moderator role from users in your channel.");

    /// <summary>
    /// Manage a channel's polls.
    /// </summary>
    /// <value>channel:manage:polls</value>
    public static readonly Scope ChannelManagePolls = new("channel:manage:polls", "Manage a channel's polls.");

    /// <summary>
    /// Manage of channel's Channel Points Predictions.
    /// </summary>
    /// <value>channel:manage:predictions</value>
    public static readonly Scope ChannelManagePredictions = new("channel:manage:predictions", "Manage of channel's Channel Points Predictions.");

    /// <summary>
    /// Manage a channel raiding another channel.
    /// </summary>
    /// <value>channel:manage:raids</value>
    public static readonly Scope ChannelManageRaids = new("channel:manage:raids", "Manage a channel raiding another channel.");

    /// <summary>
    /// Manage Channel Points custom rewards and their redemptions on a channel.
    /// </summary>
    /// <value>channel:manage:redemptions</value>
    public static readonly Scope ChannelManageRedemptions = new("channel:manage:redemptions", "Manage Channel Points custom rewards and their redemptions on a channel.");

    /// <summary>
    /// Manage a channel's stream schedule.
    /// </summary>
    /// <value>channel:manage:schedule</value>
    public static readonly Scope ChannelManageSchedule = new("channel:manage:schedule", "Manage a channel's stream schedule.");

    /// <summary>
    /// Manage a channel's videos, including deleting videos.
    /// </summary>
    /// <value>channel:manage:videos</value>
    public static readonly Scope ChannelManageVideos = new("channel:manage:videos", "Manage a channel's videos, including deleting videos.");

    /// <summary>
    /// Add or remove the VIP role from users in your channel.
    /// </summary>
    /// <value>channel:manage:vips</value>
    public static readonly Scope ChannelManageVips = new("channel:manage:vips", "Add or remove the VIP role from users in your channel.");

    /// <summary>
    /// Read the ads schedule and details on your channel.
    /// </summary>
    /// <value>channel:read:ads</value>
    public static readonly Scope ChannelReadAds = new("channel:read:ads", "Read the ads schedule and details on your channel.");

    /// <summary>
    /// Read charity campaign details and user donations on your channel.
    /// </summary>
    /// <value>channel:read:charity</value>
    public static readonly Scope ChannelReadCharity = new("channel:read:charity", "Read charity campaign details and user donations on your channel.");

    /// <summary>
    /// View a list of users with the editor role for a channel.
    /// </summary>
    /// <value>channel:read:editors</value>
    public static readonly Scope ChannelReadEditors = new("channel:read:editors", "View a list of users with the editor role for a channel.");

    /// <summary>
    /// View Creator Goals for a channel.
    /// </summary>
    /// <value>channel:read:goals</value>
    public static readonly Scope ChannelReadGoals = new("channel:read:goals", "View Creator Goals for a channel.");

    /// <summary>
    /// Read Guest Star details for your channel.
    /// </summary>
    /// <value>channel:read:guest_star</value>
    public static readonly Scope ChannelReadGuestStar = new("channel:read:guest_star", "Read Guest Star details for your channel.");

    /// <summary>
    /// View Hype Train information for a channel.
    /// </summary>
    /// <value>channel:read:hype_train</value>
    public static readonly Scope ChannelReadHypeTrain = new("channel:read:hype_train", "View Hype Train information for a channel.");

    /// <summary>
    /// View a channel's polls.
    /// </summary>
    /// <value>channel:read:polls</value>
    public static readonly Scope ChannelReadPolls = new("channel:read:polls", "View a channel's polls.");

    /// <summary>
    /// View a channel's Channel Points Predictions.
    /// </summary>
    /// <value>channel:read:predictions</value>
    public static readonly Scope ChannelReadPredictions = new("channel:read:predictions", "View a channel's Channel Points Predictions.");

    /// <summary>
    /// View Channel Points custom rewards and their redemptions on a channel.
    /// </summary>
    /// <value>channel:read:redemptions</value>
    public static readonly Scope ChannelReadRedemptions = new("channel:read:redemptions", "View Channel Points custom rewards and their redemptions on a channel.");

    /// <summary>
    /// View an authorized user's stream key.
    /// </summary>
    /// <value>channel:read:stream_key</value>
    public static readonly Scope ChannelReadStreamKey = new("channel:read:stream_key", "View an authorized user's stream key.");

    /// <summary>
    /// View a list of all subscribers to a channel and check if a user is subscribed to a channel.
    /// </summary>
    /// <value>channel:read:subscriptions</value>
    public static readonly Scope ChannelReadSubscriptions = new("channel:read:subscriptions", "View a list of all subscribers to a channel and check if a user is subscribed to a channel.");

    /// <summary>
    /// Read the list of VIPs in your channel.
    /// </summary>
    /// <value>channel:read:vips</value>
    public static readonly Scope ChannelReadVips = new("channel:read:vips", "Read the list of VIPs in your channel.");

    /// <summary>
    /// Manage Clips for a channel.
    /// </summary>
    /// <value>clips:edit</value>
    public static readonly Scope ClipsEdit = new("clips:edit", "Manage Clips for a channel.");
}
