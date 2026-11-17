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

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Web.Security;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StreamActions.Web.Endpoints;

/// <summary>
/// Maps dynamic platform OAuth, external OIDC, and sign-out HTTP endpoints for web panel authentication.
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>
    /// Lifetime of the protected <c>StreamActions.AuthFlow</c> state cookie.
    /// </summary>
    public static readonly TimeSpan AuthFlowLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Default scopes requested for login and identity linking flows.
    /// </summary>
    private static readonly string[] _loginScopes = ["openid", "user:read:email"];

    /// <summary>
    /// Default scopes requested for broadcaster workspace authorization flows.
    /// </summary>
    private static readonly string[] _broadcasterScopes =
    [
        "openid",
        "user:read:email",
        "channel:bot",
        "chat:read",
        "chat:edit",
        "moderator:manage:banned_users",
        "moderator:manage:chat_messages",
    ];

    /// <summary>
    /// Default scopes requested for bot chat account authorization flows.
    /// </summary>
    private static readonly string[] _botScopes =
    [
        "user:read:chat",
        "user:write:chat",
        "user:bot",
        "chat:read",
        "chat:edit",
    ];

    /// <summary>
    /// Maps <c>/auth/{platform}/*</c>, <c>/auth/oidc/{slug}/*</c>, and <c>/auth/logout</c> endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The configured <see cref="IEndpointRouteBuilder"/>.</returns>
    internal static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet("/auth/{platform}/login", HandlePlatformLoginAsync);
        _ = endpoints.MapPost("/auth/{platform}/login", HandlePlatformLoginAsync);
        _ = endpoints.MapGet("/auth/{platform}/callback", HandlePlatformCallbackAsync);

        _ = endpoints.MapGet("/auth/oidc/{slug}/login", HandleOidcLoginAsync);
        _ = endpoints.MapGet("/auth/oidc/{slug}/callback", HandleOidcCallbackAsync);

        _ = endpoints.MapPost("/auth/workspace/select", HandleWorkspaceSelectAsync);
        _ = endpoints.MapPost("/auth/logout", HandleLogoutAsync);

        return endpoints;
    }

    /// <summary>
    /// Initiates a platform OAuth/OIDC authorization redirect for <c>login</c>, <c>setup</c>, <c>broadcaster</c>, <c>bot</c>, <c>instance-bot</c>, or <c>link</c>.
    /// </summary>
    /// <param name="platform">The platform identifier route parameter.</param>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="providers">All registered platform OAuth providers.</param>
    /// <param name="authorizationService">The authorization service for gated flows.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="oauthClientConfigResolver">The OAuth client credential resolver.</param>
    /// <param name="dataProtectionProvider">The data protection provider for the flow cookie.</param>
    /// <param name="antiforgery">The antiforgery service for validating POST form submissions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IResult"/> redirecting to the platform authorization endpoint or an error result.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Platform identifiers and flows are normalized to lowercase invariant.")]
    private static async Task<IResult> HandlePlatformLoginAsync(
        string platform,
        HttpContext httpContext,
        IEnumerable<IPlatformOAuthProvider> providers,
        IAuthorizationService authorizationService,
        IInstanceSettingsService instanceSettingsService,
        IOAuthClientConfigResolver oauthClientConfigResolver,
        IDataProtectionProvider dataProtectionProvider,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        IPlatformOAuthProvider? provider = ResolvePlatformProvider(providers, platform);
        if (provider is null)
        {
            return Results.NotFound();
        }

        string normalizedPlatform = provider.PlatformId.Trim().ToLowerInvariant();
        string? flow = httpContext.Request.Query["flow"].FirstOrDefault();
        string? returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault();
        string? rawWorkspaceId = httpContext.Request.Query["workspaceId"].FirstOrDefault();
        string? rawScopes = httpContext.Request.Query["scopes"].FirstOrDefault();
        string? customClientId = null;
        string? customClientSecret = null;

        if (HttpMethods.IsPost(httpContext.Request.Method))
        {
            if (!httpContext.Request.HasFormContentType)
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            try
            {
                await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }

            IFormCollection form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            flow ??= form["flow"].FirstOrDefault();
            returnUrl ??= form["returnUrl"].FirstOrDefault();
            rawWorkspaceId ??= form["workspaceId"].FirstOrDefault();
            rawScopes ??= form["scopes"].FirstOrDefault();
            customClientId = form["customClientId"].FirstOrDefault();
            customClientSecret = form["customClientSecret"].FirstOrDefault();
        }

        string normalizedFlow = string.IsNullOrWhiteSpace(flow)
            ? "login"
            : flow.Trim().ToLowerInvariant();

        Guid? workspaceId = null;
        if (!string.IsNullOrWhiteSpace(rawWorkspaceId))
        {
            if (!Guid.TryParse(rawWorkspaceId, out Guid parsedWorkspaceId) || parsedWorkspaceId == Guid.Empty)
            {
                return Results.BadRequest("Invalid workspace identifier.");
            }

            workspaceId = parsedWorkspaceId;
        }

        IResult? gateFailure = await AuthorizePlatformFlowStartAsync(
            normalizedFlow,
            httpContext,
            authorizationService,
            dataProtectionProvider).ConfigureAwait(false);
        if (gateFailure is not null)
        {
            return gateFailure;
        }

        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(normalizedFlow, "login", StringComparison.Ordinal)
            && settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required)
        {
            return Results.Redirect("/login?error=oidc_required");
        }

        InstanceOAuthAppPolicy oauthPolicy = AuthPolicyParser.ParseOAuthAppPolicy(settings.InstanceOAuthAppPolicy);

        string? clientId = null;
        string? clientSecret = null;
        Uri? explicitRedirectUri = null;
        bool storeCustomCredentialsInFlow = false;

        if (HttpMethods.IsPost(httpContext.Request.Method)
            && string.Equals(normalizedFlow, "login", StringComparison.Ordinal)
            && oauthPolicy == InstanceOAuthAppPolicy.RequireStreamerOAuthApp
            && !string.IsNullOrWhiteSpace(customClientId)
            && !string.IsNullOrWhiteSpace(customClientSecret))
        {
            clientId = customClientId.Trim();
            clientSecret = customClientSecret.Trim();
            storeCustomCredentialsInFlow = true;
        }
        else
        {
            ResolvedOAuthClientConfig? resolved = await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, workspaceId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (resolved is not null)
            {
                clientId = resolved.ClientId;
                clientSecret = resolved.ClientSecret;
                explicitRedirectUri = resolved.RedirectUri;
            }
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return Results.Redirect($"/login?error=oauth_not_configured&platform={Uri.EscapeDataString(normalizedPlatform)}");
        }

        string state = GenerateRandomToken();
        bool useOidcNonce = normalizedFlow is "login" or "setup" or "link";
        string? nonce = useOidcNonce ? GenerateRandomToken() : null;
        bool forceVerify = normalizedFlow is "bot" or "instance-bot";
        IReadOnlyCollection<string> scopes = ResolveRequestedScopes(normalizedFlow, rawScopes);

        Uri redirectUri = explicitRedirectUri ?? BuildCallbackUri(httpContext.Request, $"/auth/{normalizedPlatform}/callback");
        Uri authorizationUri = provider.BuildAuthorizationUri(
            clientId,
            redirectUri,
            scopes,
            state,
            forceVerify: forceVerify,
            nonce: nonce);

        AuthFlowCookiePayload payload = new()
        {
            State = state,
            Nonce = nonce,
            Flow = normalizedFlow,
            PlatformId = normalizedPlatform,
            ReturnUrl = AuthClaims.IsSafeLocalRedirect(returnUrl) ? returnUrl!.Trim() : null,
            WorkspaceId = workspaceId,
            CustomClientId = storeCustomCredentialsInFlow ? clientId : null,
            CustomClientSecret = storeCustomCredentialsInFlow ? clientSecret : null,
            ExpiresAtUtc = GetTimeProvider(httpContext).GetUtcNow() + AuthFlowLifetime,
        };

        WriteAuthFlowCookie(httpContext, dataProtectionProvider, payload);
        return Results.Redirect(authorizationUri.ToString());
    }

    /// <summary>
    /// Handles the OAuth 2.0 / OIDC callback for a streaming platform and completes the initiated flow.
    /// </summary>
    /// <param name="platform">The platform identifier route parameter.</param>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="providers">All registered platform OAuth providers.</param>
    /// <param name="authorizationService">The authorization service for gated flows.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="oauthClientConfigResolver">The OAuth client credential resolver.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="platformCredentialStore">The encrypted platform credential store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IResult"/> redirecting to the target route or an error response.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Platform identifiers and flows are normalized to lowercase invariant.")]
    private static async Task<IResult> HandlePlatformCallbackAsync(
        string platform,
        HttpContext httpContext,
        IEnumerable<IPlatformOAuthProvider> providers,
        IAuthorizationService authorizationService,
        IInstanceSettingsService instanceSettingsService,
        IOAuthClientConfigResolver oauthClientConfigResolver,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IPlatformCredentialStore platformCredentialStore,
        IPermissionService permissionService,
        IWorkspaceAccessService workspaceAccessService,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        IPlatformOAuthProvider? provider = ResolvePlatformProvider(providers, platform);
        AuthFlowCookiePayload? flowPayload = ReadAndDeleteAuthFlowCookie(httpContext, dataProtectionProvider);

        if (provider is null)
        {
            return Results.NotFound();
        }

        string normalizedPlatform = provider.PlatformId.Trim().ToLowerInvariant();
        string? error = httpContext.Request.Query["error"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Results.Redirect($"/login?error={Uri.EscapeDataString(error)}&platform={Uri.EscapeDataString(normalizedPlatform)}");
        }

        string? code = httpContext.Request.Query["code"].FirstOrDefault();
        string? state = httpContext.Request.Query["state"].FirstOrDefault();

        if (flowPayload is null
            || string.IsNullOrWhiteSpace(code)
            || !string.Equals(flowPayload.PlatformId, normalizedPlatform, StringComparison.OrdinalIgnoreCase)
            || !ValidateFixedTimeState(flowPayload.State, state))
        {
            return Results.BadRequest("Invalid or expired authentication state.");
        }

        string? clientId;
        string? clientSecret;
        Uri? explicitRedirectUri = null;

        if (!string.IsNullOrWhiteSpace(flowPayload.CustomClientId)
            && !string.IsNullOrWhiteSpace(flowPayload.CustomClientSecret))
        {
            clientId = flowPayload.CustomClientId;
            clientSecret = flowPayload.CustomClientSecret;
        }
        else
        {
            ResolvedOAuthClientConfig? resolved = await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, flowPayload.WorkspaceId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (resolved is null)
            {
                return Results.Redirect($"/login?error=oauth_not_configured&platform={Uri.EscapeDataString(normalizedPlatform)}");
            }

            clientId = resolved.ClientId;
            clientSecret = resolved.ClientSecret;
            explicitRedirectUri = resolved.RedirectUri;
        }

        Uri redirectUri = explicitRedirectUri ?? BuildCallbackUri(httpContext.Request, $"/auth/{normalizedPlatform}/callback");
        PlatformOAuthTokenResult tokenResult = await provider
            .ExchangeCodeAsync(clientId, clientSecret, code, redirectUri, flowPayload.Nonce, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(tokenResult.PlatformUserId))
        {
            return Results.Redirect($"/login?error=identity_missing&platform={Uri.EscapeDataString(normalizedPlatform)}");
        }

        string platformUserId = tokenResult.PlatformUserId.Trim();
        string platformUsername = string.IsNullOrWhiteSpace(tokenResult.PlatformUsername)
            ? platformUserId
            : tokenResult.PlatformUsername.Trim();
        string displayName = string.IsNullOrWhiteSpace(tokenResult.DisplayName)
            ? platformUsername
            : tokenResult.DisplayName.Trim();

        PlatformTokenPayload tokenPayload = new(
            tokenResult.AccessToken,
            tokenResult.RefreshToken,
            tokenResult.ExpiresAtUtc,
            tokenResult.Scopes,
            tokenResult.TokenType,
            platformUserId,
            platformUsername,
            clientId);

        string normalizedFlow = string.IsNullOrWhiteSpace(flowPayload.Flow)
            ? "login"
            : flowPayload.Flow.Trim().ToLowerInvariant();

        return normalizedFlow switch
        {
            "setup" => await CompleteSetupFlowAsync(
                httpContext,
                normalizedPlatform,
                platformUserId,
                platformUsername,
                displayName,
                tokenPayload,
                flowPayload,
                panelUserStore,
                workspaceStore,
                platformCredentialStore,
                mfaCredentialStore,
                dataProtectionProvider,
                cancellationToken).ConfigureAwait(false),

            "login" => await CompletePlatformLoginFlowAsync(
                httpContext,
                normalizedPlatform,
                platformUserId,
                platformUsername,
                displayName,
                tokenPayload,
                flowPayload,
                instanceSettingsService,
                panelUserStore,
                workspaceStore,
                platformCredentialStore,
                permissionService,
                mfaPolicyService,
                mfaCredentialStore,
                cancellationToken).ConfigureAwait(false),

            "link" => await CompleteLinkFlowAsync(
                httpContext,
                normalizedPlatform,
                platformUserId,
                platformUsername,
                flowPayload,
                authorizationService,
                panelUserStore,
                cancellationToken).ConfigureAwait(false),

            "broadcaster" or "bot" => await CompleteWorkspaceTokenFlowAsync(
                httpContext,
                normalizedPlatform,
                normalizedFlow == "broadcaster" ? TokenRole.Broadcaster : TokenRole.BotChat,
                tokenPayload,
                flowPayload,
                authorizationService,
                panelUserStore,
                workspaceAccessService,
                platformCredentialStore,
                cancellationToken).ConfigureAwait(false),

            "instance-bot" => await CompleteInstanceBotTokenFlowAsync(
                httpContext,
                normalizedPlatform,
                tokenPayload,
                flowPayload,
                authorizationService,
                platformCredentialStore,
                cancellationToken).ConfigureAwait(false),

            _ => Results.BadRequest("Unsupported authentication flow."),
        };
    }

    /// <summary>
    /// Initiates an external OpenID Connect PKCE authorization redirect for the specified provider slug.
    /// </summary>
    /// <param name="slug">The external OIDC provider slug.</param>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="oidcProviderStore">The external OIDC provider store.</param>
    /// <param name="oidcClient">The external OIDC client.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the OIDC provider or an error response.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "OIDC provider slugs are normalized to lowercase invariant.")]
    private static async Task<IResult> HandleOidcLoginAsync(
        string slug,
        HttpContext httpContext,
        IInstanceSettingsService instanceSettingsService,
        IExternalOidcProviderStore oidcProviderStore,
        IExternalOidcClient oidcClient,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Disabled)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        string? rawWorkspaceId = httpContext.Request.Query["workspaceId"].FirstOrDefault();
        Guid? workspaceId = null;
        if (!string.IsNullOrWhiteSpace(rawWorkspaceId))
        {
            if (!Guid.TryParse(rawWorkspaceId, out Guid parsedWorkspaceId) || parsedWorkspaceId == Guid.Empty)
            {
                return Results.BadRequest("Invalid workspace identifier.");
            }

            workspaceId = parsedWorkspaceId;
        }

        string normalizedSlug = slug.Trim().ToLowerInvariant();
        ExternalOidcProvider? provider = await oidcProviderStore
            .GetBySlugAsync(workspaceId, normalizedSlug, cancellationToken)
            .ConfigureAwait(false);

        if (provider is null || !provider.IsEnabled)
        {
            return Results.NotFound();
        }

        Uri redirectUri = BuildCallbackUri(httpContext.Request, $"/auth/oidc/{Uri.EscapeDataString(provider.Slug)}/callback");
        ExternalOidcAuthorizationRequest authRequest = await oidcClient
            .BuildAuthorizationRequestAsync(provider, redirectUri, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string? returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault();
        AuthFlowCookiePayload payload = new()
        {
            State = authRequest.State,
            Nonce = authRequest.Nonce,
            CodeVerifier = authRequest.CodeVerifier,
            Flow = "oidc",
            OidcSlug = provider.Slug,
            WorkspaceId = workspaceId,
            ReturnUrl = AuthClaims.IsSafeLocalRedirect(returnUrl) ? returnUrl!.Trim() : null,
            ExpiresAtUtc = GetTimeProvider(httpContext).GetUtcNow() + AuthFlowLifetime,
        };

        WriteAuthFlowCookie(httpContext, dataProtectionProvider, payload);
        return Results.Redirect(authRequest.AuthorizationUri.ToString());
    }

    /// <summary>
    /// Handles the callback from an external OpenID Connect provider, evaluates authorization, and signs in the user.
    /// </summary>
    /// <param name="slug">The external OIDC provider slug.</param>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="oidcProviderStore">The external OIDC provider store.</param>
    /// <param name="oidcClient">The external OIDC client.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="workspaceStore">The workspace store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the panel, MFA challenge/enrollment route, or <c>/access-denied</c>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "OIDC provider slugs are normalized to lowercase invariant.")]
    private static async Task<IResult> HandleOidcCallbackAsync(
        string slug,
        HttpContext httpContext,
        IInstanceSettingsService instanceSettingsService,
        IExternalOidcProviderStore oidcProviderStore,
        IExternalOidcClient oidcClient,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IPermissionService permissionService,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        AuthFlowCookiePayload? flowPayload = ReadAndDeleteAuthFlowCookie(httpContext, dataProtectionProvider);

        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Disabled)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        string? error = httpContext.Request.Query["error"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Results.Redirect($"/login?error={Uri.EscapeDataString(error)}");
        }

        string normalizedSlug = slug.Trim().ToLowerInvariant();
        string? code = httpContext.Request.Query["code"].FirstOrDefault();
        string? state = httpContext.Request.Query["state"].FirstOrDefault();

        if (flowPayload is null
            || !string.Equals(flowPayload.Flow, "oidc", StringComparison.Ordinal)
            || !string.Equals(flowPayload.OidcSlug, normalizedSlug, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(flowPayload.CodeVerifier)
            || string.IsNullOrWhiteSpace(flowPayload.Nonce)
            || !ValidateFixedTimeState(flowPayload.State, state))
        {
            return Results.BadRequest("Invalid or expired OIDC authentication state.");
        }

        ExternalOidcProvider? provider = await oidcProviderStore
            .GetBySlugAsync(flowPayload.WorkspaceId, normalizedSlug, cancellationToken)
            .ConfigureAwait(false);
        if (provider is null || !provider.IsEnabled)
        {
            return Results.NotFound();
        }

        (bool secretOk, string? clientSecret, CredentialStatus _) = await oidcProviderStore
            .TryGetClientSecretAsync(provider.Id, cancellationToken)
            .ConfigureAwait(false);
        if (!secretOk || string.IsNullOrWhiteSpace(clientSecret))
        {
            return Results.Redirect("/login?error=oidc_secret_unavailable");
        }

        Uri redirectUri = BuildCallbackUri(httpContext.Request, $"/auth/oidc/{Uri.EscapeDataString(provider.Slug)}/callback");
        ExternalOidcAuthenticationResult authResult = await oidcClient
            .ExchangeCodeAsync(
                provider,
                clientSecret,
                code,
                redirectUri,
                flowPayload.CodeVerifier,
                flowPayload.Nonce,
                cancellationToken)
            .ConfigureAwait(false);

        string identityProviderId = $"oidc:{provider.Slug}";
        string subject = authResult.Subject.Trim();
        string username = string.IsNullOrWhiteSpace(authResult.Username) ? subject : authResult.Username.Trim();
        string displayName = string.IsNullOrWhiteSpace(authResult.DisplayName) ? username : authResult.DisplayName.Trim();
        IReadOnlyList<string> groups = authResult.Groups ?? [];

        PanelUser? user = await panelUserStore
            .GetByLoginIdentityAsync(identityProviderId, subject, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            (bool hasWorkspaceAccess, bool explicitlyDenied) = await EvaluateAnyWorkspacePanelAccessAsync(
                identityProviderId,
                subject,
                username,
                groups,
                workspaceStore,
                permissionService,
                cancellationToken).ConfigureAwait(false);

            if (explicitlyDenied || (!hasWorkspaceAccess && !settings.AllowOpenRegistration))
            {
                return Results.Redirect("/access-denied");
            }

            user = await panelUserStore.CreateUserAsync(
                new PanelUser
                {
                    DisplayName = displayName,
                    IsRoot = false,
                    IsGlobalAdmin = false,
                },
                new UserLoginIdentity
                {
                    ProviderId = identityProviderId,
                    ProviderUserId = subject,
                    ProviderUsername = username,
                },
                cancellationToken).ConfigureAwait(false);
        }

        return await CompleteInteractiveLoginSignInAsync(
            httpContext,
            user,
            groups,
            identityProviderId,
            flowPayload.ReturnUrl,
            panelUserStore,
            mfaPolicyService,
            mfaCredentialStore,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the antiforgery token, clears the authentication cookie, and redirects to <c>/login</c>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <returns>A redirect to <c>/login</c> or <c>400 Bad Request</c> if antiforgery validation fails.</returns>
    private static async Task<IResult> HandleLogoutAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        DeleteCookie(httpContext, AuthClaims.WorkspaceCookieName);
        return Results.Redirect("/login");
    }

    /// <summary>
    /// Persists the selected accessible workspace identifier into the <c>StreamActions.Workspace</c> cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A safe local redirect or an authorization/validation error result.</returns>
    private static async Task<IResult> HandleWorkspaceSelectAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IPanelUserStore panelUserStore,
        IWorkspaceAccessService workspaceAccessService,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        IResult? authFailure = await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.PanelAuthenticatedPolicy)
            .ConfigureAwait(false);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!httpContext.Request.HasFormContentType)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }

        IFormCollection form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        string? rawWorkspaceId = form["workspaceId"].FirstOrDefault();
        string? returnUrl = form["returnUrl"].FirstOrDefault();

        if (!Guid.TryParse(rawWorkspaceId, out Guid workspaceId) || workspaceId == Guid.Empty)
        {
            return Results.BadRequest("Invalid workspace identifier.");
        }

        Guid? panelUserId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!panelUserId.HasValue)
        {
            return Results.Challenge();
        }

        PanelUser? user = await panelUserStore.GetByIdAsync(panelUserId.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Challenge();
        }

        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> oidcGroups = AuthClaims.GetOidcGroups(httpContext.User);

        bool canAccess = await workspaceAccessService
            .CanAccessWorkspaceAsync(workspaceId, user, identities, oidcGroups, cancellationToken)
            .ConfigureAwait(false);

        if (!canAccess)
        {
            return Results.Forbid();
        }

        WriteWorkspaceCookie(httpContext, workspaceId);
        return Results.Redirect(AuthClaims.SafeReturnUrlOrDefault(returnUrl, "/"));
    }

    /// <summary>
    /// Validates preconditions for initiating a platform OAuth flow.
    /// </summary>
    /// <param name="normalizedFlow">The normalized flow identifier.</param>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <returns><see langword="null"/> if authorized; otherwise, the failure <see cref="IResult"/>.</returns>
    private static async Task<IResult?> AuthorizePlatformFlowStartAsync(
        string normalizedFlow,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider) => normalizedFlow switch
        {
            "login" => null,
            "setup" => SetupEndpoints.TryReadSetupTicket(httpContext, dataProtectionProvider, out _)
                                ? null
                                : Results.Unauthorized(),
            "broadcaster" or "bot" or "link" => await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.PanelAuthenticatedPolicy)
                                .ConfigureAwait(false),
            "instance-bot" => await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.RootOrGlobalAdminPolicy)
                                .ConfigureAwait(false),
            _ => Results.BadRequest("Unsupported authentication flow."),
        };

    /// <summary>
    /// Evaluates an authorization policy for the current user and returns a challenge or forbid result when denied.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="policyName">The policy name to evaluate.</param>
    /// <returns><see langword="null"/> if authorized; otherwise, a challenge or forbid <see cref="IResult"/>.</returns>
    private static async Task<IResult?> AuthorizePolicyAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        string policyName)
    {
        AuthorizationResult authResult = await authorizationService
            .AuthorizeAsync(httpContext.User, null, policyName)
            .ConfigureAwait(false);

        return authResult.Succeeded
            ? null
            : httpContext.User.Identity?.IsAuthenticated == true
            ? Results.Forbid()
            : Results.Challenge();
    }

    /// <summary>
    /// Completes the initial setup or root recovery flow after platform OAuth callback validation.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="platformId">The normalized platform identifier.</param>
    /// <param name="platformUserId">The authenticated platform user identifier.</param>
    /// <param name="platformUsername">The authenticated platform username.</param>
    /// <param name="displayName">The user's display name.</param>
    /// <param name="tokenPayload">The exchanged token payload.</param>
    /// <param name="flowPayload">The verified auth flow cookie payload.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="workspaceStore">The workspace store.</param>
    /// <param name="platformCredentialStore">The platform credential store.</param>
    /// <param name="mfaCredentialStore">The MFA credential store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to <c>/</c> or <c>401 Unauthorized</c> if the setup ticket is invalid.</returns>
    private static async Task<IResult> CompleteSetupFlowAsync(
        HttpContext httpContext,
        string platformId,
        string platformUserId,
        string platformUsername,
        string displayName,
        PlatformTokenPayload tokenPayload,
        AuthFlowCookiePayload flowPayload,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IPlatformCredentialStore platformCredentialStore,
        IMfaCredentialStore mfaCredentialStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        if (!SetupEndpoints.TryReadSetupTicket(httpContext, dataProtectionProvider, out _))
        {
            return Results.Unauthorized();
        }

        PanelUser? existingByIdentity = await panelUserStore
            .GetByLoginIdentityAsync(platformId, platformUserId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PanelUser> allUsers = await panelUserStore.GetAllUsersAsync(cancellationToken).ConfigureAwait(false);
        PanelUser? rootUser = allUsers.FirstOrDefault(static u => u.IsRoot);

        if (rootUser is null)
        {
            if (existingByIdentity is not null)
            {
                existingByIdentity.IsRoot = true;
                existingByIdentity.IsGlobalAdmin = true;
                rootUser = await panelUserStore.UpdateUserAsync(existingByIdentity, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                rootUser = await panelUserStore.CreateUserAsync(
                    new PanelUser
                    {
                        DisplayName = displayName,
                        IsRoot = true,
                        IsGlobalAdmin = true,
                    },
                    new UserLoginIdentity
                    {
                        ProviderId = platformId,
                        ProviderUserId = platformUserId,
                        ProviderUsername = platformUsername,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            if (existingByIdentity is not null && existingByIdentity.Id != rootUser.Id)
            {
                _ = await panelUserStore
                    .UnlinkLoginIdentityAsync(existingByIdentity.Id, platformId, platformUserId, cancellationToken)
                    .ConfigureAwait(false);
            }

            _ = await panelUserStore.LinkLoginIdentityAsync(
                new UserLoginIdentity
                {
                    PanelUserId = rootUser.Id,
                    ProviderId = platformId,
                    ProviderUserId = platformUserId,
                    ProviderUsername = platformUsername,
                },
                cancellationToken).ConfigureAwait(false);

            if (!rootUser.IsGlobalAdmin)
            {
                rootUser.IsGlobalAdmin = true;
                rootUser = await panelUserStore.UpdateUserAsync(rootUser, cancellationToken).ConfigureAwait(false);
            }

            _ = await mfaCredentialStore
                .ClearAllForUserAsync(rootUser.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        IReadOnlyList<Workspace> ownedWorkspaces = await workspaceStore.GetByOwnerAsync(rootUser.Id, cancellationToken).ConfigureAwait(false);
        Workspace targetWorkspace;

        if (ownedWorkspaces.Count == 0)
        {
            targetWorkspace = await workspaceStore.CreateWorkspaceAsync(
                new Workspace
                {
                    OwnerUserId = rootUser.Id,
                    Name = $"{displayName}'s Workspace",
                    PrimaryPlatformId = platformId,
                },
                new WorkspacePlatform
                {
                    PlatformId = platformId,
                    Role = PlatformRole.Primary,
                    PlatformChannelId = platformUserId,
                },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            targetWorkspace = ownedWorkspaces[0];
            WorkspacePlatform? existingPlatform = await workspaceStore
                .GetPlatformAsync(targetWorkspace.Id, platformId, cancellationToken)
                .ConfigureAwait(false);
            if (existingPlatform is null)
            {
                _ = await workspaceStore.UpsertPlatformAsync(
                    new WorkspacePlatform
                    {
                        WorkspaceId = targetWorkspace.Id,
                        PlatformId = platformId,
                        Role = PlatformRole.Addon,
                        PlatformChannelId = platformUserId,
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        PlatformCredential? existingBroadcaster = await platformCredentialStore
            .GetCredentialRecordAsync(targetWorkspace.Id, platformId, TokenRole.Broadcaster, cancellationToken)
            .ConfigureAwait(false);
        if (existingBroadcaster is null)
        {
            _ = await platformCredentialStore
                .SaveTokenAsync(targetWorkspace.Id, platformId, TokenRole.Broadcaster, tokenPayload, CredentialStatus.Active, cancellationToken)
                .ConfigureAwait(false);
        }

        DeleteCookie(httpContext, SetupEndpoints.SetupTicketCookieName);
        WriteWorkspaceCookie(httpContext, targetWorkspace.Id);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthClaims.BuildClaimsPrincipal(rootUser, authProvider: platformId)).ConfigureAwait(false);

        return Results.Redirect(AuthClaims.SafeReturnUrlOrDefault(flowPayload.ReturnUrl, "/"));
    }

    /// <summary>
    /// Completes the regular platform login flow, checking existing accounts, workspace <c>ui.panel.access</c>, or open registration.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="platformId">The normalized platform identifier.</param>
    /// <param name="platformUserId">The authenticated platform user identifier.</param>
    /// <param name="platformUsername">The authenticated platform username.</param>
    /// <param name="displayName">The user's display name.</param>
    /// <param name="tokenPayload">The exchanged token payload.</param>
    /// <param name="flowPayload">The verified auth flow cookie payload.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="workspaceStore">The workspace store.</param>
    /// <param name="platformCredentialStore">The platform credential store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the panel, MFA challenge/enrollment route, or <c>/access-denied</c>.</returns>
    private static async Task<IResult> CompletePlatformLoginFlowAsync(
        HttpContext httpContext,
        string platformId,
        string platformUserId,
        string platformUsername,
        string displayName,
        PlatformTokenPayload tokenPayload,
        AuthFlowCookiePayload flowPayload,
        IInstanceSettingsService instanceSettingsService,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IPlatformCredentialStore platformCredentialStore,
        IPermissionService permissionService,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        CancellationToken cancellationToken)
    {
        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required)
        {
            return Results.Redirect("/login?error=oidc_required");
        }

        PanelUser? user = await panelUserStore
            .GetByLoginIdentityAsync(platformId, platformUserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is not null)
        {
            _ = await panelUserStore.LinkLoginIdentityAsync(
                new UserLoginIdentity
                {
                    PanelUserId = user.Id,
                    ProviderId = platformId,
                    ProviderUserId = platformUserId,
                    ProviderUsername = platformUsername,
                },
                cancellationToken).ConfigureAwait(false);

            IReadOnlyList<Workspace> ownedWorkspaces = await workspaceStore
                .GetByOwnerAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);
            Workspace? matchingOwned = ownedWorkspaces.FirstOrDefault(w =>
                string.Equals(w.PrimaryPlatformId, platformId, StringComparison.OrdinalIgnoreCase));
            if (matchingOwned is not null)
            {
                WriteWorkspaceCookie(httpContext, matchingOwned.Id);
            }

            return await CompleteInteractiveLoginSignInAsync(
                httpContext,
                user,
                oidcGroups: null,
                platformId,
                flowPayload.ReturnUrl,
                panelUserStore,
                mfaPolicyService,
                mfaCredentialStore,
                cancellationToken).ConfigureAwait(false);
        }

        (bool hasPanelAccess, bool explicitlyDenied) = await EvaluateAnyWorkspacePanelAccessAsync(
            platformId,
            platformUserId,
            platformUsername,
            oidcGroups: null,
            workspaceStore,
            permissionService,
            cancellationToken).ConfigureAwait(false);

        if (explicitlyDenied)
        {
            return Results.Redirect("/access-denied");
        }

        if (hasPanelAccess)
        {
            user = await panelUserStore.CreateUserAsync(
                new PanelUser
                {
                    DisplayName = displayName,
                    IsRoot = false,
                    IsGlobalAdmin = false,
                },
                new UserLoginIdentity
                {
                    ProviderId = platformId,
                    ProviderUserId = platformUserId,
                    ProviderUsername = platformUsername,
                },
                cancellationToken).ConfigureAwait(false);

            return await CompleteInteractiveLoginSignInAsync(
                httpContext,
                user,
                oidcGroups: null,
                platformId,
                flowPayload.ReturnUrl,
                panelUserStore,
                mfaPolicyService,
                mfaCredentialStore,
                cancellationToken).ConfigureAwait(false);
        }

        if (!settings.AllowOpenRegistration)
        {
            return Results.Redirect("/access-denied");
        }

        user = await panelUserStore.CreateUserAsync(
            new PanelUser
            {
                DisplayName = displayName,
                IsRoot = false,
                IsGlobalAdmin = false,
            },
            new UserLoginIdentity
            {
                ProviderId = platformId,
                ProviderUserId = platformUserId,
                ProviderUsername = platformUsername,
            },
            cancellationToken).ConfigureAwait(false);

        Workspace createdWorkspace = await workspaceStore.CreateWorkspaceAsync(
            new Workspace
            {
                OwnerUserId = user.Id,
                Name = $"{displayName}'s Workspace",
                PrimaryPlatformId = platformId,
            },
            new WorkspacePlatform
            {
                PlatformId = platformId,
                Role = PlatformRole.Primary,
                PlatformChannelId = platformUserId,
                CustomClientId = flowPayload.CustomClientId,
            },
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(flowPayload.CustomClientId)
            && !string.IsNullOrWhiteSpace(flowPayload.CustomClientSecret))
        {
            _ = await workspaceStore.SetCustomOAuthAppAsync(
                createdWorkspace.Id,
                platformId,
                flowPayload.CustomClientId,
                flowPayload.CustomClientSecret,
                cancellationToken).ConfigureAwait(false);
        }

        _ = await platformCredentialStore
            .SaveTokenAsync(createdWorkspace.Id, platformId, TokenRole.Broadcaster, tokenPayload, CredentialStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        WriteWorkspaceCookie(httpContext, createdWorkspace.Id);

        return await CompleteInteractiveLoginSignInAsync(
            httpContext,
            user,
            oidcGroups: null,
            platformId,
            flowPayload.ReturnUrl,
            panelUserStore,
            mfaPolicyService,
            mfaCredentialStore,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates effective MFA policies and enrolled credentials for <paramref name="user"/>, signs in with appropriate claims, and redirects to the panel or MFA workflow.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="oidcGroups">Optional OIDC group claims from the current session.</param>
    /// <param name="authProvider">The authentication provider identifier.</param>
    /// <param name="returnUrl">The optional local return URL.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to <c>/auth/mfa</c>, <c>/account/mfa?enrollRequired=true</c>, or the safe return URL.</returns>
    private static async Task<IResult> CompleteInteractiveLoginSignInAsync(
        HttpContext httpContext,
        PanelUser user,
        IReadOnlyCollection<string>? oidcGroups,
        string authProvider,
        string? returnUrl,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        EffectiveMfaPolicy policy = await mfaPolicyService
            .GetEffectivePolicyForUserAsync(user, identities, oidcGroups, workspaceId: null, cancellationToken)
            .ConfigureAwait(false);
        bool isLoginMfaRequired = await mfaPolicyService
            .IsLoginMfaRequiredAsync(user, identities, oidcGroups, workspaceId: null, cancellationToken)
            .ConfigureAwait(false);
        bool hasAnyCredentials = await mfaCredentialStore
            .HasAnyCredentialsAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        string? safeReturnUrl = AuthClaims.IsSafeLocalRedirect(returnUrl) ? returnUrl!.Trim() : null;

        if (!policy.IsMfaDisabled && hasAnyCredentials)
        {
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AuthClaims.BuildClaimsPrincipal(
                    user,
                    oidcGroups,
                    authProvider: authProvider,
                    mfaPending: true)).ConfigureAwait(false);

            string mfaRedirect = safeReturnUrl is not null
                ? $"/auth/mfa?returnUrl={Uri.EscapeDataString(safeReturnUrl)}"
                : "/auth/mfa";
            return Results.Redirect(mfaRedirect);
        }

        if (!policy.IsMfaDisabled && isLoginMfaRequired)
        {
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AuthClaims.BuildClaimsPrincipal(
                    user,
                    oidcGroups,
                    authProvider: authProvider,
                    mfaPending: true,
                    mfaEnrollmentRequired: true)).ConfigureAwait(false);

            string enrollRedirect = safeReturnUrl is not null
                ? $"/account/mfa?enrollRequired=true&returnUrl={Uri.EscapeDataString(safeReturnUrl)}"
                : "/account/mfa?enrollRequired=true";
            return Results.Redirect(enrollRedirect);
        }

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthClaims.BuildClaimsPrincipal(user, oidcGroups, authProvider: authProvider)).ConfigureAwait(false);

        return Results.Redirect(safeReturnUrl ?? "/");
    }

    /// <summary>
    /// Links an additional platform identity to the currently signed-in <see cref="PanelUser"/>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="platformId">The normalized platform identifier.</param>
    /// <param name="platformUserId">The authenticated platform user identifier.</param>
    /// <param name="platformUsername">The authenticated platform username.</param>
    /// <param name="flowPayload">The verified auth flow cookie payload.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the return URL or an authorization failure result.</returns>
    private static async Task<IResult> CompleteLinkFlowAsync(
        HttpContext httpContext,
        string platformId,
        string platformUserId,
        string platformUsername,
        AuthFlowCookiePayload flowPayload,
        IAuthorizationService authorizationService,
        IPanelUserStore panelUserStore,
        CancellationToken cancellationToken)
    {
        IResult? authFailure = await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.PanelAuthenticatedPolicy)
            .ConfigureAwait(false);
        if (authFailure is not null)
        {
            return authFailure;
        }

        Guid? panelUserId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!panelUserId.HasValue)
        {
            return Results.Challenge();
        }

        _ = await panelUserStore.LinkLoginIdentityAsync(
            new UserLoginIdentity
            {
                PanelUserId = panelUserId.Value,
                ProviderId = platformId,
                ProviderUserId = platformUserId,
                ProviderUsername = platformUsername,
            },
            cancellationToken).ConfigureAwait(false);

        return Results.Redirect(AuthClaims.SafeReturnUrlOrDefault(flowPayload.ReturnUrl, "/"));
    }

    /// <summary>
    /// Persists a workspace-scoped broadcaster or bot chat token after verifying workspace access.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="platformId">The normalized platform identifier.</param>
    /// <param name="tokenRole">The token role to store.</param>
    /// <param name="tokenPayload">The exchanged token payload.</param>
    /// <param name="flowPayload">The verified auth flow cookie payload.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="workspaceAccessService">The workspace access service.</param>
    /// <param name="platformCredentialStore">The platform credential store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the return URL or an authorization failure result.</returns>
    private static async Task<IResult> CompleteWorkspaceTokenFlowAsync(
        HttpContext httpContext,
        string platformId,
        TokenRole tokenRole,
        PlatformTokenPayload tokenPayload,
        AuthFlowCookiePayload flowPayload,
        IAuthorizationService authorizationService,
        IPanelUserStore panelUserStore,
        IWorkspaceAccessService workspaceAccessService,
        IPlatformCredentialStore platformCredentialStore,
        CancellationToken cancellationToken)
    {
        IResult? authFailure = await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.PanelAuthenticatedPolicy)
            .ConfigureAwait(false);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (!flowPayload.WorkspaceId.HasValue)
        {
            return Results.BadRequest("Workspace identifier is required.");
        }

        Guid? panelUserId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!panelUserId.HasValue)
        {
            return Results.Challenge();
        }

        PanelUser? user = await panelUserStore.GetByIdAsync(panelUserId.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Challenge();
        }

        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> oidcGroups = AuthClaims.GetOidcGroups(httpContext.User);

        bool canAccess = await workspaceAccessService
            .CanAccessWorkspaceAsync(flowPayload.WorkspaceId.Value, user, identities, oidcGroups, cancellationToken)
            .ConfigureAwait(false);
        if (!canAccess)
        {
            return Results.Forbid();
        }

        _ = await platformCredentialStore
            .SaveTokenAsync(flowPayload.WorkspaceId.Value, platformId, tokenRole, tokenPayload, CredentialStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        return Results.Redirect(AuthClaims.SafeReturnUrlOrDefault(flowPayload.ReturnUrl, "/"));
    }

    /// <summary>
    /// Persists an instance-level default bot account token for root or global administrators.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="platformId">The normalized platform identifier.</param>
    /// <param name="tokenPayload">The exchanged token payload.</param>
    /// <param name="flowPayload">The verified auth flow cookie payload.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="platformCredentialStore">The platform credential store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the return URL or an authorization failure result.</returns>
    private static async Task<IResult> CompleteInstanceBotTokenFlowAsync(
        HttpContext httpContext,
        string platformId,
        PlatformTokenPayload tokenPayload,
        AuthFlowCookiePayload flowPayload,
        IAuthorizationService authorizationService,
        IPlatformCredentialStore platformCredentialStore,
        CancellationToken cancellationToken)
    {
        IResult? authFailure = await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.RootOrGlobalAdminPolicy)
            .ConfigureAwait(false);
        if (authFailure is not null)
        {
            return authFailure;
        }

        _ = await platformCredentialStore
            .SaveTokenAsync(workspaceId: null, platformId, TokenRole.BotChat, tokenPayload, CredentialStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        return Results.Redirect(AuthClaims.SafeReturnUrlOrDefault(flowPayload.ReturnUrl, "/admin/auth"));
    }

    /// <summary>
    /// Evaluates whether an external identity has <see cref="BuiltInPermissionKeys.UiPanelAccess"/> in any workspace without an explicit deny.
    /// </summary>
    /// <param name="providerId">The platform or OIDC provider identifier.</param>
    /// <param name="providerUserId">The provider user identifier or subject.</param>
    /// <param name="username">The username.</param>
    /// <param name="oidcGroups">Optional OIDC group claims.</param>
    /// <param name="workspaceStore">The workspace store.</param>
    /// <param name="permissionService">The permission service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating whether access was granted in at least one workspace and whether any workspace explicitly denied access.</returns>
    private static async ValueTask<(bool HasPanelAccess, bool ExplicitlyDenied)> EvaluateAnyWorkspacePanelAccessAsync(
        string providerId,
        string providerUserId,
        string username,
        IReadOnlyCollection<string>? oidcGroups,
        IWorkspaceStore workspaceStore,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Workspace> workspaces = await workspaceStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        PlatformUserIdentity actor = new(
            providerId,
            providerUserId,
            username,
            PlatformUserDesignations.None,
            OidcGroupClaims: oidcGroups);

        bool hasPanelAccess = false;
        foreach (Workspace workspace in workspaces)
        {
            PermissionEvaluationResult result = await permissionService
                .EvaluateAsync(workspace.Id, actor, BuiltInPermissionKeys.UiPanelAccess, cancellationToken)
                .ConfigureAwait(false);

            if (result.EffectiveState == PermissionState.Deny)
            {
                return (false, true);
            }

            if (result.IsAllowed)
            {
                hasPanelAccess = true;
            }
        }

        return (hasPanelAccess, false);
    }

    /// <summary>
    /// Finds the registered <see cref="IPlatformOAuthProvider"/> matching the route platform identifier.
    /// </summary>
    /// <param name="providers">The registered platform OAuth providers.</param>
    /// <param name="platform">The route platform identifier.</param>
    /// <returns>The matching provider, or <see langword="null"/> if none is registered.</returns>
    private static IPlatformOAuthProvider? ResolvePlatformProvider(
        IEnumerable<IPlatformOAuthProvider> providers,
        string platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return null;
        }

        string trimmed = platform.Trim();
        return providers.FirstOrDefault(p => string.Equals(p.PlatformId, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds an absolute callback <see cref="Uri"/> on the current request origin.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <param name="relativeCallbackPath">The relative callback path starting with <c>/</c>.</param>
    /// <returns>The absolute callback <see cref="Uri"/>.</returns>
    private static Uri BuildCallbackUri(HttpRequest request, string relativeCallbackPath)
    {
        string origin = $"{request.Scheme}://{request.Host}{request.PathBase}";
        return new Uri($"{origin}{relativeCallbackPath}", UriKind.Absolute);
    }

    /// <summary>
    /// Generates a cryptographically random 256-bit hex state/nonce string.
    /// </summary>
    /// <returns>A lowercase hexadecimal random token.</returns>
    private static string GenerateRandomToken() => RandomNumberGenerator.GetHexString(32, lowercase: true);

    /// <summary>
    /// Encrypts and writes the <c>StreamActions.AuthFlow</c> <see langword="HttpOnly"/> cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="payload">The flow state payload to protect.</param>
    private static void WriteAuthFlowCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider,
        AuthFlowCookiePayload payload)
    {
        string json = JsonSerializer.Serialize(payload);
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(AuthClaims.AuthFlowProtectorPurpose)
            .ToTimeLimitedDataProtector();
        string protectedValue = protector.Protect(json, AuthFlowLifetime);

        CookieOptions cookieOptions = new()
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            IsEssential = true,
            MaxAge = AuthFlowLifetime,
            Path = "/",
        };

        httpContext.Response.Cookies.Append(AuthClaims.AuthFlowCookieName, protectedValue, cookieOptions);
    }

    /// <summary>
    /// Reads and immediately deletes the <c>StreamActions.AuthFlow</c> cookie, returning <see langword="null"/> if absent, expired, or invalid.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <returns>The unprotected <see cref="AuthFlowCookiePayload"/>, or <see langword="null"/>.</returns>
    private static AuthFlowCookiePayload? ReadAndDeleteAuthFlowCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider)
    {
        bool hasCookie = httpContext.Request.Cookies.TryGetValue(AuthClaims.AuthFlowCookieName, out string? protectedValue);
        DeleteCookie(httpContext, AuthClaims.AuthFlowCookieName);

        if (!hasCookie || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(AuthClaims.AuthFlowProtectorPurpose)
                .ToTimeLimitedDataProtector();
            string json = protector.Unprotect(protectedValue);
            AuthFlowCookiePayload? payload = JsonSerializer.Deserialize<AuthFlowCookiePayload>(json);
            if (payload is null || string.IsNullOrWhiteSpace(payload.State))
            {
                return null;
            }

            TimeProvider timeProvider = GetTimeProvider(httpContext);
            return (payload.ExpiresAtUtc is DateTimeOffset expiresAtUtc) && (timeProvider.GetUtcNow() >= expiresAtUtc)
                ? null
                : payload;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the requested OAuth scopes for a platform authorization flow, honoring custom incremental scopes for <c>broadcaster</c>.
    /// </summary>
    /// <param name="normalizedFlow">The normalized lowercase flow name.</param>
    /// <param name="rawScopes">Optional space- or comma-delimited custom scope string.</param>
    /// <returns>The scope collection to request from the OAuth provider.</returns>
    private static string[] ResolveRequestedScopes(string normalizedFlow, string? rawScopes)
    {
        if (string.Equals(normalizedFlow, "broadcaster", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(rawScopes))
        {
            string[] customScopes = [.. rawScopes
                .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            if (customScopes.Length > 0)
            {
                return customScopes;
            }
        }

        return normalizedFlow switch
        {
            "setup" or "broadcaster" => _broadcasterScopes,
            "bot" or "instance-bot" => _botScopes,
            _ => _loginScopes,
        };
    }

    /// <summary>
    /// Writes the active workspace identifier into the <c>StreamActions.Workspace</c> cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="workspaceId">The selected workspace identifier.</param>
    private static void WriteWorkspaceCookie(HttpContext httpContext, Guid workspaceId) =>
        httpContext.Response.Cookies.Append(
            AuthClaims.WorkspaceCookieName,
            workspaceId.ToString("D"),
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(30),
                Path = "/",
            });

    /// <summary>
    /// Deletes a cookie with matching security and path attributes.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="cookieName">The cookie name to delete.</param>
    private static void DeleteCookie(HttpContext httpContext, string cookieName) =>
        httpContext.Response.Cookies.Delete(cookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            IsEssential = true,
            Path = "/",
        });

    /// <summary>
    /// Compares expected and received anti-CSRF state tokens in constant time.
    /// </summary>
    /// <param name="expectedState">The expected state from the protected flow cookie.</param>
    /// <param name="actualState">The received state from the callback query string.</param>
    /// <returns><see langword="true"/> if both strings are non-empty and identical; otherwise, <see langword="false"/>.</returns>
    private static bool ValidateFixedTimeState(string? expectedState, string? actualState)
    {
        if (string.IsNullOrEmpty(expectedState) || string.IsNullOrEmpty(actualState))
        {
            return false;
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedState);
        byte[] actualBytes = Encoding.UTF8.GetBytes(actualState);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <summary>
    /// Resolves <see cref="TimeProvider"/> from request services, falling back to <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The resolved <see cref="TimeProvider"/>.</returns>
    private static TimeProvider GetTimeProvider(HttpContext httpContext) =>
        httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
}

/// <summary>
/// Represents the protected payload stored inside the <c>StreamActions.AuthFlow</c> cookie.
/// </summary>
internal sealed record AuthFlowCookiePayload
{
    /// <summary>
    /// Gets the anti-CSRF state token.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the optional OIDC replay-protection nonce.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// Gets the authentication flow identifier.
    /// </summary>
    public string? Flow { get; init; }

    /// <summary>
    /// Gets the target platform identifier for platform OAuth flows.
    /// </summary>
    public string? PlatformId { get; init; }

    /// <summary>
    /// Gets the optional local return URL after authentication completes.
    /// </summary>
    public string? ReturnUrl { get; init; }

    /// <summary>
    /// Gets the optional target workspace identifier.
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the PKCE code verifier for external OIDC flows.
    /// </summary>
    public string? CodeVerifier { get; init; }

    /// <summary>
    /// Gets the external OIDC provider slug.
    /// </summary>
    public string? OidcSlug { get; init; }

    /// <summary>
    /// Gets the optional custom OAuth client identifier submitted for a new workspace under <c>RequireStreamerOAuthApp</c>.
    /// </summary>
    public string? CustomClientId { get; init; }

    /// <summary>
    /// Gets the optional custom OAuth client secret submitted for a new workspace under <c>RequireStreamerOAuthApp</c>.
    /// </summary>
    public string? CustomClientSecret { get; init; }

    /// <summary>
    /// Gets the UTC expiration timestamp for this auth flow payload.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
