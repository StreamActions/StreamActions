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

using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;

namespace StreamActions.Twitch.Tests.Api.Common;

/// <summary>
/// Performs unit tests for <see cref="TwitchToken"/>.
/// </summary>
public class TwitchTokenTests
{
    #region Empty

    [Fact]
    [Trait("Member", "Empty")]
    public void Empty_ReturnsDefaultTokenInstance()
    {
        TwitchToken empty = TwitchToken.Empty;
        _ = empty.Should().NotBeNull();
        _ = empty.OAuth.Should().BeNull();
        _ = empty.Refresh.Should().BeNull();
        _ = empty.Scopes.Should().BeNull();
        _ = empty.Type.Should().BeNull();
        _ = empty.UserId.Should().BeNull();
        _ = empty.Login.Should().BeNull();
    }

    #endregion Empty

    #region OAuth

    [Theory]
    [Trait("Member", "OAuth")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OAuth_WhenSetToNullOrWhitespace_ThrowsArgumentNullException(string? invalidValue)
    {
        Action action = () => _ = new TwitchToken { OAuth = invalidValue };
        _ = action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("value");
    }

    [Fact]
    [Trait("Member", "OAuth")]
    public void OAuth_WhenSetToValidString_SetsPropertyValue()
    {
        string tokenString = TestTokenFactory.CreateToken("BEARER");
        TwitchToken token = new() { OAuth = tokenString };
        _ = token.OAuth.Should().Be(tokenString);
    }

    #endregion OAuth

    #region FromApiResponse

    [Fact]
    [Trait("Member", "FromApiResponse")]
    public void FromApiResponse_WithType_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        Action action = () => TwitchToken.FromApiResponse(null!, TwitchToken.TokenType.User);
        _ = action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("response");
    }

    [Fact]
    [Trait("Member", "FromApiResponse")]
    public void FromApiResponse_WithType_WhenResponseIsValid_MapsAllProperties()
    {
        string accessToken = TestTokenFactory.CreateToken("BEARER");
        string refreshToken = TestTokenFactory.CreateToken("REFRESH");
        Scope[] scopes = [Scope.ChatRead, Scope.ChatEdit];
        Token response = new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            Scopes = scopes
        };

        TwitchToken token = TwitchToken.FromApiResponse(response, TwitchToken.TokenType.User, "141981764", "twitchdev");

        _ = token.OAuth.Should().Be(accessToken);
        _ = token.Refresh.Should().Be(refreshToken);
        _ = token.Scopes.Should().Equal(scopes);
        _ = token.Type.Should().Be(TwitchToken.TokenType.User);
        _ = token.UserId.Should().Be("141981764");
        _ = token.Login.Should().Be("twitchdev");
        _ = token.Expires.Should().BeCloseTo(response.Expires, TimeSpan.FromSeconds(2));
    }

    [Fact]
    [Trait("Member", "FromApiResponse")]
    public void FromApiResponse_WithOldToken_WhenResponseIsNull_ThrowsArgumentNullException()
    {
        TwitchToken oldToken = TestTokenFactory.CreateUserToken();
        Action action = () => TwitchToken.FromApiResponse(null!, oldToken);
        _ = action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("response");
    }

    [Fact]
    [Trait("Member", "FromApiResponse")]
    public void FromApiResponse_WithOldToken_WhenOldTokenIsNull_ThrowsArgumentNullException()
    {
        Token response = new() { AccessToken = TestTokenFactory.CreateToken("BEARER") };
        Action action = () => TwitchToken.FromApiResponse(response, (TwitchToken)null!);
        _ = action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("oldToken");
    }

    [Fact]
    [Trait("Member", "FromApiResponse")]
    public void FromApiResponse_WithOldToken_WhenBothValid_UpdatesCredentialsAndPreservesIdentity()
    {
        TwitchToken oldToken = TestTokenFactory.CreateUserToken([Scope.ChatRead], "141981764", "twitchdev");
        string newAccessToken = TestTokenFactory.CreateToken("BEARER");
        string newRefreshToken = TestTokenFactory.CreateToken("REFRESH");
        Scope[] newScopes = [Scope.ChatRead, Scope.ChatEdit];
        Token response = new()
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 7200,
            Scopes = newScopes
        };

        TwitchToken updatedToken = TwitchToken.FromApiResponse(response, oldToken);

        _ = updatedToken.OAuth.Should().Be(newAccessToken);
        _ = updatedToken.Refresh.Should().Be(newRefreshToken);
        _ = updatedToken.Scopes.Should().Equal(newScopes);
        _ = updatedToken.Type.Should().Be(oldToken.Type);
        _ = updatedToken.UserId.Should().Be(oldToken.UserId);
        _ = updatedToken.Login.Should().Be(oldToken.Login);
    }

    #endregion FromApiResponse

    #region CheckScopes

    [Fact]
    [Trait("Member", "CheckScopes")]
    public void CheckScopes_WithStringArray_WhenScopesParameterIsNull_ReturnsEmptyArrays()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        (string[] Found, string[] Missing) = token.CheckScopes((string[]?)null);

        _ = Found.Should().BeEmpty();
        _ = Missing.Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "CheckScopes")]
    public void CheckScopes_WithStringArray_WhenTokenScopesIsNull_ReturnsAllAsFound()
    {
        TwitchToken token = new() { OAuth = TestTokenFactory.CreateToken("BEARER"), Scopes = null };
        (string[] Found, string[] Missing) = token.CheckScopes("chat:read", "chat:edit");

        _ = Found.Should().Equal(["chat:read", "chat:edit"]);
        _ = Missing.Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "CheckScopes")]
    public void CheckScopes_WithStringArray_PartitionsFoundAndMissing()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        (string[] Found, string[] Missing) = token.CheckScopes("chat:read", "chat:edit");

        _ = Found.Should().Equal(["chat:read"]);
        _ = Missing.Should().Equal(["chat:edit"]);
    }

    [Fact]
    [Trait("Member", "CheckScopes")]
    public void CheckScopes_WithScopeArray_WhenScopesParameterIsNull_ReturnsEmptyArrays()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        (Scope?[] Found, Scope?[] Missing) = token.CheckScopes((Scope?[]?)null);

        _ = Found.Should().BeEmpty();
        _ = Missing.Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "CheckScopes")]
    public void CheckScopes_WithScopeArray_WhenTokenScopesIsNull_ReturnsAllAsFound()
    {
        TwitchToken token = new() { OAuth = TestTokenFactory.CreateToken("BEARER"), Scopes = null };
        (Scope?[] Found, Scope?[] Missing) = token.CheckScopes(Scope.ChatRead, Scope.ChatEdit);

        _ = Found.Should().Equal([Scope.ChatRead, Scope.ChatEdit]);
        _ = Missing.Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "CheckScopes")]
    public void CheckScopes_WithScopeArray_PartitionsFoundAndMissing()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        (Scope?[] Found, Scope?[] Missing) = token.CheckScopes(Scope.ChatRead, Scope.ChatEdit);

        _ = Found.Should().Equal([Scope.ChatRead]);
        _ = Missing.Should().Equal([Scope.ChatEdit]);
    }

    #endregion CheckScopes

    #region HasAllScopes

    [Fact]
    [Trait("Member", "HasAllScopes")]
    public void HasAllScopes_WhenAllPresent_ReturnsTrue()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead, Scope.ChatEdit]);
        _ = token.HasAllScopes("chat:read", "chat:edit").Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "HasAllScopes")]
    public void HasAllScopes_WhenAnyMissing_ReturnsFalse()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        _ = token.HasAllScopes("chat:read", "chat:edit").Should().BeFalse();
    }

    #endregion HasAllScopes

    #region HasAnyScope

    [Fact]
    [Trait("Member", "HasAnyScope")]
    public void HasAnyScope_WhenAtLeastOnePresent_ReturnsTrue()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        _ = token.HasAnyScope("chat:read", "chat:edit").Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "HasAnyScope")]
    public void HasAnyScope_WhenNonePresent_ReturnsFalse()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.WhispersRead]);
        _ = token.HasAnyScope("chat:read", "chat:edit").Should().BeFalse();
    }

    #endregion HasAnyScope

    #region HasScope

    [Fact]
    [Trait("Member", "HasScope")]
    public void HasScope_WithString_WhenPresent_ReturnsTrue()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        _ = token.HasScope("chat:read").Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "HasScope")]
    public void HasScope_WithString_WhenNotPresent_ReturnsFalse()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        _ = token.HasScope("chat:edit").Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "HasScope")]
    public void HasScope_WithScope_WhenScopeIsNull_ReturnsFalse()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        _ = token.HasScope((Scope?)null).Should().BeFalse();
    }

    [Theory]
    [Trait("Member", "HasScope")]
    [InlineData(true)]
    [InlineData(false)]
    public void HasScope_WithScope_WhenTokenScopesIsNull_ReturnsRetIfNull(bool retIfNull)
    {
        TwitchToken token = new() { OAuth = TestTokenFactory.CreateToken("BEARER"), Scopes = null };
        _ = token.HasScope(Scope.ChatRead, retIfNull).Should().Be(retIfNull);
    }

    [Fact]
    [Trait("Member", "HasScope")]
    public void HasScope_WithScope_WhenPresent_ReturnsTrue()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        _ = token.HasScope(Scope.ChatRead).Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "HasScope")]
    public void HasScope_WithScope_WhenNotPresent_ReturnsFalse()
    {
        TwitchToken token = TestTokenFactory.CreateUserToken([Scope.ChatRead]);
        _ = token.HasScope(Scope.ChatEdit).Should().BeFalse();
    }

    #endregion HasScope
}
