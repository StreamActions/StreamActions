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
using Microsoft.IdentityModel.Tokens;
using StreamActions.Common.Net;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using System.Security.Cryptography;

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

    /// <summary>
    /// Tests that <see cref="Token.AuthorizeOAuth(string, Uri, TwitchSession?, string?)"/> posts custom session client credentials when provided.
    /// </summary>
    [Fact]
    [Trait("Member", "AuthorizeOAuth")]
    public async Task AuthorizeOAuth_WhenSessionHasCustomCredentials_PostsCustomCredentialsAndReturnsToken()
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

        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "custom_client_id",
            CustomClientSecret = "custom_client_secret"
        };

        Uri redirectUri = new("http://localhost:3000/callback");
        Token? token = await Token.AuthorizeOAuth("custom_code", redirectUri, session).ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(expectedBearer);
        _ = token.RefreshToken.Should().Be(expectedRefresh);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Headers["Client-Id"].ToString().Should().Be("custom_client_id");
        _ = request.Body.Should().Contain("grant_type=authorization_code");
        _ = request.Body.Should().Contain("code=custom_code");
        _ = request.Body.Should().Contain("client_id=custom_client_id");
        _ = request.Body.Should().Contain("client_secret=custom_client_secret");
        _ = request.Body.Should().Contain("redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Fcallback");
    }

    #endregion AuthorizeOAuth

    #region AuthorizeApp

    /// <summary>
    /// Tests that <see cref="Token.AuthorizeApp(string?, TwitchSession?)"/> posts client credentials and parses the returned application token.
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
    /// Tests that <see cref="Token.GetJwtTokenAsync()"/> returns null when the ID token is null.
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
    /// Tests that <see cref="Token.GetJwtTokenAsync()"/> returns null when the ID token string is invalid.
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
    /// Tests that <see cref="Token.GetJwtTokenAsync()"/> validates and returns the parsed <see cref="JsonWebToken"/> when signed.
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

    /// <summary>
    /// Tests that <see cref="Token.GetJwtTokenAsync(string?, string?, string?, HttpClient?, CancellationToken)"/> validates against the specified expected audience.
    /// </summary>
    [Fact]
    [Trait("Member", "GetJwtTokenAsync")]
    public async Task GetJwtTokenAsync_WhenExpectedAudienceMatchesOrMismatches_ValidatesAccordingly()
    {
        const string customAudience = "custom_audience_client_id";
        string signedIdToken = this._fixture.CreateSignedJwtIdToken(customAudience, "141981764", "twitchdev");
        Token token = new() { IdToken = signedIdToken };

        JsonWebToken? matchingJwt = await token.GetJwtTokenAsync(customAudience, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        JsonWebToken? mismatchedJwt = await token.GetJwtTokenAsync("different_audience", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = matchingJwt.Should().NotBeNull();
        _ = matchingJwt.Audiences.Should().Contain(customAudience);
        _ = mismatchedJwt.Should().BeNull();
    }

    /// <summary>
    /// Tests that <see cref="Token.GetJwtTokenAsync(string?, string?, string?, HttpClient?, CancellationToken)"/> returns the parsed JWT when the <c>nonce</c> claim matches.
    /// </summary>
    [Fact]
    [Trait("Member", "GetJwtTokenAsync")]
    public async Task GetJwtTokenAsync_WhenExpectedNonceMatches_ReturnsJsonWebToken()
    {
        using RSA rsa = RSA.Create(2048);
        string signedIdToken = this.CreateSignedJwtWithOptionalNonce(rsa, this._fixture.ClientId, "expected_nonce_123");
        Token token = new() { IdToken = signedIdToken };

        JsonWebToken? jwt = await token.GetJwtTokenAsync(this._fixture.ClientId, "expected_nonce_123", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = jwt.Should().NotBeNull();
        _ = jwt.TryGetClaim("nonce", out System.Security.Claims.Claim? nonceClaim).Should().BeTrue();
        _ = nonceClaim.Value.Should().Be("expected_nonce_123");
    }

    /// <summary>
    /// Tests that <see cref="Token.GetJwtTokenAsync(string?, string?, string?, HttpClient?, CancellationToken)"/> returns <see langword="null"/> when the <c>nonce</c> claim mismatches or is missing.
    /// </summary>
    /// <param name="tokenNonce">The nonce claim embedded in the signed JWT, or <see langword="null"/> to omit it.</param>
    /// <param name="expectedNonce">The expected nonce passed to validation.</param>
    [Theory]
    [Trait("Member", "GetJwtTokenAsync")]
    [InlineData("actual_nonce", "different_nonce")]
    [InlineData(null, "expected_nonce")]
    public async Task GetJwtTokenAsync_WhenExpectedNonceMismatchesOrMissing_ReturnsNull(string? tokenNonce, string expectedNonce)
    {
        using RSA rsa = RSA.Create(2048);
        string signedIdToken = this.CreateSignedJwtWithOptionalNonce(rsa, this._fixture.ClientId, tokenNonce);
        Token token = new() { IdToken = signedIdToken };

        JsonWebToken? jwt = await token.GetJwtTokenAsync(this._fixture.ClientId, expectedNonce, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = jwt.Should().BeNull();
    }

    /// <summary>
    /// Tests that <see cref="Token.GetJwtTokenAsync(string?, string?, string?, HttpClient?, CancellationToken)"/> skips <c>nonce</c> validation when <paramref name="expectedNonce"/> is <see langword="null"/> or whitespace.
    /// </summary>
    /// <param name="expectedNonce">The null or whitespace expected nonce value.</param>
    [Theory]
    [Trait("Member", "GetJwtTokenAsync")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetJwtTokenAsync_WhenExpectedNonceIsNullOrWhitespace_SkipsNonceValidation(string? expectedNonce)
    {
        string signedIdToken = this._fixture.CreateSignedJwtIdToken(this._fixture.ClientId, "141981764", "twitchdev");
        Token token = new() { IdToken = signedIdToken };

        JsonWebToken? jwt = await token.GetJwtTokenAsync(this._fixture.ClientId, expectedNonce, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = jwt.Should().NotBeNull();
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

    #region Expires

    /// <summary>
    /// Tests that <see cref="Token.Expires"/> remains stable across multiple reads.
    /// </summary>
    [Fact]
    [Trait("Member", "Expires")]
    public async Task Expires_WhenReadMultipleTimes_ReturnsStableTimestamp()
    {
        Token token = new() { ExpiresIn = 3600 };

        DateTime first = token.Expires;
        await Task.Delay(25, TestContext.Current.CancellationToken).ConfigureAwait(true);
        DateTime second = token.Expires;

        _ = second.Should().Be(first);
    }

    #endregion Expires

    #region ToString

    /// <summary>
    /// Tests that <see cref="Token.ToString"/> redacts sensitive token values when present.
    /// </summary>
    [Fact]
    [Trait("Member", "ToString")]
    public void ToString_WhenSecretsPresent_RedactsAccessTokenRefreshTokenAndIdToken()
    {
        Token token = new()
        {
            AccessToken = "secret-access-token",
            RefreshToken = "secret-refresh-token",
            IdToken = "secret-id-token",
            TokenType = "bearer",
            ExpiresIn = 3600
        };

        string text = token.ToString();

        _ = text.Should().Contain("AccessToken = [REDACTED]");
        _ = text.Should().Contain("RefreshToken = [REDACTED]");
        _ = text.Should().Contain("IdToken = [REDACTED]");
        _ = text.Should().NotContain("secret-access-token");
        _ = text.Should().NotContain("secret-refresh-token");
        _ = text.Should().NotContain("secret-id-token");
    }

    /// <summary>
    /// Tests that <see cref="Token.ToString"/> prints <c>[EMPTY]</c> when sensitive token values are null or empty.
    /// </summary>
    [Fact]
    [Trait("Member", "ToString")]
    public void ToString_WhenSecretsNullOrEmpty_PrintsEmptyMarker()
    {
        Token token = new()
        {
            AccessToken = null,
            RefreshToken = "",
            IdToken = null
        };

        string text = token.ToString();

        _ = text.Should().Contain("AccessToken = [EMPTY]");
        _ = text.Should().Contain("RefreshToken = [EMPTY]");
        _ = text.Should().Contain("IdToken = [EMPTY]");
    }

    #endregion ToString

    #region Private Methods

    /// <summary>
    /// Registers a temporary JWKS endpoint on the fixture and creates a signed OIDC ID token with an optional <c>nonce</c> claim.
    /// </summary>
    /// <param name="rsa">The RSA key pair used to sign the token.</param>
    /// <param name="audience">The expected JWT audience.</param>
    /// <param name="nonce">Optional <c>nonce</c> claim value.</param>
    /// <returns>A serialized, signed JWT string.</returns>
    private string CreateSignedJwtWithOptionalNonce(RSA rsa, string audience, string? nonce)
    {
        string keyId = Guid.NewGuid().ToString("N");
        RSAParameters parameters = rsa.ExportParameters(false);
        this._fixture.RegisterJsonResponse("GET", "/oauth2/keys", new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = keyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent)
                }
            }
        });
        Token.SetOpenIdConnectConfigurationUrl($"{this._fixture.BaseUrl}/oauth2/.well-known/openid-configuration");

        RsaSecurityKey securityKey = new(rsa) { KeyId = keyId };
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.RsaSha256);
        Dictionary<string, object> claims = new()
        {
            { "sub", "141981764" },
            { "preferred_username", "twitchdev" }
        };

        if (nonce is not null)
        {
            claims["nonce"] = nonce;
        }

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = this._fixture.BaseUrl,
            Audience = audience,
            Expires = DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = credentials,
            Claims = claims
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    #endregion Private Methods
}
