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

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Common.Net;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.Tests.Infrastructure;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace StreamActions.Twitch.Tests.OAuth;

/// <summary>
/// Performs unit and integration testing on <see cref="TwitchOAuthClient"/>.
/// </summary>
[Collection("TwitchKestrelCollection")]
public sealed class TwitchOAuthClientTests : IDisposable
{
    #region Private Fields

    /// <summary>
    /// Sample scopes array for OAuth token responses.
    /// </summary>
    private static readonly string[] _authorizationScopes = ["chat:read", "chat:edit"];

    /// <summary>
    /// Sample scopes array for OAuth refresh responses.
    /// </summary>
    private static readonly string[] _refreshScopes = ["chat:read"];

    /// <summary>
    /// The Kestrel test server fixture.
    /// </summary>
    private readonly TwitchKestrelServerFixture _fixture;

    #endregion Private Fields

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TwitchOAuthClientTests"/> class.
    /// </summary>
    /// <param name="fixture">The test server fixture.</param>
    public TwitchOAuthClientTests(TwitchKestrelServerFixture fixture)
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

    #region GenerateCodeVerifier

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.GenerateCodeVerifier"/> produces unique, RFC 7636-compliant 43-character verifiers.
    /// </summary>
    [Fact]
    [Trait("Member", "GenerateCodeVerifier")]
    public void GenerateCodeVerifier_WhenCalled_ReturnsUniqueRfc7636CompliantVerifiers()
    {
        string first = TwitchOAuthClient.GenerateCodeVerifier();
        string second = TwitchOAuthClient.GenerateCodeVerifier();

        _ = first.Should().NotBeNullOrWhiteSpace();
        _ = first.Length.Should().BeInRange(43, 128);
        _ = first.Should().MatchRegex("^[A-Za-z0-9\\-._~]+$");
        _ = second.Should().MatchRegex("^[A-Za-z0-9\\-._~]+$");
        _ = second.Should().NotBe(first);
    }

    #endregion GenerateCodeVerifier

    #region ComputeCodeChallenge

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ComputeCodeChallenge"/> throws <see cref="ArgumentNullException"/> when <c>codeVerifier</c> is <see langword="null"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "ComputeCodeChallenge")]
    public void ComputeCodeChallenge_WhenCodeVerifierIsNull_ThrowsArgumentNullException()
    {
        Action action = () => _ = TwitchOAuthClient.ComputeCodeChallenge(null!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName("codeVerifier");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ComputeCodeChallenge"/> throws <see cref="ArgumentException"/> when <paramref name="invalidVerifier"/> violates RFC 7636 length or character rules.
    /// </summary>
    /// <param name="invalidVerifier">The invalid PKCE code verifier.</param>
    [Theory]
    [Trait("Member", "ComputeCodeChallenge")]
    [InlineData("")]
    [InlineData("                                           ")]
    [InlineData("too_short_verifier_under_43_characters_12")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("invalid+character_in_verifier_0123456789abcdef")]
    [InlineData("invalid/character_in_verifier_0123456789abcdef")]
    [InlineData("invalid=character_in_verifier_0123456789abcdef")]
    public void ComputeCodeChallenge_WhenCodeVerifierInvalid_ThrowsArgumentException(string invalidVerifier)
    {
        Action action = () => _ = TwitchOAuthClient.ComputeCodeChallenge(invalidVerifier);

        _ = action.Should().Throw<ArgumentException>()
            .WithParameterName("codeVerifier");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ComputeCodeChallenge"/> matches the RFC 7636 Appendix B S256 test vector.
    /// </summary>
    [Fact]
    [Trait("Member", "ComputeCodeChallenge")]
    public void ComputeCodeChallenge_WhenValidVerifierProvided_MatchesRfc7636TestVector()
    {
        const string rfc7636Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        string challenge = TwitchOAuthClient.ComputeCodeChallenge(rfc7636Verifier);

        _ = challenge.Should().Be(expectedChallenge);
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ComputeCodeChallenge"/> computes a 43-character unpadded base64url challenge from a generated verifier.
    /// </summary>
    [Fact]
    [Trait("Member", "ComputeCodeChallenge")]
    public void ComputeCodeChallenge_WithGeneratedVerifier_ReturnsUnpaddedBase64UrlChallenge()
    {
        string verifier = TwitchOAuthClient.GenerateCodeVerifier();
        string challenge = TwitchOAuthClient.ComputeCodeChallenge(verifier);

        _ = challenge.Length.Should().Be(43);
        _ = challenge.Should().MatchRegex("^[A-Za-z0-9\\-_]+$");
    }

    #endregion ComputeCodeChallenge

    #region BuildAuthorizationUri

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> throws <see cref="ArgumentNullException"/> when <paramref name="clientId"/>, <paramref name="state"/>, or <paramref name="authorizeEndpoint"/> is null or whitespace.
    /// </summary>
    /// <param name="clientId">The client ID argument.</param>
    /// <param name="state">The state argument.</param>
    /// <param name="authorizeEndpoint">The authorize endpoint argument.</param>
    /// <param name="expectedParamName">The expected failing parameter name.</param>
    [Theory]
    [Trait("Member", "BuildAuthorizationUri")]
    [InlineData(null, "state123", "https://id.twitch.tv/oauth2/authorize", "clientId")]
    [InlineData("   ", "state123", "https://id.twitch.tv/oauth2/authorize", "clientId")]
    [InlineData("client123", null, "https://id.twitch.tv/oauth2/authorize", "state")]
    [InlineData("client123", "", "https://id.twitch.tv/oauth2/authorize", "state")]
    [InlineData("client123", "state123", null, "authorizeEndpoint")]
    [InlineData("client123", "state123", " ", "authorizeEndpoint")]
    public void BuildAuthorizationUri_WhenRequiredStringParameterInvalid_ThrowsArgumentNullException(
        string? clientId,
        string? state,
        string? authorizeEndpoint,
        string expectedParamName)
    {
        Uri redirectUri = new("http://localhost:3000/callback");
        Action action = () => _ = TwitchOAuthClient.BuildAuthorizationUri(clientId!, redirectUri, null, state!, false, authorizeEndpoint!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName(expectedParamName);
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> throws <see cref="ArgumentNullException"/> when <c>redirectUri</c> is <see langword="null"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildAuthorizationUri")]
    public void BuildAuthorizationUri_WhenRedirectUriIsNull_ThrowsArgumentNullException()
    {
        Action action = () => _ = TwitchOAuthClient.BuildAuthorizationUri("client123", null!, null, "state123");

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName("redirectUri");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> omits optional parameters when scopes are empty and <c>forceVerify</c> is <see langword="false"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildAuthorizationUri")]
    public void BuildAuthorizationUri_WhenOptionalParametersOmitted_OmitsScopeAndForceVerify()
    {
        Uri redirectUri = new("http://localhost:3000/auth/callback");
        Uri result = TwitchOAuthClient.BuildAuthorizationUri("my_client_id", redirectUri, [], "csrf_state_123");

        _ = result.GetLeftPart(UriPartial.Path).Should().Be("https://id.twitch.tv/oauth2/authorize");

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(result.Query);
        _ = query["response_type"].ToString().Should().Be("code");
        _ = query["client_id"].ToString().Should().Be("my_client_id");
        _ = query["redirect_uri"].ToString().Should().Be("http://localhost:3000/auth/callback");
        _ = query["state"].ToString().Should().Be("csrf_state_123");
        _ = query.Should().NotContainKey("scope");
        _ = query.Should().NotContainKey("force_verify");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> includes scopes and <c>force_verify=true</c> when provided.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildAuthorizationUri")]
    public void BuildAuthorizationUri_WhenScopesAndForceVerifyProvided_IncludesAllExpectedQueryParameters()
    {
        Uri redirectUri = new("http://localhost:3000/auth/callback");
        Uri result = TwitchOAuthClient.BuildAuthorizationUri(
            "my_client_id",
            redirectUri,
            ["chat:read", "chat:edit"],
            "csrf_state_456",
            forceVerify: true,
            authorizeEndpoint: $"{this._fixture.BaseUrl}/oauth2/authorize");

        _ = result.GetLeftPart(UriPartial.Path).Should().Be($"{this._fixture.BaseUrl}/oauth2/authorize");

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(result.Query);
        _ = query["response_type"].ToString().Should().Be("code");
        _ = query["client_id"].ToString().Should().Be("my_client_id");
        _ = query["redirect_uri"].ToString().Should().Be("http://localhost:3000/auth/callback");
        _ = query["scope"].ToString().Should().Be("chat:read chat:edit");
        _ = query["state"].ToString().Should().Be("csrf_state_456");
        _ = query["force_verify"].ToString().Should().Be("true");
        _ = query.Should().NotContainKey("code_challenge");
        _ = query.Should().NotContainKey("code_challenge_method");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> defaults <c>code_challenge_method</c> to <c>S256</c> when <c>codeChallenge</c> is provided.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildAuthorizationUri")]
    public void BuildAuthorizationUri_WhenCodeChallengeProvidedWithoutMethod_DefaultsMethodToS256()
    {
        string verifier = TwitchOAuthClient.GenerateCodeVerifier();
        string challenge = TwitchOAuthClient.ComputeCodeChallenge(verifier);

        Uri result = TwitchOAuthClient.BuildAuthorizationUri(
            "my_client_id",
            new("http://localhost:3000/auth/callback"),
            ["chat:read"],
            "csrf_state_pkce",
            codeChallenge: challenge);

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(result.Query);
        _ = query["code_challenge"].ToString().Should().Be(challenge);
        _ = query["code_challenge_method"].ToString().Should().Be("S256");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> includes <c>code_challenge</c> and <c>code_challenge_method=S256</c> when explicitly specified.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildAuthorizationUri")]
    public void BuildAuthorizationUri_WhenCodeChallengeAndS256Provided_IncludesPkceParameters()
    {
        const string challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Uri result = TwitchOAuthClient.BuildAuthorizationUri(
            "my_client_id",
            new("http://localhost:3000/auth/callback"),
            null,
            "csrf_state_pkce",
            codeChallenge: challenge,
            codeChallengeMethod: "S256");

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(result.Query);
        _ = query["code_challenge"].ToString().Should().Be(challenge);
        _ = query["code_challenge_method"].ToString().Should().Be("S256");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> throws <see cref="ArgumentException"/> for unsupported code challenge methods.
    /// </summary>
    /// <param name="unsupportedMethod">The unsupported PKCE code challenge method.</param>
    [Theory]
    [Trait("Member", "BuildAuthorizationUri")]
    [InlineData("plain")]
    [InlineData("s256")]
    [InlineData("SHA256")]
    public void BuildAuthorizationUri_WhenUnsupportedCodeChallengeMethodProvided_ThrowsArgumentException(string unsupportedMethod)
    {
        Action action = () => _ = TwitchOAuthClient.BuildAuthorizationUri(
            "my_client_id",
            new("http://localhost:3000/auth/callback"),
            null,
            "csrf_state_pkce",
            codeChallenge: "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            codeChallengeMethod: unsupportedMethod);

        _ = action.Should().Throw<ArgumentException>()
            .WithParameterName("codeChallengeMethod");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildAuthorizationUri"/> throws <see cref="ArgumentException"/> when <c>codeChallengeMethod</c> is provided without <c>codeChallenge</c>.
    /// </summary>
    /// <param name="codeChallenge">The missing or whitespace code challenge.</param>
    /// <param name="codeChallengeMethod">The code challenge method.</param>
    [Theory]
    [Trait("Member", "BuildAuthorizationUri")]
    [InlineData(null, "S256")]
    [InlineData("", "S256")]
    [InlineData("   ", "S256")]
    [InlineData(null, "plain")]
    public void BuildAuthorizationUri_WhenCodeChallengeMethodSetWithoutCodeChallenge_ThrowsArgumentException(string? codeChallenge, string codeChallengeMethod)
    {
        Action action = () => _ = TwitchOAuthClient.BuildAuthorizationUri(
            "my_client_id",
            new("http://localhost:3000/auth/callback"),
            null,
            "csrf_state_pkce",
            codeChallenge: codeChallenge,
            codeChallengeMethod: codeChallengeMethod);

        _ = action.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(codeChallengeMethod));
    }

    #endregion BuildAuthorizationUri

    #region BuildOidcAuthorizationUri

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildOidcAuthorizationUri"/> throws <see cref="ArgumentNullException"/> when <c>nonce</c> is null or whitespace.
    /// </summary>
    /// <param name="invalidNonce">The invalid nonce value.</param>
    [Theory]
    [Trait("Member", "BuildOidcAuthorizationUri")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildOidcAuthorizationUri_WhenNonceIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidNonce)
    {
        Action action = () => _ = TwitchOAuthClient.BuildOidcAuthorizationUri(
            "my_client_id",
            new("http://localhost:3000/callback"),
            null,
            "state123",
            invalidNonce!);

        _ = action.Should().Throw<ArgumentNullException>()
            .WithParameterName("nonce");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildOidcAuthorizationUri"/> automatically injects <c>openid</c> and includes <c>nonce</c>.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildOidcAuthorizationUri")]
    public void BuildOidcAuthorizationUri_WhenOpenIdScopeMissing_AddsOpenIdAndNonceAndOmitsClaims()
    {
        Uri result = TwitchOAuthClient.BuildOidcAuthorizationUri(
            "oidc_client_id",
            new("http://localhost:3000/oidc"),
            ["user:read:email"],
            "oidc_state",
            "oidc_nonce_789");

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(result.Query);
        _ = query["response_type"].ToString().Should().Be("code");
        _ = query["client_id"].ToString().Should().Be("oidc_client_id");
        _ = query["redirect_uri"].ToString().Should().Be("http://localhost:3000/oidc");
        _ = query["scope"].ToString().Should().Be("openid user:read:email");
        _ = query["state"].ToString().Should().Be("oidc_state");
        _ = query["nonce"].ToString().Should().Be("oidc_nonce_789");
        _ = query.Should().NotContainKey("claims");
        _ = query.Should().NotContainKey("force_verify");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildOidcAuthorizationUri"/> does not duplicate <c>openid</c> and includes <c>claims</c> when provided.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildOidcAuthorizationUri")]
    public void BuildOidcAuthorizationUri_WhenOpenIdPresentAndClaimsProvided_DoesNotDuplicateOpenIdAndIncludesClaims()
    {
        const string claimsJson = "{\"id_token\":{\"email\":null,\"email_verified\":null}}";
        Uri result = TwitchOAuthClient.BuildOidcAuthorizationUri(
            "oidc_client_id",
            new("http://localhost:3000/oidc"),
            ["openid", "user:read:email"],
            "oidc_state",
            "oidc_nonce_abc",
            forceVerify: true,
            claimsJson: claimsJson);

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(result.Query);
        _ = query["scope"].ToString().Should().Be("openid user:read:email");
        _ = query["nonce"].ToString().Should().Be("oidc_nonce_abc");
        _ = query["force_verify"].ToString().Should().Be("true");
        _ = query["claims"].ToString().Should().Be(claimsJson);
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.BuildOidcAuthorizationUri"/> includes PKCE parameters and defaults <c>code_challenge_method</c> to <c>S256</c>.
    /// </summary>
    [Fact]
    [Trait("Member", "BuildOidcAuthorizationUri")]
    public void BuildOidcAuthorizationUri_WhenCodeChallengeProvided_IncludesPkceParametersAndDefaultsMethodToS256()
    {
        const string challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
        Uri result = TwitchOAuthClient.BuildOidcAuthorizationUri(
            "oidc_client_id",
            new("http://localhost:3000/oidc"),
            ["user:read:email"],
            "oidc_state",
            "oidc_nonce",
            codeChallenge: challenge);

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(result.Query);
        _ = query["scope"].ToString().Should().Be("openid user:read:email");
        _ = query["nonce"].ToString().Should().Be("oidc_nonce");
        _ = query["code_challenge"].ToString().Should().Be(challenge);
        _ = query["code_challenge_method"].ToString().Should().Be("S256");
    }

    #endregion BuildOidcAuthorizationUri

    #region ExchangeAuthorizationCodeAsync

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ExchangeAuthorizationCodeAsync"/> sends custom session credentials and parses the returned token.
    /// </summary>
    [Fact]
    [Trait("Member", "ExchangeAuthorizationCodeAsync")]
    public async Task ExchangeAuthorizationCodeAsync_WhenSessionHasCustomCredentials_PostsCustomCredentialsAndReturnsToken()
    {
        string expectedBearer = TestTokenFactory.CreateToken("BEARER");
        string expectedRefresh = TestTokenFactory.CreateToken("REFRESH");
        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = expectedBearer,
            expires_in = 14400,
            refresh_token = expectedRefresh,
            scope = _authorizationScopes,
            token_type = "bearer"
        });

        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "custom_exchange_client",
            CustomClientSecret = "custom_exchange_secret"
        };

        Token? token = await TwitchOAuthClient.ExchangeAuthorizationCodeAsync(
            "auth_code_123",
            new("http://localhost:3000/callback"),
            session,
            $"{this._fixture.BaseUrl}/oauth2/token").ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(expectedBearer);
        _ = token.RefreshToken.Should().Be(expectedRefresh);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Headers["Client-Id"].ToString().Should().Be("custom_exchange_client");
        _ = request.Body.Should().Contain("grant_type=authorization_code");
        _ = request.Body.Should().Contain("code=auth_code_123");
        _ = request.Body.Should().Contain("client_id=custom_exchange_client");
        _ = request.Body.Should().Contain("client_secret=custom_exchange_secret");
        _ = request.Body.Should().NotContain("code_verifier=");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ExchangeAuthorizationCodeAsync"/> omits <c>client_secret</c> and sends <c>code_verifier</c> for public PKCE clients.
    /// </summary>
    /// <param name="publicSecret">The null or whitespace client secret.</param>
    [Theory]
    [Trait("Member", "ExchangeAuthorizationCodeAsync")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExchangeAuthorizationCodeAsync_WhenPublicPkceClientWithoutSecret_OmitsClientSecretAndPostsCodeVerifier(string? publicSecret)
    {
        string expectedBearer = TestTokenFactory.CreateToken("BEARER");
        string verifier = TwitchOAuthClient.GenerateCodeVerifier();
        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = expectedBearer,
            expires_in = 14400,
            refresh_token = TestTokenFactory.CreateToken("REFRESH"),
            scope = _authorizationScopes,
            token_type = "bearer"
        });

        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "public_pkce_client",
            CustomClientSecret = publicSecret
        };

        Token? token = await TwitchOAuthClient.ExchangeAuthorizationCodeAsync(
            "auth_code_pkce",
            new("http://localhost:3000/callback"),
            session,
            $"{this._fixture.BaseUrl}/oauth2/token",
            verifier).ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(expectedBearer);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("grant_type=authorization_code");
        _ = request.Body.Should().Contain("client_id=public_pkce_client");
        _ = request.Body.Should().Contain($"code_verifier={Uri.EscapeDataString(verifier)}");
        _ = request.Body.Should().NotContain("client_secret");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ExchangeAuthorizationCodeAsync"/> sends both <c>client_secret</c> and <c>code_verifier</c> for confidential PKCE clients.
    /// </summary>
    [Fact]
    [Trait("Member", "ExchangeAuthorizationCodeAsync")]
    public async Task ExchangeAuthorizationCodeAsync_WhenConfidentialPkceClientWithSecret_PostsClientSecretAndCodeVerifier()
    {
        string expectedBearer = TestTokenFactory.CreateToken("BEARER");
        string verifier = TwitchOAuthClient.GenerateCodeVerifier();
        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = expectedBearer,
            expires_in = 14400,
            refresh_token = TestTokenFactory.CreateToken("REFRESH"),
            scope = _authorizationScopes,
            token_type = "bearer"
        });

        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "confidential_pkce_client",
            CustomClientSecret = "confidential_pkce_secret"
        };

        Token? token = await TwitchOAuthClient.ExchangeAuthorizationCodeAsync(
            "auth_code_pkce",
            new("http://localhost:3000/callback"),
            session,
            $"{this._fixture.BaseUrl}/oauth2/token",
            verifier).ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(expectedBearer);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("client_id=confidential_pkce_client");
        _ = request.Body.Should().Contain("client_secret=confidential_pkce_secret");
        _ = request.Body.Should().Contain($"code_verifier={Uri.EscapeDataString(verifier)}");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ExchangeAuthorizationCodeAsync"/> throws <see cref="ArgumentException"/> when <c>codeVerifier</c> violates RFC 7636 rules.
    /// </summary>
    /// <param name="invalidVerifier">The invalid code verifier.</param>
    [Theory]
    [Trait("Member", "ExchangeAuthorizationCodeAsync")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short_verifier")]
    [InlineData("invalid$character_in_verifier_0123456789abcdef")]
    public async Task ExchangeAuthorizationCodeAsync_WhenCodeVerifierInvalid_ThrowsArgumentException(string invalidVerifier)
    {
        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "public_pkce_client"
        };

        Func<Task> action = async () => await TwitchOAuthClient.ExchangeAuthorizationCodeAsync(
            "auth_code_pkce",
            new("http://localhost:3000/callback"),
            session,
            $"{this._fixture.BaseUrl}/oauth2/token",
            invalidVerifier).ConfigureAwait(true);

        _ = (await action.Should().ThrowAsync<ArgumentException>().ConfigureAwait(true))
            .WithParameterName("codeVerifier");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ExchangeAuthorizationCodeAsync"/> throws <see cref="InvalidOperationException"/> when <c>codeVerifier</c> is omitted and no client secret is configured.
    /// </summary>
    [Fact]
    [Trait("Member", "ExchangeAuthorizationCodeAsync")]
    public async Task ExchangeAuthorizationCodeAsync_WhenCodeVerifierOmittedAndSecretMissing_ThrowsInvalidOperationException()
    {
        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "confidential_without_secret",
            CustomClientSecret = null
        };

        Func<Task> action = async () => await TwitchOAuthClient.ExchangeAuthorizationCodeAsync(
            "auth_code",
            new("http://localhost:3000/callback"),
            session,
            $"{this._fixture.BaseUrl}/oauth2/token").ConfigureAwait(true);

        _ = await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    #endregion ExchangeAuthorizationCodeAsync

    #region RequestAppTokenAsync

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.RequestAppTokenAsync"/> posts client credentials from the custom session and returns the app token.
    /// </summary>
    [Fact]
    [Trait("Member", "RequestAppTokenAsync")]
    public async Task RequestAppTokenAsync_WhenSessionHasCustomCredentials_PostsCustomCredentialsAndReturnsAppToken()
    {
        string expectedBearer = TestTokenFactory.CreateToken("APP");
        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = expectedBearer,
            expires_in = 5000000,
            token_type = "bearer"
        });

        using TwitchSession session = new()
        {
            Token = TwitchToken.Empty,
            CustomClientId = "custom_app_client",
            CustomClientSecret = "custom_app_secret"
        };

        Token? token = await TwitchOAuthClient.RequestAppTokenAsync(session, $"{this._fixture.BaseUrl}/oauth2/token").ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(expectedBearer);
        _ = token.ExpiresIn.Should().Be(5000000);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Headers["Client-Id"].ToString().Should().Be("custom_app_client");
        _ = request.Body.Should().Contain("grant_type=client_credentials");
        _ = request.Body.Should().Contain("client_id=custom_app_client");
        _ = request.Body.Should().Contain("client_secret=custom_app_secret");
    }

    #endregion RequestAppTokenAsync

    #region RefreshTokenAsync

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.RefreshTokenAsync"/> posts the refresh token with custom credentials and returns the refreshed token.
    /// </summary>
    [Fact]
    [Trait("Member", "RefreshTokenAsync")]
    public async Task RefreshTokenAsync_WhenValidUserSession_PostsRefreshTokenAndReturnsNewToken()
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

        using TwitchSession session = new()
        {
            CustomClientId = "custom_refresh_client",
            CustomClientSecret = "custom_refresh_secret",
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                OAuth = TestTokenFactory.CreateToken("OLD"),
                Refresh = "original_refresh_token"
            }
        };

        Token? token = await TwitchOAuthClient.RefreshTokenAsync(session, $"{this._fixture.BaseUrl}/oauth2/token").ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(newBearer);
        _ = token.RefreshToken.Should().Be(newRefresh);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Headers["Client-Id"].ToString().Should().Be("custom_refresh_client");
        _ = request.Body.Should().Contain("grant_type=refresh_token");
        _ = request.Body.Should().Contain("refresh_token=original_refresh_token");
        _ = request.Body.Should().Contain("client_id=custom_refresh_client");
        _ = request.Body.Should().Contain("client_secret=custom_refresh_secret");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.RefreshTokenAsync"/> omits <c>client_secret</c> from the form body for public clients when no secret is configured.
    /// </summary>
    /// <param name="publicSecret">The null or whitespace client secret.</param>
    [Theory]
    [Trait("Member", "RefreshTokenAsync")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshTokenAsync_WhenPublicClientHasNoSecret_OmitsClientSecret(string? publicSecret)
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

        using TwitchSession session = new()
        {
            CustomClientId = "public_refresh_client",
            CustomClientSecret = publicSecret,
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.User,
                OAuth = TestTokenFactory.CreateToken("OLD"),
                Refresh = "public_refresh_token"
            }
        };

        Token? token = await TwitchOAuthClient.RefreshTokenAsync(session, $"{this._fixture.BaseUrl}/oauth2/token").ConfigureAwait(true);

        _ = token.Should().NotBeNull();
        _ = token.AccessToken.Should().Be(newBearer);
        _ = token.RefreshToken.Should().Be(newRefresh);

        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Body.Should().Contain("grant_type=refresh_token");
        _ = request.Body.Should().Contain("refresh_token=public_refresh_token");
        _ = request.Body.Should().Contain("client_id=public_refresh_client");
        _ = request.Body.Should().NotContain("client_secret");
    }

    #endregion RefreshTokenAsync

    #region ValidateTokenAsync

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ValidateTokenAsync"/> skips automatic token refresh on 401 when <c>dontRefresh</c> is <see langword="true"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "ValidateTokenAsync")]
    public async Task ValidateTokenAsync_WhenDontRefreshIsTrueAndServerReturns401_SkipsRefreshAndReturnsUnauthorized()
    {
        this._fixture.RegisterJsonResponse("GET", "/oauth2/validate", new
        {
            status = 401,
            message = "invalid access token"
        }, StatusCodes.Status401Unauthorized);

        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = TestTokenFactory.CreateToken("REFRESHED"),
            expires_in = 14000,
            refresh_token = TestTokenFactory.CreateToken("REFRESH"),
            scope = _refreshScopes,
            token_type = "bearer"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession();
        string originalToken = session.Token!.OAuth!;

        Validate? result = await TwitchOAuthClient.ValidateTokenAsync(session, $"{this._fixture.BaseUrl}/oauth2/validate", dontRefresh: true).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result!.Status.Should().Be(HttpStatusCode.Unauthorized);
        _ = result.Message.Should().Be("invalid access token");
        _ = session.Token.OAuth.Should().Be(originalToken);
        _ = this._fixture.RecordedRequests.Should().ContainSingle();
        _ = this._fixture.RecordedRequests[0].Path.Should().Be("/oauth2/validate");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ValidateTokenAsync"/> automatically refreshes and retries when <c>dontRefresh</c> is <see langword="false"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "ValidateTokenAsync")]
    public async Task ValidateTokenAsync_WhenDontRefreshIsFalseAndServerReturns401_RefreshesTokenAndRetries()
    {
        string refreshedAccess = TestTokenFactory.CreateToken("REFRESHED");
        string refreshedRefresh = TestTokenFactory.CreateToken("NEW_REFRESH");

        this._fixture.RegisterHandler("GET", "/oauth2/validate", async context =>
        {
            string authorization = context.Request.Headers.Authorization.ToString();
            if (string.Equals(authorization, $"Bearer {refreshedAccess}", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    client_id = this._fixture.ClientId,
                    login = "twitchdev",
                    user_id = "141981764",
                    expires_in = 14000
                })).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"status\":401,\"message\":\"invalid access token\"}").ConfigureAwait(false);
        });

        this._fixture.RegisterJsonResponse("POST", "/oauth2/token", new
        {
            access_token = refreshedAccess,
            expires_in = 14000,
            refresh_token = refreshedRefresh,
            scope = _refreshScopes,
            token_type = "bearer"
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.ChatRead);

        Validate? result = await TwitchOAuthClient.ValidateTokenAsync(session, $"{this._fixture.BaseUrl}/oauth2/validate", dontRefresh: false).ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result!.Status.Should().Be(HttpStatusCode.OK);
        _ = result.Login.Should().Be("twitchdev");
        _ = session.Token!.OAuth.Should().Be(refreshedAccess);
        _ = session.Token.Refresh.Should().Be(refreshedRefresh);
    }

    #endregion ValidateTokenAsync

    #region RevokeTokenAsync

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.RevokeTokenAsync"/> throws <see cref="ArgumentNullException"/> when <c>session</c> is <see langword="null"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "RevokeTokenAsync")]
    public async Task RevokeTokenAsync_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await TwitchOAuthClient.RevokeTokenAsync(null!, $"{this._fixture.BaseUrl}/oauth2/revoke").ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("session");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.RevokeTokenAsync"/> throws <see cref="ArgumentNullException"/> when <c>revokeEndpoint</c> is null or whitespace.
    /// </summary>
    /// <param name="invalidEndpoint">The invalid revoke endpoint.</param>
    [Theory]
    [Trait("Member", "RevokeTokenAsync")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_WhenEndpointIsNullOrWhitespace_ThrowsArgumentNullException(string? invalidEndpoint)
    {
        using TwitchSession session = TestTokenFactory.CreateUserSession();
        Func<Task> action = async () => await TwitchOAuthClient.RevokeTokenAsync(session, invalidEndpoint!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("revokeEndpoint");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.RevokeTokenAsync"/> throws <see cref="TokenTypeException"/> when the session token is neither a User nor App token.
    /// </summary>
    [Fact]
    [Trait("Member", "RevokeTokenAsync")]
    public async Task RevokeTokenAsync_WhenTokenTypeIsJwt_ThrowsTokenTypeException()
    {
        using TwitchSession session = new()
        {
            Token = new TwitchToken
            {
                Type = TwitchToken.TokenType.Jwt,
                OAuth = TestTokenFactory.CreateToken("JWT")
            }
        };

        Func<Task> action = async () => await TwitchOAuthClient.RevokeTokenAsync(session, $"{this._fixture.BaseUrl}/oauth2/revoke").ConfigureAwait(true);
        _ = await action.Should().ThrowAsync<TokenTypeException>().ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.RevokeTokenAsync"/> supports both User and App tokens and posts custom client IDs when configured.
    /// </summary>
    /// <param name="tokenType">The token type to revoke.</param>
    /// <param name="customClientId">Optional custom client ID on the session.</param>
    [Theory]
    [Trait("Member", "RevokeTokenAsync")]
    [InlineData(TwitchToken.TokenType.User, null)]
    [InlineData(TwitchToken.TokenType.App, null)]
    [InlineData(TwitchToken.TokenType.App, "custom_revoke_client")]
    public async Task RevokeTokenAsync_WhenUserOrAppToken_PostsClientIdAndTokenAndReturnsOk(TwitchToken.TokenType tokenType, string? customClientId)
    {
        this._fixture.RegisterJsonResponse("POST", "/oauth2/revoke", new
        {
            status = 200,
            message = ""
        });

        string oauthToken = TestTokenFactory.CreateToken(tokenType.ToString().ToUpperInvariant());
        using TwitchSession session = new()
        {
            CustomClientId = customClientId,
            Token = new TwitchToken
            {
                Type = tokenType,
                OAuth = oauthToken,
                Refresh = tokenType == TwitchToken.TokenType.User ? TestTokenFactory.CreateToken("REFRESH") : null
            }
        };

        JsonApiResponse? result = await TwitchOAuthClient.RevokeTokenAsync(session, $"{this._fixture.BaseUrl}/oauth2/revoke").ConfigureAwait(true);

        _ = result.Should().NotBeNull();
        _ = result.Status.Should().Be(HttpStatusCode.OK);

        string expectedClientId = customClientId ?? this._fixture.ClientId;
        RecordedRequest? request = this._fixture.LastRequest;
        _ = request.Should().NotBeNull();
        _ = request.Method.Should().Be("POST");
        _ = request.Headers["Client-Id"].ToString().Should().Be(expectedClientId);
        _ = request.Body.Should().Contain($"client_id={expectedClientId}");
        _ = request.Body.Should().Contain($"token={oauthToken}");
    }

    #endregion RevokeTokenAsync

    #region ValidateIdTokenAsync

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ValidateIdTokenAsync"/> throws <see cref="ArgumentNullException"/> when <c>token</c> is <see langword="null"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "ValidateIdTokenAsync")]
    public async Task ValidateIdTokenAsync_WhenTokenIsNull_ThrowsArgumentNullException()
    {
        Func<Task> action = async () => await TwitchOAuthClient.ValidateIdTokenAsync(null!).ConfigureAwait(true);
        _ = (await action.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true))
            .WithParameterName("token");
    }

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.ValidateIdTokenAsync"/> validates audience and nonce claims on an OIDC ID token.
    /// </summary>
    [Fact]
    [Trait("Member", "ValidateIdTokenAsync")]
    public async Task ValidateIdTokenAsync_WhenAudienceAndNonceValidated_ReturnsJwtOnlyOnMatch()
    {
        using RSA rsa = RSA.Create(2048);
        const string customAudience = "custom_oidc_client";
        const string expectedNonce = "nonce_match_value";
        string signedIdToken = this.CreateSignedJwtWithNonce(rsa, customAudience, expectedNonce);
        Token token = new() { IdToken = signedIdToken };

        JsonWebToken? validJwt = await TwitchOAuthClient.ValidateIdTokenAsync(token, customAudience, expectedNonce, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        JsonWebToken? wrongNonceJwt = await TwitchOAuthClient.ValidateIdTokenAsync(token, customAudience, "wrong_nonce", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        JsonWebToken? wrongAudienceJwt = await TwitchOAuthClient.ValidateIdTokenAsync(token, "wrong_audience", expectedNonce, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = validJwt.Should().NotBeNull();
        _ = validJwt.Subject.Should().Be("141981764");
        _ = validJwt.TryGetClaim("nonce", out Claim? nonceClaim).Should().BeTrue();
        _ = nonceClaim.Value.Should().Be(expectedNonce);
        _ = wrongNonceJwt.Should().BeNull();
        _ = wrongAudienceJwt.Should().BeNull();
    }

    #endregion ValidateIdTokenAsync

    #region GetUserInfoAsync

    /// <summary>
    /// Tests that <see cref="TwitchOAuthClient.GetUserInfoAsync"/> requests the UserInfo endpoint and returns parsed claims.
    /// </summary>
    [Fact]
    [Trait("Member", "GetUserInfoAsync")]
    public async Task GetUserInfoAsync_WhenSessionHasOpenIdScope_ReturnsUserInfo()
    {
        this._fixture.RegisterJsonResponse("GET", "/oauth2/userinfo", new
        {
            aud = this._fixture.ClientId,
            azp = this._fixture.ClientId,
            sub = "141981764",
            preferred_username = "twitchdev",
            email = "twitchdev@example.com",
            email_verified = true
        });

        using TwitchSession session = TestTokenFactory.CreateUserSession(Scope.OpenID);

        UserInfo? userInfo = await TwitchOAuthClient.GetUserInfoAsync(session, $"{this._fixture.BaseUrl}/oauth2/userinfo").ConfigureAwait(true);

        _ = userInfo.Should().NotBeNull();
        _ = userInfo.Subject.Should().Be("141981764");
        _ = userInfo.PreferredUsername.Should().Be("twitchdev");
        _ = userInfo.Email.Should().Be("twitchdev@example.com");
        _ = userInfo.EmailVerified.Should().BeTrue();
    }

    #endregion GetUserInfoAsync

    #region Private Methods

    /// <summary>
    /// Registers a temporary JWKS endpoint on the fixture and creates a signed OIDC ID token with the specified <c>nonce</c> claim.
    /// </summary>
    /// <param name="rsa">The RSA key pair used to sign the token.</param>
    /// <param name="audience">The expected JWT audience.</param>
    /// <param name="nonce">The <c>nonce</c> claim value.</param>
    /// <returns>A serialized, signed JWT string.</returns>
    private string CreateSignedJwtWithNonce(RSA rsa, string audience, string nonce)
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
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = this._fixture.BaseUrl,
            Audience = audience,
            Expires = DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                { "sub", "141981764" },
                { "preferred_username", "twitchdev" },
                { "nonce", nonce }
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    #endregion Private Methods
}
