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
public sealed partial record Scope
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
    /// OpenID Connect.
    /// </summary>
    /// <value>openid</value>
    public static readonly Scope OpenID = new("openid", "OpenID Connect.");

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
