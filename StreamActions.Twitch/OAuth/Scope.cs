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
using StreamActions.Common.Extensions;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// A Twitch OAuth scope.
/// </summary>
[ETag("https://dev.twitch.tv/docs/authentication/scopes", "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a",
    "2022-12-15T16:40Z", new string[] { "-stripblank", "-strip", "-findfirst", "'<div class=\"main\">'", "-findlast",
        "'<div class=\"subscribe-footer\">'", "-remre", "'cloudcannon[^\"]*'" })]
[JsonConverter(typeof(ScopeJsonConverter))]
public sealed record Scope
{
    /// <summary>
    /// The description of the permissions provided by the scope.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// For scopes providing <c>edit/manage</c> permissions, indicates what <c>read</c> scope, if any, is implied by this scope.
    /// </summary>
    public Scope? Implies { get; init; }

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
    /// analytics:read:extensions - View analytics data for the Twitch Extensions owned by the authenticated account.
    /// </summary>
    public static readonly Scope AnalyticsReadExtensions = new("analytics:read:extensions", "View analytics data for the Twitch Extensions owned by the authenticated account.");

    /// <summary>
    /// analytics:read:games - View analytics data for the games owned by the authenticated account.
    /// </summary>
    public static readonly Scope AnalyticsReadGames = new("analytics:read:games", "View analytics data for the games owned by the authenticated account.");

    /// <summary>
    /// bits:read - View Bits information for a channel.
    /// </summary>
    public static readonly Scope BitsRead = new("bits:read", "View Bits information for a channel.");

    /// <summary>
    /// channel:edit:commercial - Run commercials on a channel.
    /// </summary>
    public static readonly Scope ChannelEditCommercial = new("channel:edit:commercial", "Run commercials on a channel.");

    /// <summary>
    /// channel:manage:broadcast - Manage a channel's broadcast configuration, including updating channel configuration and managing stream markers and stream tags.
    /// </summary>
    public static readonly Scope ChannelManageBroadcast = new("channel:manage:broadcast", "Manage a channel's broadcast configuration, including updating channel configuration and managing stream markers and stream tags.");

    /// <summary>
    /// channel:manage:extensions - Manage a channel's Extension configuration, including activating Extensions.
    /// </summary>
    public static readonly Scope ChannelManageExtensions = new("channel:manage:extensions", "Manage a channel's Extension configuration, including activating Extensions.");

    /// <summary>
    /// channel:manage:moderators - Add or remove the moderator role from users in your channel.
    /// </summary>
    public static readonly Scope ChannelManageModerators = new("channel:manage:moderators", "Add or remove the moderator role from users in your channel.");

    /// <summary>
    /// channel:manage:polls - Manage a channel's polls.
    /// </summary>
    public static readonly Scope ChannelManagePolls = new("channel:manage:polls", "Manage a channel's polls.");

    /// <summary>
    /// channel:manage:predictions - Manage of channel's Channel Points Predictions.
    /// </summary>
    public static readonly Scope ChannelManagePredictions = new("channel:manage:predictions", "Manage of channel's Channel Points Predictions.");

    /// <summary>
    /// channel:manage:raids - Manage a channel raiding another channel.
    /// </summary>
    public static readonly Scope ChannelManageRaids = new("channel:manage:raids", "Manage a channel raiding another channel.");

    /// <summary>
    ///channel:manage:redemptions - Manage Channel Points custom rewards and their redemptions on a channel.
    /// </summary>
    public static readonly Scope ChannelManageRedemptions = new("channel:manage:redemptions", "Manage Channel Points custom rewards and their redemptions on a channel.");

    /// <summary>
    /// channel:manage:schedule - Manage a channel's stream schedule.
    /// </summary>
    public static readonly Scope ChannelManageSchedule = new("channel:manage:schedule", "Manage a channel's stream schedule.");

    /// <summary>
    /// channel:manage:videos - Manage a channel's videos, including deleting videos.
    /// </summary>
    public static readonly Scope ChannelManageVideos = new("channel:manage:videos", "Manage a channel's videos, including deleting videos.");

    /// <summary>
    /// channel:manage:vips - Add or remove the VIP role from users in your channel.
    /// </summary>
    public static readonly Scope ChannelManageVips = new("channel:manage:vips", "Add or remove the VIP role from users in your channel.", ChannelReadVips);

    /// <summary>
    /// channel:moderate - Perform moderation actions in a channel.
    /// </summary>
    public static readonly Scope ChannelModerate = new("channel:moderate", "Perform moderation actions in a channel.");

    /// <summary>
    /// channel:read:charity - Read charity campaign details and user donations on your channel.
    /// </summary>
    public static readonly Scope ChannelReadCharity = new("channel:read:charity", "Read charity campaign details and user donations on your channel.");

    /// <summary>
    /// channel:read:editors - View a list of users with the editor role for a channel.
    /// </summary>
    public static readonly Scope ChannelReadEditors = new("channel:read:editors", "View a list of users with the editor role for a channel.");

    /// <summary>
    /// channel:read:goals - View Creator Goals for a channel.
    /// </summary>
    public static readonly Scope ChannelReadGoals = new("channel:read:goals", "View Creator Goals for a channel.");

    /// <summary>
    /// channel:read:hype_train - View Hype Train information for a channel.
    /// </summary>
    public static readonly Scope ChannelReadHypeTrain = new("channel:read:hype_train", "View Hype Train information for a channel.");

    /// <summary>
    /// channel:read:polls - View a channel's polls.
    /// </summary>
    public static readonly Scope ChannelReadPolls = new("channel:read:polls", "View a channel's polls.");

    /// <summary>
    /// channel:read:predictions - View a channel's Channel Points Predictions.
    /// </summary>
    public static readonly Scope ChannelReadPredictions = new("channel:read:predictions", "View a channel's Channel Points Predictions.");

    /// <summary>
    /// channel:read:redemptions - View Channel Points custom rewards and their redemptions on a channel.
    /// </summary>
    public static readonly Scope ChannelReadRedemptions = new("channel:read:redemptions", "View Channel Points custom rewards and their redemptions on a channel.");

    /// <summary>
    /// channel:read:stream_key - View an authorized user's stream key.
    /// </summary>
    public static readonly Scope ChannelReadStreamKey = new("channel:read:stream_key", "View an authorized user's stream key.");

    /// <summary>
    /// channel:read:subscriptions - View a list of all subscribers to a channel and check if a user is subscribed to a channel.
    /// </summary>
    public static readonly Scope ChannelReadSubscriptions = new("channel:read:subscriptions", "View a list of all subscribers to a channel and check if a user is subscribed to a channel.");

    /// <summary>
    /// channel:read:vips - Read the list of VIPs in your channel.
    /// </summary>
    public static readonly Scope ChannelReadVips = new("channel:read:vips", "Read the list of VIPs in your channel.");

    /// <summary>
    /// chat:edit - Send live stream chat messages.
    /// </summary>
    public static readonly Scope ChatEdit = new("chat:edit", "Send live stream chat messages.");

    /// <summary>
    /// chat:read - View live stream chat messages.
    /// </summary>
    public static readonly Scope ChatRead = new("chat:read", "View live stream chat messages.");

    /// <summary>
    /// clips:edit - Manage Clips for a channel.
    /// </summary>
    public static readonly Scope ClipsEdit = new("clips:edit", "Manage Clips for a channel.");

    /// <summary>
    /// moderation:read - View a channel's moderation data including Moderators, Bans, Timeouts, and Automod settings.
    /// </summary>
    public static readonly Scope ModerationRead = new("moderation:read", "View a channel's moderation data including Moderators, Bans, Timeouts, and Automod settings.");

    /// <summary>
    /// moderator:manage:announcements - Send announcements in channels where you have the moderator role.
    /// </summary>
    public static readonly Scope ModeratorManageAnnouncements = new("moderator:manage:announcements", "Send announcements in channels where you have the moderator role.");

    /// <summary>
    /// moderator:manage:automod - Manage messages held for review by AutoMod in channels where you are a moderator.
    /// </summary>
    public static readonly Scope ModeratorManageAutomod = new("moderator:manage:automod", "Manage messages held for review by AutoMod in channels where you are a moderator.");

    /// <summary>
    /// moderator:manage:automod_settings - Manage a broadcaster's AutoMod settings.
    /// </summary>
    public static readonly Scope ModeratorManageAutomodSettings = new("moderator:manage:automod_settings", "Manage a broadcaster's AutoMod settings.");

    /// <summary>
    /// moderator:manage:banned_users - Ban and unban users.
    /// </summary>
    public static readonly Scope ModeratorManageBannedUsers = new("moderator:manage:banned_users", "Ban and unban users.");

    /// <summary>
    /// moderator:manage:blocked_terms - Manage a broadcaster's list of blocked terms.
    /// </summary>
    public static readonly Scope ModeratormanageBlockedTerms = new("moderator:manage:blocked_terms", "Manage a broadcaster's list of blocked terms.");

    /// <summary>
    /// moderator:manage:chat_messages - Delete chat messages in channels where you have the moderator role.
    /// </summary>
    public static readonly Scope ModeratorManageChatMessages = new("moderator:manage:chat_messages", "Delete chat messages in channels where you have the moderator role.");

    /// <summary>
    /// moderator:manage:chat_settings - Manage a broadcaster's chat room settings.
    /// </summary>
    public static readonly Scope ModeratorManageChatSettings = new("moderator:manage:chat_settings", "Manage a broadcaster's chat room settings.");

    /// <summary>
    /// moderator:manage:shield_mode - Manage a broadcaster's Shield Mode status.
    /// </summary>
    public static readonly Scope ModeratormanageShieldMode = new("moderator:manage:shield_mode", "Manage a broadcaster's Shield Mode status.", ModeratorReadShieldMode);

    /// <summary>
    /// moderator:read:automod_settings - View a broadcaster's AutoMod settings.
    /// </summary>
    public static readonly Scope ModeratorReadAutomodSettings = new("moderator:read:automod_settings", "View a broadcaster's AutoMod settings.");

    /// <summary>
    /// moderator:read:blocked_terms - View a broadcaster's list of blocked terms.
    /// </summary>
    public static readonly Scope ModeratorReadBlockedTerms = new("moderator:read:blocked_terms", "View a broadcaster's list of blocked terms.");

    /// <summary>
    /// moderator:read:chat_settings - View a broadcaster's chat room settings.
    /// </summary>
    public static readonly Scope ModeratorReadChatSettings = new("moderator:read:chat_settings", "View a broadcaster's chat room settings.");

    /// <summary>
    /// moderator:read:chatters - View the chatters in a broadcaster's chat room.
    /// </summary>
    public static readonly Scope ModeratorReadChatters = new("moderator:read:chatters", "View the chatters in a broadcaster's chat room.");

    /// <summary>
    /// moderator:read:shield_mode - View a broadcaster's Shield Mode status.
    /// </summary>
    public static readonly Scope ModeratorReadShieldMode = new("moderator:read:shield_mode", "View a broadcaster's Shield Mode status.");

    /// <summary>
    /// openid - OpenID Connect.
    /// </summary>
    public static readonly Scope OpenID = new("openid", "OpenID Connect.");

    /// <summary>
    /// user:edit - Manage a user object.
    /// </summary>
    public static readonly Scope UserEdit = new("user:edit", "Manage a user object.");

    /// <summary>
    /// user:manage:blocked_users - Manage the block list of a user.
    /// </summary>
    public static readonly Scope UserManageBlockedUsers = new("user:manage:blocked_users", "Manage the block list of a user.");

    /// <summary>
    /// user:manage:chat_color - Update the color used for the user's name in chat.
    /// </summary>
    public static readonly Scope UserManageChatColor = new("user:manage:chat_color", "Update the color used for the user's name in chat.");

    /// <summary>
    /// user:manage:whispers - Read whispers that you send and receive, and send whispers on your behalf.
    /// </summary>
    public static readonly Scope UserManageWhispers = new("user:manage:whispers", "Read whispers that you send and receive, and send whispers on your behalf.");

    /// <summary>
    /// user:read:blocked_users - View the block list of a user.
    /// </summary>
    public static readonly Scope UserReadBlockedUsers = new("user:read:blocked_users", "View the block list of a user.");

    /// <summary>
    /// user:read:broadcast - View a user's broadcasting configuration, including Extension configurations.
    /// </summary>
    public static readonly Scope UserReadBroadcast = new("user:read:broadcast", "View a user's broadcasting configuration, including Extension configurations.");

    /// <summary>
    /// user:read:email - Manage a user object.
    /// </summary>
    public static readonly Scope UserReadEmail = new("user:read:email", "View a user's email address.");

    /// <summary>
    /// user:read:follows - View the list of channels a user follows.
    /// </summary>
    public static readonly Scope UserReadFollows = new("user:read:follows", "View the list of channels a user follows.");

    /// <summary>
    /// user:read:subscriptions - View if an authorized user is subscribed to specific channels.
    /// </summary>
    public static readonly Scope UserReadSubscriptions = new("user:read:subscriptions", "View if an authorized user is subscribed to specific channels.");

    /// <summary>
    /// whispers:read - View your whisper messages.
    /// </summary>
    public static readonly Scope WhispersRead = new("whispers:read", "View your whisper messages.");

    #endregion Scopes

    /// <summary>
    /// Returns all scopes that imply <paramref name="scope"/>.
    /// </summary>
    /// <param name="scope">The scope to check.</param>
    /// <returns>A list of scopes that imply <paramref name="scope"/>.</returns>
    public static IReadOnlyList<Scope> ImpliedBy(Scope scope)
    {
        List<Scope> scopes = new();

        foreach (KeyValuePair<string, Scope> s in _scopes)
        {
            if (s.Value.Implies == scope)
            {
                scopes.Add(s.Value);
            }
        }

        return scopes.AsReadOnly();
    }

    /// <summary>
    /// Backing dictionary for <see cref="Scopes"/>.
    /// </summary>
    private static readonly Dictionary<string, Scope> _scopes = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="name">The name of the scope, which is included in OAuth responses.</param>
    /// <param name="description">The description of the permissions provided by the scope.</param>
    /// <param name="implies">For scopes providing <c>edit/manage</c> permissions, indicates what <c>read</c> scope, if any, is implied by this scope.</param>
    private Scope(string name, string description, Scope? implies = null)
    {
        this.Name = name;
        this.Description = description;
        this.Implies = implies;

        _scopes.Add(name, this);
    }
}
