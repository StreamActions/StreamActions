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

using StreamActions.Common.Limiters;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;

namespace StreamActions.Twitch.Api.Common;

/// <summary>
/// Contains parameters for a Twitch API session.
/// </summary>
public sealed record TwitchSession : IDisposable
{
    /// <summary>
    /// A default TwitchSession with an empty token, for requests that do not need one.
    /// </summary>
    public static readonly TwitchSession Empty = new() { Token = TwitchToken.Empty };

    /// <summary>
    /// Rate Limiter for TwitchAPI.
    /// </summary>
    public TokenBucketRateLimiter RateLimiter { get; init; } = new(1, TimeSpan.FromSeconds(60));

    /// <summary>
    /// The OAuth token data.
    /// </summary>
    /// <exception cref="ArgumentNullException">Attempt to set to <see langword="null"/>.</exception>
    public TwitchToken? Token
    {
        get => this._token;
        internal set => this._token = value ?? throw new ArgumentNullException(nameof(value)).Log(TwitchApi.GetLogger());
    }

    /// <summary>
    /// Used as a locking object by <see cref="TwitchApi.PerformHttpRequest(HttpMethod, Uri, TwitchSession, HttpContent?, bool)"/> to prevent multiple simultaneous or back-to-back OAuth Refreshes.
    /// </summary>
    internal Mutex _refreshLock = new();

    /// <summary>
    /// Backer for <see cref="Token"/>.
    /// </summary>
    private TwitchToken? _token;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void Dispose() => this._refreshLock.Close();

    /// <summary>
    /// Checks if a token is present and, optionally, a scope.
    /// </summary>
    /// <param name="scope">The scope to check for.</param>
    /// <param name="alternateScope">An alternate scope to check for.</param>
    /// <param name="retIfNull">If <see cref="TwitchToken.Scopes"/> is <see langword="null"/> and this is <see langword="true"/>, <see cref="TwitchScopeMissingException"/> is not thrown.</param>
    /// <exception cref="InvalidOperationException"><see cref="Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><see cref="TwitchToken.Scopes"/> does not contain <paramref name="scope"/> or <paramref name="alternateScope"/>.</exception>
    public void RequireToken(Scope? scope = null, Scope? alternateScope = null, bool retIfNull = false)
    {
        if (this.Token is null)
        {
            throw new InvalidOperationException(nameof(this.Token) + " is null.").Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(this.Token.OAuth))
        {
            throw new InvalidOperationException(nameof(this.Token.OAuth) + " is null, empty, or whitespace.").Log(TwitchApi.GetLogger());
        }

        bool hasScope = false;
        if (scope is not null)
        {
            hasScope = this.Token.HasScope(scope, alternateScope is null ? retIfNull : false);
        }

        if (!hasScope && alternateScope is not null)
        {
            hasScope = this.Token.HasScope(alternateScope, retIfNull);
        }

        if (!hasScope)
        {
            throw new TwitchScopeMissingException(scope).Log(TwitchApi.GetLogger());
        }
    }
}
