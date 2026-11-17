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

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Core.Models;
using StreamActions.Daemon.Platforms;
using System.Security.Claims;
using System.Security.Cryptography;
using Xunit;

namespace StreamActions.Daemon.Tests;

public sealed class TwitchPlatformOAuthProviderTests
{
    #region BuildAuthorizationUri

    [Fact]
    [Trait("Member", "BuildAuthorizationUri")]
    public void BuildAuthorizationUri_WithAndWithoutNonce_BuildsOAuthAndOidcUrls()
    {
        TwitchOAuthEndpointOptions options = new()
        {
            AuthorizeEndpoint = "http://127.0.0.1:5000/oauth2/authorize",
        };
        TwitchPlatformOAuthProvider provider = new(options);
        Uri redirectUri = new("https://panel.example.com/auth/twitch/callback");

        Uri oauthUri = provider.BuildAuthorizationUri("cid-1", redirectUri, ["chat:read", "chat:edit"], "state-1", forceVerify: true);
        Dictionary<string, StringValues> oauthQuery = QueryHelpers.ParseQuery(oauthUri.Query);
        _ = oauthQuery["client_id"].ToString().Should().Be("cid-1");
        _ = oauthQuery["scope"].ToString().Should().Be("chat:read chat:edit");
        _ = oauthQuery["force_verify"].ToString().Should().Be("true");
        _ = oauthQuery.Should().NotContainKey("nonce");

        Uri oidcUri = provider.BuildAuthorizationUri("cid-1", redirectUri, ["user:read:email"], "state-2", nonce: "nonce-1");
        Dictionary<string, StringValues> oidcQuery = QueryHelpers.ParseQuery(oidcUri.Query);
        _ = oidcQuery["nonce"].ToString().Should().Be("nonce-1");
        _ = oidcQuery["scope"].ToString().Should().Be("openid user:read:email");
    }

    #endregion

    #region ExchangeCodeAsync

    [Fact]
    [Trait("Member", "ExchangeCodeAsync")]
    public async Task ExchangeCodeAsync_WhenOidcIdTokenReturned_ExtractsIdentityFromValidatedIdToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer server = await MockTwitchOAuthServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            using HttpClient httpClient = new();
            FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
            TwitchPlatformOAuthProvider provider = new(server.EndpointOptions, httpClient, timeProvider);

            server.IdTokenFactory = baseUrl => server.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "twitch-cid",
                subject: "123456",
                preferredUsername: "oidc_streamer",
                nonce: "expected-nonce-1",
                email: "streamer@example.com");

            PlatformOAuthTokenResult result = await provider.ExchangeCodeAsync(
                "twitch-cid",
                "twitch-secret",
                "auth-code-oidc",
                new Uri("https://panel.example.com/callback"),
                "expected-nonce-1",
                ct);

            _ = result.AccessToken.Should().Be("mock-access-token");
            _ = result.RefreshToken.Should().Be("mock-refresh-token");
            _ = result.PlatformUserId.Should().Be("123456");
            _ = result.PlatformUsername.Should().Be("oidc_streamer");
            _ = result.DisplayName.Should().Be("oidc_streamer");
            _ = result.Email.Should().Be("streamer@example.com");
            _ = result.Scopes.Should().Contain(["openid", "chat:read"]);
            _ = result.ExpiresAtUtc.Should().Be(timeProvider.GetUtcNow().AddSeconds(3600));
            _ = server.ValidateCalls.Should().Be(0);
        }
    }

    [Fact]
    [Trait("Member", "ExchangeCodeAsync")]
    public async Task ExchangeCodeAsync_WhenPureOAuthWithoutIdToken_FallsBackToValidateTokenAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer server = await MockTwitchOAuthServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            server.IdTokenFactory = null;
            server.ValidatedUserId = "987654";
            server.ValidatedLogin = "pure_oauth_user";

            using HttpClient httpClient = new();
            TwitchPlatformOAuthProvider provider = new(server.EndpointOptions, httpClient);

            PlatformOAuthTokenResult result = await provider.ExchangeCodeAsync(
                "twitch-cid",
                "twitch-secret",
                "auth-code-oauth",
                new Uri("https://panel.example.com/callback"),
                expectedNonce: null,
                ct);

            _ = result.AccessToken.Should().Be("mock-access-token");
            _ = result.PlatformUserId.Should().Be("987654");
            _ = result.PlatformUsername.Should().Be("pure_oauth_user");
            _ = result.DisplayName.Should().Be("pure_oauth_user");
            _ = server.ValidateCalls.Should().Be(1);
        }
    }

    [Fact]
    [Trait("Member", "ExchangeCodeAsync")]
    public async Task ExchangeCodeAsync_WhenIdTokenNonceMismatches_ThrowsSecurityTokenException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer server = await MockTwitchOAuthServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            server.IdTokenFactory = baseUrl => server.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "twitch-cid",
                subject: "123456",
                preferredUsername: "oidc_streamer",
                nonce: "wrong-nonce");

            using HttpClient httpClient = new();
            TwitchPlatformOAuthProvider provider = new(server.EndpointOptions, httpClient);

            Func<Task> act = async () => _ = await provider.ExchangeCodeAsync(
                "twitch-cid",
                "twitch-secret",
                "auth-code-oidc",
                new Uri("https://panel.example.com/callback"),
                "expected-nonce",
                ct).ConfigureAwait(false);

            _ = await act.Should().ThrowAsync<SecurityTokenException>();
        }
    }

    #endregion

    #region RefreshAndValidateAndRevoke

    [Fact]
    [Trait("Member", "RefreshTokenAsync")]
    public async Task RefreshValidateAndRevokeAsync_RoundTripsAgainstMockEndpoints()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockTwitchOAuthServer server = await MockTwitchOAuthServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            using HttpClient httpClient = new();
            FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
            TwitchPlatformOAuthProvider provider = new(server.EndpointOptions, httpClient, timeProvider);

            PlatformOAuthTokenResult refreshed = await provider.RefreshTokenAsync("twitch-cid", "twitch-secret", "old-refresh", ct);
            _ = refreshed.AccessToken.Should().Be("refreshed-access-token");
            _ = refreshed.RefreshToken.Should().Be("new-refresh-token");

            PlatformTokenValidationResult valid = await provider.ValidateTokenAsync("refreshed-access-token", ct);
            _ = valid.IsValid.Should().BeTrue();
            _ = valid.PlatformUserId.Should().Be("987654");
            _ = valid.PlatformUsername.Should().Be("validated_user");

            await provider.RevokeTokenAsync("twitch-cid", "refreshed-access-token", ct);
            _ = server.RevokedTokens.Should().Contain("refreshed-access-token");
        }
    }

    #endregion

    private sealed class MockTwitchOAuthServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly RSA _rsa;
        private readonly RsaSecurityKey _signingKey;

        private MockTwitchOAuthServer(WebApplication app, string baseUrl, RSA rsa, RsaSecurityKey signingKey)
        {
            this._app = app;
            this.BaseUrl = baseUrl;
            this._rsa = rsa;
            this._signingKey = signingKey;
            this.EndpointOptions = new TwitchOAuthEndpointOptions
            {
                AuthorizeEndpoint = $"{baseUrl}/oauth2/authorize",
                TokenEndpoint = $"{baseUrl}/oauth2/token",
                ValidateEndpoint = $"{baseUrl}/oauth2/validate",
                RevokeEndpoint = $"{baseUrl}/oauth2/revoke",
                UserInfoEndpoint = $"{baseUrl}/oauth2/userinfo",
                OpenIdConfigurationEndpoint = $"{baseUrl}/.well-known/openid-configuration",
            };
        }

        public string BaseUrl { get; }

        public TwitchOAuthEndpointOptions EndpointOptions { get; }

        public Func<string, string>? IdTokenFactory { get; set; }

        public string ValidatedUserId { get; set; } = "987654";

        public string ValidatedLogin { get; set; } = "validated_user";

        public int ValidateCalls { get; private set; }

        public List<string> RevokedTokens { get; } = [];

        private static readonly string[] _chatReadScopes = ["chat:read"];

        public static async Task<MockTwitchOAuthServer> StartAsync(CancellationToken cancellationToken)
        {
            RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-twitch-key" };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
            WebApplication app = builder.Build();

            MockTwitchOAuthServer? holder = null;

            _ = app.MapGet("/.well-known/openid-configuration", () =>
            {
                string baseUrl = holder!.BaseUrl;
                return Results.Json(new
                {
                    issuer = baseUrl,
                    authorization_endpoint = $"{baseUrl}/oauth2/authorize",
                    token_endpoint = $"{baseUrl}/oauth2/token",
                    userinfo_endpoint = $"{baseUrl}/oauth2/userinfo",
                    jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                });
            });

            _ = app.MapGet("/.well-known/jwks.json", () =>
            {
                RSAParameters parameters = rsa.ExportParameters(false);
                return Results.Json(new
                {
                    keys = new[]
                    {
                        new
                        {
                            kty = "RSA",
                            use = "sig",
                            kid = signingKey.KeyId,
                            alg = "RS256",
                            n = Base64UrlEncoder.Encode(parameters.Modulus!),
                            e = Base64UrlEncoder.Encode(parameters.Exponent!),
                        },
                    },
                });
            });

            _ = app.MapPost("/oauth2/token", async (HttpRequest request) =>
            {
                IFormCollection form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
                string grantType = form["grant_type"].ToString();
                if (string.Equals(grantType, "refresh_token", StringComparison.Ordinal))
                {
                    return Results.Json(new
                    {
                        access_token = "refreshed-access-token",
                        refresh_token = "new-refresh-token",
                        expires_in = 1800,
                        scope = _chatReadScopes,
                        token_type = "bearer",
                    });
                }

                string? idToken = holder!.IdTokenFactory?.Invoke(holder.BaseUrl);
                string[] scopes = idToken is not null ? ["openid", "chat:read"] : ["chat:read"];
                return Results.Json(new
                {
                    access_token = "mock-access-token",
                    refresh_token = "mock-refresh-token",
                    expires_in = 3600,
                    scope = scopes,
                    id_token = idToken,
                    token_type = "bearer",
                });
            });

            _ = app.MapGet("/oauth2/validate", () =>
            {
                holder!.ValidateCalls++;
                return Results.Json(new
                {
                    client_id = "twitch-cid",
                    login = holder.ValidatedLogin,
                    scopes = _chatReadScopes,
                    user_id = holder.ValidatedUserId,
                    expires_in = 3500,
                });
            });

            _ = app.MapPost("/oauth2/revoke", async (HttpRequest request) =>
            {
                IFormCollection form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
                holder!.RevokedTokens.Add(form["token"].ToString());
                return Results.Json(new { status = 200 });
            });

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            string baseUrl = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .First()
                .TrimEnd('/');

            holder = new MockTwitchOAuthServer(app, baseUrl, rsa, signingKey);
            return holder;
        }

        public string CreateSignedIdToken(
            string issuer,
            string audience,
            string subject,
            string preferredUsername,
            string nonce,
            string? email = null)
        {
            List<Claim> claims =
            [
                new("sub", subject),
                new("preferred_username", preferredUsername),
                new("nonce", nonce),
            ];

            if (!string.IsNullOrWhiteSpace(email))
            {
                claims.Add(new Claim("email", email));
            }

            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = issuer,
                Audience = audience,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(this._signingKey, SecurityAlgorithms.RsaSha256),
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        public async ValueTask DisposeAsync()
        {
            await this._app.StopAsync().ConfigureAwait(false);
            await this._app.DisposeAsync().ConfigureAwait(false);
            this._rsa.Dispose();
        }
    }
}
