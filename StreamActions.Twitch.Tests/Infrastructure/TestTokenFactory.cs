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

using StreamActions.Twitch.OAuth;
using System.Collections.Concurrent;

namespace StreamActions.Twitch.Tests.Infrastructure;

/// <summary>
/// Factory for generating dummy tokens and sessions for unit testing.
/// </summary>
public static class TestTokenFactory
{
    #region Private Fields

    /// <summary>
    /// Thread-safe map of token counters keyed by token type.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Counter> _counters = new(StringComparer.OrdinalIgnoreCase);

    #endregion Private Fields

    #region Nested Types

    /// <summary>
    /// Thread-safe integer counter.
    /// </summary>
    private sealed class Counter
    {
        /// <summary>
        /// Backing value for the counter.
        /// </summary>
        private int _value;

        /// <summary>
        /// Gets the next incremented value.
        /// </summary>
        /// <returns>The next integer value starting at 1.</returns>
        public int Next() => Interlocked.Increment(ref this._value);
    }

    #endregion Nested Types

    #region Public Methods

    /// <summary>
    /// Creates a dummy token string with an incrementing number per type.
    /// </summary>
    /// <param name="type">The token type string (e.g. BEARER, REFRESH, APP, JWT, CLIENT, SECRET).</param>
    /// <returns>A dummy token string in the format <c>TEST_{TYPE}_ABC123_{NUMBER}</c>.</returns>
    public static string CreateToken(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Counter counter = _counters.GetOrAdd(type, _ => new Counter());
        int number = counter.Next();
        return $"TEST_{type.ToUpperInvariant()}_ABC123_{number}";
    }

    /// <summary>
    /// Creates a test <see cref="TwitchToken"/> of type <see cref="TwitchToken.TokenType.User"/>.
    /// </summary>
    /// <param name="scopes">The authorized scopes, or <see langword="null"/>.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="login">The login name.</param>
    /// <returns>A new <see cref="TwitchToken"/> instance.</returns>
    public static TwitchToken CreateUserToken(IReadOnlyList<Scope>? scopes = null, string? userId = "141981764", string? login = "twitchdev") => new()
    {
        Type = TwitchToken.TokenType.User,
        OAuth = CreateToken("BEARER"),
        Refresh = CreateToken("REFRESH"),
        Scopes = scopes,
        UserId = userId,
        Login = login,
        Expires = DateTime.UtcNow.AddHours(4)
    };

    /// <summary>
    /// Creates a test <see cref="TwitchToken"/> of type <see cref="TwitchToken.TokenType.App"/>.
    /// </summary>
    /// <returns>A new <see cref="TwitchToken"/> instance.</returns>
    public static TwitchToken CreateAppToken() => new()
    {
        Type = TwitchToken.TokenType.App,
        OAuth = CreateToken("APP"),
        Expires = DateTime.UtcNow.AddHours(4)
    };

    /// <summary>
    /// Creates a test <see cref="TwitchToken"/> of type <see cref="TwitchToken.TokenType.Jwt"/>.
    /// </summary>
    /// <returns>A new <see cref="TwitchToken"/> instance.</returns>
    public static TwitchToken CreateJwtToken() => new()
    {
        Type = TwitchToken.TokenType.Jwt,
        OAuth = CreateToken("JWT"),
        Expires = DateTime.UtcNow.AddHours(1)
    };

    /// <summary>
    /// Creates a test <see cref="TwitchSession"/> configured with a user token and optional scopes.
    /// </summary>
    /// <param name="scopes">The scopes to assign to the user token.</param>
    /// <returns>A new <see cref="TwitchSession"/> instance.</returns>
    public static TwitchSession CreateUserSession(params Scope[] scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        return new()
        {
            Token = CreateUserToken(scopes.Length > 0 ? scopes : null)
        };
    }

    /// <summary>
    /// Creates a test <see cref="TwitchSession"/> configured with an app token.
    /// </summary>
    /// <param name="scopes">Optional scopes (ignored for app tokens or stored if provided).</param>
    /// <returns>A new <see cref="TwitchSession"/> instance.</returns>
    public static TwitchSession CreateAppSession(params Scope[] scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        return new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.App,
                OAuth = CreateToken("APP"),
                Scopes = scopes.Length > 0 ? scopes : null,
                Expires = DateTime.UtcNow.AddHours(4)
            }
        };
    }

    /// <summary>
    /// Creates a test <see cref="TwitchSession"/> configured with a JWT token.
    /// </summary>
    /// <returns>A new <see cref="TwitchSession"/> instance.</returns>
    public static TwitchSession CreateJwtSession() => new()
    {
        Token = CreateJwtToken()
    };

    /// <summary>
    /// Creates a test <see cref="TwitchSession"/> configured with an unknown/none token type.
    /// </summary>
    /// <returns>A new <see cref="TwitchSession"/> instance.</returns>
    public static TwitchSession CreateNoneSession() => new()
    {
        Token = new TwitchToken
        {
            Type = TwitchToken.TokenType.Unknown,
            OAuth = CreateToken("UNKNOWN")
        }
    };

    #endregion Public Methods
}
