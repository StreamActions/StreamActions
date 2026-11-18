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
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Web.Services;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace StreamActions.Web.Tests;

public sealed class ExternalOidcClientTests
{
    #region BuildAuthorizationRequestAsync

    [Fact]
    [Trait("Member", "BuildAuthorizationRequestAsync")]
    public async Task BuildAuthorizationRequestAsync_DiscoversEndpointsAndIncludesPkceS256Challenge()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockOidcServer server = await MockOidcServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            using HttpClient httpClient = new();
            ExternalOidcClient client = new(httpClient);
            ExternalOidcProvider provider = new()
            {
                Id = Guid.NewGuid(),
                Slug = "authentik",
                DisplayName = "Authentik",
                AuthorityUrl = server.BaseUrl,
                ClientId = "streamactions-web",
                ScopesJson = "[\"profile\",\"email\"]",
                IsEnabled = true,
            };

            Uri redirectUri = new("https://panel.example.com/auth/oidc/callback");
            ExternalOidcAuthorizationRequest req = await client.BuildAuthorizationRequestAsync(provider, redirectUri, cancellationToken: ct);

            _ = req.State.Should().NotBeNullOrWhiteSpace();
            _ = req.Nonce.Should().NotBeNullOrWhiteSpace();
            _ = req.CodeVerifier.Should().NotBeNullOrWhiteSpace();
            _ = req.AuthorizationUri.GetLeftPart(UriPartial.Path).Should().Be($"{server.BaseUrl}/authorize");

            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(req.AuthorizationUri.Query);
            _ = query["response_type"].ToString().Should().Be("code");
            _ = query["client_id"].ToString().Should().Be("streamactions-web");
            _ = query["redirect_uri"].ToString().Should().Be(redirectUri.ToString());
            _ = query["scope"].ToString().Should().Be("openid profile email");
            _ = query["state"].ToString().Should().Be(req.State);
            _ = query["nonce"].ToString().Should().Be(req.Nonce);
            _ = query["code_challenge_method"].ToString().Should().Be("S256");

            string expectedChallenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(req.CodeVerifier)));
            _ = query["code_challenge"].ToString().Should().Be(expectedChallenge);
            _ = query.ContainsKey("prompt").Should().BeFalse();

            ExternalOidcAuthorizationRequest stepUpReq = await client.BuildAuthorizationRequestAsync(
                provider,
                redirectUri,
                prompt: "login",
                cancellationToken: ct);
            Dictionary<string, StringValues> stepUpQuery = QueryHelpers.ParseQuery(stepUpReq.AuthorizationUri.Query);
            _ = stepUpQuery["prompt"].ToString().Should().Be("login");
        }
    }

    [Fact]
    [Trait("Member", "BuildAuthorizationRequestAsync")]
    public async Task BuildAuthorizationRequestAsync_WhenNonLoopbackHttpAuthority_ThrowsInvalidOperationException()
    {
        using HttpClient httpClient = new();
        ExternalOidcClient client = new(httpClient);
        ExternalOidcProvider provider = new()
        {
            Id = Guid.NewGuid(),
            Slug = "insecure",
            DisplayName = "Insecure",
            AuthorityUrl = "http://sso.example.com",
            ClientId = "cid",
            ScopesJson = "[\"openid\"]",
            IsEnabled = true,
        };

        Func<Task> act = async () => _ = await client.BuildAuthorizationRequestAsync(
            provider,
            new Uri("https://panel.example.com/callback"),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static readonly string[] _groupsClaimValue = ["stream-admins", "moderators", "stream-admins"];

    #endregion

    #region ExchangeCodeAsync

    [Fact]
    [Trait("Member", "ExchangeCodeAsync")]
    public async Task ExchangeCodeAsync_ValidatesIdTokenAndExtractsArrayGroupClaims()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockOidcServer server = await MockOidcServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            using HttpClient httpClient = new();
            ExternalOidcClient client = new(httpClient);
            ExternalOidcProvider provider = new()
            {
                Id = Guid.NewGuid(),
                Slug = "authentik",
                DisplayName = "Authentik",
                AuthorityUrl = server.BaseUrl,
                ClientId = "streamactions-web",
                ScopesJson = "[\"openid\",\"profile\",\"groups\"]",
                GroupClaimType = "groups",
                IsEnabled = true,
            };

            Uri redirectUri = new("https://panel.example.com/auth/oidc/callback");
            ExternalOidcAuthorizationRequest authReq = await client.BuildAuthorizationRequestAsync(provider, redirectUri, cancellationToken: ct);

            server.IdTokenFactory = () => server.CreateSignedIdToken(
                audience: "streamactions-web",
                subject: "user-sub-001",
                preferredUsername: "jdoe",
                name: "Jane Doe",
                email: "jane@example.com",
                nonce: authReq.Nonce,
                groupsClaimValue: _groupsClaimValue);

            ExternalOidcAuthenticationResult result = await client.ExchangeCodeAsync(
                provider,
                "oidc-client-secret",
                "auth-code-123",
                redirectUri,
                authReq.CodeVerifier,
                authReq.Nonce,
                ct);

            _ = server.LastTokenForm["code_verifier"].Should().Be(authReq.CodeVerifier);
            _ = server.LastTokenForm["client_secret"].Should().Be("oidc-client-secret");
            _ = result.Subject.Should().Be("user-sub-001");
            _ = result.Username.Should().Be("jdoe");
            _ = result.DisplayName.Should().Be("Jane Doe");
            _ = result.Email.Should().Be("jane@example.com");
            _ = result.Groups.Should().BeEquivalentTo(["stream-admins", "moderators"]);
        }
    }

    [Fact]
    [Trait("Member", "ExchangeCodeAsync")]
    public async Task ExchangeCodeAsync_ExtractsSingleStringGroupClaim()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockOidcServer server = await MockOidcServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            using HttpClient httpClient = new();
            ExternalOidcClient client = new(httpClient);
            ExternalOidcProvider provider = new()
            {
                Id = Guid.NewGuid(),
                Slug = "keycloak",
                DisplayName = "Keycloak",
                AuthorityUrl = server.BaseUrl,
                ClientId = "streamactions-web",
                ScopesJson = "[\"openid\"]",
                GroupClaimType = "role",
                IsEnabled = true,
            };

            Uri redirectUri = new("https://panel.example.com/auth/oidc/callback");
            ExternalOidcAuthorizationRequest authReq = await client.BuildAuthorizationRequestAsync(provider, redirectUri, cancellationToken: ct);

            server.IdTokenFactory = () => server.CreateSignedIdToken(
                audience: "streamactions-web",
                subject: "user-sub-002",
                preferredUsername: null,
                name: "Single Role User",
                email: null,
                nonce: authReq.Nonce,
                groupClaimType: "role",
                groupsClaimValue: "stream-editors");

            ExternalOidcAuthenticationResult result = await client.ExchangeCodeAsync(
                provider,
                "oidc-client-secret",
                "auth-code-456",
                redirectUri,
                authReq.CodeVerifier,
                authReq.Nonce,
                ct);

            _ = result.Subject.Should().Be("user-sub-002");
            _ = result.Username.Should().Be("Single Role User");
            _ = result.DisplayName.Should().Be("Single Role User");
            _ = result.Groups.Should().ContainSingle().Which.Should().Be("stream-editors");
        }
    }

    [Fact]
    [Trait("Member", "ExchangeCodeAsync")]
    public async Task ExchangeCodeAsync_WhenNonceMismatches_ThrowsSecurityTokenException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockOidcServer server = await MockOidcServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            using HttpClient httpClient = new();
            ExternalOidcClient client = new(httpClient);
            ExternalOidcProvider provider = new()
            {
                Id = Guid.NewGuid(),
                Slug = "authentik",
                DisplayName = "Authentik",
                AuthorityUrl = server.BaseUrl,
                ClientId = "streamactions-web",
                ScopesJson = "[\"openid\"]",
                IsEnabled = true,
            };

            server.IdTokenFactory = () => server.CreateSignedIdToken(
                audience: "streamactions-web",
                subject: "user-sub-003",
                preferredUsername: "alice",
                name: "Alice",
                email: null,
                nonce: "tampered-nonce");

            Func<Task> act = async () => _ = await client.ExchangeCodeAsync(
                provider,
                "oidc-client-secret",
                "code-1",
                new Uri("https://panel.example.com/callback"),
                "verifier-1",
                "expected-nonce",
                ct).ConfigureAwait(false);

            _ = await act.Should().ThrowAsync<SecurityTokenException>();
        }
    }

    #endregion

    private sealed class MockOidcServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly RSA _rsa;
        private readonly RsaSecurityKey _signingKey;

        private MockOidcServer(WebApplication app, string baseUrl, RSA rsa, RsaSecurityKey signingKey)
        {
            this._app = app;
            this.BaseUrl = baseUrl;
            this._rsa = rsa;
            this._signingKey = signingKey;
        }

        public string BaseUrl { get; }

        public Func<string>? IdTokenFactory { get; set; }

        public Dictionary<string, string> LastTokenForm { get; } = new(StringComparer.Ordinal);

        public static async Task<MockOidcServer> StartAsync(CancellationToken cancellationToken)
        {
            RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-oidc-key" };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
            WebApplication app = builder.Build();

            MockOidcServer? holder = null;

            _ = app.MapGet("/.well-known/openid-configuration", () =>
            {
                string baseUrl = holder!.BaseUrl;
                return Results.Json(new
                {
                    issuer = baseUrl,
                    authorization_endpoint = $"{baseUrl}/authorize",
                    token_endpoint = $"{baseUrl}/token",
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

            _ = app.MapPost("/token", async (HttpRequest request) =>
            {
                IFormCollection form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
                holder!.LastTokenForm.Clear();
                foreach (KeyValuePair<string, StringValues> entry in form)
                {
                    holder.LastTokenForm[entry.Key] = entry.Value.ToString();
                }

                string idToken = holder.IdTokenFactory!();
                return Results.Json(new
                {
                    access_token = "oidc-access-token",
                    token_type = "Bearer",
                    expires_in = 3600,
                    id_token = idToken,
                });
            });

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            string baseUrl = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .First()
                .TrimEnd('/');

            holder = new MockOidcServer(app, baseUrl, rsa, signingKey);
            return holder;
        }

        public string CreateSignedIdToken(
            string audience,
            string subject,
            string? preferredUsername,
            string? name,
            string? email,
            string nonce,
            string groupClaimType = "groups",
            object? groupsClaimValue = null)
        {
            Dictionary<string, object> claims = new()
            {
                { "sub", subject },
                { "nonce", nonce },
            };

            if (!string.IsNullOrWhiteSpace(preferredUsername))
            {
                claims["preferred_username"] = preferredUsername;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                claims["name"] = name;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                claims["email"] = email;
            }

            if (groupsClaimValue is not null)
            {
                claims[groupClaimType] = groupsClaimValue;
            }

            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = this.BaseUrl,
                Audience = audience,
                Claims = claims,
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
