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

public sealed partial class MfaIntegrationTests
{
    private static readonly string[] _internalTransports = ["internal"];
    #region LoginMfaAndTotp

    [Fact]
    [Trait("Member", "MapMfaEndpoints")]
    public async Task LoginMfa_WithEnrolledTotp_BlocksPanelUntilTotpVerifiedThenGrantsAccess()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using MfaWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            PanelUser user = await SeedUserWithOAuthAsync(factory.Services, "twitch", "mfa-user-1", "mfa_streamer", isRoot: false, ct);
            ITotpService totpService = factory.Services.GetRequiredService<ITotpService>();
            IMfaCredentialStore credentialStore = factory.Services.GetRequiredService<IMfaCredentialStore>();

            string secret = totpService.GenerateSecretBase32();
            _ = await credentialStore.SaveTotpAsync(
                user.Id,
                new TotpSecretPayload(secret, user.DisplayName, fakeTime.GetUtcNow()),
                [],
                ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage callback = await SignInViaMockTwitchAsync(client, mockTwitch, "mfa-user-1", "mfa_streamer", ct);
            _ = callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callback.Headers.Location!.ToString().Should().Be("/auth/mfa");

            // While mfa-pending, panel routes are blocked by PanelAuthenticatedPolicy.
            HttpResponseMessage blockedPermissions = await client.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = blockedPermissions.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = blockedPermissions.Headers.Location!.AbsolutePath.Should().Be("/access-denied");

            // /auth/mfa challenge page renders TOTP and recovery forms.
            HttpResponseMessage mfaPage = await client.GetAsync(new Uri("/auth/mfa", UriKind.Relative), ct);
            _ = mfaPage.StatusCode.Should().Be(HttpStatusCode.OK);
            string mfaHtml = await mfaPage.Content.ReadAsStringAsync(ct);
            string antiforgery = ExtractAntiforgeryToken(mfaHtml);

            // Submit valid TOTP code via form POST.
            string validCode = totpService.ComputeCode(secret, fakeTime.GetUtcNow());
            using FormUrlEncodedContent verifyForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                new KeyValuePair<string, string>("code", validCode),
                new KeyValuePair<string, string>("returnUrl", "/permissions"),
            ]);

            using HttpRequestMessage verifyRequest = new(HttpMethod.Post, "/auth/mfa/totp/verify")
            {
                Content = verifyForm,
            };
            verifyRequest.Headers.Add("Accept", "text/html");

            HttpResponseMessage verifyResponse = await client.SendAsync(verifyRequest, ct);
            _ = verifyResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = verifyResponse.Headers.Location!.ToString().Should().Be("/permissions");

            // Upgraded auth cookie now permits full panel access.
            HttpResponseMessage permissionsResponse = await client.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion

    #region PasskeysOnlyPolicy

    [Fact]
    [Trait("Member", "MapMfaEndpoints")]
    public async Task PasskeysOnlyPolicy_WhenTotpDisabled_Returns403ForTotpEndpointsAndHidesTotpChallengeUi()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using MfaWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
            InstanceSettings settings = await settingsService.GetAsync(ct);
            settings.MfaPasskeyAvailability = MfaMethodAvailability.Allowed;
            settings.MfaTotpAvailability = MfaMethodAvailability.Disabled;
            await settingsService.UpdateAsync(settings, ct);

            PanelUser user = await SeedUserWithOAuthAsync(factory.Services, "twitch", "pk-only-1", "passkey_user", isRoot: false, ct);
            ITotpService totpService = factory.Services.GetRequiredService<ITotpService>();
            IMfaCredentialStore credentialStore = factory.Services.GetRequiredService<IMfaCredentialStore>();
            string secret = totpService.GenerateSecretBase32();
            _ = await credentialStore.SaveTotpAsync(
                user.Id,
                new TotpSecretPayload(secret, user.DisplayName, fakeTime.GetUtcNow()),
                [],
                ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage callback = await SignInViaMockTwitchAsync(client, mockTwitch, "pk-only-1", "passkey_user", ct);
            _ = callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callback.Headers.Location!.ToString().Should().Be("/auth/mfa");

            HttpResponseMessage challengeGet = await client.GetAsync(new Uri("/auth/mfa", UriKind.Relative), ct);
            _ = challengeGet.StatusCode.Should().Be(HttpStatusCode.OK);
            string challengeHtml = await challengeGet.Content.ReadAsStringAsync(ct);
            _ = challengeHtml.Should().Contain("data-mfa-passkey=\"true\"");
            _ = challengeHtml.Should().NotContain("/auth/mfa/totp/verify");

            using JsonContent emptyBody = JsonContent.Create(new { });
            HttpResponseMessage enrollBegin = await client.PostAsync(
                new Uri("/auth/mfa/totp/enroll/begin", UriKind.Relative),
                emptyBody,
                ct);
            _ = enrollBegin.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            string code = totpService.ComputeCode(secret, fakeTime.GetUtcNow());
            HttpResponseMessage enrollConfirm = await client.PostAsJsonAsync(
                new Uri("/auth/mfa/totp/enroll/confirm", UriKind.Relative),
                new { code },
                ct);
            _ = enrollConfirm.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            HttpResponseMessage totpVerify = await client.PostAsJsonAsync(
                new Uri("/auth/mfa/totp/verify", UriKind.Relative),
                new { code },
                ct);
            _ = totpVerify.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    #endregion

    #region StepUpSudoModeLifecycle

    [Fact]
    [Trait("Member", "IsStepUpElevated")]
    public async Task StepUpSudoMode_WithFakeTimeProvider_RemainsElevatedFor15MinutesAndExpiresAfterward()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        DateTimeOffset t0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fakeTime = new(t0);
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using MfaWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
            InstanceSettings settings = await settingsService.GetAsync(ct);
            settings.StepUpSudoPolicy = StepUpSudoPolicy.Required;
            await settingsService.UpdateAsync(settings, ct);

            PanelUser rootUser = await SeedUserWithOAuthAsync(factory.Services, "twitch", "sudo-root-1", "sudo_root", isRoot: true, ct);
            ITotpService totpService = factory.Services.GetRequiredService<ITotpService>();
            IMfaCredentialStore credentialStore = factory.Services.GetRequiredService<IMfaCredentialStore>();
            string secret = totpService.GenerateSecretBase32();
            _ = await credentialStore.SaveTotpAsync(
                rootUser.Id,
                new TotpSecretPayload(secret, rootUser.DisplayName, fakeTime.GetUtcNow()),
                [],
                ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage callback = await SignInViaMockTwitchAsync(client, mockTwitch, "sudo-root-1", "sudo_root", ct);
            _ = callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callback.Headers.Location!.ToString().Should().Be("/auth/mfa");

            string code = totpService.ComputeCode(secret, fakeTime.GetUtcNow());
            HttpResponseMessage verifyResponse = await client.PostAsJsonAsync(
                new Uri("/auth/mfa/totp/verify", UriKind.Relative),
                new { code, returnUrl = "/admin/auth" },
                ct);
            _ = verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // At t0 + 14m59s: step-up elevation is active -> /admin/auth renders protected content (not StepUpMfaGuard).
            fakeTime.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
            HttpResponseMessage activeAdminGet = await client.GetAsync(new Uri("/admin/auth", UriKind.Relative), ct);
            _ = activeAdminGet.StatusCode.Should().Be(HttpStatusCode.OK);
            string activeHtml = await activeAdminGet.Content.ReadAsStringAsync(ct);
            _ = activeHtml.Should().Contain("Instance Authentication Policies");
            _ = activeHtml.Should().NotContain("data-step-up-guard=\"true\"");

            // Verify AuthClaims.IsStepUpElevated directly at t0 + 14m59s.
            ClaimsPrincipal elevatedPrincipal = AuthClaims.BuildClaimsPrincipal(
                rootUser,
                mfaElevatedUntilUtc: t0 + AuthClaims.StepUpWindow);
            _ = AuthClaims.IsStepUpElevated(elevatedPrincipal, fakeTime).Should().BeTrue();

            // Advance 2 seconds to t0 + 15m01s -> elevation expires -> /admin/auth renders Sudo Mode guard.
            fakeTime.Advance(TimeSpan.FromSeconds(2));
            _ = AuthClaims.IsStepUpElevated(elevatedPrincipal, fakeTime).Should().BeFalse();

            HttpResponseMessage expiredAdminGet = await client.GetAsync(new Uri("/admin/auth", UriKind.Relative), ct);
            _ = expiredAdminGet.StatusCode.Should().Be(HttpStatusCode.OK);
            string expiredHtml = await expiredAdminGet.Content.ReadAsStringAsync(ct);
            _ = expiredHtml.Should().Contain("data-step-up-guard=\"true\"");
            _ = expiredHtml.Should().NotContain("Instance Authentication Policies");
        }
    }

    #endregion

    #region BreakGlassRootRecovery

    [Fact]
    [Trait("Member", "MapAuthEndpoints")]
    public async Task BreakGlassRootRecovery_ViaConsoleBootstrapToken_ClearsRootMfaCredentialsAndSignsIn()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using MfaWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            PanelUser rootUser = await SeedUserWithOAuthAsync(factory.Services, "twitch", "root-orig-1", "root_owner", isRoot: true, ct);
            ITotpService totpService = factory.Services.GetRequiredService<ITotpService>();
            IMfaCredentialStore credentialStore = factory.Services.GetRequiredService<IMfaCredentialStore>();
            _ = await credentialStore.SaveTotpAsync(
                rootUser.Id,
                new TotpSecretPayload(totpService.GenerateSecretBase32(), rootUser.DisplayName, fakeTime.GetUtcNow()),
                [totpService.HashRecoveryCode("AAAAA-BBBBB")],
                ct);
            _ = (await credentialStore.HasAnyCredentialsAsync(rootUser.Id, ct)).Should().BeTrue();

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            // Verify Console Bootstrap Token via /setup/verify.
            HttpResponseMessage setupGet = await client.GetAsync(new Uri("/setup-recovery", UriKind.Relative), ct);
            _ = setupGet.StatusCode.Should().Be(HttpStatusCode.OK);
            string setupHtml = await setupGet.Content.ReadAsStringAsync(ct);
            string antiforgery = ExtractAntiforgeryToken(setupHtml);

            IBootstrapTokenService bootstrapTokens = factory.Services.GetRequiredService<IBootstrapTokenService>();
            string rawToken = bootstrapTokens.RequestOrReuseActiveToken().Token!;

            using FormUrlEncodedContent setupForm = new(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
                new KeyValuePair<string, string>("bootstrapToken", rawToken),
                new KeyValuePair<string, string>("platformId", "twitch"),
            ]);
            HttpResponseMessage verifyResponse = await client.PostAsync(new Uri("/setup/verify", UriKind.Relative), setupForm, ct);
            _ = verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Complete /auth/twitch/login?flow=setup -> /auth/twitch/callback.
            HttpResponseMessage setupLogin = await client.GetAsync(new Uri("/auth/twitch/login?flow=setup", UriKind.Relative), ct);
            _ = setupLogin.StatusCode.Should().Be(HttpStatusCode.Redirect);
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(setupLogin.Headers.Location!.Query);
            string state = query["state"].ToString();
            string nonce = query["nonce"].ToString();
            mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(baseUrl, "instance-cid", "root-orig-1", "root_owner", nonce);

            HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri($"/auth/twitch/callback?code=recovery-code-1&state={Uri.EscapeDataString(state)}", UriKind.Relative),
                ct);
            _ = callbackResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callbackResponse.Headers.Location!.ToString().Should().Be("/");

            // Root MFA credentials were cleared and root is signed in without mfa-pending.
            _ = (await credentialStore.HasAnyCredentialsAsync(rootUser.Id, ct)).Should().BeFalse();

            HttpResponseMessage adminAuthResponse = await client.GetAsync(new Uri("/admin/auth", UriKind.Relative), ct);
            _ = adminAuthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion

    #region RecoveryCodeSingleUse

    [Fact]
    [Trait("Member", "MapMfaEndpoints")]
    public async Task RecoveryCode_Verify_ConsumesCodeOnFirstUseAndRejectsReuse()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using MfaWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            PanelUser user = await SeedUserWithOAuthAsync(factory.Services, "twitch", "rec-user-1", "recovery_user", isRoot: false, ct);
            ITotpService totpService = factory.Services.GetRequiredService<ITotpService>();
            IMfaCredentialStore credentialStore = factory.Services.GetRequiredService<IMfaCredentialStore>();

            IReadOnlyList<string> plaintextCodes = totpService.GenerateRecoveryCodes(2);
            List<string> hashedCodes = [.. plaintextCodes.Select(totpService.HashRecoveryCode)];
            _ = await credentialStore.SaveTotpAsync(
                user.Id,
                new TotpSecretPayload(totpService.GenerateSecretBase32(), user.DisplayName, fakeTime.GetUtcNow()),
                hashedCodes,
                ct);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

            HttpResponseMessage callback = await SignInViaMockTwitchAsync(client, mockTwitch, "rec-user-1", "recovery_user", ct);
            _ = callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = callback.Headers.Location!.ToString().Should().Be("/auth/mfa");

            // First use of code[0] succeeds and upgrades cookie.
            HttpResponseMessage firstVerify = await client.PostAsJsonAsync(
                new Uri("/auth/mfa/recovery/verify", UriKind.Relative),
                new { code = plaintextCodes[0], returnUrl = "/permissions" },
                ct);
            _ = firstVerify.StatusCode.Should().Be(HttpStatusCode.OK);

            // Second use of the same code[0] is rejected with 401 Unauthorized.
            HttpResponseMessage secondVerify = await client.PostAsJsonAsync(
                new Uri("/auth/mfa/recovery/verify", UriKind.Relative),
                new { code = plaintextCodes[0], returnUrl = "/permissions" },
                ct);
            _ = secondVerify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Second unused recovery code[1] still succeeds.
            HttpResponseMessage unusedCodeVerify = await client.PostAsJsonAsync(
                new Uri("/auth/mfa/recovery/verify", UriKind.Relative),
                new { code = plaintextCodes[1], returnUrl = "/permissions" },
                ct);
            _ = unusedCodeVerify.StatusCode.Should().Be(HttpStatusCode.OK);

            _ = (await credentialStore.GetRemainingRecoveryCodesCountAsync(user.Id, ct)).Should().Be(0);
        }
    }

    #endregion

    #region RealWebAuthnPasskeyFlow

    [Fact]
    [Trait("Member", "MapMfaEndpoints")]
    public async Task WebAuthnPasskey_RealP256AttestationAndAssertion_IncrementsSignCountClearsMfaPendingAndGrantsStepUp()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        DateTimeOffset t0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider fakeTime = new(t0);
        MockTwitchOAuthServer mockTwitch = await MockTwitchOAuthServer.StartAsync(ct);
        await using (mockTwitch.ConfigureAwait(false))
        {
            using TempDaemonDirectories dirs = new();
            using MfaWebTestApplicationFactory factory = new(dirs, mockTwitch.EndpointOptions, fakeTime);

            IInstanceSettingsService settingsService = factory.Services.GetRequiredService<IInstanceSettingsService>();
            InstanceSettings settings = await settingsService.GetAsync(ct);
            settings.StepUpSudoPolicy = StepUpSudoPolicy.Required;
            await settingsService.UpdateAsync(settings, ct);

            PanelUser user = await SeedUserWithOAuthAsync(factory.Services, "twitch", "pk-user-1", "passkey_owner", isRoot: true, ct);
            IMfaCredentialStore credentialStore = factory.Services.GetRequiredService<IMfaCredentialStore>();

            // 1. Sign in initially (no MFA enrolled yet) and register a real P-256 passkey.
            using HttpClient enrollClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            enrollClient.DefaultRequestHeaders.Add("Origin", enrollClient.BaseAddress!.GetLeftPart(UriPartial.Authority));
            _ = await SignInViaMockTwitchAsync(enrollClient, mockTwitch, "pk-user-1", "passkey_owner", ct);

            using JsonContent createOptionsBody = JsonContent.Create(new { });
            HttpResponseMessage createOptionsResponse = await enrollClient.PostAsync(
                new Uri("/auth/mfa/passkey/create-options", UriKind.Relative),
                createOptionsBody,
                ct);
            _ = createOptionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using JsonDocument createOptionsDoc = JsonDocument.Parse(await createOptionsResponse.Content.ReadAsStringAsync(ct));

            string createChallenge = createOptionsDoc.RootElement.GetProperty("challenge").GetString()!;
            string rpId = createOptionsDoc.RootElement.GetProperty("rp").GetProperty("id").GetString()!;
            string userHandle = createOptionsDoc.RootElement.GetProperty("user").GetProperty("id").GetString()!;
            string origin = enrollClient.BaseAddress!.GetLeftPart(UriPartial.Authority);

            using ECDsa authenticatorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            byte[] credentialId = RandomNumberGenerator.GetBytes(32);

            string attestationCredentialJson = BuildPasskeyAttestationJson(
                credentialId,
                authenticatorKey,
                rpId,
                createChallenge,
                origin,
                signCount: 0);

            HttpResponseMessage registerResponse = await enrollClient.PostAsJsonAsync(
                new Uri("/auth/mfa/passkey/register", UriKind.Relative),
                new { credentialJson = attestationCredentialJson, name = "Hardware P-256 Key" },
                ct);
            string registerBody = await registerResponse.Content.ReadAsStringAsync(ct);
            _ = registerResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"register failed: {registerBody}");

            IReadOnlyList<PasskeyCredentialPayload> storedPasskeys = await credentialStore.GetPasskeysAsync(user.Id, ct);
            _ = storedPasskeys.Should().ContainSingle();
            _ = storedPasskeys[0].Name.Should().Be("Hardware P-256 Key");
            _ = storedPasskeys[0].SignCount.Should().Be(0u);

            // 2. Fresh browser session signs in via OAuth -> enters mfa-pending -> /permissions blocked.
            using HttpClient loginClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
            loginClient.DefaultRequestHeaders.Add("Origin", loginClient.BaseAddress!.GetLeftPart(UriPartial.Authority));
            HttpResponseMessage loginCallback = await SignInViaMockTwitchAsync(loginClient, mockTwitch, "pk-user-1", "passkey_owner", ct);
            _ = loginCallback.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = loginCallback.Headers.Location!.ToString().Should().Be("/auth/mfa");

            HttpResponseMessage blockedPermissions = await loginClient.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = blockedPermissions.StatusCode.Should().Be(HttpStatusCode.Redirect);
            _ = blockedPermissions.Headers.Location!.AbsolutePath.Should().Be("/access-denied");

            // 3. Request assertion options and sign real WebAuthn challenge with ECDSA P-256.
            using JsonContent requestOptionsBody = JsonContent.Create(new { });
            HttpResponseMessage requestOptionsResponse = await loginClient.PostAsync(
                new Uri("/auth/mfa/passkey/request-options", UriKind.Relative),
                requestOptionsBody,
                ct);
            _ = requestOptionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using JsonDocument requestOptionsDoc = JsonDocument.Parse(await requestOptionsResponse.Content.ReadAsStringAsync(ct));

            string assertionChallenge = requestOptionsDoc.RootElement.GetProperty("challenge").GetString()!;
            string assertionRpId = requestOptionsDoc.RootElement.GetProperty("rpId").GetString()!;

            string assertionCredentialJson = BuildPasskeyAssertionJson(
                credentialId,
                authenticatorKey,
                assertionRpId,
                assertionChallenge,
                origin,
                userHandle,
                signCount: 1);

            HttpResponseMessage verifyResponse = await loginClient.PostAsJsonAsync(
                new Uri("/auth/mfa/passkey/verify", UriKind.Relative),
                new { credentialJson = assertionCredentialJson, returnUrl = "/permissions" },
                ct);
            _ = verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Verify SignCount incremented, mfa-pending cleared, and 15-min step-up elevation granted.
            IReadOnlyList<PasskeyCredentialPayload> updatedPasskeys = await credentialStore.GetPasskeysAsync(user.Id, ct);
            _ = updatedPasskeys.Should().ContainSingle();
            _ = updatedPasskeys[0].SignCount.Should().Be(1u);

            HttpResponseMessage permissionsResponse = await loginClient.GetAsync(new Uri("/permissions", UriKind.Relative), ct);
            _ = permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            HttpResponseMessage adminAuthResponse = await loginClient.GetAsync(new Uri("/admin/auth", UriKind.Relative), ct);
            _ = adminAuthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            string adminHtml = await adminAuthResponse.Content.ReadAsStringAsync(ct);
            _ = adminHtml.Should().Contain("Instance Authentication Policies");
            _ = adminHtml.Should().NotContain("data-step-up-guard=\"true\"");
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

        // UP (0x01) | UV (0x04) | AT (0x40)
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
        uint signCount)
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
        // UP (0x01) | UV (0x04)
        authData[32] = 0x05;
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
        // kty = EC2 (2)
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        // alg = ES256 (-7)
        writer.WriteInt32(3);
        writer.WriteInt32(-7);
        // crv = P-256 (1)
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        // x coordinate
        writer.WriteInt32(-2);
        writer.WriteByteString(ecParams.Q.X!);
        // y coordinate
        writer.WriteInt32(-3);
        writer.WriteByteString(ecParams.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static async Task<PanelUser> SeedUserWithOAuthAsync(
        IServiceProvider services,
        string platformId,
        string platformUserId,
        string username,
        bool isRoot,
        CancellationToken ct)
    {
        IInstanceOAuthConfigService oauthService = services.GetRequiredService<IInstanceOAuthConfigService>();
        await oauthService.SetAsync(platformId, new PlatformOAuthAppCredentials("instance-cid", "instance-secret"), ct).ConfigureAwait(false);

        IPanelUserStore userStore = services.GetRequiredService<IPanelUserStore>();
        PanelUser user = await userStore.CreateUserAsync(
            new PanelUser
            {
                DisplayName = username,
                IsRoot = isRoot,
                IsGlobalAdmin = isRoot,
            },
            new UserLoginIdentity
            {
                ProviderId = platformId,
                ProviderUserId = platformUserId,
                ProviderUsername = username,
            },
            ct).ConfigureAwait(false);

        IWorkspaceStore wsStore = services.GetRequiredService<IWorkspaceStore>();
        _ = await wsStore.CreateWorkspaceAsync(
            new Workspace
            {
                OwnerUserId = user.Id,
                Name = $"{username}'s Workspace",
                PrimaryPlatformId = platformId,
            },
            new WorkspacePlatform
            {
                PlatformId = platformId,
                Role = PlatformRole.Primary,
                PlatformChannelId = platformUserId,
            },
            ct).ConfigureAwait(false);

        return user;
    }

    private static async Task<HttpResponseMessage> SignInViaMockTwitchAsync(
        HttpClient client,
        MockTwitchOAuthServer mockTwitch,
        string subject,
        string username,
        CancellationToken ct)
    {
        HttpResponseMessage loginRedirect = await client.GetAsync(new Uri("/auth/twitch/login?flow=login", UriKind.Relative), ct).ConfigureAwait(false);
        _ = loginRedirect.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(loginRedirect.Headers.Location!.Query);
        string state = query["state"].ToString();
        string nonce = query["nonce"].ToString();

        mockTwitch.IdTokenFactory = baseUrl => mockTwitch.CreateSignedIdToken(
            issuer: baseUrl,
            audience: "instance-cid",
            subject: subject,
            preferredUsername: username,
            nonce: nonce);

        return await client.GetAsync(
            new Uri($"/auth/twitch/callback?code=code-{Uri.EscapeDataString(subject)}&state={Uri.EscapeDataString(state)}", UriKind.Relative),
            ct).ConfigureAwait(false);
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
            this.Root = Path.Combine(Path.GetTempPath(), $"sa-mfa-test-{Guid.NewGuid():N}");
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

    private sealed class MfaWebTestApplicationFactory(
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
            _ = builder.UseSetting("Bootstrap:Node:InstanceId", $"mfa-test-{Guid.NewGuid():N}");
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

        public static async Task<MockTwitchOAuthServer> StartAsync(CancellationToken cancellationToken)
        {
            RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa) { KeyId = "mock-twitch-mfa-key" };

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

            holder = new MockTwitchOAuthServer(app, baseUrl, rsa, signingKey);
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

