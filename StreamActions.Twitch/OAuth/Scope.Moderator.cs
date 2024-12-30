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

namespace StreamActions.Twitch.OAuth;

public sealed partial record Scope
{
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
}
