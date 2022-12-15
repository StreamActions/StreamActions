/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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
    /// The name of the scope, which is included in OAuth responses.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The description of the permissions provided by the scope.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// For scopes providing <c>edit/manage</c> permissions, indicates what <c>read</c> scope, if any, is implied by this scope.
    /// </summary>
    public Scope? Implies { get; init; }

    /// <summary>
    /// A <see cref="IReadOnlyDictionary{TKey, TValue}"/> mapping <see cref="Scope.Name"/> to a <see cref="Scope"/> object.
    /// </summary>
    public static IReadOnlyDictionary<string, Scope> Scopes => _scopes.AsReadOnly();

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
    /// channel:read:editors - View a list of users with the editor role for a channel.
    /// </summary>
    public static readonly Scope ChannelReadEditors = new("channel:read:editors", "View a list of users with the editor role for a channel.");

    /// <summary>
    /// channel:read:charity - Read charity campaign details and user donations on your channel.
    /// </summary>
    public static readonly Scope ChannelReadCharity = new("channel:read:charity", "Read charity campaign details and user donations on your channel.");

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
    /// channel:manage:vips - Add or remove the VIP role from users in your channel.
    /// </summary>
    public static readonly Scope ChannelManageVips = new("channel:manage:vips", "Add or remove the VIP role from users in your channel.", ChannelReadVips);

    /// <summary>
    /// openid - OpenID Connect.
    /// </summary>
    public static readonly Scope OpenID = new("openid", "OpenID Connect.");

    /// <summary>
    /// Backing dictionary for <see cref="Scope.Scopes"/>.
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
