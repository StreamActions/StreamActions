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

using StreamActions.Common.Attributes;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// A Twitch OAuth scope.
/// </summary>
[ETag("Twitch API Scopes", 82,
    "https://dev.twitch.tv/docs/authentication/scopes", "C5C47EB0D3003023F71C158B871FA4769E7C12CF551F4062467977336462F271",
    "2024-12-07T22:24Z", "TwitchScopesParser", [])]
[JsonConverter(typeof(ScopeJsonConverter))]
public sealed record Scope
{
    /// <summary>
    /// The description of the permissions provided by the scope.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The name of the scope, which is included in OAuth responses.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// A <see cref="IReadOnlyDictionary{TKey, TValue}"/> mapping <see cref="Name"/> to a <see cref="Scope"/> object.
    /// </summary>
    public static IReadOnlyDictionary<string, Scope> Scopes => _scopes.AsReadOnly();

    /// <summary>
    /// Backing dictionary for <see cref="Scopes"/>.
    /// </summary>
    private static readonly Dictionary<string, Scope> _scopes = [];

    #region Scopes

    /// <summary>
    /// View analytics data for the Twitch Extensions owned by the authenticated account.
    /// </summary>
    /// <value>analytics:read:extensions</value>
    public static readonly Scope AnalyticsReadExtensions = new("analytics:read:extensions", "View analytics data for the Twitch Extensions owned by the authenticated account.");

    /// <summary>
    /// View analytics data for the games owned by the authenticated account.
    /// </summary>
    /// <value>analytics:read:games</value>
    public static readonly Scope AnalyticsReadGames = new("analytics:read:games", "View analytics data for the games owned by the authenticated account.");

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
    /// Send chat messages to a chatroom using an IRC connection.
    /// </summary>
    /// <value>chat:edit</value>
    public static readonly Scope ChatEdit = new("chat:edit", "Send chat messages to a chatroom using an IRC connection.");

    /// <summary>
    /// View chat messages sent in a chatroom using an IRC connection.
    /// </summary>
    /// <value>chat:read</value>
    public static readonly Scope ChatRead = new("chat:read", "View chat messages sent in a chatroom using an IRC connection.");

    /// <summary>
    /// Manage Clips for a channel.
    /// </summary>
    /// <value>clips:edit</value>
    public static readonly Scope ClipsEdit = new("clips:edit", "Manage Clips for a channel.");

    /// <summary>
    /// View a channel's moderation data including Moderators, Bans, Timeouts, and Automod settings.
    /// </summary>
    /// <value>moderation:read</value>
    public static readonly Scope ModerationRead = new("moderation:read", "View a channel's moderation data including Moderators, Bans, Timeouts, and Automod settings.");

    /// <summary>
    /// Send announcements in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:manage:announcements</value>
    public static readonly Scope ModeratorManageAnnouncements = new("moderator:manage:announcements", "Send announcements in channels where you have the moderator role.");

    /// <summary>
    /// Manage messages held for review by AutoMod in channels where you are a moderator.
    /// </summary>
    /// <value>moderator:manage:automod</value>
    public static readonly Scope ModeratorManageAutomod = new("moderator:manage:automod", "Manage messages held for review by AutoMod in channels where you are a moderator.");

    /// <summary>
    /// Manage a broadcaster's AutoMod settings.
    /// </summary>
    /// <value>moderator:manage:automod_settings</value>
    public static readonly Scope ModeratorManageAutomodSettings = new("moderator:manage:automod_settings", "Manage a broadcaster's AutoMod settings.");

    /// <summary>
    /// Ban and unban users.
    /// </summary>
    /// <value>moderator:manage:banned_users</value>
    public static readonly Scope ModeratorManageBannedUsers = new("moderator:manage:banned_users", "Ban and unban users.");

    /// <summary>
    /// Manage a broadcaster's list of blocked terms.
    /// </summary>
    /// <value>moderator:manage:blocked_terms</value>
    public static readonly Scope ModeratorManageBlockedTerms = new("moderator:manage:blocked_terms", "Manage a broadcaster's list of blocked terms.");

    /// <summary>
    /// Delete chat messages in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:manage:chat_messages</value>
    public static readonly Scope ModeratorManageChatMessages = new("moderator:manage:chat_messages", "Delete chat messages in channels where you have the moderator role.");

    /// <summary>
    /// Manage a broadcaster's chat room settings.
    /// </summary>
    /// <value>moderator:manage:chat_settings</value>
    public static readonly Scope ModeratorManageChatSettings = new("moderator:manage:chat_settings", "Manage a broadcaster's chat room settings.");

    /// <summary>
    /// Manage Guest Star for channels where you are a Guest Star moderator.
    /// </summary>
    /// <value>moderator:manage:guest_star</value>
    public static readonly Scope ModeratorManageGuestStar = new("moderator:manage:guest_star", "Manage Guest Star for channels where you are a Guest Star moderator.");

    /// <summary>
    /// Manage a broadcaster's Shield Mode status.
    /// </summary>
    /// <value>moderator:manage:shield_mode</value>
    public static readonly Scope ModeratorManageShieldMode = new("moderator:manage:shield_mode", "Manage a broadcaster's Shield Mode status.");

    /// <summary>
    /// Manage a broadcaster's shoutouts.
    /// </summary>
    /// <value>moderator:manage:shoutouts</value>
    public static readonly Scope ModeratorManageShoutouts = new("moderator:manage:shoutouts", "Manage a broadcaster's shoutouts.");

    /// <summary>
    /// Manage a broadcaster's unban requests.
    /// </summary>
    /// <value>moderator:manage:unban_requests</value>
    public static readonly Scope ModeratorManageUnbanRequests = new("moderator:manage:unban_requests", "Manage a broadcaster's unban requests.");

    /// <summary>
    /// Warn users in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:manage:warnings</value>
    public static readonly Scope ModeratorManageWarnings = new("moderator:manage:warnings", "Warn users in channels where you have the moderator role.");

    /// <summary>
    /// View a broadcaster's AutoMod settings.
    /// </summary>
    /// <value>moderator:read:automod_settings</value>
    public static readonly Scope ModeratorReadAutomodSettings = new("moderator:read:automod_settings", "View a broadcaster's AutoMod settings.");

    /// <summary>
    /// Read the list of bans or unbans in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:read:banned_users</value>
    public static readonly Scope ModeratorReadBannedUsers = new("moderator:read:banned_users", "Read the list of bans or unbans in channels where you have the moderator role.");

    /// <summary>
    /// View a broadcaster's list of blocked terms.
    /// </summary>
    /// <value>moderator:read:blocked_terms</value>
    public static readonly Scope ModeratorReadBlockedTerms = new("moderator:read:blocked_terms", "View a broadcaster's list of blocked terms.");

    /// <summary>
    /// Read deleted chat messages in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:read:chat_messages</value>
    public static readonly Scope ModeratorReadChatMessages = new("moderator:read:chat_messages", "Read deleted chat messages in channels where you have the moderator role.");

    /// <summary>
    /// View a broadcaster's chat room settings.
    /// </summary>
    /// <value>moderator:read:chat_settings</value>
    public static readonly Scope ModeratorReadChatSettings = new("moderator:read:chat_settings", "View a broadcaster's chat room settings.");

    /// <summary>
    /// View the chatters in a broadcaster's chat room.
    /// </summary>
    /// <value>moderator:read:chatters</value>
    public static readonly Scope ModeratorReadChatters = new("moderator:read:chatters", "View the chatters in a broadcaster's chat room.");

    /// <summary>
    /// Read the followers of a broadcaster.
    /// </summary>
    /// <value>moderator:read:followers</value>
    public static readonly Scope ModeratorReadFollowers = new("moderator:read:followers", "Read the followers of a broadcaster.");

    /// <summary>
    /// Read Guest Star details for channels where you are a Guest Star moderator.
    /// </summary>
    /// <value>moderator:read:guest_star</value>
    public static readonly Scope ModeratorReadGuestStar = new("moderator:read:guest_star", "Read Guest Star details for channels where you are a Guest Star moderator.");

    /// <summary>
    /// Read the list of moderators in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:read:moderators</value>
    public static readonly Scope ModeratorReadModerators = new("moderator:read:moderators", "Read the list of moderators in channels where you have the moderator role.");

    /// <summary>
    /// View a broadcaster's Shield Mode status.
    /// </summary>
    /// <value>moderator:read:shield_mode</value>
    public static readonly Scope ModeratorReadShieldMode = new("moderator:read:shield_mode", "View a broadcaster's Shield Mode status.");

    /// <summary>
    /// View a broadcaster's shoutouts.
    /// </summary>
    /// <value>moderator:read:shoutouts</value>
    public static readonly Scope ModeratorReadShoutouts = new("moderator:read:shoutouts", "View a broadcaster's shoutouts.");

    /// <summary>
    /// Read chat messages from suspicious users and see users flagged as suspicious in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:read:suspicious_users</value>
    public static readonly Scope ModeratorReadSuspiciousUsers = new("moderator:read:suspicious_users", "Read chat messages from suspicious users and see users flagged as suspicious in channels where you have the moderator role.");

    /// <summary>
    /// View a broadcaster's unban requests.
    /// </summary>
    /// <value>moderator:read:unban_requests</value>
    public static readonly Scope ModeratorReadUnbanRequests = new("moderator:read:unban_requests", "View a broadcaster's unban requests.");

    /// <summary>
    /// Read the list of VIPs in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:read:vips</value>
    public static readonly Scope ModeratorReadVips = new("moderator:read:vips", "Read the list of VIPs in channels where you have the moderator role.");

    /// <summary>
    /// Read warnings in channels where you have the moderator role.
    /// </summary>
    /// <value>moderator:read:warnings</value>
    public static readonly Scope ModeratorReadWarnings = new("moderator:read:warnings", "Read warnings in channels where you have the moderator role.");

    /// <summary>
    /// OpenID Connect.
    /// </summary>
    /// <value>openid</value>
    public static readonly Scope OpenID = new("openid", "OpenID Connect.");

    /// <summary>
    /// Join a specified chat channel as your user and appear as a bot, and perform chat-related actions as your user.
    /// </summary>
    /// <remarks>
    /// This scope is used on a Twitch user to allow access to act on their behalf as a registered bot with the EventSub Chat subscriptions and sending chat messages when using an App Access Token.
    /// </remarks>
    /// <value>user:bot</value>
    public static readonly Scope UserBot = new("user:bot", "Join a specified chat channel as your user and appear as a bot, and perform chat-related actions as your user.");

    /// <summary>
    /// Manage a user object.
    /// </summary>
    /// <value>user:edit</value>
    public static readonly Scope UserEdit = new("user:edit", "Manage a user object.");

    /// <summary>
    /// View and edit a user's broadcasting configuration, including Extension configurations.
    /// </summary>
    /// <value>user:edit:broadcast</value>
    public static readonly Scope UserEditBroadcast = new("user:edit:broadcast", "View and edit a user's broadcasting configuration, including Extension configurations.");

    /// <summary>
    /// Manage the block list of a user.
    /// </summary>
    /// <value>user:manage:blocked_users</value>
    public static readonly Scope UserManageBlockedUsers = new("user:manage:blocked_users", "Manage the block list of a user.");

    /// <summary>
    /// Update the color used for the user's name in chat.
    /// </summary>
    /// <value>user:manage:chat_color</value>
    public static readonly Scope UserManageChatColor = new("user:manage:chat_color", "Update the color used for the user's name in chat.");

    /// <summary>
    /// Receive whispers sent to your user, and send whispers on your user's behalf.
    /// </summary>
    /// <value>user:manage:whispers</value>
    public static readonly Scope UserManageWhispers = new("user:manage:whispers", "Receive whispers sent to your user, and send whispers on your user's behalf.");

    /// <summary>
    /// View the block list of a user.
    /// </summary>
    /// <value>user:read:blocked_users</value>
    public static readonly Scope UserReadBlockedUsers = new("user:read:blocked_users", "View the block list of a user.");

    /// <summary>
    /// View a user's broadcasting configuration, including Extension configurations.
    /// </summary>
    /// <value>user:read:broadcast</value>
    public static readonly Scope UserReadBroadcast = new("user:read:broadcast", "View a user's broadcasting configuration, including Extension configurations.");

    /// <summary>
    /// Receive chatroom messages and informational notifications relating to a channel's chatroom.
    /// </summary>
    /// <value>user:read:chat</value>
    public static readonly Scope UserReadChat = new("user:read:chat", "Receive chatroom messages and informational notifications relating to a channel's chatroom.");

    /// <summary>
    /// View a user's email address.
    /// </summary>
    /// <value>user:read:email</value>
    public static readonly Scope UserReadEmail = new("user:read:email", "View a user's email address.");

    /// <summary>
    /// View emotes available to a user.
    /// </summary>
    /// <value>user:read:emotes</value>
    public static readonly Scope UserReadEmotes = new("user:read:emotes", "View emotes available to a user.");

    /// <summary>
    /// View the list of channels a user follows.
    /// </summary>
    /// <value>user:read:follows</value>
    public static readonly Scope UserReadFollows = new("user:read:follows", "View the list of channels a user follows.");

    /// <summary>
    /// Read the list of channels you have moderator privileges in.
    /// </summary>
    /// <value>user:read:moderated_channels</value>
    public static readonly Scope UserReadModeratedChannels = new("user:read:moderated_channels", "Read the list of channels you have moderator privileges in.");

    /// <summary>
    /// View if an authorized user is subscribed to specific channels.
    /// </summary>
    /// <value>user:read:subscriptions</value>
    public static readonly Scope UserReadSubscriptions = new("user:read:subscriptions", "View if an authorized user is subscribed to specific channels.");

    /// <summary>
    /// Receive whispers sent to your user.
    /// </summary>
    /// <value>user:read:whispers</value>
    public static readonly Scope UserReadWhispers = new("user:read:whispers", "Receive whispers sent to your user.");

    /// <summary>
    /// Send chat messages to a chatroom.
    /// </summary>
    /// <value>user:write:chat</value>
    public static readonly Scope UserWriteChat = new("user:write:chat", "Send chat messages to a chatroom.");

    /// <summary>
    /// Receive whisper messages for your user using PubSub.
    /// </summary>
    /// <value>whispers:read</value>
    public static readonly Scope WhispersRead = new("whispers:read", "Receive whisper messages for your user using PubSub.");

    #endregion Scopes

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="name">The name of the scope, which is included in OAuth responses.</param>
    /// <param name="description">The description of the permissions provided by the scope.</param>
    private Scope(string name, string description)
    {
        this.Name = name;
        this.Description = description;

        _scopes.Add(name, this);
    }
}
