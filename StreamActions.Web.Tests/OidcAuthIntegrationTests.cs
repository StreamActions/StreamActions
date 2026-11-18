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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Daemon;
using StreamActions.Web.Security;
using System.Net;
using System.Security.Cryptography;
using Xunit;

namespace StreamActions.Web.Tests;

public sealed class OidcAuthIntegrationTests
{
    private static readonly string[] _oidcGroupsClaim = ["stream-mods", "vip-team"];

    #region MapOidcEndpoints

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task ExternalOidcLogin_WithPkceAndMappedGroupClaim_SignsInAndGrantsWorkspaceAccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockOidcServer mockOidc = await MockOidcServer.StartAsync(ct);
        await using (mockOidc.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using OidcWebTestApplicationFactory factory = new(dirs);

            IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
            InstanceSettings settings = await settingsService.GetAsync(ct);
            settings.AllowOpenRegistration = false;
            settings.ExternalOidcLoginMode = ExternalOidcLoginMode.Allowed;
            await settingsService.UpdateAsync(settings, ct);

            IExternalOidcProviderStore oidcStore = factory.Services.GetRequiredService<IExternalOidcProviderStore>();
            _ = await oidcStore.UpsertAsync(
                new ExternalOidcProvider
                {
                    Slug = "authentik",
                    DisplayName = "Authentik",
                    AuthorityUrl = mockOidc.BaseUrl,
                    ClientId = "sa-oidc-client",
                    ScopesJson = "[\"openid\",\"profile\",\"groups\"]",
                    GroupClaimType = "groups",
                    IsEnabled = true,
                },
                plaintextClientSecret: "sa-oidc-secret",
                ct);

            IPanelUserStore userStore = factory.Services.GetRequiredService<IPanelUserStore>();
            PanelUser owner = await userStore.CreateUserAsync(
                new PanelUser { DisplayName = "Owner" },
                new UserLoginIdentity { ProviderId = "twitch", ProviderUserId = "owner-1", ProviderUsername = "owner" },
                ct);

            IWorkspaceStore wsStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            Workspace ws = await wsStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = owner.Id, Name = "OIDC Workspace", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "owner-1" },
                ct);

            IPermissionStore permStore = factory.Services.GetRequiredService<IPermissionStore>();
            PermissionGroup ssoGroup = await permStore.UpsertGroupAsync(new PermissionGroup
            {
                WorkspaceId = ws.Id,
                Name = "Authentik Mods",
                MappedOidcGroupClaim = "stream-mods",
            }, ct);
            await permStore.SetGrantAsync(ssoGroup.Id, BuiltInPermissionKeys.UiPanelAccess, PermissionState.Allow, ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/oidc/authentik/login", UriKind.Relative), ct);
            _ = loginRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = loginRedirect.Headers.Location!.GetLeftPart(UriPartial.Path).Should().Be($"{mockOidc.BaseUrl}/authorize");

            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(loginRedirect.Headers.Location.Query);
            _ = query["code_challenge_method"].ToString().Should().Be("S256");
            string state = query["state"].ToString();
            string nonce = query["nonce"].ToString();

            mockOidc.IdTokenFactory = () => mockOidc.CreateSignedIdToken(
                audience: "sa-oidc-client",
                subject: "oidc-sub-42",
                preferredUsername: "oidc_mod",
                name: "OIDC Moderator",
                email: "mod@example.com",
                nonce: nonce,
                groupsClaimValue: _oidcGroupsClaim);

            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/oidc/authentik/callback?code=oidc-code-1&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);

            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.Location!.ToString().Should().Be("/");
            _ = callbackResponse.Headers.GetValues("Set-Cookie")
                .Should().Contain(c => c.StartsWith($"{AuthClaims.AuthCookieName}=", StringComparison.Ordinal));
            _ = mockOidc.LastTokenForm["code_verifier"].Should().NotBeNullOrWhiteSpace();
            _ = mockOidc.LastTokenForm["client_secret"].Should().Be("sa-oidc-secret");

            PanelUser? oidcUser = await userStore.GetByLoginIdentityAsync("oidc:authentik", "oidc-sub-42", ct);
            _ = oidcUser.Should().NotBeNull();

            IReadOnlyList<UserLoginIdentity> identities = await userStore.GetLoginIdentitiesAsync(oidcUser!.Id, ct);
            using IServiceScope scope = factory.Services.CreateScope();
            IWorkspaceAccessService accessService = scope.ServiceProvider.GetRequiredService<IWorkspaceAccessService>();
            IReadOnlyList<AccessibleWorkspace> accessible = await accessService.GetAccessibleWorkspacesAsync(
                oidcUser,
                identities,
                _oidcGroupsClaim,
                authProvider: "oidc:authentik",
                ct: ct);
            _ = accessible.Should().ContainSingle().Which.Workspace.Id.Should().Be(ws.Id);

            HttpResponseMessage permissionsResponse = await client.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task ExternalOidcLogin_WhenModeDisabled_ReturnsForbidden()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockOidcServer mockOidc = await MockOidcServer.StartAsync(ct);
        await using (mockOidc.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using OidcWebTestApplicationFactory factory = new(dirs);

            IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
            InstanceSettings settings = await settingsService.GetAsync(ct);
            settings.ExternalOidcLoginMode = ExternalOidcLoginMode.Disabled;
            await settingsService.UpdateAsync(settings, ct);

            IExternalOidcProviderStore oidcStore = factory.Services.GetRequiredService<IExternalOidcProviderStore>();
            _ = await oidcStore.UpsertAsync(
                new ExternalOidcProvider
                {
                    Slug = "authentik",
                    DisplayName = "Authentik",
                    AuthorityUrl = mockOidc.BaseUrl,
                    ClientId = "sa-oidc-client",
                    ScopesJson = "[\"openid\"]",
                    IsEnabled = true,
                },
                plaintextClientSecret: "sa-oidc-secret",
                ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = false,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage response = await client.GetAsync(new Uri("/auth/oidc/authentik/login", UriKind.Relative), ct);
            _ = response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    #endregion

    private sealed class TempDaemonDirectories : IDisposable
    {
        public TempDaemonDirectories()
        {
            this.Root = Path.Combine(Path.GetTempPath(), $"sa-oidc-test-{Guid.NewGuid():N}");
            this.ConfigDir = Path.Combine(this.Root, "config");
            this.DataDir = Path.Combine(this.Root, "data");
            this.LogDir = Path.Combine(this.Root, "logs");
            this.PluginsDir = Path.Combine(this.Root, "plugins");
            this.BackupDir = Path.Combine(this.Root, "backups");
        }

        public string Root { get; }
        public string ConfigDir { get; }
        public string DataDir { get; }
        public string LogDir { get; }
        public string PluginsDir { get; }
        public string BackupDir { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Root))
            {
                try
                {
                    Directory.Delete(this.Root, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private sealed class OidcWebTestApplicationFactory(TempDaemonDirectories dirs) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _ = builder.UseEnvironment("Testing");
            _ = builder.UseSetting("NoWeb", "false");
            _ = builder.UseSetting("Bootstrap:Paths:ConfigDir", dirs.ConfigDir);
            _ = builder.UseSetting("Bootstrap:Paths:DataDir", dirs.DataDir);
            _ = builder.UseSetting("Bootstrap:Paths:LogDir", dirs.LogDir);
            _ = builder.UseSetting("Bootstrap:Paths:PluginsDir", dirs.PluginsDir);
            _ = builder.UseSetting("Bootstrap:Paths:BackupDir", dirs.BackupDir);
            _ = builder.UseSetting("Bootstrap:Node:InstanceId", $"oidc-test-{Guid.NewGuid():N}");
            _ = builder.UseSetting("Bootstrap:Sentry:Enabled", "false");
        }
    }

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
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-oidc-int-key" };

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
                claims["groups"] = groupsClaimValue;
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
