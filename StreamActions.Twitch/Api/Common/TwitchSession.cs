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

using StreamActions.Common.Exceptions;
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
    #region Public Fields

    /// <summary>
    /// A default TwitchSession with an empty token, for requests that do not need one.
    /// </summary>
    public static readonly TwitchSession Empty = new() { Token = TwitchToken.Empty };

    #endregion Public Fields

    #region Public Properties

    /// <summary>
    /// Rate Limiter for TwitchAPI.
    /// </summary>
    public TokenBucketRateLimiter RateLimiter { get; init; } = new(20, TimeSpan.FromSeconds(60));

    /// <summary>
    /// The OAuth token data.
    /// </summary>
    /// <exception cref="ArgumentNullException">Attempt to set to <see langword="null"/>.</exception>
    public TwitchToken? Token
    {
        get => this._token;
        internal set => this._token = value ?? throw new ArgumentNullException(nameof(value)).Log(TwitchApi.GetLogger());
    }

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void Dispose() => this._refreshLock.Close();

    /// <summary>
    /// Checks if an app token is present.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.App"/>.</exception>
    public void RequireAppToken()
    {
        if (this.Token is null)
        {
            throw new InvalidOperationException(nameof(this.Token) + " is null.").Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(this.Token.OAuth))
        {
            throw new InvalidOperationException(nameof(this.Token.OAuth) + " is null, empty, or whitespace.").Log(TwitchApi.GetLogger());
        }

        if (this.Token.Type is not TwitchToken.TokenType.App)
        {
            throw new TokenTypeException(Enum.GetName(TwitchToken.TokenType.App), Enum.GetName(this.Token.Type ?? TwitchToken.TokenType.Unknown)).Log(TwitchApi.GetLogger());
        }
    }

    /// <summary>
    /// Checks if either an app or user token is present. If it is a user token, also checks if the specified scopes are present.
    /// </summary>
    /// <param name="scopes">The scopes to check for.</param>
    /// <exception cref="InvalidOperationException"><see cref="Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.App"/> or <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <exception cref="TwitchScopeMissingException"><see cref="TwitchToken.Type"/> is <see cref="TwitchToken.TokenType.User"/> and <see cref="TwitchToken.Scopes"/> does not contain all of the scopes defined in <paramref name="scopes"/>.</exception>
    /// <remarks>
    /// If <see cref="TwitchToken.Scopes"/> is <see langword="null"/>, then checking of <paramref name="scopes"/> is skipped.
    /// </remarks>
    public void RequireUserOrAppToken(params Scope?[]? scopes)
    {
        if (this.Token is null)
        {
            throw new InvalidOperationException(nameof(this.Token) + " is null.").Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(this.Token.OAuth))
        {
            throw new InvalidOperationException(nameof(this.Token.OAuth) + " is null, empty, or whitespace.").Log(TwitchApi.GetLogger());
        }

        if (this.Token.Type is not TwitchToken.TokenType.App or TwitchToken.TokenType.User)
        {
            throw new TokenTypeException(Enum.GetName(TwitchToken.TokenType.App) + " or " + Enum.GetName(TwitchToken.TokenType.User), Enum.GetName(this.Token.Type ?? TwitchToken.TokenType.Unknown)).Log(TwitchApi.GetLogger());
        }

        if (this.Token.Type is TwitchToken.TokenType.User)
        {
            (_, Scope?[] Missing) = this.Token.CheckScopes(scopes);

            if (Missing.Length > 0)
            {
                throw new TwitchScopeMissingException(Missing).Log(TwitchApi.GetLogger());
            }
        }
    }

    /// <summary>
    /// Checks if a user token is present and contains the specified scopes.
    /// </summary>
    /// <param name="scopes">The scopes to check for.</param>
    /// <exception cref="InvalidOperationException"><see cref="Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TokenTypeException"><see cref="TwitchToken.Type"/> is not <see cref="TwitchToken.TokenType.User"/>.</exception>
    /// <exception cref="TwitchScopeMissingException"><see cref="TwitchToken.Scopes"/> does not contain all of the scopes defined in <paramref name="scopes"/>.</exception>
    /// <remarks>
    /// If <see cref="TwitchToken.Scopes"/> is <see langword="null"/>, then checking of <paramref name="scopes"/> is skipped.
    /// </remarks>
    public void RequireUserToken(params Scope?[]? scopes)
    {
        if (this.Token is null)
        {
            throw new InvalidOperationException(nameof(this.Token) + " is null.").Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(this.Token.OAuth))
        {
            throw new InvalidOperationException(nameof(this.Token.OAuth) + " is null, empty, or whitespace.").Log(TwitchApi.GetLogger());
        }

        if (this.Token.Type is not TwitchToken.TokenType.User)
        {
            throw new TokenTypeException(Enum.GetName(TwitchToken.TokenType.User), Enum.GetName(this.Token.Type ?? TwitchToken.TokenType.Unknown)).Log(TwitchApi.GetLogger());
        }

        (_, Scope?[] Missing) = this.Token.CheckScopes(scopes);

        if (Missing.Length > 0)
        {
            throw new TwitchScopeMissingException(Missing).Log(TwitchApi.GetLogger());
        }
    }

    #endregion Public Methods

    #region Internal Fields

    /// <summary>
    /// Used as a locking object by <see cref="TwitchApi.PerformHttpRequest(HttpMethod, Uri, TwitchSession, HttpContent?, bool)"/> to prevent multiple simultaneous or back-to-back OAuth Refreshes.
    /// </summary>
    internal Mutex _refreshLock = new();

    #endregion Internal Fields

    #region Private Fields

    /// <summary>
    /// Backer for <see cref="Token"/>.
    /// </summary>
    private TwitchToken? _token;

    #endregion Private Fields
}
