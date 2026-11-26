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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Daemon;
using StreamActions.Daemon.Platforms;
using StreamActions.Web.Security;
using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using XunitTestContext = Xunit.TestContext;

namespace StreamActions.Web.Tests;

public sealed partial class LoginAndWorkspaceTests
{
    private static readonly string[] _internalTransports = ["internal"];

    #region RequireStreamerOAuthApp4Tiers

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task RequireStreamerOAuthApp_FourTierResolution_CoversTier4ReuseCustomRedirect_Tier2LoginHint_AndTier3IdentityBinding()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        MockOAuthAndOidcServer mockServer = await MockOAuthAndOidcServer.StartAsync(ct);
        await using (mockServer.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using IntegrationTestFactory factory = new(dirs, mockServer.TwitchEndpointOptions, new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)));

            IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
            InstanceSettings settings = await settingsService.GetAsync(ct);
            settings.AllowOpenRegistration = true;
            settings.InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.RequireStreamerOAuthApp);
            settings.ExternalOidcLoginMode = ExternalOidcLoginMode.Disabled;
            await settingsService.UpdateAsync(settings, ct);

            // Tier 4: Streamer A submits customClientId, customClientSecret, and customRedirectUri.
            using HttpClient clientA = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            string loginHtmlA = await (await clientA.GetAsync(new Uri("/login", UriKind.Relative), ct)).Content.ReadAsStringAsync(ct);
            string antiforgeryA = ExtractAntiforgeryToken(loginHtmlA);
            const string customRedirectUri = "https://panel.example.com/auth/twitch/callback";

            using FormUrlEncodedContent tier4FormA = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryA),
                new KeyValuePair<string, string>("flow", "login"),
                new KeyValuePair<string, string>("customClientId", "shared-streamer-cid"),
                new KeyValuePair<string, string>("customClientSecret", "shared-streamer-secret"),
                new KeyValuePair<string, string>("customRedirectUri", customRedirectUri),
            ]);

            HttpResponseMessage startA = await clientA.PostAsync(new Uri("/auth/twitch/login", UriKind.Relative), tier4FormA, ct);
            _ = startA.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> queryA = QueryHelpers.ParseQuery(startA.Headers.Location!.Query);
            _ = queryA["client_id"].ToString().Should().Be("shared-streamer-cid");
            _ = queryA["redirect_uri"].ToString().Should().Be(customRedirectUri);

            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "shared-streamer-cid",
                subject: "streamer-a-id",
                preferredUsername: "streamer_a",
                nonce: queryA["nonce"].ToString());

            HttpResponseMessage callbackA = await clientA.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-a&state={Uri.EscapeDataString(queryA["state"].ToString())}", UriKind.Relative),
                ct);
            _ = callbackA.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackA.Headers.Location!.ToString().Should().Be("/");
            _ = callbackA.Headers.GetValues("Set-Cookie")
                .Should().Contain(c => c.StartsWith($"{AuthClaims.LoginHintCookieName}=", StringComparison.Ordinal));

            IPanelUserStore userStore = factory.Services.GetRequiredService<IPanelUserStore>();
            IWorkspaceStore wsStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            PanelUser streamerA = (await userStore.GetByLoginIdentityAsync("twitch", "streamer-a-id", ct))!;
            IReadOnlyList<Workspace> wsListA = await wsStore.GetByOwnerAsync(streamerA.Id, ct);
            _ = wsListA.Should().ContainSingle();
            Workspace wsA = wsListA[0];

            PlatformOAuthAppCredentials? savedCredsA = await wsStore.GetCustomOAuthAppAsync(wsA.Id, "twitch", ct);
            _ = savedCredsA.Should().NotBeNull();
            _ = savedCredsA!.ClientId.Should().Be("shared-streamer-cid");
            _ = savedCredsA.RedirectUri.Should().Be(new Uri(customRedirectUri));

            // Tier 4 Multi-Workspace ClientId Reuse: Streamer B registers a second workspace with the SAME ClientId.
            using HttpClient clientB = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            string loginHtmlB = await (await clientB.GetAsync(new Uri("/login", UriKind.Relative), ct)).Content.ReadAsStringAsync(ct);
            string antiforgeryB = ExtractAntiforgeryToken(loginHtmlB);

            using FormUrlEncodedContent tier4FormB = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryB),
                new KeyValuePair<string, string>("flow", "login"),
                new KeyValuePair<string, string>("customClientId", "shared-streamer-cid"),
                new KeyValuePair<string, string>("customClientSecret", "shared-streamer-secret"),
            ]);
            HttpResponseMessage startB = await clientB.PostAsync(new Uri("/auth/twitch/login", UriKind.Relative), tier4FormB, ct);
            _ = startB.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> queryB = QueryHelpers.ParseQuery(startB.Headers.Location!.Query);

            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "shared-streamer-cid",
                subject: "streamer-b-id",
                preferredUsername: "streamer_b",
                nonce: queryB["nonce"].ToString());

            HttpResponseMessage callbackB = await clientB.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-b&state={Uri.EscapeDataString(queryB["state"].ToString())}", UriKind.Relative),
                ct);
            _ = callbackB.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackB.Headers.Location!.ToString().Should().Be("/");

            PanelUser streamerB = (await userStore.GetByLoginIdentityAsync("twitch", "streamer-b-id", ct))!;
            IReadOnlyList<Workspace> wsListB = await wsStore.GetByOwnerAsync(streamerB.Id, ct);
            _ = wsListB.Should().ContainSingle();

            // Update Streamer B's custom ClientId to a distinct one so Eve's identity binding test doesn't match shared-streamer-cid.
            _ = await wsStore.SetCustomOAuthAppAsync(wsListB[0].Id, "twitch", "distinct-b-cid", "distinct-b-secret", cancellationToken: ct);

            // Tier 2: Streamer A signs out (keeps LoginHint cookie), visits /login -> sees "Continue as ...".
            using FormUrlEncodedContent logoutFormA = new([new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryA)]);
            _ = await clientA.PostAsync(new Uri("/auth/logout", UriKind.Relative), logoutFormA, ct);

            HttpResponseMessage hintLoginPage = await clientA.GetAsync(new Uri("/login", UriKind.Relative), ct);
            string hintHtml = await hintLoginPage.Content.ReadAsStringAsync(ct);
            _ = hintHtml.Should().Contain($"Continue as {streamerA.DisplayName}");
            _ = hintHtml.Should().Contain("Use a different account");
            _ = hintHtml.Should().NotContain("shared-streamer-secret");

            string hintAntiforgery = ExtractAntiforgeryToken(hintHtml);
            using FormUrlEncodedContent hintPostForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", hintAntiforgery),
                new KeyValuePair<string, string>("flow", "login"),
                new KeyValuePair<string, string>("useLoginHint", "true"),
            ]);
            HttpResponseMessage hintStart = await clientA.PostAsync(new Uri("/auth/twitch/login", UriKind.Relative), hintPostForm, ct);
            _ = hintStart.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> hintQuery = QueryHelpers.ParseQuery(hintStart.Headers.Location!.Query);
            _ = hintQuery["client_id"].ToString().Should().Be("shared-streamer-cid");

            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "shared-streamer-cid",
                subject: "streamer-a-id",
                preferredUsername: "streamer_a",
                nonce: hintQuery["nonce"].ToString());
            HttpResponseMessage hintCallback = await clientA.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-hint&state={Uri.EscapeDataString(hintQuery["state"].ToString())}", UriKind.Relative),
                ct);
            _ = hintCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = hintCallback.Headers.Location!.ToString().Should().Be("/");

            // Clear LoginHint endpoint deletes cookie and redirects to ?useDifferentAccount=true.
            using FormUrlEncodedContent clearForm = new([new KeyValuePair<string, string>("__RequestVerificationToken", hintAntiforgery)]);
            HttpResponseMessage clearHintResp = await clientA.PostAsync(
                new Uri("/auth/login-hint/clear", UriKind.Relative),
                clearForm,
                ct);
            _ = clearHintResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = clearHintResp.Headers.Location!.ToString().Should().Be("/login?useDifferentAccount=true");

            // Tier 3: Identifier-First Lookup + Post-Callback Identity Binding.
            // 3a. Owner logs in using workspace slug.
            using HttpClient tier3OwnerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            string tier3Html = await (await tier3OwnerClient.GetAsync(new Uri("/login", UriKind.Relative), ct)).Content.ReadAsStringAsync(ct);
            string tier3Antiforgery = ExtractAntiforgeryToken(tier3Html);

            using FormUrlEncodedContent identifierOwnerForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", tier3Antiforgery),
                new KeyValuePair<string, string>("flow", "login"),
                new KeyValuePair<string, string>("identifier", wsA.Slug),
            ]);
            HttpResponseMessage tier3Start = await tier3OwnerClient.PostAsync(new Uri("/auth/twitch/login", UriKind.Relative), identifierOwnerForm, ct);
            _ = tier3Start.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> tier3Query = QueryHelpers.ParseQuery(tier3Start.Headers.Location!.Query);
            _ = tier3Query["client_id"].ToString().Should().Be("shared-streamer-cid");

            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "shared-streamer-cid",
                subject: "streamer-a-id",
                preferredUsername: "streamer_a",
                nonce: tier3Query["nonce"].ToString());
            HttpResponseMessage tier3OwnerCallback = await tier3OwnerClient.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-t3-owner&state={Uri.EscapeDataString(tier3Query["state"].ToString())}", UriKind.Relative),
                ct);
            _ = tier3OwnerCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = tier3OwnerCallback.Headers.Location!.ToString().Should().Be("/");

            // 3b. Unrelated user (Streamer B or brand-new user) tries to piggyback on Streamer A's identifier -> Post-Callback Identity Binding rejects with /access-denied.
            using HttpClient piggybackClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            string piggyHtml = await (await piggybackClient.GetAsync(new Uri("/login", UriKind.Relative), ct)).Content.ReadAsStringAsync(ct);
            string piggyAntiforgery = ExtractAntiforgeryToken(piggyHtml);

            using FormUrlEncodedContent piggyForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", piggyAntiforgery),
                new KeyValuePair<string, string>("flow", "login"),
                new KeyValuePair<string, string>("identifier", "streamer_a"),
            ]);
            HttpResponseMessage piggyStart = await piggybackClient.PostAsync(new Uri("/auth/twitch/login", UriKind.Relative), piggyForm, ct);
            _ = piggyStart.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> piggyQuery = QueryHelpers.ParseQuery(piggyStart.Headers.Location!.Query);

            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "shared-streamer-cid",
                subject: "streamer-b-id",
                preferredUsername: "streamer_b",
                nonce: piggyQuery["nonce"].ToString());
            HttpResponseMessage piggyCallback = await piggybackClient.GetAsync(
                new Uri($"/auth/twitch/callback?code=code-piggy&state={Uri.EscapeDataString(piggyQuery["state"].ToString())}", UriKind.Relative),
                ct);
            _ = piggyCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = piggyCallback.Headers.Location!.ToString().Should().Be("/access-denied");
        }
    }

    #endregion

    #region PasskeyFirstLogin

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task PasskeyFirstLogin_RealP256_EnforcesUserVerification_GrantsMfaVerifiedAndStepUp_AndEnforcesOidcGuards()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        DateTimeOffset t0 = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fakeTime = new(t0);
        MockOAuthAndOidcServer mockServer = await MockOAuthAndOidcServer.StartAsync(ct);
        await using (mockServer.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using IntegrationTestFactory factory = new(dirs, mockServer.TwitchEndpointOptions, fakeTime);

            IInstanceOAuthConfigService oauthService = factory.Services.GetRequiredService<IInstanceOAuthConfigService>();
            await oauthService.SetAsync("twitch", new PlatformOAuthAppCredentials("instance-cid", "instance-secret"), ct);

            IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
            InstanceSettings settings = await settingsService.GetAsync(ct);
            settings.StepUpSudoPolicy = StepUpSudoPolicy.Required;
            await settingsService.UpdateAsync(settings, ct);

            IPanelUserStore userStore = factory.Services.GetRequiredService<IPanelUserStore>();
            PanelUser user = await userStore.CreateUserAsync(
                new PanelUser { DisplayName = "pk_first_user", IsRoot = false, IsGlobalAdmin = false },
                new UserLoginIdentity { ProviderId = "twitch", ProviderUserId = "pk-first-1", ProviderUsername = "pk_first_user" },
                ct);
            IWorkspaceStore wsStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            Workspace ws = await wsStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = user.Id, Name = "Passkey Workspace", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "pk-first-1" },
                ct);

            // Enroll real P-256 passkey.
            using HttpClient enrollClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            enrollClient.DefaultRequestHeaders.Add("Origin", "https://localhost");

            HttpResponseMessage loginRedirect = await enrollClient.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> loginQuery = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "pk-first-1",
                preferredUsername: "pk_first_user",
                nonce: loginQuery["nonce"].ToString());
            _ = await enrollClient.GetAsync(
                new Uri($"/auth/twitch/callback?code=enroll-c&state={Uri.EscapeDataString(loginQuery["state"].ToString())}", UriKind.Relative),
                ct);

            HttpResponseMessage stepUpRedirect = await enrollClient.GetAsync(
                new Uri("/auth/twitch/login?flow=stepup&returnUrl=%2Faccount%2Fmfa", UriKind.Relative),
                ct);
            _ = stepUpRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> stepUpQuery = QueryHelpers.ParseQuery(stepUpRedirect.Headers.Location!.Query);
            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "pk-first-1",
                preferredUsername: "pk_first_user",
                nonce: stepUpQuery["nonce"].ToString());
            HttpResponseMessage stepUpCallback = await enrollClient.GetAsync(
                new Uri($"/auth/twitch/callback?code=stepup-c&state={Uri.EscapeDataString(stepUpQuery["state"].ToString())}", UriKind.Relative),
                ct);
            _ = stepUpCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);

            using JsonContent emptyBody = JsonContent.Create(new { });
            HttpResponseMessage createOptsResp = await enrollClient.PostAsync(new Uri("/auth/mfa/passkey/create-options", UriKind.Relative), emptyBody, ct);
            using JsonDocument createOptsDoc = JsonDocument.Parse(await createOptsResp.Content.ReadAsStringAsync(ct));
            string createChallenge = createOptsDoc.RootElement.GetProperty("challenge").GetString()!;
            string rpId = createOptsDoc.RootElement.GetProperty("rp").GetProperty("id").GetString()!;
            string userHandle = createOptsDoc.RootElement.GetProperty("user").GetProperty("id").GetString()!;

            using ECDsa authenticatorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            byte[] credentialId = RandomNumberGenerator.GetBytes(32);
            string attestationJson = BuildPasskeyAttestationJson(credentialId, authenticatorKey, rpId, createChallenge, "https://localhost", signCount: 0);

            HttpResponseMessage registerResp = await enrollClient.PostAsJsonAsync(
                new Uri("/auth/mfa/passkey/register", UriKind.Relative),
                new { credentialJson = attestationJson, name = "Primary Key" },
                ct);
            _ = registerResp.StatusCode.Should().Be(HttpStatusCode.OK);

            // 1. Fresh anonymous client calls POST /auth/passkey/request-options -> userVerification == "required".
            using HttpClient passkeyClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            passkeyClient.DefaultRequestHeaders.Add("Origin", "https://localhost");

            HttpResponseMessage reqOptsNoUv = await passkeyClient.PostAsync(new Uri("/auth/passkey/request-options", UriKind.Relative), emptyBody, ct);
            _ = reqOptsNoUv.StatusCode.Should().Be(HttpStatusCode.OK);
            using JsonDocument reqOptsNoUvDoc = JsonDocument.Parse(await reqOptsNoUv.Content.ReadAsStringAsync(ct));
            _ = reqOptsNoUvDoc.RootElement.GetProperty("userVerification").GetString().Should().Be("required");

            // 2. Assertion WITHOUT UV bit (0x01) is rejected with 401 Unauthorized.
            string assertionWithoutUv = BuildPasskeyAssertionJson(
                credentialId,
                authenticatorKey,
                reqOptsNoUvDoc.RootElement.GetProperty("rpId").GetString()!,
                reqOptsNoUvDoc.RootElement.GetProperty("challenge").GetString()!,
                "https://localhost",
                userHandle,
                signCount: 1,
                userVerified: false);

            HttpResponseMessage noUvResp = await passkeyClient.PostAsJsonAsync(
                new Uri("/auth/passkey/login", UriKind.Relative),
                new { credentialJson = assertionWithoutUv, returnUrl = "/workspace/platforms" },
                ct);
            _ = noUvResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 3. Assertion WITH UV bit (0x05) succeeds, increments SignCount, grants 15m step-up, and sets LoginHint.
            HttpResponseMessage reqOptsUv = await passkeyClient.PostAsync(new Uri("/auth/passkey/request-options", UriKind.Relative), emptyBody, ct);
            using JsonDocument reqOptsUvDoc = JsonDocument.Parse(await reqOptsUv.Content.ReadAsStringAsync(ct));

            string assertionWithUv = BuildPasskeyAssertionJson(
                credentialId,
                authenticatorKey,
                reqOptsUvDoc.RootElement.GetProperty("rpId").GetString()!,
                reqOptsUvDoc.RootElement.GetProperty("challenge").GetString()!,
                "https://localhost",
                userHandle,
                signCount: 2,
                userVerified: true);

            HttpResponseMessage passkeyLoginResp = await passkeyClient.PostAsJsonAsync(
                new Uri("/auth/passkey/login", UriKind.Relative),
                new { credentialJson = assertionWithUv, returnUrl = "/workspace/platforms" },
                ct);
            _ = passkeyLoginResp.StatusCode.Should().Be(HttpStatusCode.OK);
            _ = passkeyLoginResp.Headers.GetValues("Set-Cookie")
                .Should().Contain(c => c.StartsWith($"{AuthClaims.LoginHintCookieName}=", StringComparison.Ordinal));

            using JsonDocument loginResultDoc = JsonDocument.Parse(await passkeyLoginResp.Content.ReadAsStringAsync(ct));
            _ = loginResultDoc.RootElement.GetProperty("returnUrl").GetString().Should().Be("/workspace/platforms");

            IMfaCredentialStore mfaCredStore = factory.Services.GetRequiredService<IMfaCredentialStore>();
            IReadOnlyList<PasskeyCredentialPayload> storedPasskeys = await mfaCredStore.GetPasskeysAsync(user.Id, ct);
            _ = storedPasskeys.Should().ContainSingle().Which.SignCount.Should().Be(2u);

            // Verify 15-minute step-up elevation is active on /workspace/platforms (no StepUpMfaGuard).
            HttpResponseMessage platformsResp = await passkeyClient.GetAsync(new Uri("/workspace/platforms", UriKind.Relative), ct);
            _ = platformsResp.StatusCode.Should().Be(HttpStatusCode.OK);
            string platformsHtml = await platformsResp.Content.ReadAsStringAsync(ct);
            _ = platformsHtml.Should().NotContain("data-step-up-guard=\"true\"");

            // 4. When user's ONLY workspace requires External OIDC, passkey-first login is rejected with workspace_oidc_required.
            ws.RequireExternalOidcLogin = true;
            _ = await wsStore.UpdateWorkspaceAsync(ws, ct);

            using HttpClient blockedClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            blockedClient.DefaultRequestHeaders.Add("Origin", "https://localhost");

            HttpResponseMessage reqOptsGuard = await blockedClient.PostAsync(new Uri("/auth/passkey/request-options", UriKind.Relative), emptyBody, ct);
            using JsonDocument reqOptsGuardDoc = JsonDocument.Parse(await reqOptsGuard.Content.ReadAsStringAsync(ct));
            string assertionGuard = BuildPasskeyAssertionJson(
                credentialId,
                authenticatorKey,
                reqOptsGuardDoc.RootElement.GetProperty("rpId").GetString()!,
                reqOptsGuardDoc.RootElement.GetProperty("challenge").GetString()!,
                "https://localhost",
                userHandle,
                signCount: 3,
                userVerified: true);

            HttpResponseMessage guardResp = await blockedClient.PostAsJsonAsync(
                new Uri("/auth/passkey/login", UriKind.Relative),
                new { credentialJson = assertionGuard },
                ct);
            _ = guardResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            string guardBody = await guardResp.Content.ReadAsStringAsync(ct);
            _ = guardBody.Should().Contain("workspace_oidc_required");

            // Non-OIDC platform login is also rejected with /login?error=workspace_oidc_required.
            HttpResponseMessage twitchGuardRedirect = await blockedClient.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> twitchGuardQuery = QueryHelpers.ParseQuery(twitchGuardRedirect.Headers.Location!.Query);
            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "pk-first-1",
                preferredUsername: "pk_first_user",
                nonce: twitchGuardQuery["nonce"].ToString());
            HttpResponseMessage twitchGuardCallback = await blockedClient.GetAsync(
                new Uri($"/auth/twitch/callback?code=guard-c&state={Uri.EscapeDataString(twitchGuardQuery["state"].ToString())}", UriKind.Relative),
                ct);
            _ = twitchGuardCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = twitchGuardCallback.Headers.Location!.ToString().Should().Be("/login?error=workspace_oidc_required");

            // 5. Under ExternalOidcLoginMode.Required, /auth/passkey/request-options rejects with oidc_required.
            settings.ExternalOidcLoginMode = ExternalOidcLoginMode.Required;
            await settingsService.UpdateAsync(settings, ct);
            HttpResponseMessage oidcReqResp = await blockedClient.PostAsync(new Uri("/auth/passkey/request-options", UriKind.Relative), emptyBody, ct);
            _ = oidcReqResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            _ = (await oidcReqResp.Content.ReadAsStringAsync(ct)).Should().Contain("oidc_required");
        }
    }

    #endregion

    #region ReauthenticateWorkspaceSwitcher

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task WorkspaceSwitcher_ReauthenticateFlow_RedirectsToOidcAndMfaAndSwitchesCookieOnCompletion()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        MockOAuthAndOidcServer mockServer = await MockOAuthAndOidcServer.StartAsync(ct);
        await using (mockServer.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using IntegrationTestFactory factory = new(dirs, mockServer.TwitchEndpointOptions, fakeTime);

            IInstanceOAuthConfigService oauthService = factory.Services.GetRequiredService<IInstanceOAuthConfigService>();
            await oauthService.SetAsync("twitch", new PlatformOAuthAppCredentials("instance-cid", "instance-secret"), ct);

            IPanelUserStore userStore = factory.Services.GetRequiredService<IPanelUserStore>();
            PanelUser owner = await userStore.CreateUserAsync(
                new PanelUser { DisplayName = "ChannelOwner" },
                new UserLoginIdentity { ProviderId = "twitch", ProviderUserId = "owner-1", ProviderUsername = "channel_owner" },
                ct);
            PanelUser multiUser = await userStore.CreateUserAsync(
                new PanelUser { DisplayName = "MultiMod" },
                new UserLoginIdentity { ProviderId = "twitch", ProviderUserId = "multi-1", ProviderUsername = "multi_mod" },
                ct);

            IWorkspaceStore wsStore = factory.Services.GetRequiredService<IWorkspaceStore>();
            Workspace wsReady = await wsStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = owner.Id, Name = "Ready WS", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "owner-1" },
                ct);
            Workspace wsOidc = await wsStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = owner.Id, Name = "Corp SSO WS", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "owner-2" },
                ct);
            Workspace wsMfa = await wsStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = owner.Id, Name = "Strict MFA WS", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "owner-3" },
                ct);
            Workspace wsForbidden = await wsStore.CreateWorkspaceAsync(
                new Workspace { OwnerUserId = owner.Id, Name = "Private WS", PrimaryPlatformId = "twitch" },
                new WorkspacePlatform { PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "owner-4" },
                ct);

            // Grant multiUser ui.panel.access in wsReady, wsOidc, and wsMfa (NOT wsForbidden).
            IPermissionStore permStore = factory.Services.GetRequiredService<IPermissionStore>();
            foreach (Workspace ws in new[] { wsReady, wsOidc, wsMfa })
            {
                PermissionGroup group = await permStore.UpsertGroupAsync(new PermissionGroup
                {
                    WorkspaceId = ws.Id,
                    Name = "Staff",
                }, ct);
                await permStore.SetGrantAsync(group.Id, BuiltInPermissionKeys.UiPanelAccess, PermissionState.Allow, ct);
                await permStore.AddGroupMemberAsync(new PermissionGroupMember
                {
                    GroupId = group.Id,
                    PlatformId = "twitch",
                    PlatformUserId = "multi-1",
                }, ct);
                await permStore.AddGroupMemberAsync(new PermissionGroupMember
                {
                    GroupId = group.Id,
                    PlatformId = "oidc:corp-sso",
                    PlatformUserId = "oidc-multi-sub",
                }, ct);
            }

            IExternalOidcProviderStore oidcStore = factory.Services.GetRequiredService<IExternalOidcProviderStore>();
            _ = await oidcStore.UpsertAsync(
                new ExternalOidcProvider
                {
                    WorkspaceId = wsOidc.Id,
                    Slug = "corp-sso",
                    DisplayName = "Corp SSO",
                    AuthorityUrl = mockServer.BaseUrl,
                    ClientId = "corp-oidc-cid",
                    ScopesJson = "[\"openid\",\"profile\"]",
                    IsEnabled = true,
                },
                plaintextClientSecret: "corp-oidc-secret",
                ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            // Sign in as multiUser via Twitch (no MFA enrolled yet -> sa:mfa_verified is false).
            HttpResponseMessage twitchRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct);
            Dictionary<string, StringValues> twitchQuery = QueryHelpers.ParseQuery(twitchRedirect.Headers.Location!.Query);
            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "instance-cid",
                subject: "multi-1",
                preferredUsername: "multi_mod",
                nonce: twitchQuery["nonce"].ToString());
            HttpResponseMessage twitchCallback = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=c-multi&state={Uri.EscapeDataString(twitchQuery["state"].ToString())}", UriKind.Relative),
                ct);
            _ = twitchCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);

            // Require MFA on wsMfa and enroll TOTP for multiUser after initial Twitch sign-in
            // so the active session does not yet carry sa:mfa_verified when viewing the switcher.
            wsMfa.LoginMfaRequirement = PanelLoginMfaRequirement.RequiredForAll;
            wsMfa = await wsStore.UpdateWorkspaceAsync(wsMfa, ct);
            ITotpService totpService = factory.Services.GetRequiredService<ITotpService>();
            IMfaCredentialStore mfaCredStore = factory.Services.GetRequiredService<IMfaCredentialStore>();
            string totpSecret = totpService.GenerateSecretBase32();
            _ = await mfaCredStore.SaveTotpAsync(
                multiUser.Id,
                new TotpSecretPayload(totpSecret, multiUser.DisplayName, fakeTime.GetUtcNow()),
                [],
                ct);

            // /permissions renders switcher with (reauthenticate) for wsOidc and wsMfa.
            HttpResponseMessage permPage = await client.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = permPage.StatusCode.Should().Be(HttpStatusCode.OK);
            string permHtml = await permPage.Content.ReadAsStringAsync(ct);
            _ = permHtml.Should().Contain("Ready WS (twitch)");
            _ = permHtml.Should().Contain("Corp SSO WS (twitch) (reauthenticate)");
            _ = permHtml.Should().Contain("Strict MFA WS (twitch) (reauthenticate)");
            string antiforgery = ExtractAntiforgeryToken(permHtml);

            // 1. Selecting wsMfa redirects to /auth/mfa?workspaceId={wsMfa.Id}&returnUrl=%2Fpermissions.
            using FormUrlEncodedContent selectMfaForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                new KeyValuePair<string, string>("workspaceId", wsMfa.Id.ToString("D")),
                new KeyValuePair<string, string>("returnUrl", "/permissions"),
            ]);
            HttpResponseMessage selectMfaResp = await client.PostAsync(new Uri("/auth/workspace/select", UriKind.Relative), selectMfaForm, ct);
            _ = selectMfaResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = selectMfaResp.Headers.Location!.ToString().Should().Be($"/auth/mfa?workspaceId={wsMfa.Id:D}&returnUrl=%2Fpermissions");

            // Completing TOTP verification with workspaceId sets StreamActions.Workspace={wsMfa.Id} cookie!
            string validCode = totpService.ComputeCode(totpSecret, fakeTime.GetUtcNow());
            HttpResponseMessage totpVerifyResp = await client.PostAsJsonAsync(
                new Uri("/auth/mfa/totp/verify", UriKind.Relative),
                new { code = validCode, returnUrl = "/permissions", workspaceId = wsMfa.Id.ToString("D") },
                ct);
            _ = totpVerifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
            _ = totpVerifyResp.Headers.GetValues("Set-Cookie")
                .Should().Contain(c => c.Contains($"{AuthClaims.WorkspaceCookieName}={wsMfa.Id:D}", StringComparison.Ordinal));

            // 2. Selecting wsOidc redirects to /auth/oidc/corp-sso/login?workspaceId={wsOidc.Id}&returnUrl=%2Fpermissions.
            using FormUrlEncodedContent selectOidcForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                new KeyValuePair<string, string>("workspaceId", wsOidc.Id.ToString("D")),
                new KeyValuePair<string, string>("returnUrl", "/permissions"),
            ]);
            HttpResponseMessage selectOidcResp = await client.PostAsync(new Uri("/auth/workspace/select", UriKind.Relative), selectOidcForm, ct);
            _ = selectOidcResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = selectOidcResp.Headers.Location!.ToString().Should().Be($"/auth/oidc/corp-sso/login?workspaceId={wsOidc.Id:D}&returnUrl=%2Fpermissions");

            // Following OIDC login & callback signs in as corp-sso and sets StreamActions.Workspace={wsOidc.Id} cookie!
            HttpResponseMessage oidcStartResp = await client.GetAsync(selectOidcResp.Headers.Location, ct);
            _ = oidcStartResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> oidcQuery = QueryHelpers.ParseQuery(oidcStartResp.Headers.Location!.Query);

            // Link multiUser to oidc:corp-sso or let OIDC login match/provision via Staff group.
            _ = await userStore.LinkLoginIdentityAsync(new UserLoginIdentity
            {
                PanelUserId = multiUser.Id,
                ProviderId = "oidc:corp-sso",
                ProviderUserId = "oidc-multi-sub",
                ProviderUsername = "multi_mod",
            }, ct);

            mockServer.IdTokenFactory = baseUrl => mockServer.CreateSignedIdToken(
                issuer: baseUrl,
                audience: "corp-oidc-cid",
                subject: "oidc-multi-sub",
                preferredUsername: "multi_mod",
                nonce: oidcQuery["nonce"].ToString());

            HttpResponseMessage oidcCallbackResp = await client.GetAsync(
                new Uri($"/auth/oidc/corp-sso/callback?code=oidc-ws&state={Uri.EscapeDataString(oidcQuery["state"].ToString())}", UriKind.Relative),
                ct);
            _ = oidcCallbackResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = oidcCallbackResp.Headers.GetValues("Set-Cookie")
                .Should().Contain(c => c.Contains($"{AuthClaims.WorkspaceCookieName}={wsOidc.Id:D}", StringComparison.Ordinal));

            // 3. Selecting wsForbidden redirects to /access-denied (403 Forbid).
            using FormUrlEncodedContent selectForbiddenForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                new KeyValuePair<string, string>("workspaceId", wsForbidden.Id.ToString("D")),
                new KeyValuePair<string, string>("returnUrl", "/permissions"),
            ]);
            HttpResponseMessage selectForbiddenResp = await client.PostAsync(new Uri("/auth/workspace/select", UriKind.Relative), selectForbiddenForm, ct);
            _ = selectForbiddenResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = selectForbiddenResp.Headers.Location!.AbsolutePath.Should().Be("/access-denied");
        }
    }

    #endregion

    private static string BuildPasskeyAttestationJson(
        byte[] credentialId,
        ECDsa ecdsa,
        string rpId,
        string challengeBase64Url,
        string origin,
        uint signCount)
    {
        byte[] clientDataJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "webauthn.create",
            challenge = challengeBase64Url,
            origin,
            crossOrigin = false,
        });

        ECParameters ecParams = ecdsa.ExportParameters(includePrivateParameters: false);
        byte[] cosePublicKey = EncodeCoseEc2PublicKey(ecParams);

        byte[] rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        byte[] aaguid = new byte[16];
        byte[] authData = new byte[32 + 1 + 4 + 16 + 2 + credentialId.Length + cosePublicKey.Length];
        int offset = 0;

        rpIdHash.CopyTo(authData, offset);
        offset += 32;

        authData[offset++] = 0x45;

        BinaryPrimitives.WriteUInt32BigEndian(authData.AsSpan(offset, 4), signCount);
        offset += 4;

        aaguid.CopyTo(authData, offset);
        offset += 16;

        BinaryPrimitives.WriteUInt16BigEndian(authData.AsSpan(offset, 2), (ushort)credentialId.Length);
        offset += 2;

        credentialId.CopyTo(authData, offset);
        offset += credentialId.Length;

        cosePublicKey.CopyTo(authData, offset);

        CborWriter attestationWriter = new(CborConformanceMode.Canonical);
        attestationWriter.WriteStartMap(3);
        attestationWriter.WriteTextString("fmt");
        attestationWriter.WriteTextString("none");
        attestationWriter.WriteTextString("attStmt");
        attestationWriter.WriteStartMap(0);
        attestationWriter.WriteEndMap();
        attestationWriter.WriteTextString("authData");
        attestationWriter.WriteByteString(authData);
        attestationWriter.WriteEndMap();
        byte[] attestationObject = attestationWriter.Encode();

        string credIdBase64Url = Base64UrlEncoder.Encode(credentialId);
        return JsonSerializer.Serialize(new
        {
            id = credIdBase64Url,
            rawId = credIdBase64Url,
            type = "public-key",
            authenticatorAttachment = "platform",
            clientExtensionResults = new { },
            response = new
            {
                clientDataJSON = Base64UrlEncoder.Encode(clientDataJson),
                attestationObject = Base64UrlEncoder.Encode(attestationObject),
                transports = _internalTransports,
            },
        });
    }

    private static string BuildPasskeyAssertionJson(
        byte[] credentialId,
        ECDsa ecdsa,
        string rpId,
        string challengeBase64Url,
        string origin,
        string userHandleBase64Url,
        uint signCount,
        bool userVerified)
    {
        byte[] clientDataJson = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "webauthn.get",
            challenge = challengeBase64Url,
            origin,
            crossOrigin = false,
        });

        byte[] rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        byte[] authData = new byte[32 + 1 + 4];
        rpIdHash.CopyTo(authData, 0);
        authData[32] = userVerified ? (byte)0x05 : (byte)0x01;
        BinaryPrimitives.WriteUInt32BigEndian(authData.AsSpan(33, 4), signCount);

        byte[] clientDataHash = SHA256.HashData(clientDataJson);
        byte[] signedBytes = [.. authData, .. clientDataHash];
        byte[] signature = ecdsa.SignData(signedBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        string credIdBase64Url = Base64UrlEncoder.Encode(credentialId);
        return JsonSerializer.Serialize(new
        {
            id = credIdBase64Url,
            rawId = credIdBase64Url,
            type = "public-key",
            authenticatorAttachment = "platform",
            clientExtensionResults = new { },
            response = new
            {
                clientDataJSON = Base64UrlEncoder.Encode(clientDataJson),
                authenticatorData = Base64UrlEncoder.Encode(authData),
                signature = Base64UrlEncoder.Encode(signature),
                userHandle = userHandleBase64Url,
            },
        });
    }

    private static byte[] EncodeCoseEc2PublicKey(ECParameters ecParams)
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        writer.WriteInt32(-7);
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(ecParams.Q.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(ecParams.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        Match match = AntiforgeryInputRegex().Match(html);
        _ = match.Success.Should().BeTrue("HTML should contain an antiforgery token");
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="(?<value>[^"]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryInputRegex();

    private sealed class TempDaemonDirectories : IDisposable
    {
        public TempDaemonDirectories()
        {
            this.Root = Path.Combine(Path.GetTempPath(), $"sa-login-test-{Guid.NewGuid():N}");
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

    private sealed class IntegrationTestFactory(
        TempDaemonDirectories dirs,
        TwitchOAuthEndpointOptions twitchOptions,
        FakeTimeProvider fakeTimeProvider) : WebApplicationFactory<Program>
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
            _ = builder.UseSetting("Bootstrap:Node:InstanceId", $"login-test-{Guid.NewGuid():N}");
            _ = builder.UseSetting("Bootstrap:Sentry:Enabled", "false");

            _ = builder.ConfigureServices(services =>
            {
                _ = services.RemoveAll<TimeProvider>();
                _ = services.AddSingleton<TimeProvider>(fakeTimeProvider);
                _ = services.PostConfigure<CookieAuthenticationOptions>(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    options => options.TimeProvider = fakeTimeProvider);

                _ = services.Configure<TwitchOAuthEndpointOptions>(opts =>
                {
                    opts.AuthorizeEndpoint = twitchOptions.AuthorizeEndpoint;
                    opts.TokenEndpoint = twitchOptions.TokenEndpoint;
                    opts.ValidateEndpoint = twitchOptions.ValidateEndpoint;
                    opts.RevokeEndpoint = twitchOptions.RevokeEndpoint;
                    opts.UserInfoEndpoint = twitchOptions.UserInfoEndpoint;
                    opts.OpenIdConfigurationEndpoint = twitchOptions.OpenIdConfigurationEndpoint;
                });
            });
        }
    }

    private sealed class MockOAuthAndOidcServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly RSA _rsa;
        private readonly RsaSecurityKey _signingKey;

        private MockOAuthAndOidcServer(WebApplication app, string baseUrl, RSA rsa, RsaSecurityKey signingKey)
        {
            this._app = app;
            this.BaseUrl = baseUrl;
            this._rsa = rsa;
            this._signingKey = signingKey;
            this.TwitchEndpointOptions = new TwitchOAuthEndpointOptions
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
        public TwitchOAuthEndpointOptions TwitchEndpointOptions { get; }
        public Func<string, string>? IdTokenFactory { get; set; }

        public static async Task<MockOAuthAndOidcServer> StartAsync(CancellationToken cancellationToken)
        {
            RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-oidc-key" };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
            WebApplication app = builder.Build();

            MockOAuthAndOidcServer? holder = null;

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

            _ = app.MapPost("/oauth2/token", () =>
            {
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

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            string baseUrl = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .First()
                .TrimEnd('/');

            holder = new MockOAuthAndOidcServer(app, baseUrl, rsa, signingKey);
            return holder;
        }

        public string CreateSignedIdToken(
            string issuer,
            string audience,
            string subject,
            string preferredUsername,
            string nonce)
        {
            List<Claim> claims =
            [
                new("sub", subject),
                new("preferred_username", preferredUsername),
                new("nonce", nonce),
            ];

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
