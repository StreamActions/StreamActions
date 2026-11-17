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

using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Web.Components.Pages;
using StreamActions.Web.Components.Security;
using StreamActions.Web.Security;
using System.Security.Claims;
using Xunit;

namespace StreamActions.Web.Tests;

public sealed class MfaPagesTests
{
    #region StepUpMfaGuardComponent

    [Fact]
    [Trait("Member", "StepUpMfaGuard")]
    public void StepUpMfaGuard_WhenStepUpOptional_RendersChildContent()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Optional);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user),
            policy,
            timeProvider);

        IRenderedComponent<StepUpMfaGuard> component = context.Render<StepUpMfaGuard>(parameters =>
            parameters.AddChildContent("<div data-protected-content=\"true\">Protected Settings</div>"));

        _ = component.Markup.Should().Contain("Protected Settings");
        _ = component.Markup.Should().NotContain("data-step-up-guard=\"true\"");
    }

    [Fact]
    [Trait("Member", "StepUpMfaGuard")]
    public void StepUpMfaGuard_WhenStepUpRequiredAndElevated_RendersChildContentUntilWindowExpires()
    {
        DateTimeOffset t0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        ClaimsPrincipal elevatedPrincipal = AuthClaims.BuildClaimsPrincipal(
            user,
            mfaElevatedUntilUtc: t0 + AuthClaims.StepUpWindow);

        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.RequiredForRootAndStreamers,
            StepUpSudoPolicy.Required);

        // At t0 + 14m59s, step-up elevation is still active.
        timeProvider.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
        using BunitContext activeContext = CreateAuthorizedContext(elevatedPrincipal, policy, timeProvider);
        IRenderedComponent<StepUpMfaGuard> activeComponent = activeContext.Render<StepUpMfaGuard>(parameters =>
            parameters.AddChildContent("<div data-protected-content=\"true\">Protected Settings</div>"));

        _ = activeComponent.Markup.Should().Contain("Protected Settings");
        _ = activeComponent.Markup.Should().NotContain("data-step-up-guard=\"true\"");

        // At t0 + 15m01s, elevation has expired -> renders Sudo Mode guard.
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        using BunitContext expiredContext = CreateAuthorizedContext(elevatedPrincipal, policy, timeProvider);
        IRenderedComponent<StepUpMfaGuard> expiredComponent = expiredContext.Render<StepUpMfaGuard>(parameters =>
            parameters
                .Add(p => p.OperationDescription, "Verify MFA to rotate secrets.")
                .AddChildContent("<div data-protected-content=\"true\">Protected Settings</div>"));

        _ = expiredComponent.Markup.Should().NotContain("Protected Settings");
        _ = expiredComponent.Markup.Should().Contain("data-step-up-guard=\"true\"");
        _ = expiredComponent.Markup.Should().Contain("Verify MFA to rotate secrets.");
        _ = expiredComponent.Markup.Should().Contain("data-step-up-passkey=\"true\"");
        _ = expiredComponent.Markup.Should().Contain("data-step-up-totp=\"true\"");
        _ = expiredComponent.Markup.Should().Contain("action=\"/auth/mfa/totp/verify\"");
        _ = expiredComponent.Markup.Should().Contain("action=\"/auth/mfa/recovery/verify\"");
    }

    [Fact]
    [Trait("Member", "StepUpMfaGuard")]
    public void StepUpMfaGuard_WhenPasskeysOnly_HidesTotpFormAndShowsPasskeyAndRecovery()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy passkeysOnlyPolicy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Disabled,
            PanelLoginMfaRequirement.RequiredForRootAndStreamers,
            StepUpSudoPolicy.Required);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user),
            passkeysOnlyPolicy,
            timeProvider);

        IRenderedComponent<StepUpMfaGuard> component = context.Render<StepUpMfaGuard>(parameters =>
            parameters.AddChildContent("<div data-protected-content=\"true\">Protected Settings</div>"));

        _ = component.Markup.Should().NotContain("Protected Settings");
        _ = component.Markup.Should().Contain("data-step-up-guard=\"true\"");
        _ = component.Markup.Should().Contain("data-step-up-passkey=\"true\"");
        _ = component.Markup.Should().Contain("action=\"/auth/mfa/recovery/verify\"");
        _ = component.Markup.Should().NotContain("data-step-up-totp=\"true\"");
        _ = component.Markup.Should().NotContain("/auth/mfa/totp/verify");
    }

    #endregion

    #region MfaChallengeComponent

    [Fact]
    [Trait("Member", "MfaChallenge")]
    public void MfaChallenge_WhenBothMethodsAllowed_RendersPasskeyTotpAndRecoveryForms()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Optional);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user, mfaPending: true),
            policy,
            timeProvider);
        NavigationManager nav = context.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("returnUrl", "/permissions"));

        IRenderedComponent<MfaChallenge> component = context.Render<MfaChallenge>();

        _ = component.Markup.Should().Contain("data-mfa-passkey=\"true\"");
        _ = component.Markup.Should().Contain("data-mfa-totp=\"true\"");
        _ = component.Markup.Should().Contain("action=\"/auth/mfa/totp/verify\"");
        _ = component.Markup.Should().Contain("action=\"/auth/mfa/recovery/verify\"");
        _ = component.Markup.Should().Contain("value=\"/permissions\"");
    }

    [Fact]
    [Trait("Member", "MfaChallenge")]
    public void MfaChallenge_WhenPasskeysOnly_HidesTotpForm()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy passkeysOnlyPolicy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Disabled,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Optional);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user, mfaPending: true),
            passkeysOnlyPolicy,
            timeProvider);

        IRenderedComponent<MfaChallenge> component = context.Render<MfaChallenge>();

        _ = component.Markup.Should().Contain("data-mfa-passkey=\"true\"");
        _ = component.Markup.Should().Contain("action=\"/auth/mfa/recovery/verify\"");
        _ = component.Markup.Should().NotContain("data-mfa-totp=\"true\"");
        _ = component.Markup.Should().NotContain("/auth/mfa/totp/verify");
    }

    #endregion

    #region AccountMfaComponent

    [Fact]
    [Trait("Member", "AccountMfa")]
    public void AccountMfa_WhenEnrollRequiredAndPasskeysOnly_ShowsBannerAndHidesTotpSection()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy passkeysOnlyPolicy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Disabled,
            PanelLoginMfaRequirement.RequiredForRootAndStreamers,
            StepUpSudoPolicy.Optional);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user, mfaPending: true, mfaEnrollmentRequired: true),
            passkeysOnlyPolicy,
            timeProvider);
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore());
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        NavigationManager nav = context.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("enrollRequired", "true"));

        IRenderedComponent<AccountMfa> component = context.Render<AccountMfa>();

        _ = component.Markup.Should().Contain("data-mfa-enroll-required=\"true\"");
        _ = component.Markup.Should().Contain("data-passkey-enroll-section=\"true\"");
        _ = component.Markup.Should().NotContain("data-totp-enroll-section=\"true\"");
    }

    #endregion

    private static BunitContext CreateAuthorizedContext(
        ClaimsPrincipal principal,
        EffectiveMfaPolicy policy,
        TimeProvider timeProvider)
    {
        BunitContext context = new();
        _ = context.AddAuthorization()
            .SetAuthorized(principal.Identity?.Name ?? "user")
            .SetPolicies(AuthClaims.MfaPendingPolicy, AuthClaims.PanelAuthenticatedPolicy);

        _ = context.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthenticationStateProvider(principal));
        _ = context.Services.AddSingleton<IMfaPolicyService>(new StubMfaPolicyService(policy));
        _ = context.Services.AddSingleton(timeProvider);
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        return context;
    }

    private sealed class StubAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class StubAntiforgeryStateProvider : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken? GetAntiforgeryToken() =>
            new("test-antiforgery-token", "__RequestVerificationToken");
    }

    private sealed class StubMfaPolicyService(EffectiveMfaPolicy policy) : IMfaPolicyService
    {
        public EffectiveMfaPolicy ComputeEffectivePolicy(InstanceSettings instanceSettings, Workspace? workspace = null) =>
            policy;

        public ValueTask<EffectiveMfaPolicy> GetEffectivePolicyAsync(Guid? workspaceId = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy);

        public ValueTask<EffectiveMfaPolicy> GetEffectivePolicyForUserAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            Guid? workspaceId = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy);

        public ValueTask<bool> IsLoginMfaRequiredAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            Guid? workspaceId = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class StubMfaCredentialStore : IMfaCredentialStore
    {
        public ValueTask<bool> HasAnyCredentialsAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<IReadOnlyList<MfaCredentialSummary>> GetSummariesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<MfaCredentialSummary>>([]);

        public ValueTask<Guid> SaveTotpAsync(Guid panelUserId, TotpSecretPayload payload, IReadOnlyList<string> recoveryCodeHashes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<TotpSecretPayload?> GetTotpAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TotpSecretPayload?>(null);

        public ValueTask<bool> VerifyAndConsumeRecoveryCodeAsync(Guid panelUserId, string code, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<int> GetRemainingRecoveryCodesCountAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);

        public ValueTask UpsertPasskeyAsync(Guid panelUserId, PasskeyCredentialPayload passkey, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<PasskeyCredentialPayload>> GetPasskeysAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PasskeyCredentialPayload>>([]);

        public ValueTask<PasskeyCredentialPayload?> FindPasskeyAsync(Guid panelUserId, byte[] credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PasskeyCredentialPayload?>(null);

        public ValueTask<(Guid PanelUserId, PasskeyCredentialPayload Passkey)?> FindPasskeyByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<(Guid, PasskeyCredentialPayload)?>(null);

        public ValueTask<bool> RemovePasskeyAsync(Guid panelUserId, byte[] credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<bool> DeleteCredentialAsync(Guid panelUserId, Guid credentialId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<int> ClearAllForUserAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
    }

    private sealed class StubPlatformOAuthProvider(string platformId) : IPlatformOAuthProvider
    {
        public string PlatformId { get; } = platformId;
        public Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IReadOnlyCollection<string>? scopes, string state, bool forceVerify = false, string? nonce = null) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, Uri redirectUri, string? expectedNonce = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask RevokeTokenAsync(string clientId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

