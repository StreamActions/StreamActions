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
    /// A <see cref="IReadOnlyDictionary{TKey, TValue}"/> mapping <see cref="Scope.Name"/> to a <see cref="Scope"/> object.
    /// </summary>
    public static IReadOnlyDictionary<string, Scope> Scopes => _scopes.AsReadOnly();

    /// <summary>
    /// analytics:read:extensions - View analytics data for the Twitch Extensions owned by the authenticated account.
    /// </summary>
    public static readonly Scope AnalyticsReadExtensions = new("analytics:read:extensions", "View analytics data for the Twitch Extensions owned by the authenticated account.");

    /// <summary>
    /// Backing dictionary for <see cref="Scope.Scopes"/>.
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
