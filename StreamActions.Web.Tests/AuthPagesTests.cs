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
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Web.Components.Layout;
using StreamActions.Web.Components.Pages;
using StreamActions.Web.Security;
using System.Security.Claims;
using Xunit;
using XunitTestContext = Xunit.TestContext;

namespace StreamActions.Web.Tests;

public sealed class AuthPagesTests
{
    #region LoginComponent

    [Fact]
    [Trait("Member", "Login")]
    public void Login_WhenDefaultPolicies_RendersPlatformOAuthOidcSsoAndAdminSetupLink()
    {
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings
        {
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceOnly),
            ExternalOidcLoginMode = ExternalOidcLoginMode.Allowed,
        }));
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore(
        [
            new ExternalOidcProvider
            {
                Id = Guid.NewGuid(),
                Slug = "authentik",
                DisplayName = "Authentik",
                AuthorityUrl = "https://sso.example.com",
                ClientId = "cid",
                IsEnabled = true,
            },
        ]));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<Login> component = context.Render<Login>();

        _ = component.Markup.Should().Contain("/auth/twitch/login?flow=login");
        _ = component.Markup.Should().Contain("/auth/oidc/authentik/login");
        _ = component.Markup.Should().Contain("/setup-recovery");
        _ = component.Markup.Should().Contain("Admin Setup / Recovery");
    }

    [Fact]
    [Trait("Member", "Login")]
    public void Login_WhenRequireStreamerOAuthApp_RendersInterstitialPostForm()
    {
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings
        {
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.RequireStreamerOAuthApp),
            ExternalOidcLoginMode = ExternalOidcLoginMode.Disabled,
        }));
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<Login> component = context.Render<Login>();

        _ = component.Markup.Should().Contain("action=\"/auth/twitch/login\"");
        _ = component.Markup.Should().Contain("name=\"identifier\"");
        _ = component.Markup.Should().Contain("<details");
        _ = component.Markup.Should().Contain("First-time channel setup: Enter OAuth App credentials");
        _ = component.Markup.Should().Contain("name=\"customClientId\"");
        _ = component.Markup.Should().Contain("name=\"customClientSecret\"");
        _ = component.Markup.Should().Contain("name=\"customRedirectUri\"");
        _ = component.Markup.Should().Contain("Sign in with a Passkey");
        _ = component.Markup.Should().NotContain("External SSO");
    }

    [Fact]
    [Trait("Member", "Login")]
    public void Login_WhenPasskeyDisabledOrOidcRequired_HidesPasskeyFirstButton()
    {
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings
        {
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceOnly),
            ExternalOidcLoginMode = ExternalOidcLoginMode.Allowed,
            MfaPasskeyAvailability = MfaMethodAvailability.Disabled,
        }));
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<Login> component = context.Render<Login>();

        _ = component.Markup.Should().NotContain("Sign in with a Passkey");
    }

    [Fact]
    [Trait("Member", "Login")]
    public void Login_WhenLoginHintCookiePresent_RendersContinueAsAndUseDifferentAccountLink()
    {
        EphemeralDataProtectionProvider dataProtection = new();
        DefaultHttpContext httpContext = new();
        AuthClaims.WriteLoginHintCookie(
            httpContext,
            dataProtection,
            new LoginHintPayload(Guid.NewGuid(), Guid.NewGuid(), "twitch", "RememberedStreamer"));

        string setCookie = httpContext.Response.Headers.SetCookie.ToString();
        string cookieValue = setCookie.Split(';', 2)[0].Split('=', 2)[1];
        httpContext.Request.Headers.Cookie = $"{AuthClaims.LoginHintCookieName}={cookieValue}";

        using BunitContext context = new();
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings
        {
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.RequireStreamerOAuthApp),
            ExternalOidcLoginMode = ExternalOidcLoginMode.Disabled,
        }));
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());
        _ = context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        _ = context.Services.AddSingleton<IDataProtectionProvider>(dataProtection);

        IRenderedComponent<Login> component = context.Render<Login>();

        _ = component.Markup.Should().Contain("Continue as RememberedStreamer");
        _ = component.Markup.Should().Contain("name=\"useLoginHint\" value=\"true\"");
        _ = component.Markup.Should().Contain("Use a different account");
    }

    [Fact]
    [Trait("Member", "Login")]
    public void Login_WhenMixedPerPlatformPoliciesAndCustomInstanceName_RendersPerPlatformUiAndDynamicPageTitle()
    {
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("kick"));

        InstanceSettings settings = new()
        {
            InstanceName = "CreatorHub",
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth),
            ExternalOidcLoginMode = ExternalOidcLoginMode.Disabled,
        };
        AuthPolicyParser.SetPlatformOAuthAppPolicy(settings, "kick", InstanceOAuthAppPolicy.RequireStreamerOAuthApp);

        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(settings));
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());
        _ = context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);

        IRenderedComponent<HeadOutlet> head = context.Render<HeadOutlet>();
        IRenderedComponent<Login> component = context.Render<Login>();

        _ = component.Markup.Should().Contain("/auth/twitch/login?flow=login");
        _ = component.Markup.Should().Contain("action=\"/auth/kick/login\"");
        _ = component.Markup.Should().NotContain("action=\"/auth/twitch/login\"");
        _ = head.Markup.Should().Contain("<title>Sign In - CreatorHub - StreamActions</title>");
    }

    #endregion

    #region HomeComponent

    [Theory]
    [Trait("Member", "Home")]
    [InlineData("CreatorHub", "CreatorHub - StreamActions", "CreatorHub")]
    [InlineData("StreamActions", "StreamActions", "StreamActions")]
    [InlineData("", "StreamActions", "StreamActions")]
    public void Home_WhenInstanceNameConfigured_RendersFormattedPageTitleAndHeading(
        string configuredInstanceName,
        string expectedTitle,
        string expectedHeading)
    {
        using BunitContext context = new();
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings
        {
            InstanceName = configuredInstanceName,
        }));
        _ = context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);

        IRenderedComponent<HeadOutlet> head = context.Render<HeadOutlet>();
        IRenderedComponent<Home> component = context.Render<Home>();

        _ = head.Markup.Should().Contain($"<title>{expectedTitle}</title>");
        _ = component.Find("h1").TextContent.Should().Be(expectedHeading);
    }

    #endregion

    #region AccessDeniedComponent

    [Fact]
    [Trait("Member", "AccessDenied")]
    public void AccessDenied_RendersExpectedMessage()
    {
        using BunitContext context = new();

        IRenderedComponent<AccessDenied> component = context.Render<AccessDenied>();

        _ = component.Markup.Should().Contain("Access Denied — Ask a channel owner to grant your account panel access");
    }

    #endregion

    #region AdminAuthComponent

    [Fact]
    [Trait("Member", "AdminAuth")]
    public void AdminAuth_WriteOnlySecretFields_NeverExposeExistingSecretsAndCanSaveUpdates()
    {
        const string existingTwitchSecret = "top-secret-twitch-client-secret";
        const string existingOidcSecret = "top-secret-oidc-client-secret";

        using BunitContext context = new();
        _ = context.AddAuthorization().SetAuthorized("root").SetPolicies("RootOrGlobalAdmin");

        StubInstanceSettingsService settingsService = new(new InstanceSettings
        {
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceOnly),
            InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.DefaultOnly),
            ExternalOidcLoginMode = ExternalOidcLoginMode.Allowed,
            AllowOpenRegistration = false,
            StepUpSudoPolicy = StepUpSudoPolicy.Optional,
        });
        StubInstanceOAuthConfigService oauthService = new();
        oauthService.Stored["twitch"] = new PlatformOAuthAppCredentials("existing-twitch-cid", existingTwitchSecret);

        Guid oidcId = Guid.NewGuid();
        StubOidcProviderStore oidcStore = new(
        [
            new ExternalOidcProvider
            {
                Id = oidcId,
                Slug = "authentik",
                DisplayName = "Authentik",
                AuthorityUrl = "https://sso.example.com",
                ClientId = "existing-oidc-cid",
                EncryptedClientSecret = existingOidcSecret,
                ScopesJson = "[\"openid\",\"profile\"]",
                GroupClaimType = "groups",
                IsEnabled = true,
            },
        ]);

        _ = context.Services.AddSingleton<IInstanceSettingsService>(settingsService);
        _ = context.Services.AddSingleton<IInstanceOAuthConfigService>(oauthService);
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(oidcStore);
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IMfaPolicyService>(new StubMfaPolicyService(settingsService));
        _ = context.Services.AddSingleton(TimeProvider.System);

        IRenderedComponent<AdminAuth> component = context.Render<AdminAuth>();

        _ = component.Markup.Should().Contain("existing-twitch-cid");
        _ = component.Markup.Should().Contain("authentik");
        _ = component.Markup.Should().NotContain(existingTwitchSecret);
        _ = component.Markup.Should().NotContain(existingOidcSecret);

        component.Find("#allow-open-registration").Change(true);
        component.Find("#oidc-login-mode").Change(nameof(ExternalOidcLoginMode.Required));
        component.Find("button:contains('Save Policies')").Click();

        _ = settingsService.Current.AllowOpenRegistration.Should().BeTrue();
        _ = settingsService.Current.ExternalOidcLoginMode.Should().Be(ExternalOidcLoginMode.Required);

        component.Find("#platform-client-secret").Change("brand-new-twitch-secret");
        component.Find("button:contains('Save Platform OAuth')").Click();

        _ = oauthService.Stored["twitch"].ClientSecret.Should().Be("brand-new-twitch-secret");
        _ = component.Markup.Should().NotContain("brand-new-twitch-secret");
    }

    [Fact]
    [Trait("Member", "AdminAuth")]
    public void AdminAuth_CanUpdateInstanceNameTotpIssuerAndPerPlatformOAuthPolicies()
    {
        using BunitContext context = new();
        _ = context.AddAuthorization().SetAuthorized("root").SetPolicies("RootOrGlobalAdmin");

        StubInstanceSettingsService settingsService = new(new InstanceSettings
        {
            InstanceName = "StreamActions",
            TotpIssuerName = "StreamActions",
            InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceOnly),
            InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.DefaultOnly),
            StepUpSudoPolicy = StepUpSudoPolicy.Optional,
        });

        _ = context.Services.AddSingleton<IInstanceSettingsService>(settingsService);
        _ = context.Services.AddSingleton<IInstanceOAuthConfigService>(new StubInstanceOAuthConfigService());
        _ = context.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = context.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("kick"));
        _ = context.Services.AddSingleton<IMfaPolicyService>(new StubMfaPolicyService(settingsService));
        _ = context.Services.AddSingleton(TimeProvider.System);
        _ = context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);

        IRenderedComponent<HeadOutlet> head = context.Render<HeadOutlet>();
        IRenderedComponent<AdminAuth> component = context.Render<AdminAuth>();

        _ = head.Markup.Should().Contain("<title>Authentication Administration - StreamActions</title>");
        _ = component.Find("#oauth-app-policy").InnerHtml.Should().Contain(nameof(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth));

        component.Find("#instance-name").Change("My Bot Panel");
        component.Find("#totp-issuer-name").Change("My Bot Auth");
        component.Find("#oauth-app-policy").Change(nameof(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth));
        component.Find("#platform-oauth-policy-kick").Change(nameof(InstanceOAuthAppPolicy.RequireStreamerOAuthApp));
        component.Find("button:contains('Save Policies')").Click();

        _ = settingsService.Current.InstanceName.Should().Be("My Bot Panel");
        _ = settingsService.Current.TotpIssuerName.Should().Be("My Bot Auth");
        _ = AuthPolicyParser.ParseOAuthAppPolicy(settingsService.Current.InstanceOAuthAppPolicy)
            .Should().Be(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth);
        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settingsService.Current, "twitch")
            .Should().Be(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth);
        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settingsService.Current, "kick")
            .Should().Be(InstanceOAuthAppPolicy.RequireStreamerOAuthApp);
        _ = head.Markup.Should().Contain("<title>Authentication Administration - My Bot Panel - StreamActions</title>");

        // Resetting platform override to Use Instance Default removes the per-platform entry.
        component.Find("#platform-oauth-policy-kick").Change(string.Empty);
        component.Find("button:contains('Save Policies')").Click();

        _ = AuthPolicyParser.TryGetPlatformOAuthAppPolicyOverride(settingsService.Current, "kick", out _).Should().BeFalse();
        _ = AuthPolicyParser.ResolveOAuthAppPolicy(settingsService.Current, "kick")
            .Should().Be(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth);
    }

    [Fact]
    [Trait("Member", "AdminAuth")]
    public void AdminAuth_PanelUsersMfaManagement_AllowsRootToResetAnyAndBlocksGlobalAdminFromResettingRoot()
    {
        PanelUser rootUser = new() { Id = Guid.NewGuid(), DisplayName = "RootOperator", IsRoot = true, IsGlobalAdmin = true };
        PanelUser adminUser = new() { Id = Guid.NewGuid(), DisplayName = "GlobalAdmin", IsRoot = false, IsGlobalAdmin = true };
        PanelUser modUser = new() { Id = Guid.NewGuid(), DisplayName = "ModUser", IsRoot = false, IsGlobalAdmin = false };

        StubPanelUserStore userStore = new([rootUser, adminUser, modUser]);
        StubAdminMfaCredentialStore mfaStore = new();
        mfaStore.EnrolledUsers[rootUser.Id] = 1;
        mfaStore.EnrolledUsers[modUser.Id] = 2;

        // 1. GlobalAdmin sees Remove MFA for ModUser, but NOT for RootOperator.
        using BunitContext adminContext = new();
        _ = adminContext.AddAuthorization().SetAuthorized("GlobalAdmin").SetPolicies(AuthClaims.RootOrGlobalAdminPolicy);
        _ = adminContext.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthStateProvider(
            AuthClaims.BuildClaimsPrincipal(adminUser, mfaElevatedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(10))));
        StubInstanceSettingsService adminSettings = new(new InstanceSettings());
        _ = adminContext.Services.AddSingleton<IInstanceSettingsService>(adminSettings);
        _ = adminContext.Services.AddSingleton<IInstanceOAuthConfigService>(new StubInstanceOAuthConfigService());
        _ = adminContext.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = adminContext.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = adminContext.Services.AddSingleton<IMfaPolicyService>(new StubMfaPolicyService(adminSettings));
        _ = adminContext.Services.AddSingleton<IPanelUserStore>(userStore);
        _ = adminContext.Services.AddSingleton<IMfaCredentialStore>(mfaStore);
        _ = adminContext.Services.AddSingleton(TimeProvider.System);

        IRenderedComponent<AdminAuth> adminComponent = adminContext.Render<AdminAuth>();
        _ = adminComponent.Markup.Should().Contain("data-admin-users-mfa=\"true\"");
        _ = adminComponent.Markup.Should().Contain($"data-admin-reset-mfa=\"{modUser.Id:D}\"");
        _ = adminComponent.Markup.Should().NotContain($"data-admin-reset-mfa=\"{rootUser.Id:D}\"");

        adminComponent.Find($"[data-admin-reset-mfa='{modUser.Id:D}']").Click();
        _ = mfaStore.EnrolledUsers.ContainsKey(modUser.Id).Should().BeFalse();
        _ = modUser.MfaResetByAdminNoticePending.Should().BeTrue();

        // 2. RootOperator CAN remove MFA for RootOperator as well.
        using BunitContext rootContext = new();
        _ = rootContext.AddAuthorization().SetAuthorized("RootOperator").SetPolicies(AuthClaims.RootOrGlobalAdminPolicy);
        _ = rootContext.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthStateProvider(
            AuthClaims.BuildClaimsPrincipal(rootUser, mfaElevatedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(10))));
        StubInstanceSettingsService rootSettings = new(new InstanceSettings());
        _ = rootContext.Services.AddSingleton<IInstanceSettingsService>(rootSettings);
        _ = rootContext.Services.AddSingleton<IInstanceOAuthConfigService>(new StubInstanceOAuthConfigService());
        _ = rootContext.Services.AddSingleton<IExternalOidcProviderStore>(new StubOidcProviderStore([]));
        _ = rootContext.Services.AddSingleton<IPlatformOAuthProvider>(new StubPlatformOAuthProvider("twitch"));
        _ = rootContext.Services.AddSingleton<IMfaPolicyService>(new StubMfaPolicyService(rootSettings));
        _ = rootContext.Services.AddSingleton<IPanelUserStore>(userStore);
        _ = rootContext.Services.AddSingleton<IMfaCredentialStore>(mfaStore);
        _ = rootContext.Services.AddSingleton(TimeProvider.System);

        IRenderedComponent<AdminAuth> rootComponent = rootContext.Render<AdminAuth>();
        _ = rootComponent.Markup.Should().Contain($"data-admin-reset-mfa=\"{rootUser.Id:D}\"");
        rootComponent.Find($"[data-admin-reset-mfa='{rootUser.Id:D}']").Click();
        _ = mfaStore.EnrolledUsers.ContainsKey(rootUser.Id).Should().BeFalse();
        _ = rootUser.MfaResetByAdminNoticePending.Should().BeTrue();
    }

    #endregion

    #region MainLayoutComponent

    [Fact]
    [Trait("Member", "MainLayout")]
    public async Task MainLayout_DisplaysInstanceNameUpdatesOnSettingsChangedAndUnsubscribesOnDispose()
    {
        using BunitContext context = new();
        _ = context.AddAuthorization().SetAuthorized("streamer").SetPolicies(AuthClaims.PanelAuthenticatedPolicy);

        StubInstanceSettingsService settingsService = new(new InstanceSettings
        {
            InstanceName = "InitialBrand",
        });

        _ = context.Services.AddSingleton<IInstanceSettingsService>(settingsService);
        _ = context.Services.AddSingleton<IWorkspaceSessionContext>(new StubWorkspaceSessionContext());
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<MainLayout> component = context.Render<MainLayout>();
        _ = component.Find("[data-instance-name='true']").TextContent.Should().Be("InitialBrand");
        _ = settingsService.SettingsChangedSubscriberCount.Should().Be(1);

        await settingsService.UpdateAsync(new InstanceSettings { InstanceName = "UpdatedBrand" }, XunitTestContext.Current.CancellationToken);

        component.WaitForAssertion(() =>
            _ = component.Find("[data-instance-name='true']").TextContent.Should().Be("UpdatedBrand"));

        component.Instance.Dispose();
        _ = settingsService.SettingsChangedSubscriberCount.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "MainLayout")]
    public void MainLayout_WhenWorkspaceRequiresReauthentication_RendersMutedReauthenticateOption()
    {
        using BunitContext context = new();
        _ = context.AddAuthorization().SetAuthorized("streamer").SetPolicies(AuthClaims.PanelAuthenticatedPolicy);

        Workspace readyWs = new() { Id = Guid.NewGuid(), OwnerUserId = Guid.NewGuid(), Name = "Active Channel", Slug = "active", PrimaryPlatformId = "twitch" };
        Workspace oidcWs = new() { Id = Guid.NewGuid(), OwnerUserId = Guid.NewGuid(), Name = "SSO Channel", Slug = "sso", PrimaryPlatformId = "twitch" };
        Workspace mfaWs = new() { Id = Guid.NewGuid(), OwnerUserId = Guid.NewGuid(), Name = "MFA Channel", Slug = "mfa", PrimaryPlatformId = "kick" };

        StubWorkspaceSessionContext sessionContext = new(
            readyWs.Id,
            [
                new AccessibleWorkspace(readyWs, true, [], WorkspaceSessionAuthStatus.Ready),
                new AccessibleWorkspace(oidcWs, false, [], WorkspaceSessionAuthStatus.RequiresOidcReauth, "corp-sso"),
                new AccessibleWorkspace(mfaWs, false, [], WorkspaceSessionAuthStatus.RequiresMfaChallenge),
            ]);

        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings()));
        _ = context.Services.AddSingleton<IWorkspaceSessionContext>(sessionContext);
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<MainLayout> component = context.Render<MainLayout>();

        _ = component.Markup.Should().Contain("Active Channel (twitch)</option>");
        _ = component.Markup.Should().Contain("SSO Channel (twitch) (reauthenticate)");
        _ = component.Markup.Should().Contain("MFA Channel (kick) (reauthenticate)");
        _ = component.FindAll("option.workspace-option-reauth").Should().HaveCount(2);
        _ = component.FindAll("option[disabled]").Should().BeEmpty();
    }

    [Fact]
    [Trait("Member", "MainLayout")]
    public void MainLayout_WhenMfaResetByAdminNoticePending_RendersAlertBannerWithAccountMfaLink()
    {
        PanelUser resetUser = new()
        {
            Id = Guid.NewGuid(),
            DisplayName = "streamer",
            MfaResetByAdminNoticePending = true,
        };

        using BunitContext context = new();
        _ = context.AddAuthorization().SetAuthorized("streamer").SetPolicies(AuthClaims.PanelAuthenticatedPolicy);
        _ = context.Services.AddSingleton<AuthenticationStateProvider>(new StubAuthStateProvider(
            AuthClaims.BuildClaimsPrincipal(resetUser)));
        _ = context.Services.AddSingleton<IPanelUserStore>(new StubPanelUserStore([resetUser]));
        _ = context.Services.AddSingleton<IInstanceSettingsService>(new StubInstanceSettingsService(new InstanceSettings()));
        _ = context.Services.AddSingleton<IWorkspaceSessionContext>(new StubWorkspaceSessionContext());
        _ = context.Services.AddSingleton<AntiforgeryStateProvider>(new StubAntiforgeryStateProvider());

        IRenderedComponent<MainLayout> component = context.Render<MainLayout>();

        _ = component.Markup.Should().Contain("data-mfa-admin-reset-notice=\"true\"");
        _ = component.Markup.Should().Contain("Your multi-factor authentication credentials were removed by an instance administrator.");
        _ = component.Markup.Should().Contain("href=\"/account/mfa\"");

        resetUser.MfaResetByAdminNoticePending = false;
        IRenderedComponent<MainLayout> clearedComponent = context.Render<MainLayout>();
        _ = clearedComponent.Markup.Should().NotContain("data-mfa-admin-reset-notice=\"true\"");
    }

    #endregion

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

    private sealed class StubInstanceSettingsService(InstanceSettings initial) : IInstanceSettingsService
    {
        private EventHandler<InstanceSettingsChangedEventArgs>? _settingsChanged;

        public InstanceSettings Current { get; private set; } = initial;
        public int SettingsChangedSubscriberCount => this._settingsChanged?.GetInvocationList().Length ?? 0;

        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged
        {
            add => this._settingsChanged += value;
            remove => this._settingsChanged -= value;
        }

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Current);

        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default)
        {
            this.Current = settings;
            this._settingsChanged?.Invoke(this, new InstanceSettingsChangedEventArgs(settings));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubWorkspaceSessionContext(
        Guid? activeWorkspaceId = null,
        IReadOnlyList<AccessibleWorkspace>? workspaces = null) : IWorkspaceSessionContext
    {
        public IReadOnlyList<AccessibleWorkspace> AccessibleWorkspaces { get; } = workspaces ?? [];
        public Guid? ActiveWorkspaceId { get; } = activeWorkspaceId;
        public AccessibleWorkspace? ActiveWorkspace => this.AccessibleWorkspaces.FirstOrDefault(w => w.Workspace.Id == this.ActiveWorkspaceId);
        public event EventHandler? WorkspaceChanged
        {
            add { }
            remove { }
        }

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RefreshAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<bool> SelectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class StubInstanceOAuthConfigService : IInstanceOAuthConfigService
    {
        public Dictionary<string, PlatformOAuthAppCredentials> Stored { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ValueTask<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>>(this.Stored);

        public ValueTask<PlatformOAuthAppCredentials?> GetAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Stored.GetValueOrDefault(platformId));

        public ValueTask SetAsync(string platformId, PlatformOAuthAppCredentials credentials, CancellationToken cancellationToken = default)
        {
            this.Stored[platformId] = credentials;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Stored.Remove(platformId));
    }

    private sealed class StubOidcProviderStore(List<ExternalOidcProvider> providers) : IExternalOidcProviderStore
    {
        private readonly List<ExternalOidcProvider> _providers = providers;

        public ValueTask<ExternalOidcProvider?> GetByIdAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._providers.FirstOrDefault(p => p.Id == providerId));

        public ValueTask<ExternalOidcProvider?> GetBySlugAsync(Guid? workspaceId, string slug, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._providers.FirstOrDefault(p => p.WorkspaceId == workspaceId && string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase)));

        public ValueTask<IReadOnlyList<ExternalOidcProvider>> GetProvidersAsync(Guid? workspaceId, bool enabledOnly = false, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<ExternalOidcProvider>>([.. this._providers
                .Where(p => p.WorkspaceId == workspaceId && (!enabledOnly || p.IsEnabled))]);

        public ValueTask<ExternalOidcProvider> UpsertAsync(ExternalOidcProvider provider, string? plaintextClientSecret = null, CancellationToken cancellationToken = default)
        {
            if (provider.Id == Guid.Empty)
            {
                provider.Id = Guid.NewGuid();
            }

            _ = this._providers.RemoveAll(p => p.Id == provider.Id);
            this._providers.Add(provider);
            return ValueTask.FromResult(provider);
        }

        public ValueTask<(bool Success, string? ClientSecret, CredentialStatus Status)> TryGetClientSecretAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<(bool, string?, CredentialStatus)>((false, null, CredentialStatus.Active));

        public ValueTask<bool> DeleteAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._providers.RemoveAll(p => p.Id == providerId) > 0);
    }

    private sealed class StubAntiforgeryStateProvider : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken? GetAntiforgeryToken() =>
            new("test-antiforgery-token", "__RequestVerificationToken");
    }

    private sealed class StubMfaPolicyService(StubInstanceSettingsService settingsService) : IMfaPolicyService
    {
        public EffectiveMfaPolicy ComputeEffectivePolicy(InstanceSettings instanceSettings, Workspace? workspace = null) =>
            new(
                instanceSettings.MfaPasskeyAvailability,
                instanceSettings.MfaTotpAvailability,
                instanceSettings.LoginMfaRequirement,
                instanceSettings.StepUpSudoPolicy);

        public ValueTask<EffectiveMfaPolicy> GetEffectivePolicyAsync(Guid? workspaceId = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.ComputeEffectivePolicy(settingsService.Current));

        public ValueTask<EffectiveMfaPolicy> GetEffectivePolicyForUserAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            Guid? workspaceId = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.ComputeEffectivePolicy(settingsService.Current));

        public ValueTask<bool> IsLoginMfaRequiredAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            Guid? workspaceId = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    /// <summary>
    /// Stub <see cref="AuthenticationStateProvider"/> for bUnit tests.
    /// </summary>
    /// <param name="principal">The claims principal to return.</param>
    private sealed class StubAuthStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        /// <inheritdoc />
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    /// <summary>
    /// Stub <see cref="IPanelUserStore"/> for admin MFA management and layout notice tests.
    /// </summary>
    /// <param name="users">The initial user list.</param>
    private sealed class StubPanelUserStore(List<PanelUser> users) : IPanelUserStore
    {
        /// <summary>
        /// In-memory user collection.
        /// </summary>
        private readonly List<PanelUser> _users = users;

        /// <inheritdoc />
        public ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this._users.Count > 0);

        /// <inheritdoc />
        public ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this._users.Any(static u => u.IsRoot));

        /// <inheritdoc />
        public ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._users.FirstOrDefault(u => u.Id == userId));

        /// <inheritdoc />
        public ValueTask<PanelUser?> GetByLoginIdentityAsync(string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PanelUser?>(null);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PanelUser>>(this._users);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserLoginIdentity>>([]);

        /// <inheritdoc />
        public ValueTask<PanelUser> CreateUserAsync(PanelUser user, UserLoginIdentity? initialIdentity = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(user);

        /// <inheritdoc />
        public ValueTask<PanelUser> UpdateUserAsync(PanelUser user, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(user);

        /// <inheritdoc />
        public ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(UserLoginIdentity identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(identity);

        /// <inheritdoc />
        public ValueTask<bool> UnlinkLoginIdentityAsync(Guid panelUserId, string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    /// <summary>
    /// Stub <see cref="IMfaCredentialStore"/> tracking enrolled credential counts per user for admin reset tests.
    /// </summary>
    private sealed class StubAdminMfaCredentialStore : IMfaCredentialStore
    {
        /// <summary>
        /// Gets the mutable dictionary of credential counts keyed by user identifier.
        /// </summary>
        public Dictionary<Guid, int> EnrolledUsers { get; } = [];

        /// <inheritdoc />
        public ValueTask<bool> HasAnyCredentialsAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.EnrolledUsers.TryGetValue(panelUserId, out int count) && count > 0);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<MfaCredentialSummary>> GetSummariesAsync(Guid panelUserId, CancellationToken cancellationToken = default)
        {
            if (!this.EnrolledUsers.TryGetValue(panelUserId, out int count) || count <= 0)
            {
                return ValueTask.FromResult<IReadOnlyList<MfaCredentialSummary>>([]);
            }

            List<MfaCredentialSummary> list = new(count);
            for (int index = 0; index < count; index++)
            {
                list.Add(new MfaCredentialSummary(Guid.NewGuid(), MfaMethod.Totp, $"Factor {index + 1}", DateTimeOffset.UtcNow));
            }

            return ValueTask.FromResult<IReadOnlyList<MfaCredentialSummary>>(list);
        }

        /// <inheritdoc />
        public ValueTask<int> ClearAllForUserAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.EnrolledUsers.Remove(panelUserId, out int removed) ? removed : 0);

        /// <inheritdoc />
        public ValueTask<Guid> SaveTotpAsync(Guid panelUserId, TotpSecretPayload payload, IReadOnlyList<string> recoveryCodeHashes, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<TotpSecretPayload?> GetTotpAsync(Guid panelUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> VerifyAndConsumeRecoveryCodeAsync(Guid panelUserId, string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<int> GetRemainingRecoveryCodesCountAsync(Guid panelUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask UpsertPasskeyAsync(Guid panelUserId, PasskeyCredentialPayload passkey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PasskeyCredentialPayload>> GetPasskeysAsync(Guid panelUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<PasskeyCredentialPayload?> FindPasskeyAsync(Guid panelUserId, byte[] credentialId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<(Guid PanelUserId, PasskeyCredentialPayload Passkey)?> FindPasskeyByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> RemovePasskeyAsync(Guid panelUserId, byte[] credentialId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> DeleteCredentialAsync(Guid panelUserId, Guid credentialId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
