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

using StreamActions.Common.Attributes;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StreamActions.Twitch.OAuth;

/// <summary>
/// A Twitch OAuth scope.
/// </summary>
[DocParser("Twitch API Scopes", 82, "https://dev.twitch.tv/docs/authentication/scopes", "twitch/TwitchScopesParser")]
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
    public static IReadOnlyDictionary<string, Scope> Scopes => ScopeCache.FrozenScopes;

    /// <summary>
    /// Backing dictionary populated during static field initialization for <see cref="Scopes"/>.
    /// </summary>
    private static Dictionary<string, Scope>? _scopes;

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
    /// Constructor for known scopes registered in <see cref="Scopes"/>.
    /// </summary>
    /// <param name="name">The name of the scope, which is included in OAuth responses.</param>
    /// <param name="description">The description of the permissions provided by the scope.</param>
    private Scope(string name, string description)
    {
        this.Name = name;
        this.Description = description;

        _scopes ??= [];
        _scopes.Add(name, this);
    }

    /// <summary>
    /// Constructor for an unrecognized scope returned by Twitch without registering it in <see cref="Scopes"/>.
    /// </summary>
    /// <param name="name">The unknown scope name.</param>
    private Scope(string name) => this.Name = name;

    /// <summary>
    /// Creates a <see cref="Scope"/> instance for an unrecognized scope name without adding it to <see cref="Scopes"/>.
    /// </summary>
    /// <param name="name">The scope name.</param>
    /// <returns>A <see cref="Scope"/> representing <paramref name="name"/>.</returns>
    internal static Scope CreateUnknown(string name) => new(name);

    /// <summary>
    /// Lazily initializes and caches the frozen dictionary of known <see cref="Scope"/> instances after all partial type fields are constructed.
    /// </summary>
    private static class ScopeCache
    {
        /// <summary>
        /// The cached <see cref="FrozenDictionary{TKey, TValue}"/> mapping scope names to <see cref="Scope"/> instances.
        /// </summary>
        internal static readonly FrozenDictionary<string, Scope> FrozenScopes = BuildFrozenScopes();

        /// <summary>
        /// Ensures the <see cref="Scope"/> static constructor has executed across all partial files and freezes the registered scopes.
        /// </summary>
        /// <returns>A <see cref="FrozenDictionary{TKey, TValue}"/> of all known scopes.</returns>
        private static FrozenDictionary<string, Scope> BuildFrozenScopes()
        {
            RuntimeHelpers.RunClassConstructor(typeof(Scope).TypeHandle);
            return (_scopes ?? []).ToFrozenDictionary(StringComparer.Ordinal);
        }
    }
}
