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
using Microsoft.AspNetCore.Components.Web;
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

    [Fact]
    [Trait("Member", "StepUpMfaGuard")]
    public void StepUpMfaGuard_WhenZeroCredentialsAndForceReauthAllowed_RendersPlatformAndOidcReauthLinks()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Required);

        using BunitContext platformContext = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user, authProvider: "twitch"),
            policy,
            timeProvider);
        _ = platformContext.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore(hasAnyCredentials: false));
        NavigationManager platformNav = platformContext.Services.GetRequiredService<NavigationManager>();
        platformNav.NavigateTo("/admin/auth");

        IRenderedComponent<StepUpMfaGuard> platformGuard = platformContext.Render<StepUpMfaGuard>(parameters =>
            parameters.AddChildContent("<div data-protected-content=\"true\">Protected</div>"));

        _ = platformGuard.Markup.Should().Contain("data-step-up-reauth=\"true\"");
        _ = platformGuard.Markup.Should().Contain("/auth/twitch/login?flow=stepup&amp;returnUrl=%2Fadmin%2Fauth");
        _ = platformGuard.Markup.Should().Contain("Re-authenticate with Twitch");

        Guid wsId = Guid.NewGuid();
        using BunitContext oidcContext = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user, authProvider: "oidc:authentik"),
            policy,
            timeProvider);
        _ = oidcContext.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore(hasAnyCredentials: false));
        NavigationManager oidcNav = oidcContext.Services.GetRequiredService<NavigationManager>();
        oidcNav.NavigateTo("/workspace/platforms");

        IRenderedComponent<StepUpMfaGuard> oidcGuard = oidcContext.Render<StepUpMfaGuard>(parameters =>
            parameters
                .Add(p => p.WorkspaceId, wsId)
                .AddChildContent("<div data-protected-content=\"true\">Protected</div>"));

        _ = oidcGuard.Markup.Should().Contain("data-step-up-reauth=\"true\"");
        _ = oidcGuard.Markup.Should().Contain($"/auth/oidc/authentik/login?flow=stepup&amp;workspaceId={wsId:D}&amp;returnUrl=%2Fworkspace%2Fplatforms");
    }

    [Fact]
    [Trait("Member", "StepUpMfaGuard")]
    public void StepUpMfaGuard_WhenHasAnyCredentialsTrue_NeverRendersForceReauthLink()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Required);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user, authProvider: "twitch"),
            policy,
            timeProvider);
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore(hasAnyCredentials: true));

        IRenderedComponent<StepUpMfaGuard> component = context.Render<StepUpMfaGuard>(parameters =>
            parameters
                .Add(p => p.RequireEvenIfPolicyOptional, true)
                .AddChildContent("<div data-protected-content=\"true\">Protected</div>"));

        _ = component.Markup.Should().Contain("data-step-up-guard=\"true\"");
        _ = component.Markup.Should().NotContain("data-step-up-reauth=\"true\"");
    }

    [Fact]
    [Trait("Member", "StepUpMfaGuard")]
    public void StepUpMfaGuard_WhenPasskeyEnrolled_HidesTotpAndRecoveryBehindToggleButtonsAndClearsInputsOnSwitch()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Required);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user),
            policy,
            timeProvider);
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore(
            hasAnyCredentials: true,
            summaries: [new MfaCredentialSummary(Guid.NewGuid(), MfaMethod.Passkey, "YubiKey", timeProvider.GetUtcNow())]));

        IRenderedComponent<StepUpMfaGuard> component = context.Render<StepUpMfaGuard>(parameters =>
            parameters.AddChildContent("<div>Protected</div>"));

        _ = component.Find("form[data-step-up-totp='true']").HasAttribute("hidden").Should().BeTrue();
        _ = component.Find("form[data-step-up-recovery='true']").HasAttribute("hidden").Should().BeTrue();

        component.Find("[data-step-up-toggle-totp='true']").Click();
        _ = component.Find("form[data-step-up-totp='true']").HasAttribute("hidden").Should().BeFalse();
        _ = component.Find("form[data-step-up-recovery='true']").HasAttribute("hidden").Should().BeTrue();

        component.Find("#step-up-totp-code").Input("123456");
        _ = component.Find("#step-up-totp-code").GetAttribute("value").Should().Be("123456");

        component.Find("[data-step-up-toggle-recovery='true']").Click();
        _ = component.Find("form[data-step-up-totp='true']").HasAttribute("hidden").Should().BeTrue();
        _ = component.Find("form[data-step-up-recovery='true']").HasAttribute("hidden").Should().BeFalse();
        _ = component.Find("#step-up-totp-code").GetAttribute("value").Should().BeNullOrEmpty();

        component.Find("#step-up-recovery-code").Input("ABCDE-12345");
        _ = component.Find("#step-up-recovery-code").GetAttribute("value").Should().Be("ABCDE-12345");

        component.Find("[data-step-up-toggle-totp='true']").Click();
        _ = component.Find("form[data-step-up-totp='true']").HasAttribute("hidden").Should().BeFalse();
        _ = component.Find("form[data-step-up-recovery='true']").HasAttribute("hidden").Should().BeTrue();
        _ = component.Find("#step-up-recovery-code").GetAttribute("value").Should().BeNullOrEmpty();
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

    [Fact]
    [Trait("Member", "MfaChallenge")]
    public void MfaChallenge_WhenPasskeyAttached_HidesTotpAndRecoveryBehindToggleButtonsAndClearsInputsOnSwitch()
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
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore(
            hasAnyCredentials: true,
            summaries: [new MfaCredentialSummary(Guid.NewGuid(), MfaMethod.Passkey, "Hardware Key", timeProvider.GetUtcNow())]));

        IRenderedComponent<MfaChallenge> component = context.Render<MfaChallenge>();

        _ = component.Find("form[data-mfa-totp='true']").HasAttribute("hidden").Should().BeTrue();
        _ = component.Find("form[data-mfa-recovery='true']").HasAttribute("hidden").Should().BeTrue();

        component.Find("[data-mfa-toggle-totp='true']").Click();
        _ = component.Find("form[data-mfa-totp='true']").HasAttribute("hidden").Should().BeFalse();
        _ = component.Find("form[data-mfa-recovery='true']").HasAttribute("hidden").Should().BeTrue();

        component.Find("#mfa-totp-code").Input("654321");
        component.Find("[data-mfa-toggle-recovery='true']").Click();

        _ = component.Find("form[data-mfa-totp='true']").HasAttribute("hidden").Should().BeTrue();
        _ = component.Find("form[data-mfa-recovery='true']").HasAttribute("hidden").Should().BeFalse();
        _ = component.Find("#mfa-totp-code").GetAttribute("value").Should().BeNullOrEmpty();

        component.Find("#mfa-recovery-code").Input("ZZZZZ-99999");
        component.Find("[data-mfa-toggle-totp='true']").Click();

        _ = component.Find("form[data-mfa-totp='true']").HasAttribute("hidden").Should().BeFalse();
        _ = component.Find("form[data-mfa-recovery='true']").HasAttribute("hidden").Should().BeTrue();
        _ = component.Find("#mfa-recovery-code").GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    [Trait("Member", "MfaChallenge")]
    public void MfaChallenge_WhenNoPasskeyAttached_ShowsTotpImmediatelyAndHidesRecoveryInitially()
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
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore(
            hasAnyCredentials: true,
            summaries: [new MfaCredentialSummary(Guid.NewGuid(), MfaMethod.Totp, "Authenticator App", timeProvider.GetUtcNow())]));

        IRenderedComponent<MfaChallenge> component = context.Render<MfaChallenge>();

        _ = component.Find("form[data-mfa-totp='true']").HasAttribute("hidden").Should().BeFalse();
        _ = component.Find("form[data-mfa-recovery='true']").HasAttribute("hidden").Should().BeTrue();
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
            AuthClaims.BuildClaimsPrincipal(
                user,
                mfaPending: true,
                mfaEnrollmentRequired: true,
                mfaElevatedUntilUtc: timeProvider.GetUtcNow() + AuthClaims.StepUpWindow),
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

    [Fact]
    [Trait("Member", "AccountMfa")]
    public void AccountMfa_WhenNotStepUpElevatedEvenIfPolicyOptional_ShowsBannersAndMandatoryStepUpGuard()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new()
        {
            Id = Guid.NewGuid(),
            DisplayName = "streamer",
            MfaResetByAdminNoticePending = true,
        };
        EffectiveMfaPolicy optionalPolicy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Optional);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(user, authProvider: "twitch", mfaPending: true, mfaEnrollmentRequired: true),
            optionalPolicy,
            timeProvider);
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore(hasAnyCredentials: false));
        _ = context.Services.AddSingleton<IPanelUserStore>(new StubSingleUserStore(user));
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        NavigationManager nav = context.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/account/mfa?enrollRequired=true");

        IRenderedComponent<AccountMfa> component = context.Render<AccountMfa>();

        _ = component.Markup.Should().Contain("data-mfa-enroll-required=\"true\"");
        _ = component.Markup.Should().Contain("data-mfa-admin-reset-notice=\"true\"");
        _ = component.Markup.Should().Contain("data-step-up-guard=\"true\"");
        _ = component.Markup.Should().Contain("data-step-up-reauth=\"true\"");
        _ = component.Markup.Should().NotContain("data-passkey-enroll-section=\"true\"");
    }

    [Fact]
    [Trait("Member", "AccountMfa")]
    public void AccountMfa_WhenTotpEnrollmentBegins_RendersInlineSvgQrAndTotpIssuerName()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUser user = new() { Id = Guid.NewGuid(), DisplayName = "streamer" };
        EffectiveMfaPolicy policy = new(
            MfaMethodAvailability.Allowed,
            MfaMethodAvailability.Allowed,
            PanelLoginMfaRequirement.Optional,
            StepUpSudoPolicy.Optional);

        using BunitContext context = CreateAuthorizedContext(
            AuthClaims.BuildClaimsPrincipal(
                user,
                mfaElevatedUntilUtc: timeProvider.GetUtcNow() + AuthClaims.StepUpWindow),
            policy,
            timeProvider);
        _ = context.Services.AddSingleton<IMfaCredentialStore>(new StubMfaCredentialStore());
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings
        {
            InstanceName = "Studio Panel",
            TotpIssuerName = "My Custom Issuer",
        }));
        _ = context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        _ = context.JSInterop.Setup<AccountMfa.TotpEnrollBeginDto>("streamActionsWebAuthn.beginTotpEnrollment")
            .SetResult(new AccountMfa.TotpEnrollBeginDto
            {
                SecretBase32 = "JBSWY3DPEHPK3PXP",
                OtpAuthUri = "otpauth://totp/My%20Custom%20Issuer:streamer?secret=JBSWY3DPEHPK3PXP&issuer=My%20Custom%20Issuer",
                IssuerName = "My Custom Issuer",
            });

        IRenderedComponent<HeadOutlet> head = context.Render<HeadOutlet>();
        IRenderedComponent<AccountMfa> component = context.Render<AccountMfa>();

        component.Find("[data-totp-begin='true']").Click();

        _ = head.Markup.Should().Contain("<title>Security &amp; MFA - Studio Panel - StreamActions</title>");
        _ = component.Find("[data-totp-qr='true']").InnerHtml.Should().Contain("<svg ").And.Contain("<path d=\"");
        _ = component.Find("[data-totp-issuer='true']").TextContent.Should().Be("My Custom Issuer");
        _ = component.Find("[data-totp-secret='true']").TextContent.Should().Be("JBSWY3DPEHPK3PXP");
        _ = component.Find("[data-totp-uri='true']").TextContent.Should().Contain("otpauth://totp/");
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

    private sealed class StubMfaCredentialStore(
        bool hasAnyCredentials = false,
        IReadOnlyList<MfaCredentialSummary>? summaries = null) : IMfaCredentialStore
    {
        public ValueTask<bool> HasAnyCredentialsAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(hasAnyCredentials);

        public ValueTask<IReadOnlyList<MfaCredentialSummary>> GetSummariesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<MfaCredentialSummary>>(summaries ?? []);

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
        public IReadOnlyList<string> DefaultLoginScopes => ["openid", "user:read:email"];
        public IReadOnlyList<string> DefaultBroadcasterScopes => ["chat:read", "chat:edit"];
        public IReadOnlyList<string> DefaultBotScopes => ["user:bot", "chat:read"];
        public Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IReadOnlyCollection<string>? scopes, string state, bool forceVerify = false, string? nonce = null) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, Uri redirectUri, string? expectedNonce = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask RevokeTokenAsync(string clientId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// Minimal <see cref="IPanelUserStore"/> stub returning a single <see cref="PanelUser"/> for bUnit tests.
    /// </summary>
    /// <param name="user">The user record to return.</param>
    private sealed class StubSingleUserStore(PanelUser user) : IPanelUserStore
    {
        /// <inheritdoc />
        public ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);

        /// <inheritdoc />
        public ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(user.IsRoot);

        /// <inheritdoc />
        public ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(userId == user.Id ? user : null);

        /// <inheritdoc />
        public ValueTask<PanelUser?> GetByLoginIdentityAsync(string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PanelUser?>(null);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PanelUser>>([user]);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserLoginIdentity>>([]);

        /// <inheritdoc />
        public ValueTask<PanelUser> CreateUserAsync(PanelUser newUser, UserLoginIdentity? initialIdentity = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(newUser);

        /// <inheritdoc />
        public ValueTask<PanelUser> UpdateUserAsync(PanelUser updatedUser, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(updatedUser);

        /// <inheritdoc />
        public ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(UserLoginIdentity identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(identity);

        /// <inheritdoc />
        public ValueTask<bool> UnlinkLoginIdentityAsync(Guid panelUserId, string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class StubInstanceSettingsService(InstanceSettings settings) : IInstanceSettingsService
    {
        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged
        {
            add { }
            remove { }
        }

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(settings);

        public ValueTask UpdateAsync(InstanceSettings updated, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}

