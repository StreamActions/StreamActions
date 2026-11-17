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
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Web.Components.Pages;
using Xunit;

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
        _ = component.Markup.Should().Contain("name=\"customClientId\"");
        _ = component.Markup.Should().Contain("name=\"customClientSecret\"");
        _ = component.Markup.Should().NotContain("External SSO");
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

    #endregion

    private sealed class StubPlatformOAuthProvider(string platformId) : IPlatformOAuthProvider
    {
        public string PlatformId { get; } = platformId;
        public Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IReadOnlyCollection<string>? scopes, string state, bool forceVerify = false, string? nonce = null) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, Uri redirectUri, string? expectedNonce = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask RevokeTokenAsync(string clientId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubInstanceSettingsService(InstanceSettings initial) : IInstanceSettingsService
    {
        public InstanceSettings Current { get; private set; } = initial;
        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged;

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Current);

        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default)
        {
            this.Current = settings;
            this.SettingsChanged?.Invoke(this, new InstanceSettingsChangedEventArgs(settings));
            return ValueTask.CompletedTask;
        }
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
}
