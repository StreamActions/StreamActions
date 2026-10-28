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

using Microsoft.IdentityModel.JsonWebTokens;
using StreamActions.Common.Net;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;

namespace StreamActions.Twitch.Tests.OAuth;

/// <summary>
/// Performs unit and integration testing on <see cref="Token"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class TokenTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// Sample scopes array for OAuth authorization response.
    /// </summary>
    private static readonly string[] _authorizationScopes = ["chat:read", "chat:edit"];

    /// <summary>
    /// Sample scopes array for OAuth refresh response.
    /// </summary>
    private static readonly string[] _refreshScopes = ["chat:read"];

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public TokenTests(TwitchKestrelServerFixture fixture)
    {
        this._fixture = fixture;
        this._fixture.Reset();
    }

    #endregion Public Constructors

    #region IDisposable Implementation

    /// <summary>
    /// Resets the server fixture state after test completion.
    /// </summary>
    public void Dispose() => this._fixture.Reset();

    #endregion IDisposable Implementation

    #region AuthorizeOAuth

    /// <summary>
    /// Tests that <see cref="Token.AuthorizeOAuth(string, Uri, string?)"/> throws an <see cref="ArgumentNullException"/> when the authorization code is null or whitespace.
    /// </summary>
    [Theory]
    [Trait("Member", "AuthorizeOAuth")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AuthorizeOAuth_WhenCodeIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidCode)
    {
        Func<Task> action = async () => await Token.AuthorizeOAuth(invalidCode!, new Uri("http://localhost:3000")).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("code");
    }

    /// <summary>
    /// Tests that <see cref="Token.AuthorizeOAuth(string, Uri, string?)"/> throws an <see cref="ArgumentNullException"/> when the redirect URI is null.
    /// </summary>
    [Fact]
    [Trait("Member", "AuthorizeOAuth")]
    public async Task AuthorizeOAuth_WhenRedirectUriIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Token.AuthorizeOAuth("valid_code", null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("redirectUri");
    }

    /// <summary>
    /// Tests that <see cref="Token.AuthorizeOAuth(string, Uri, string?)"/> posts the authorization code, credentials, and redirect URI, and parses the returned token.
    /// </summary>
    [Fact]
    [Trait("Member", "AuthorizeOAuth")]
    public async Task AuthorizeOAuth_WhenValidRequest_PostsCorrectFormAndReturnsToken()
    {
        string expectedBearer = TestTokenFactory.CreateToken("BEARER");
        string expectedRefresh = TestTokenFactory.CreateToken("REFRESH");
        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = expectedBearer,
            expires_in = 14124,
            refresh_token = expectedRefresh,
            scope = _authorizationScopes,
            token_type = "bearer"
        });

        Uri redirectUri = new("http://localhost:3000");
        Token? token = await Token.AuthorizeOAuth("test_auth_code", redirectUri).ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(expectedBearer);
        _ = token.RefreshToken.Should().Be(expectedRefresh);
        _ = token.ExpiresIn.Should().Be(14124);
        _ = token.TokenType.Should().Be("bearer");
        _ = token.Scopes.Should().NotBeNull();
        _ = token.Scopes.Select(s => s.Name).Should().Equal(["chat:read", "chat:edit"]);
        _ = token.Expires.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(14124), TimeSpan.FromSeconds(5));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Body.Should().Contain("grant_type=authorization_code");
        _ = request.Body.Should().Contain("code=test_auth_code");
        _ = request.Body.Should().Contain($"client_id={this._fixture.ClientId}");
        _ = request.Body.Should().Contain($"client_secret={this._fixture.ClientSecret}");
        _ = request.Body.Should().Contain("redirect_uri=http%3A%2F%2Flocalhost%3A3000");
    }

    #endregion AuthorizeOAuth

    #region AuthorizeApp

    /// <summary>
    /// Tests that <see cref="Token.AuthorizeApp(string?)"/> posts client credentials and parses the returned application token.
    /// </summary>
    [Fact]
    [Trait("Member", "AuthorizeApp")]
    public async Task AuthorizeApp_WhenValidRequest_PostsClientCredentialsAndReturnsToken()
    {
        string expectedBearer = TestTokenFactory.CreateToken("APP");
        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = expectedBearer,
            expires_in = 5011271,
            token_type = "bearer"
        });

        Token? token = await Token.AuthorizeApp().ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(expectedBearer);
        _ = token.ExpiresIn.Should().Be(5011271);
        _ = token.TokenType.Should().Be("bearer");
        _ = token.Expires.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(5011271), TimeSpan.FromSeconds(5));
        _ = token.RefreshToken.Should().BeNull();
        _ = token.Scopes.Should().BeNull();

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Body.Should().Contain("grant_type=client_credentials");
        _ = request.Body.Should().Contain($"client_id={this._fixture.ClientId}");
        _ = request.Body.Should().Contain($"client_secret={this._fixture.ClientSecret}");
    }

    #endregion AuthorizeApp

    #region RefreshOAuth

    /// <summary>
    /// Tests that <see cref="Token.RefreshOAuth(TwitchSession, string?)"/> throws an <see cref="ArgumentNullException"/> when the session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "RefreshOAuth")]
    public async Task RefreshOAuth_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Token.RefreshOAuth(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Token.RefreshOAuth(TwitchSession, string?)"/> throws a <see cref="TokenTypeException"/> when the session token is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "RefreshOAuth")]
    public async Task RefreshOAuth_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Token.RefreshOAuth(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Token.RefreshOAuth(TwitchSession, string?)"/> throws an <see cref="InvalidOperationException"/> when the refresh token is empty.
    /// </summary>
    [Fact]
    [Trait("Member", "RefreshOAuth")]
    public async Task RefreshOAuth_WhenRefreshTokenIsEmpty_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                OAuth = TestTokenFactory.CreateToken("BEARER"),
                Refresh = null
            }
        };

        Func<Task> action = async () => await Token.RefreshOAuth(session).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true))
            .WithMessage("Refresh is not a valid refresh token");
    }

    /// <summary>
    /// Tests that <see cref="Token.RefreshOAuth(TwitchSession, string?)"/> posts the refresh token and returns a newly refreshed token.
    /// </summary>
    [Fact]
    [Trait("Member", "RefreshOAuth")]
    public async Task RefreshOAuth_WhenValidRequest_PostsRefreshTokenAndReturnsNewToken()
    {
        string newBearer = TestTokenFactory.CreateToken("BEARER");
        string newRefresh = TestTokenFactory.CreateToken("REFRESH");
        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = newBearer,
            expires_in = 14000,
            refresh_token = newRefresh,
            scope = _refreshScopes,
            token_type = "bearer"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);
        string originalRefresh = session.Token!.Refresh!;

        Token? token = await Token.RefreshOAuth(session).ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token!.AccessToken.Should().Be(newBearer);
        _ = token.RefreshToken.Should().Be(newRefresh);
        _ = token.ExpiresIn.Should().Be(14000);
        _ = token.TokenType.Should().Be("bearer");
        _ = token.Scopes.Should().NotBeNull();
        _ = token.Scopes!.Select(s => s.Name).Should().BeEquivalentTo(_refreshScopes);
        _ = token.Expires.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(14000), TimeSpan.FromSeconds(10));

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Body.Should().Contain("grant_type=refresh_token");
        _ = request.Body.Should().Contain($"refresh_token={originalRefresh}");
        _ = request.Body.Should().Contain($"client_id={this._fixture.ClientId}");
        _ = request.Body.Should().Contain($"client_secret={this._fixture.ClientSecret}");
    }

    #endregion RefreshOAuth

    #region RevokeOAuth

    /// <summary>
    /// Tests that <see cref="Token.RevokeOAuth(TwitchSession, string)"/> throws an <see cref="ArgumentNullException"/> when the session is null.
    /// </summary>
    [Fact]
    [Trait("Member", "RevokeOAuth")]
    public async Task RevokeOAuth_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await Token.RevokeOAuth(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="Token.RevokeOAuth(TwitchSession, string)"/> throws a <see cref="TokenTypeException"/> when the session token is not a user token.
    /// </summary>
    [Fact]
    [Trait("Member", "RevokeOAuth")]
    public async Task RevokeOAuth_WhenSessionIsNotUserToken_ThrowsTokenTypeException()
    {
        using TwitchSession session = TestTokenFactory.CreateAppSession();
        Func<Task> action = async () => await Token.RevokeOAuth(session, $"{this._fixture.BaseUrl}/oauth2/revoke").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Token.RevokeOAuth(TwitchSession, string)"/> throws an <see cref="InvalidOperationException"/> when the session OAuth token is null or whitespace.
    /// </summary>
    [Fact]
    [Trait("Member", "RevokeOAuth")]
    public async Task RevokeOAuth_WhenOAuthIsNullOrWhitespace_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User
            }
        };

        Func<Task> action = async () => await Token.RevokeOAuth(session).ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="Token.RevokeOAuth(TwitchSession, string)"/> posts the token and client ID and returns a successful response.
    /// </summary>
    [Fact]
    [Trait("Member", "RevokeOAuth")]
    public async Task RevokeOAuth_WhenValidRequest_PostsTokenAndReturnsResponse()
    {
        this._fixture.RegisterJsonResponse("POST", "/oauth2/revoke", new
        {
            status = 200,
            message = ""
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        string tokenToRevoke = session.Token!.OAuth!;

        JsonApiResponse? result = await Token.RevokeOAuth(session, $"{this._fixture.BaseUrl}/oauth2/revoke").ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result!.Status.Should().Be(HttpStatusCode.OK);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request!.Method.Should().Be("POST");
        _ = request.Body.Should().Contain($"token={tokenToRevoke}");
        _ = request.Body.Should().Contain($"client_id={this._fixture.ClientId}");
    }

    #endregion RevokeOAuth

    #region GetJwtTokenAsync

    /// <summary>
    /// Tests that <see cref="Token.GetJwtTokenAsync"/> returns null when the ID token is null.
    /// </summary>
    [Fact]
    [Trait("Member", "GetJwtTokenAsync")]
    public async Task GetJwtTokenAsync_WhenIdTokenIsNull_ReturnsNull()
    {
        Token token = new() { IdToken = null };
        JsonWebToken? jwt = await token.GetJwtTokenAsync().ConfigureAwait(true);
        _ = jwt.Should().BeNull();
    }

    /// <summary>
    /// Tests that <see cref="Token.GetJwtTokenAsync"/> returns null when the ID token string is invalid.
    /// </summary>
    [Fact]
    [Trait("Member", "GetJwtTokenAsync")]
    public async Task GetJwtTokenAsync_WhenIdTokenIsInvalid_ReturnsNull()
    {
        Token token = new() { IdToken = "invalid.jwt.token" };
        JsonWebToken? jwt = await token.GetJwtTokenAsync().ConfigureAwait(true);
        _ = jwt.Should().BeNull();
    }

    /// <summary>
    /// Tests that <see cref="Token.GetJwtTokenAsync"/> validates and returns the parsed <see cref="JsonWebToken"/> when signed.
    /// </summary>
    [Fact]
    [Trait("Member", "GetJwtTokenAsync")]
    public async Task GetJwtTokenAsync_WhenIdTokenIsSignedByFixture_ValidatesAndReturnsJsonWebToken()
    {
        string signedIdToken = this._fixture.CreateSignedJwtIdToken(this._fixture.ClientId, "141981764", "twitchdev");
        Token token = new() { IdToken = signedIdToken };

        JsonWebToken? jwt = await token.GetJwtTokenAsync().ConfigureAwait(true);

        _ = jwt.Should().NotBeNull();
        _ = jwt.Subject.Should().Be("141981764");
        _ = jwt.Audiences.Should().Contain(this._fixture.ClientId);
        _ = jwt.Issuer.Should().Be(this._fixture.BaseUrl);
        _ = jwt.TryGetClaim("preferred_username", out System.Security.Claims.Claim? usernameClaim).Should().BeTrue();
        _ = usernameClaim.Value.Should().Be("twitchdev");
    }

    #endregion GetJwtTokenAsync

    #region SetOpenIdConnectConfigurationUrl

    /// <summary>
    /// Tests that <see cref="Token.SetOpenIdConnectConfigurationUrl(string)"/> successfully updates the OpenID Connect configuration URL.
    /// </summary>
    [Fact]
    [Trait("Member", "SetOpenIdConnectConfigurationUrl")]
    public void SetOpenIdConnectConfigurationUrl_WhenCalled_UpdatesConfigurationLazyInstance()
    {
        string customUrl = $"{this._fixture.BaseUrl}/oauth2/.well-known/openid-configuration";
        Action action = () => Token.SetOpenIdConnectConfigurationUrl(customUrl);
        _ = action.Should().NotThrow();
    }

    #endregion SetOpenIdConnectConfigurationUrl
}
