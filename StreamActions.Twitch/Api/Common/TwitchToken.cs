/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using StreamActions.Twitch.OAuth;
using System.Collections.Immutable;

namespace StreamActions.Twitch.Api.Common;

/// <summary>
/// Contains information for a Twitch API token.
/// </summary>
public sealed record TwitchToken
{
    #region Public Fields

    /// <summary>
    /// An empty token, for requests that do not need one.
    /// </summary>
    public static readonly TwitchToken Empty = new();

    #endregion Public Fields

    #region Public Enums

    /// <summary>
    /// The type of OAuth token.
    /// </summary>
    public enum TokenType
    {
        /// <summary>
        /// A User token.
        /// </summary>
        User,

        /// <summary>
        /// An App token.
        /// </summary>
        App
    }

    #endregion Public Enums

    #region Public Properties

    /// <summary>
    /// The expiration timestamp of the <see cref="OAuth"/>.
    /// </summary>
    public DateTime? Expires { get; init; }

    /// <summary>
    /// The login name associated with the OAuth token.
    /// </summary>
    public string? Login { get; init; }

    /// <summary>
    /// The OAuth token.
    /// </summary>
    /// <exception cref="ArgumentNullException">Attempt to set to <see langword="null"/> or whitespace.</exception>
    public string? OAuth
    {
        get => this._oauth;
        init => this._oauth = string.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(value)) : value;
    }

    /// <summary>
    /// The refresh token.
    /// </summary>
    public string? Refresh { get; init; }

    /// <summary>
    /// The scopes that are authorized under the OAuth token.
    /// </summary>
    public IReadOnlyList<Scope>? Scopes { get; init; }

    /// <summary>
    /// The type of OAuth token.
    /// </summary>
    public TokenType? Type { get; init; }

    /// <summary>
    /// The User Id associated with the OAuth token.
    /// </summary>
    public string? UserId { get; init; }

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// Checks if the specified scope is present in <see cref="Scopes"/>.
    /// </summary>
    /// <param name="scope">The scope to find.</param>
    /// <returns><see langword="true"/> if <see cref="Scopes"/> is <see langword="null"/> or contains <paramref name="scope"/>.</returns>
    public bool HasScope(string scope) => this.HasScope(Scope.Scopes.GetValueOrDefault(scope));

    /// <summary>
    /// Checks if the specified scope is present in <see cref="Scopes"/>.
    /// </summary>
    /// <param name="scope">The scope to find.</param>
    /// <param name="retIfNull">Returned if <see cref="Scopes"/> is <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <see cref="Scopes"/> contains <paramref name="scope"/>; <paramref name="retIfNull"/> if <see cref="Scopes"/> is <see langword="null"/>.</returns>
    public bool HasScope(Scope? scope, bool retIfNull = false) => scope is not null && (this.Scopes is null ? retIfNull : this.Scopes.Contains(scope));

    #endregion Public Methods

    #region Private Fields

    /// <summary>
    /// Backer for <see cref="OAuth"/>.
    /// </summary>
    private string? _oauth;

    #endregion Private Fields
}
