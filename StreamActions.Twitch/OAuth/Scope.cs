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

using StreamActions.Common.Attributes;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// A Twitch OAuth scope.
/// </summary>
[ETag("https://dev.twitch.tv/docs/authentication/scopes", "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a",
    "2023-01-21T06:21Z", new string[] { "-stripblank", "-strip", "-findfirst", "'<div class=\"main\">'", "-findlast",
        "'<div class=\"subscribe-footer\">'", "-remre", "'cloudcannon[^\"]*'" })]
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
    /// Run commercials on a channel.
    /// </summary>
    /// <value>channel:edit:commercial</value>
    public static readonly Scope ChannelEditCommercial = new("channel:edit:commercial", "Run commercials on a channel.");

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
    /// Perform moderation actions in a channel.
    /// </summary>
    /// <value>channel:moderate</value>
    public static readonly Scope ChannelModerate = new("channel:moderate", "Perform moderation actions in a channel.");

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
    /// Send live stream chat messages.
    /// </summary>
    /// <value>chat:edit</value>
    public static readonly Scope ChatEdit = new("chat:edit", "Send live stream chat messages.");

    /// <summary>
    /// View live stream chat messages.
    /// </summary>
    /// <value>chat:read</value>
    public static readonly Scope ChatRead = new("chat:read", "View live stream chat messages.");

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
    /// View a broadcaster's AutoMod settings.
    /// </summary>
    /// <value>moderator:read:automod_settings</value>
    public static readonly Scope ModeratorReadAutomodSettings = new("moderator:read:automod_settings", "View a broadcaster's AutoMod settings.");

    /// <summary>
    /// View a broadcaster's list of blocked terms.
    /// </summary>
    /// <value>moderator:read:blocked_terms</value>
    public static readonly Scope ModeratorReadBlockedTerms = new("moderator:read:blocked_terms", "View a broadcaster's list of blocked terms.");

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
    /// OpenID Connect.
    /// </summary>
    /// <value>openid</value>
    public static readonly Scope OpenID = new("openid", "OpenID Connect.");

    /// <summary>
    /// Manage a user object.
    /// </summary>
    /// <value>user:edit</value>
    public static readonly Scope UserEdit = new("user:edit", "Manage a user object.");

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
    /// Read whispers that you send and receive, and send whispers on your behalf.
    /// </summary>
    /// <value>user:manage:whispers</value>
    public static readonly Scope UserManageWhispers = new("user:manage:whispers", "Read whispers that you send and receive, and send whispers on your behalf.");

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
    /// View a user's email address.
    /// </summary>
    /// <value>user:read:email</value>
    public static readonly Scope UserReadEmail = new("user:read:email", "View a user's email address.");

    /// <summary>
    /// View the list of channels a user follows.
    /// </summary>
    /// <value>user:read:follows</value>
    public static readonly Scope UserReadFollows = new("user:read:follows", "View the list of channels a user follows.");

    /// <summary>
    /// View if an authorized user is subscribed to specific channels.
    /// </summary>
    /// <value>user:read:subscriptions</value>
    public static readonly Scope UserReadSubscriptions = new("user:read:subscriptions", "View if an authorized user is subscribed to specific channels.");

    /// <summary>
    /// View your whisper messages.
    /// </summary>
    /// <value>whispers:read</value>
    public static readonly Scope WhispersRead = new("whispers:read", "View your whisper messages.");

    #endregion Scopes

    /// <summary>
    /// Backing dictionary for <see cref="Scopes"/>.
    /// </summary>
    private static readonly Dictionary<string, Scope> _scopes = new();

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
