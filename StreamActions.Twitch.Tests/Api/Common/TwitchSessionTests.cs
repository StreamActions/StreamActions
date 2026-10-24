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
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Common;

/// <summary>
/// Performs unit tests for <see cref="TwitchSession"/>.
/// </summary>
public class TwitchSessionTests
{
    #region Empty

    [Fact]
    [Trait("Member", "Empty")]
    public void Empty_ReturnsSessionWithEmptyToken()
    {
        TwitchSession empty = TwitchSession.Empty;
        _ = empty.Should().NotBeNull();
        _ = empty.Token.Should().Be(TwitchToken.Empty);
    }

    #endregion Empty

    #region RateLimiter

    [Fact]
    [Trait("Member", "RateLimiter")]
    public void RateLimiter_ReturnsNonNullRateLimiter()
    {
        using TwitchSession session = new();
        _ = session.RateLimiter.Should().NotBeNull();
    }

    #endregion RateLimiter

    #region Token

    [Fact]
    [Trait("Member", "Token")]
    public void Token_WhenSetToNull_ThrowsArgumentNullException()
    {
        using TwitchSession session = new();
        Action action = () => session.Token = null;
        _ = action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("value");
    }

    [Fact]
    [Trait("Member", "Token")]
    public void Token_WhenSetToValidToken_ReturnsAssignedToken()
    {
        using TwitchSession session = new();
        TwitchToken token = TestTokenFactory.CreateUserToken();
        session.Token = token;
        _ = session.Token.Should().Be(token);
    }

    #endregion Token

    #region Dispose

    [Fact]
    [Trait("Member", "Dispose")]
    public void Dispose_WhenCalled_ClosesMutexWithoutThrowing()
    {
        using TwitchSession session = new();
        Action action = () => session.Dispose();
        _ = action.Should().NotThrow();
    }

    #endregion Dispose

    #region RequireJwtToken

    [Fact]
    [Trait("Member", "RequireJwtToken")]
    public void RequireJwtToken_WhenTokenIsNull_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new();
        Action action = () => session.RequireJwtToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("Token is null.");
    }

    [Fact]
    [Trait("Member", "RequireJwtToken")]
    public void RequireJwtToken_WhenOAuthIsEmpty_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new() { Token = new TwitchToken { Type = TwitchToken.TokenType.Jwt } };
        Action action = () => session.RequireJwtToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("OAuth is null, empty, or whitespace.");
    }

    [Fact]
    [Trait("Member", "RequireJwtToken")]
    public void RequireJwtToken_WhenTokenTypeIsNotJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Action action = () => session.RequireJwtToken();
        _ = action.Should().Throw<TokenTypeException>();
    }

    [Fact]
    [Trait("Member", "RequireJwtToken")]
    public void RequireJwtToken_WhenValidJwtToken_DoesNotThrow()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Action action = () => session.RequireJwtToken();
        _ = action.Should().NotThrow();
    }

    #endregion RequireJwtToken

    #region RequireAppToken

    [Fact]
    [Trait("Member", "RequireAppToken")]
    public void RequireAppToken_WhenTokenIsNull_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new();
        Action action = () => session.RequireAppToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("Token is null.");
    }

    [Fact]
    [Trait("Member", "RequireAppToken")]
    public void RequireAppToken_WhenOAuthIsEmpty_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new() { Token = new TwitchToken { Type = TwitchToken.TokenType.App } };
        Action action = () => session.RequireAppToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("OAuth is null, empty, or whitespace.");
    }

    [Fact]
    [Trait("Member", "RequireAppToken")]
    public void RequireAppToken_WhenTokenTypeIsNotApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Action action = () => session.RequireAppToken();
        _ = action.Should().Throw<TokenTypeException>();
    }

    [Fact]
    [Trait("Member", "RequireAppToken")]
    public void RequireAppToken_WhenValidAppToken_DoesNotThrow()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Action action = () => session.RequireAppToken();
        _ = action.Should().NotThrow();
    }

    #endregion RequireAppToken

    #region RequireUserOrAppToken

    [Fact]
    [Trait("Member", "RequireUserOrAppToken")]
    public void RequireUserOrAppToken_WhenTokenIsNull_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new();
        Action action = () => session.RequireUserOrAppToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("Token is null.");
    }

    [Fact]
    [Trait("Member", "RequireUserOrAppToken")]
    public void RequireUserOrAppToken_WhenOAuthIsEmpty_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new() { Token = new TwitchToken { Type = TwitchToken.TokenType.User } };
        Action action = () => session.RequireUserOrAppToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("OAuth is null, empty, or whitespace.");
    }

    [Fact]
    [Trait("Member", "RequireUserOrAppToken")]
    public void RequireUserOrAppToken_WhenTokenTypeIsJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateJwtSession();
        Action action = () => session.RequireUserOrAppToken();
        _ = action.Should().Throw<TokenTypeException>();
    }

    [Fact]
    [Trait("Member", "RequireUserOrAppToken")]
    public void RequireUserOrAppToken_WhenAppToken_DoesNotThrowEvenIfScopesSpecified()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Action action = () => session.RequireUserOrAppToken(Scope.ChatRead);
        _ = action.Should().NotThrow();
    }

    [Fact]
    [Trait("Member", "RequireUserOrAppToken")]
    public void RequireUserOrAppToken_WhenUserTokenHasMatchingScope_DoesNotThrow()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Action action = () => session.RequireUserOrAppToken(Scope.ChatRead, Scope.ChatEdit);
        _ = action.Should().NotThrow();
    }

    [Fact]
    [Trait("Member", "RequireUserOrAppToken")]
    public void RequireUserOrAppToken_WhenUserTokenMissingAllScopes_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.WhispersRead);
        Action action = () => session.RequireUserOrAppToken(Scope.ChatRead, Scope.ChatEdit);
        _ = action.Should().Throw<TwitchScopeMissingException>();
    }

    [Fact]
    [Trait("Member", "RequireUserOrAppToken")]
    public void RequireUserOrAppToken_WhenUserTokenScopesIsNull_DoesNotThrow()
    {
        using TwitchSession session = new() { Token = new TwitchToken { Type = TwitchToken.TokenType.User, OAuth = TestTokenFactory.CreateToken("BEARER") } };
        Action action = () => session.RequireUserOrAppToken(Scope.ChatRead);
        _ = action.Should().NotThrow();
    }

    #endregion RequireUserOrAppToken

    #region RequireUserToken

    [Fact]
    [Trait("Member", "RequireUserToken")]
    public void RequireUserToken_WhenTokenIsNull_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new();
        Action action = () => session.RequireUserToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("Token is null.");
    }

    [Fact]
    [Trait("Member", "RequireUserToken")]
    public void RequireUserToken_WhenOAuthIsEmpty_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new() { Token = new TwitchToken { Type = TwitchToken.TokenType.User } };
        Action action = () => session.RequireUserToken();
        _ = action.Should().Throw<InvalidOperationException>()
            .WithMessage("OAuth is null, empty, or whitespace.");
    }

    [Fact]
    [Trait("Member", "RequireUserToken")]
    public void RequireUserToken_WhenTokenTypeIsApp_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Action action = () => session.RequireUserToken();
        _ = action.Should().Throw<TokenTypeException>();
    }

    [Fact]
    [Trait("Member", "RequireUserToken")]
    public void RequireUserToken_WhenNoScopesSpecified_DoesNotThrow()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Action action = () => session.RequireUserToken();
        _ = action.Should().NotThrow();
    }

    [Fact]
    [Trait("Member", "RequireUserToken")]
    public void RequireUserToken_WhenUserTokenHasMatchingScope_DoesNotThrow()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        Action action = () => session.RequireUserToken(Scope.ChatRead, Scope.ChatEdit);
        _ = action.Should().NotThrow();
    }

    [Fact]
    [Trait("Member", "RequireUserToken")]
    public void RequireUserToken_WhenUserTokenMissingAllScopes_ThrowsTwitchScopeMissingException()
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.WhispersRead);
        Action action = () => session.RequireUserToken(Scope.ChatRead, Scope.ChatEdit);
        _ = action.Should().Throw<TwitchScopeMissingException>();
    }

    [Fact]
    [Trait("Member", "RequireUserToken")]
    public void RequireUserToken_WhenUserTokenScopesIsNull_DoesNotThrow()
    {
        using TwitchSession session = new() { Token = new TwitchToken { Type = TwitchToken.TokenType.User, OAuth = TestTokenFactory.CreateToken("BEARER") } };
        Action action = () => session.RequireUserToken(Scope.ChatRead);
        _ = action.Should().NotThrow();
    }

    #endregion RequireUserToken
}
