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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Web.Security;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StreamActions.Web.Endpoints;

/// <summary>
/// Maps dynamic platform OAuth, external OIDC, and sign-out HTTP endpoints for web panel authentication.
/// </summary>
internal static class AuthEndpoints
{
    /// <summary>
    /// Name of the rate-limiter policy protecting login initiation and passkey-first sign-in endpoints.
    /// </summary>
    public const string LoginRateLimitPolicyName = "auth-login";

    /// <summary>
    /// Lifetime of the protected <c>StreamActions.AuthFlow</c> state cookie.
    /// </summary>
    public static readonly TimeSpan AuthFlowLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maps <c>/auth/{platform}/*</c>, <c>/auth/passkey/*</c>, <c>/auth/oidc/{slug}/*</c>, <c>/auth/login-hint/clear</c>, and <c>/auth/logout</c> endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The configured <see cref="IEndpointRouteBuilder"/>.</returns>
    internal static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet("/auth/{platform}/login", HandlePlatformLoginAsync);
        _ = endpoints.MapPost("/auth/{platform}/login", HandlePlatformLoginAsync)
            .RequireRateLimiting(LoginRateLimitPolicyName);
        _ = endpoints.MapGet("/auth/{platform}/callback", HandlePlatformCallbackAsync);

        _ = endpoints.MapPost("/auth/passkey/request-options", HandlePasskeyRequestOptionsAsync)
            .RequireRateLimiting(LoginRateLimitPolicyName);
        _ = endpoints.MapPost("/auth/passkey/login", HandlePasskeyLoginAsync)
            .RequireRateLimiting(LoginRateLimitPolicyName);

        _ = endpoints.MapGet("/auth/oidc/{slug}/login", HandleOidcLoginAsync);
        _ = endpoints.MapGet("/auth/oidc/{slug}/callback", HandleOidcCallbackAsync);

        _ = endpoints.MapPost("/auth/login-hint/clear", HandleClearLoginHintAsync);
        _ = endpoints.MapPost("/auth/workspace/select", HandleWorkspaceSelectAsync);
        _ = endpoints.MapPost("/auth/logout", HandleLogoutAsync);
        _ = endpoints.MapPost("/account/sessions/revoke-others", HandleRevokeOtherSessionsAsync);

        return endpoints;
    }

    /// <summary>
    /// Initiates a platform OAuth/OIDC authorization redirect for <c>login</c>, <c>setup</c>, <c>broadcaster</c>, <c>bot</c>, <c>instance-bot</c>, <c>link</c>, or <c>stepup</c>.
    /// </summary>
    /// <param name="platform">The platform identifier route parameter.</param>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="providers">All registered platform OAuth providers.</param>
    /// <param name="authorizationService">The authorization service for gated flows.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="oauthClientConfigResolver">The OAuth client credential resolver.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
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
        IWorkspaceStore workspaceStore,
        IPanelUserStore panelUserStore,
        IWorkspaceAccessService workspaceAccessService,
        IMfaCredentialStore mfaCredentialStore,
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
        string? customRedirectUri = null;
        string? identifier = null;
        string? useLoginHint = null;

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
            customRedirectUri = form["customRedirectUri"].FirstOrDefault();
            identifier = form["identifier"].FirstOrDefault();
            useLoginHint = form["useLoginHint"].FirstOrDefault();
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

        Guid? stepUpUserId = null;
        if (string.Equals(normalizedFlow, "stepup", StringComparison.Ordinal))
        {
            stepUpUserId = AuthClaims.GetPanelUserId(httpContext.User);
            if (!stepUpUserId.HasValue
                || await mfaCredentialStore.HasAnyCredentialsAsync(stepUpUserId.Value, cancellationToken).ConfigureAwait(false))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!workspaceId.HasValue
                && httpContext.Request.Cookies.TryGetValue(AuthClaims.WorkspaceCookieName, out string? rawCookieWorkspaceId)
                && Guid.TryParse(rawCookieWorkspaceId, out Guid cookieWorkspaceId)
                && cookieWorkspaceId != Guid.Empty)
            {
                workspaceId = cookieWorkspaceId;
            }
        }

        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(normalizedFlow, "login", StringComparison.Ordinal)
            && settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required)
        {
            return Results.Redirect("/login?error=oidc_required");
        }

        InstanceOAuthAppPolicy oauthPolicy = AuthPolicyParser.ResolveOAuthAppPolicy(settings, normalizedPlatform);

        string? clientId = null;
        string? clientSecret = null;
        Uri? explicitRedirectUri = null;
        bool storeCustomCredentialsInFlow = false;
        bool usedDbLookupOAuthApp = false;
        string? lookupClientId = null;

        if (HttpMethods.IsPost(httpContext.Request.Method)
            && string.Equals(normalizedFlow, "login", StringComparison.Ordinal)
            && oauthPolicy == InstanceOAuthAppPolicy.RequireStreamerOAuthApp
            && !string.IsNullOrWhiteSpace(customClientId))
        {
            if (!string.IsNullOrWhiteSpace(customRedirectUri))
            {
                if (!TryParseHttpRedirectUri(customRedirectUri, out Uri? parsedCustomRedirectUri))
                {
                    return Results.Redirect($"/login?error=invalid_redirect_uri&platform={Uri.EscapeDataString(normalizedPlatform)}");
                }

                explicitRedirectUri = parsedCustomRedirectUri;
            }

            clientId = customClientId.Trim();
            clientSecret = string.IsNullOrWhiteSpace(customClientSecret)
                ? null
                : customClientSecret.Trim();
            storeCustomCredentialsInFlow = true;
        }
        else if (HttpMethods.IsPost(httpContext.Request.Method)
            && string.Equals(normalizedFlow, "login", StringComparison.Ordinal)
            && string.Equals(useLoginHint, "true", StringComparison.OrdinalIgnoreCase))
        {
            LoginHintPayload? hint = AuthClaims.TryReadLoginHint(httpContext, dataProtectionProvider);
            if (hint is not null)
            {
                ResolvedOAuthClientConfig? resolvedFromHint = await ResolveOAuthConfigFromLoginHintAsync(
                    normalizedPlatform,
                    hint,
                    oauthClientConfigResolver,
                    panelUserStore,
                    workspaceStore,
                    workspaceAccessService,
                    cancellationToken).ConfigureAwait(false);

                if (resolvedFromHint is not null)
                {
                    clientId = resolvedFromHint.ClientId;
                    clientSecret = resolvedFromHint.ClientSecret;
                    explicitRedirectUri = resolvedFromHint.RedirectUri;
                    workspaceId ??= resolvedFromHint.WorkspaceId;

                    if (oauthPolicy == InstanceOAuthAppPolicy.RequireStreamerOAuthApp && resolvedFromHint.IsWorkspaceOverride)
                    {
                        usedDbLookupOAuthApp = true;
                        lookupClientId = resolvedFromHint.ClientId;
                    }
                }
            }
        }
        else if (HttpMethods.IsPost(httpContext.Request.Method)
            && string.Equals(normalizedFlow, "login", StringComparison.Ordinal)
            && oauthPolicy == InstanceOAuthAppPolicy.RequireStreamerOAuthApp
            && !string.IsNullOrWhiteSpace(identifier))
        {
            (Workspace MatchedWorkspace, PlatformOAuthAppCredentials Credentials)? lookupMatch = await LookupWorkspaceOAuthForIdentifierAsync(
                normalizedPlatform,
                identifier.Trim(),
                workspaceStore,
                panelUserStore,
                workspaceAccessService,
                cancellationToken).ConfigureAwait(false);

            if (lookupMatch.HasValue)
            {
                clientId = lookupMatch.Value.Credentials.ClientId;
                clientSecret = lookupMatch.Value.Credentials.ClientSecret;
                explicitRedirectUri = lookupMatch.Value.Credentials.RedirectUri;
                workspaceId ??= lookupMatch.Value.MatchedWorkspace.Id;
                usedDbLookupOAuthApp = true;
                lookupClientId = lookupMatch.Value.Credentials.ClientId;
            }
        }
        else
        {
            ResolvedOAuthClientConfig? resolved = await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, workspaceId, flow: normalizedFlow, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (resolved is null
                && string.Equals(normalizedFlow, "stepup", StringComparison.Ordinal)
                && stepUpUserId.HasValue)
            {
                resolved = await ResolveOAuthConfigFromLoginHintAsync(
                    normalizedPlatform,
                    new LoginHintPayload(stepUpUserId.Value, workspaceId, normalizedPlatform, "stepup"),
                    oauthClientConfigResolver,
                    panelUserStore,
                    workspaceStore,
                    workspaceAccessService,
                    cancellationToken).ConfigureAwait(false);
            }

            if (resolved is not null)
            {
                clientId = resolved.ClientId;
                clientSecret = resolved.ClientSecret;
                explicitRedirectUri = resolved.RedirectUri;
                workspaceId ??= resolved.WorkspaceId;
            }
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Results.Redirect($"/login?error=oauth_not_configured&platform={Uri.EscapeDataString(normalizedPlatform)}");
        }

        string state = GenerateRandomToken();
        bool useOidcNonce = normalizedFlow is "login" or "setup" or "link" or "stepup";
        string? nonce = useOidcNonce ? GenerateRandomToken() : null;
        string codeVerifier = GeneratePkceCodeVerifier();
        string codeChallenge = ComputePkceS256Challenge(codeVerifier);
        bool forceVerify = normalizedFlow is "bot" or "instance-bot" or "stepup";
        IReadOnlyList<string> scopes = ResolveRequestedScopes(provider, normalizedFlow, rawScopes);

        Uri redirectUri = explicitRedirectUri ?? BuildCallbackUri(httpContext.Request, $"/auth/{normalizedPlatform}/callback");
        Uri authorizationUri = provider.BuildAuthorizationUri(
            clientId,
            redirectUri,
            scopes,
            state,
            forceVerify: forceVerify,
            nonce: nonce,
            codeChallenge: codeChallenge,
            codeChallengeMethod: "S256");

        AuthFlowCookiePayload payload = new()
        {
            State = state,
            Nonce = nonce,
            Flow = normalizedFlow,
            PlatformId = normalizedPlatform,
            ReturnUrl = AuthClaims.IsSafeLocalRedirect(returnUrl) ? returnUrl!.Trim() : null,
            WorkspaceId = workspaceId,
            StepUpUserId = stepUpUserId,
            CodeVerifier = codeVerifier,
            CustomClientId = storeCustomCredentialsInFlow ? clientId : null,
            CustomClientSecret = storeCustomCredentialsInFlow ? clientSecret : null,
            CustomRedirectUri = storeCustomCredentialsInFlow ? explicitRedirectUri?.ToString() : null,
            UsedDbLookupOAuthApp = usedDbLookupOAuthApp,
            LookupClientId = lookupClientId,
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

        string normalizedFlow = string.IsNullOrWhiteSpace(flowPayload.Flow)
            ? "login"
            : flowPayload.Flow.Trim().ToLowerInvariant();

        string? clientId;
        string? clientSecret;
        Uri? explicitRedirectUri = null;

        if (!string.IsNullOrWhiteSpace(flowPayload.CustomClientId))
        {
            clientId = flowPayload.CustomClientId;
            clientSecret = string.IsNullOrWhiteSpace(flowPayload.CustomClientSecret)
                ? null
                : flowPayload.CustomClientSecret;
            if (!string.IsNullOrWhiteSpace(flowPayload.CustomRedirectUri)
                && TryParseHttpRedirectUri(flowPayload.CustomRedirectUri, out Uri? parsedCustomRedirectUri))
            {
                explicitRedirectUri = parsedCustomRedirectUri;
            }
        }
        else
        {
            ResolvedOAuthClientConfig? resolved = await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, flowPayload.WorkspaceId, flow: normalizedFlow, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (resolved is null)
            {
                return Results.Redirect($"/login?error=oauth_not_configured&platform={Uri.EscapeDataString(normalizedPlatform)}");
            }

            clientId = resolved.ClientId;
            clientSecret = string.IsNullOrWhiteSpace(resolved.ClientSecret) ? null : resolved.ClientSecret;
            explicitRedirectUri = resolved.RedirectUri;
        }

        string? codeVerifier = string.IsNullOrWhiteSpace(flowPayload.CodeVerifier)
            ? null
            : flowPayload.CodeVerifier;

        if (string.IsNullOrWhiteSpace(clientId)
            || (string.IsNullOrWhiteSpace(clientSecret) && codeVerifier is null))
        {
            return Results.BadRequest("Invalid or expired authentication state.");
        }

        Uri redirectUri = explicitRedirectUri ?? BuildCallbackUri(httpContext.Request, $"/auth/{normalizedPlatform}/callback");
        PlatformOAuthTokenResult tokenResult = await provider
            .ExchangeCodeAsync(clientId, clientSecret, code, redirectUri, flowPayload.Nonce, codeVerifier, cancellationToken)
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
                mfaPolicyService,
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
                workspaceAccessService,
                platformCredentialStore,
                permissionService,
                mfaPolicyService,
                mfaCredentialStore,
                dataProtectionProvider,
                cancellationToken).ConfigureAwait(false),

            "stepup" => await CompleteStepUpReauthCallbackAsync(
                httpContext,
                normalizedPlatform,
                platformUserId,
                flowPayload,
                authorizationService,
                panelUserStore,
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
    /// <param name="authorizationService">The authorization service for gated step-up flows.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="oidcProviderStore">The external OIDC provider store.</param>
    /// <param name="oidcClient">The external OIDC client.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the OIDC provider or an error response.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "OIDC provider slugs are normalized to lowercase invariant.")]
    private static async Task<IResult> HandleOidcLoginAsync(
        string slug,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInstanceSettingsService instanceSettingsService,
        IExternalOidcProviderStore oidcProviderStore,
        IExternalOidcClient oidcClient,
        IMfaCredentialStore mfaCredentialStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Disabled)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        string? flow = httpContext.Request.Query["flow"].FirstOrDefault();
        string normalizedFlow = string.IsNullOrWhiteSpace(flow)
            ? "login"
            : flow.Trim().ToLowerInvariant();
        if (normalizedFlow is not ("login" or "oidc" or "stepup"))
        {
            return Results.BadRequest("Unsupported authentication flow.");
        }

        bool isStepUp = string.Equals(normalizedFlow, "stepup", StringComparison.Ordinal);
        Guid? stepUpUserId = null;
        if (isStepUp)
        {
            IResult? gateFailure = await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.MfaPendingPolicy)
                .ConfigureAwait(false);
            if (gateFailure is not null)
            {
                return gateFailure;
            }

            stepUpUserId = AuthClaims.GetPanelUserId(httpContext.User);
            if (!stepUpUserId.HasValue
                || await mfaCredentialStore.HasAnyCredentialsAsync(stepUpUserId.Value, cancellationToken).ConfigureAwait(false))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
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
        if (provider is null && workspaceId.HasValue)
        {
            provider = await oidcProviderStore
                .GetBySlugAsync(workspaceId: null, normalizedSlug, cancellationToken)
                .ConfigureAwait(false);
        }

        if (provider is null || !provider.IsEnabled)
        {
            return Results.NotFound();
        }

        Uri redirectUri = BuildCallbackUri(httpContext.Request, $"/auth/oidc/{Uri.EscapeDataString(provider.Slug)}/callback");
        ExternalOidcAuthorizationRequest authRequest = await oidcClient
            .BuildAuthorizationRequestAsync(
                provider,
                redirectUri,
                prompt: isStepUp ? "login" : null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string? returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault();
        AuthFlowCookiePayload payload = new()
        {
            State = authRequest.State,
            Nonce = authRequest.Nonce,
            CodeVerifier = authRequest.CodeVerifier,
            Flow = isStepUp ? "stepup" : "oidc",
            OidcSlug = provider.Slug,
            WorkspaceId = workspaceId,
            StepUpUserId = stepUpUserId,
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
    /// <param name="authorizationService">The authorization service for gated step-up flows.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="oidcProviderStore">The external OIDC provider store.</param>
    /// <param name="oidcClient">The external OIDC client.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="workspaceStore">The workspace store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
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
        IAuthorizationService authorizationService,
        IInstanceSettingsService instanceSettingsService,
        IExternalOidcProviderStore oidcProviderStore,
        IExternalOidcClient oidcClient,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IWorkspaceAccessService workspaceAccessService,
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

        bool isStepUp = flowPayload is not null && string.Equals(flowPayload.Flow, "stepup", StringComparison.Ordinal);
        if (flowPayload is null
            || (!string.Equals(flowPayload.Flow, "oidc", StringComparison.Ordinal) && !isStepUp)
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
        if (provider is null && flowPayload.WorkspaceId.HasValue)
        {
            provider = await oidcProviderStore
                .GetBySlugAsync(workspaceId: null, normalizedSlug, cancellationToken)
                .ConfigureAwait(false);
        }

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
        if (isStepUp)
        {
            return await CompleteStepUpReauthCallbackAsync(
                httpContext,
                identityProviderId,
                subject,
                flowPayload,
                authorizationService,
                panelUserStore,
                mfaCredentialStore,
                cancellationToken).ConfigureAwait(false);
        }
        string username = string.IsNullOrWhiteSpace(authResult.Username) ? subject : authResult.Username.Trim();
        string displayName = string.IsNullOrWhiteSpace(authResult.DisplayName) ? username : authResult.DisplayName.Trim();
        IReadOnlyList<string> groups = authResult.Groups ?? [];

        PanelUser? user = await panelUserStore
            .GetByLoginIdentityAsync(identityProviderId, subject, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            (bool hasWorkspaceAccess, bool explicitlyDenied, List<Workspace> _) = await EvaluateAnyWorkspacePanelAccessAsync(
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

        if (flowPayload.WorkspaceId.HasValue)
        {
            IReadOnlyList<UserLoginIdentity> userIdentities = await panelUserStore
                .GetLoginIdentitiesAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);
            bool hasWorkspacePermission = await workspaceAccessService
                .HasPermissionInWorkspaceAsync(
                    flowPayload.WorkspaceId.Value,
                    user,
                    userIdentities,
                    BuiltInPermissionKeys.UiPanelAccess,
                    groups,
                    cancellationToken)
                .ConfigureAwait(false);
            if (hasWorkspacePermission)
            {
                WriteWorkspaceCookie(httpContext, flowPayload.WorkspaceId.Value);
            }
        }

        return await CompleteInteractiveLoginSignInAsync(
            httpContext,
            user,
            groups,
            identityProviderId,
            flowPayload.ReturnUrl,
            loginHintWorkspaceId: flowPayload.WorkspaceId,
            mfaPolicyWorkspaceId: flowPayload.WorkspaceId,
            panelUserStore,
            workspaceStore,
            mfaPolicyService,
            mfaCredentialStore,
            dataProtectionProvider,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the <c>StreamActions.LoginHint</c> returning-user cookie and redirects to <c>/login?useDifferentAccount=true</c>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <returns>A redirect to <c>/login?useDifferentAccount=true</c> or <c>400 Bad Request</c> if antiforgery validation fails.</returns>
    private static async Task<IResult> HandleClearLoginHintAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery)
    {
        if (httpContext.Request.HasFormContentType)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }
        }

        AuthClaims.DeleteLoginHintCookie(httpContext);
        return Results.Redirect("/login?useDifferentAccount=true");
    }

    /// <summary>
    /// Validates the antiforgery token, revokes the active session if tracked, clears the authentication cookie, and redirects to <c>/login</c>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to <c>/login</c> or <c>400 Bad Request</c> if antiforgery validation fails.</returns>
    private static async Task<IResult> HandleLogoutAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }

        if (AuthClaims.GetSessionId(httpContext.User) is Guid sid
            && httpContext.RequestServices.GetService<IPanelSessionService>() is IPanelSessionService sessionService)
        {
            _ = await sessionService.RevokeSessionAsync(sid, cancellationToken).ConfigureAwait(false);
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        DeleteCookie(httpContext, AuthClaims.WorkspaceCookieName);
        return Results.Redirect("/login");
    }

    /// <summary>
    /// Revokes all active panel sessions for the signed-in user other than the current session.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A JSON response containing <c>revokedCount</c>, a redirect to <c>/account/mfa</c>, or an authorization failure result.</returns>
    private static async Task<IResult> HandleRevokeOtherSessionsAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        IResult? authFailure = await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.MfaPendingPolicy)
            .ConfigureAwait(false);
        if (authFailure is not null)
        {
            return authFailure;
        }

        if (httpContext.Request.HasFormContentType)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }
        }

        Guid? currentUserId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!currentUserId.HasValue)
        {
            return Results.Challenge();
        }

        Guid currentSessionId = AuthClaims.GetSessionId(httpContext.User) ?? Guid.Empty;
        int revokedCount = 0;
        if (httpContext.RequestServices.GetService<IPanelSessionService>() is IPanelSessionService sessionService)
        {
            revokedCount = await sessionService
                .RevokeOtherSessionsAsync(currentUserId.Value, currentSessionId, cancellationToken)
                .ConfigureAwait(false);
        }

        return PrefersHtmlResponse(httpContext.Request)
            ? Results.Redirect("/account/mfa")
            : Results.Ok(new RevokeOtherSessionsResponse
            {
                RevokedCount = revokedCount,
            });
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
        string? authProvider = httpContext.User.FindFirstValue(AuthClaims.AuthProviderClaimType);
        bool isMfaVerified = AuthClaims.HasTrueClaim(httpContext.User, AuthClaims.MfaVerifiedClaimType);

        IReadOnlyList<AccessibleWorkspace> accessible = await workspaceAccessService
            .GetAccessibleWorkspacesAsync(user, identities, oidcGroups, authProvider, isMfaVerified, cancellationToken)
            .ConfigureAwait(false);

        AccessibleWorkspace? target = accessible.FirstOrDefault(w => w.Workspace.Id == workspaceId);
        if (target is null)
        {
            return Results.Forbid();
        }

        string safeReturnUrl = AuthClaims.SafeReturnUrlOrDefault(returnUrl, "/");

        if (target.AuthStatus == WorkspaceSessionAuthStatus.RequiresOidcReauth)
        {
            if (!string.IsNullOrWhiteSpace(target.RequiredOidcProviderSlug))
            {
                string oidcUrl = $"/auth/oidc/{Uri.EscapeDataString(target.RequiredOidcProviderSlug)}/login?workspaceId={workspaceId:D}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
                return Results.Redirect(oidcUrl);
            }

            return Results.Redirect("/login?error=workspace_oidc_required");
        }

        if (target.AuthStatus == WorkspaceSessionAuthStatus.RequiresMfaChallenge)
        {
            string mfaUrl = $"/auth/mfa?workspaceId={workspaceId:D}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
            return Results.Redirect(mfaUrl);
        }

        WriteWorkspaceCookie(httpContext, workspaceId);
        return Results.Redirect(safeReturnUrl);
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
            "stepup" => await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.MfaPendingPolicy)
                                .ConfigureAwait(false),
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
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to <c>/</c>, <c>/auth/mfa</c>, or <c>401 Unauthorized</c> if the setup ticket is invalid.</returns>
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
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        if (!SetupEndpoints.TryReadSetupTicket(httpContext, dataProtectionProvider, out SetupTicketPayload? setupTicket))
        {
            return Results.Unauthorized();
        }

        bool resetRootMfa = setupTicket?.ResetRootMfa ?? true;

        PanelUser? existingByIdentity = await panelUserStore
            .GetByLoginIdentityAsync(platformId, platformUserId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PanelUser> allUsers = await panelUserStore.GetAllUsersAsync(cancellationToken).ConfigureAwait(false);
        PanelUser? rootUser = allUsers.FirstOrDefault(static u => u.IsRoot);

        bool isRootRecovery = setupTicket?.IsRootRecovery == true || rootUser is not null;
        if (!isRootRecovery)
        {
            IPermissionStore? permissionStore = httpContext.RequestServices.GetService<IPermissionStore>();
            if (permissionStore is not null)
            {
                _ = await permissionStore
                    .SeedDefaultGlobalGroupAsync(setupTicket?.EnabledDangerousFeatures, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

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

            if (resetRootMfa)
            {
                _ = await mfaCredentialStore
                    .ClearAllForUserAsync(rootUser.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
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

        if (!resetRootMfa
            && await mfaCredentialStore.HasAnyCredentialsAsync(rootUser.Id, cancellationToken).ConfigureAwait(false))
        {
            return await CompleteInteractiveLoginSignInAsync(
                httpContext,
                rootUser,
                oidcGroups: null,
                platformId,
                flowPayload.ReturnUrl,
                loginHintWorkspaceId: targetWorkspace.Id,
                mfaPolicyWorkspaceId: null,
                panelUserStore,
                workspaceStore,
                mfaPolicyService,
                mfaCredentialStore,
                dataProtectionProvider,
                cancellationToken).ConfigureAwait(false);
        }

        AuthClaims.WriteLoginHintCookie(
            httpContext,
            dataProtectionProvider,
            new LoginHintPayload(rootUser.Id, targetWorkspace.Id, platformId, rootUser.DisplayName));

        Guid? sessionId = await CreatePanelSessionIfRegisteredAsync(
            httpContext,
            rootUser.Id,
            platformId,
            cancellationToken).ConfigureAwait(false);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthClaims.BuildClaimsPrincipal(rootUser, authProvider: platformId, sessionId: sessionId)).ConfigureAwait(false);

        return Results.Redirect(AuthClaims.SafeReturnUrlOrDefault(flowPayload.ReturnUrl, "/"));
    }

    /// <summary>
    /// Completes a forced re-authentication step-up (<c>flow=stepup</c>) callback for users with zero enrolled MFA credentials.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="providerId">The authenticated provider identifier (<c>twitch</c> or <c>oidc:{slug}</c>).</param>
    /// <param name="providerUserId">The authenticated provider user identifier or OIDC subject.</param>
    /// <param name="flowPayload">The verified auth flow cookie payload.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="panelUserStore">The panel user store.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the safe return URL, or <c>403</c>/<c>/access-denied</c> on validation failure.</returns>
    private static async Task<IResult> CompleteStepUpReauthCallbackAsync(
        HttpContext httpContext,
        string providerId,
        string providerUserId,
        AuthFlowCookiePayload flowPayload,
        IAuthorizationService authorizationService,
        IPanelUserStore panelUserStore,
        IMfaCredentialStore mfaCredentialStore,
        CancellationToken cancellationToken)
    {
        IResult? authFailure = await AuthorizePolicyAsync(httpContext, authorizationService, AuthClaims.MfaPendingPolicy)
            .ConfigureAwait(false);
        if (authFailure is not null)
        {
            return authFailure;
        }

        Guid? currentUserId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!currentUserId.HasValue
            || !flowPayload.StepUpUserId.HasValue
            || currentUserId.Value != flowPayload.StepUpUserId.Value)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        PanelUser? user = await panelUserStore.GetByIdAsync(currentUserId.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (await mfaCredentialStore.HasAnyCredentialsAsync(user.Id, cancellationToken).ConfigureAwait(false))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);
        string normalizedProviderId = providerId.Trim();
        string normalizedProviderUserId = providerUserId.Trim();

        bool identityMatches = identities.Any(identity =>
            string.Equals(identity.ProviderId, normalizedProviderId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(identity.ProviderUserId, normalizedProviderUserId, StringComparison.OrdinalIgnoreCase));
        if (!identityMatches)
        {
            return Results.Redirect("/access-denied");
        }

        if (user.MfaResetByAdminNoticePending)
        {
            user.MfaResetByAdminNoticePending = false;
            _ = await panelUserStore.UpdateUserAsync(user, cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset elevatedUntilUtc = GetTimeProvider(httpContext).GetUtcNow() + AuthClaims.StepUpWindow;
        ClaimsPrincipal elevatedPrincipal = AuthClaims.WithStepUpElevated(httpContext.User, elevatedUntilUtc);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            elevatedPrincipal).ConfigureAwait(false);

        return Results.Redirect(AuthClaims.SafeReturnUrlOrDefault(flowPayload.ReturnUrl, "/"));
    }

    /// <summary>
    /// Completes the regular platform login flow, checking DB-lookup identity binding, External OIDC workspace guards, existing accounts, workspace <c>ui.panel.access</c>, or open registration.
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
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    /// <param name="platformCredentialStore">The platform credential store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to the panel, MFA challenge/enrollment route, login error, or <c>/access-denied</c>.</returns>
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
        IWorkspaceAccessService workspaceAccessService,
        IPlatformCredentialStore platformCredentialStore,
        IPermissionService permissionService,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required)
        {
            return Results.Redirect("/login?error=oidc_required");
        }

        if (flowPayload.UsedDbLookupOAuthApp)
        {
            bool identityBound = !string.IsNullOrWhiteSpace(flowPayload.LookupClientId)
                && await VerifyDbLookupIdentityBindingAsync(
                    platformId,
                    platformUserId,
                    platformUsername,
                    flowPayload.LookupClientId,
                    panelUserStore,
                    workspaceStore,
                    permissionService,
                    cancellationToken).ConfigureAwait(false);

            if (!identityBound)
            {
                return Results.Redirect("/access-denied");
            }
        }

        Uri? customRedirectUri = !string.IsNullOrWhiteSpace(flowPayload.CustomRedirectUri)
            && TryParseHttpRedirectUri(flowPayload.CustomRedirectUri, out Uri? parsedCustomRedirectUri)
            ? parsedCustomRedirectUri
            : null;

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

            IReadOnlyList<UserLoginIdentity> userIdentities = await panelUserStore
                .GetLoginIdentitiesAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            if (!user.IsRoot && !user.IsGlobalAdmin)
            {
                IReadOnlyList<AccessibleWorkspace> preCheckAccessible = await workspaceAccessService
                    .GetAccessibleWorkspacesAsync(user, userIdentities, oidcGroupClaims: null, authProvider: platformId, isMfaVerified: false, cancellationToken)
                    .ConfigureAwait(false);

                if (preCheckAccessible.Count > 0
                    && preCheckAccessible.All(static w => w.AuthStatus == WorkspaceSessionAuthStatus.RequiresOidcReauth))
                {
                    return Results.Redirect("/login?error=workspace_oidc_required");
                }
            }

            IReadOnlyList<Workspace> ownedWorkspaces = await workspaceStore
                .GetByOwnerAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);
            Workspace? matchingOwned = ownedWorkspaces.FirstOrDefault(w =>
                string.Equals(w.PrimaryPlatformId, platformId, StringComparison.OrdinalIgnoreCase));

            if (!flowPayload.UsedDbLookupOAuthApp
                && !string.IsNullOrWhiteSpace(flowPayload.CustomClientId))
            {
                bool clearSecret = string.IsNullOrWhiteSpace(flowPayload.CustomClientSecret);
                if (matchingOwned is not null)
                {
                    _ = await workspaceStore.SetCustomOAuthAppAsync(
                        matchingOwned.Id,
                        platformId,
                        flowPayload.CustomClientId,
                        flowPayload.CustomClientSecret,
                        customRedirectUri,
                        clearClientSecret: clearSecret,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else if (ownedWorkspaces.Count == 0 && settings.AllowOpenRegistration)
                {
                    Workspace additionalWorkspace = await workspaceStore.CreateWorkspaceAsync(
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
                            CustomRedirectUri = customRedirectUri?.ToString(),
                        },
                        cancellationToken).ConfigureAwait(false);

                    _ = await workspaceStore.SetCustomOAuthAppAsync(
                        additionalWorkspace.Id,
                        platformId,
                        flowPayload.CustomClientId,
                        flowPayload.CustomClientSecret,
                        customRedirectUri,
                        clearClientSecret: clearSecret,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _ = await platformCredentialStore
                        .SaveTokenAsync(additionalWorkspace.Id, platformId, TokenRole.Broadcaster, tokenPayload, CredentialStatus.Active, cancellationToken)
                        .ConfigureAwait(false);

                    matchingOwned = additionalWorkspace;
                }
            }

            IReadOnlyList<AccessibleWorkspace> accessibleAfter = await workspaceAccessService
                .GetAccessibleWorkspacesAsync(user, userIdentities, oidcGroupClaims: null, authProvider: platformId, isMfaVerified: false, cancellationToken)
                .ConfigureAwait(false);

            Guid? preferredWorkspaceId = SelectPreferredReadyWorkspaceId(
                accessibleAfter,
                flowPayload.WorkspaceId,
                matchingOwned?.Id);
            if (preferredWorkspaceId.HasValue)
            {
                WriteWorkspaceCookie(httpContext, preferredWorkspaceId.Value);
            }

            return await CompleteInteractiveLoginSignInAsync(
                httpContext,
                user,
                oidcGroups: null,
                platformId,
                flowPayload.ReturnUrl,
                loginHintWorkspaceId: preferredWorkspaceId,
                mfaPolicyWorkspaceId: null,
                panelUserStore,
                workspaceStore,
                mfaPolicyService,
                mfaCredentialStore,
                dataProtectionProvider,
                cancellationToken).ConfigureAwait(false);
        }

        (bool hasPanelAccess, bool explicitlyDenied, List<Workspace> grantedWorkspaces) = await EvaluateAnyWorkspacePanelAccessAsync(
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
            IExternalOidcProviderStore? oidcProviderStore = httpContext.RequestServices.GetService<IExternalOidcProviderStore>();
            if (await AllWorkspacesRequireExternalOidcAsync(settings, grantedWorkspaces, oidcProviderStore, cancellationToken).ConfigureAwait(false))
            {
                return Results.Redirect("/login?error=workspace_oidc_required");
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

            IReadOnlyList<UserLoginIdentity> newUserIdentities = await panelUserStore
                .GetLoginIdentitiesAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<AccessibleWorkspace> newUserAccessible = await workspaceAccessService
                .GetAccessibleWorkspacesAsync(user, newUserIdentities, oidcGroupClaims: null, authProvider: platformId, isMfaVerified: false, cancellationToken)
                .ConfigureAwait(false);
            Guid? preferredWorkspaceId = SelectPreferredReadyWorkspaceId(
                newUserAccessible,
                flowPayload.WorkspaceId,
                preferredOwnedId: null);
            if (preferredWorkspaceId.HasValue)
            {
                WriteWorkspaceCookie(httpContext, preferredWorkspaceId.Value);
            }

            return await CompleteInteractiveLoginSignInAsync(
                httpContext,
                user,
                oidcGroups: null,
                platformId,
                flowPayload.ReturnUrl,
                loginHintWorkspaceId: preferredWorkspaceId,
                mfaPolicyWorkspaceId: null,
                panelUserStore,
                workspaceStore,
                mfaPolicyService,
                mfaCredentialStore,
                dataProtectionProvider,
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
                CustomRedirectUri = customRedirectUri?.ToString(),
            },
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(flowPayload.CustomClientId))
        {
            _ = await workspaceStore.SetCustomOAuthAppAsync(
                createdWorkspace.Id,
                platformId,
                flowPayload.CustomClientId,
                flowPayload.CustomClientSecret,
                customRedirectUri,
                clearClientSecret: string.IsNullOrWhiteSpace(flowPayload.CustomClientSecret),
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
            loginHintWorkspaceId: createdWorkspace.Id,
            mfaPolicyWorkspaceId: null,
            panelUserStore,
            workspaceStore,
            mfaPolicyService,
            mfaCredentialStore,
            dataProtectionProvider,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates effective MFA policies and enrolled credentials for <paramref name="user"/>, signs in with appropriate claims, issues the login hint cookie when complete, and redirects to the panel or MFA workflow.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="oidcGroups">Optional OIDC group claims from the current session.</param>
    /// <param name="authProvider">The authentication provider identifier.</param>
    /// <param name="returnUrl">The optional local return URL.</param>
    /// <param name="loginHintWorkspaceId">The optional workspace identifier stored in the login hint cookie.</param>
    /// <param name="mfaPolicyWorkspaceId">The optional explicit workspace identifier for targeted MFA challenge redirects.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect to <c>/auth/mfa</c>, <c>/account/mfa?enrollRequired=true</c>, or the safe return URL.</returns>
    private static async Task<IResult> CompleteInteractiveLoginSignInAsync(
        HttpContext httpContext,
        PanelUser user,
        IReadOnlyCollection<string>? oidcGroups,
        string authProvider,
        string? returnUrl,
        Guid? loginHintWorkspaceId,
        Guid? mfaPolicyWorkspaceId,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        EffectiveMfaPolicy policy = await mfaPolicyService
            .GetEffectivePolicyForUserAsync(user, identities, oidcGroups, workspaceId: mfaPolicyWorkspaceId, cancellationToken)
            .ConfigureAwait(false);
        bool isLoginMfaRequired = await mfaPolicyService
            .IsLoginMfaRequiredAsync(user, identities, oidcGroups, workspaceId: mfaPolicyWorkspaceId, cancellationToken)
            .ConfigureAwait(false);
        bool hasAnyCredentials = await mfaCredentialStore
            .HasAnyCredentialsAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        string? safeReturnUrl = AuthClaims.IsSafeLocalRedirect(returnUrl) ? returnUrl!.Trim() : null;
        Guid? sessionId = await CreatePanelSessionIfRegisteredAsync(
            httpContext,
            user.Id,
            authProvider,
            cancellationToken).ConfigureAwait(false);

        if (!policy.IsMfaDisabled && hasAnyCredentials)
        {
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AuthClaims.BuildClaimsPrincipal(
                    user,
                    oidcGroups,
                    authProvider: authProvider,
                    mfaPending: true,
                    sessionId: sessionId)).ConfigureAwait(false);

            string mfaRedirect = BuildMfaChallengeUrl("/auth/mfa", mfaPolicyWorkspaceId, safeReturnUrl, enrollRequired: false);
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
                    mfaEnrollmentRequired: true,
                    sessionId: sessionId)).ConfigureAwait(false);

            string enrollRedirect = BuildMfaChallengeUrl("/account/mfa", mfaPolicyWorkspaceId, safeReturnUrl, enrollRequired: true);
            return Results.Redirect(enrollRedirect);
        }

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthClaims.BuildClaimsPrincipal(user, oidcGroups, authProvider: authProvider, sessionId: sessionId)).ConfigureAwait(false);

        await IssueLoginHintForUserAsync(
            httpContext,
            user,
            identities,
            authProvider,
            loginHintWorkspaceId,
            workspaceStore,
            dataProtectionProvider,
            cancellationToken).ConfigureAwait(false);

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
        string? authProvider = httpContext.User.FindFirstValue(AuthClaims.AuthProviderClaimType);
        bool isMfaVerified = AuthClaims.HasTrueClaim(httpContext.User, AuthClaims.MfaVerifiedClaimType);

        bool canAccess = await workspaceAccessService
            .CanAccessWorkspaceAsync(flowPayload.WorkspaceId.Value, user, identities, oidcGroups, authProvider, isMfaVerified, cancellationToken)
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
    /// Generates discoverable WebAuthn passkey request options (<c>userVerification: "required"</c>) for Tier 1 passwordless sign-in.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="passkeyHandler">The ASP.NET Core Identity passkey handler.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The WebAuthn request options JSON, or <c>403 Forbidden</c> / redirect when disabled by policy.</returns>
    private static async Task<IResult> HandlePasskeyRequestOptionsAsync(
        HttpContext httpContext,
        IInstanceSettingsService instanceSettingsService,
        IPasskeyHandler<PanelUser> passkeyHandler,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required)
        {
            return CreateOidcRejectionResult(httpContext, "oidc_required");
        }

        if (settings.MfaPasskeyAvailability == MfaMethodAvailability.Disabled)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        PasskeyRequestOptionsResult optionsResult = await passkeyHandler
            .MakeRequestOptionsAsync(user: null, httpContext)
            .ConfigureAwait(false);

        string requestOptionsJson = EnsureRequiredUserVerification(optionsResult.RequestOptionsJson);

        WritePasskeyLoginStateCookie(
            httpContext,
            dataProtectionProvider,
            new PasskeyLoginStatePayload
            {
                State = optionsResult.AssertionState,
                ExpiresAtUtc = GetTimeProvider(httpContext).GetUtcNow() + AuthFlowLifetime,
            });

        return Results.Content(requestOptionsJson, "application/json");
    }

    /// <summary>
    /// Verifies a discoverable WebAuthn passkey assertion with required user verification, enforces External OIDC workspace guards, and signs in the user.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    /// <param name="passkeyHandler">The ASP.NET Core Identity passkey handler.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A JSON sign-in response containing <c>returnUrl</c>, a redirect, or an authorization error.</returns>
    private static async Task<IResult> HandlePasskeyLoginAsync(
        HttpContext httpContext,
        IInstanceSettingsService instanceSettingsService,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IWorkspaceAccessService workspaceAccessService,
        IPasskeyHandler<PanelUser> passkeyHandler,
        UserManager<PanelUser> userManager,
        IDataProtectionProvider dataProtectionProvider,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        InstanceSettings settings = await instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required)
        {
            return CreateOidcRejectionResult(httpContext, "oidc_required");
        }

        if (settings.MfaPasskeyAvailability == MfaMethodAvailability.Disabled)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        (bool parsed, IResult? errorResult, PasskeyLoginSubmission? request) = await TryReadPasskeyLoginRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || request is null || string.IsNullOrWhiteSpace(request.CredentialJson))
        {
            return errorResult ?? Results.BadRequest("Credential payload is required.");
        }

        PasskeyLoginStatePayload? statePayload = ReadAndDeletePasskeyLoginStateCookie(httpContext, dataProtectionProvider);
        if (statePayload is null || string.IsNullOrWhiteSpace(statePayload.State))
        {
            return Results.BadRequest("Invalid or expired passkey login state.");
        }

        PasskeyAssertionResult<PanelUser> assertionResult = await passkeyHandler
            .PerformAssertionAsync(new PasskeyAssertionContext
            {
                HttpContext = httpContext,
                CredentialJson = request.CredentialJson,
                AssertionState = statePayload.State,
            })
            .ConfigureAwait(false);

        if (!assertionResult.Succeeded
            || assertionResult.Passkey is null
            || assertionResult.User is null
            || !assertionResult.Passkey.IsUserVerified)
        {
            return Results.Unauthorized();
        }

        PanelUser user = assertionResult.User;
        UserPasskeyInfo? existingPasskey = await userManager
            .GetPasskeyAsync(user, assertionResult.Passkey.CredentialId)
            .ConfigureAwait(false);
        if (existingPasskey is not null)
        {
            existingPasskey.SignCount = assertionResult.Passkey.SignCount;
            existingPasskey.IsBackedUp = assertionResult.Passkey.IsBackedUp;
            existingPasskey.IsUserVerified = assertionResult.Passkey.IsUserVerified;
            _ = await userManager.AddOrUpdatePasskeyAsync(user, existingPasskey).ConfigureAwait(false);
        }
        else
        {
            _ = await userManager.AddOrUpdatePasskeyAsync(user, assertionResult.Passkey).ConfigureAwait(false);
        }

        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<AccessibleWorkspace> accessible = await workspaceAccessService
            .GetAccessibleWorkspacesAsync(user, identities, oidcGroupClaims: null, authProvider: "passkey", isMfaVerified: true, cancellationToken)
            .ConfigureAwait(false);

        if (!user.IsRoot
            && !user.IsGlobalAdmin
            && accessible.Count > 0
            && accessible.All(static w => w.AuthStatus == WorkspaceSessionAuthStatus.RequiresOidcReauth))
        {
            return CreateOidcRejectionResult(httpContext, "workspace_oidc_required");
        }

        Guid? readyWorkspaceId = SelectPreferredReadyWorkspaceId(accessible, preferredWorkspaceId: null, preferredOwnedId: null);
        if (readyWorkspaceId.HasValue)
        {
            WriteWorkspaceCookie(httpContext, readyWorkspaceId.Value);
        }

        DateTimeOffset elevatedUntilUtc = GetTimeProvider(httpContext).GetUtcNow() + AuthClaims.StepUpWindow;
        Guid? sessionId = await CreatePanelSessionIfRegisteredAsync(
            httpContext,
            user.Id,
            "passkey",
            cancellationToken).ConfigureAwait(false);
        ClaimsPrincipal principal = AuthClaims.BuildClaimsPrincipal(
            user,
            oidcGroups: null,
            authProvider: "passkey",
            mfaPending: false,
            mfaEnrollmentRequired: false,
            mfaElevatedUntilUtc: elevatedUntilUtc,
            mfaVerified: true,
            sessionId: sessionId);

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal).ConfigureAwait(false);

        await IssueLoginHintForUserAsync(
            httpContext,
            user,
            identities,
            authProvider: "passkey",
            readyWorkspaceId,
            workspaceStore,
            dataProtectionProvider,
            cancellationToken).ConfigureAwait(false);

        string safeReturnUrl = AuthClaims.SafeReturnUrlOrDefault(request.ReturnUrl, "/");
        return PrefersHtmlResponse(httpContext.Request)
            ? Results.Redirect(safeReturnUrl)
            : Results.Ok(new PasskeyLoginResponse
            {
                Verified = true,
                ReturnUrl = safeReturnUrl,
            });
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
    /// <returns>A tuple indicating whether access was granted, whether any workspace explicitly denied access, and the workspaces granting access.</returns>
    private static async ValueTask<(bool HasPanelAccess, bool ExplicitlyDenied, List<Workspace> GrantedWorkspaces)> EvaluateAnyWorkspacePanelAccessAsync(
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

        List<Workspace> grantedWorkspaces = [];
        foreach (Workspace workspace in workspaces)
        {
            PermissionEvaluationResult result = await permissionService
                .EvaluateAsync(workspace.Id, actor, BuiltInPermissionKeys.UiPanelAccess, cancellationToken)
                .ConfigureAwait(false);

            if (result.EffectiveState == PermissionState.Deny)
            {
                return (false, true, []);
            }

            if (result.IsAllowed)
            {
                grantedWorkspaces.Add(workspace);
            }
        }

        return (grantedWorkspaces.Count > 0, false, grantedWorkspaces);
    }

    /// <summary>
    /// Resolves OAuth client credentials from a <see cref="LoginHintPayload"/> for Tier 2 returning-user sign-in.
    /// </summary>
    /// <param name="normalizedPlatform">The normalized lowercase platform identifier.</param>
    /// <param name="hint">The validated login hint payload.</param>
    /// <param name="oauthClientConfigResolver">The OAuth client credential resolver.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="ResolvedOAuthClientConfig"/>, or <see langword="null"/> if none could be resolved.</returns>
    private static async ValueTask<ResolvedOAuthClientConfig?> ResolveOAuthConfigFromLoginHintAsync(
        string normalizedPlatform,
        LoginHintPayload hint,
        IOAuthClientConfigResolver oauthClientConfigResolver,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IWorkspaceAccessService workspaceAccessService,
        CancellationToken cancellationToken)
    {
        if (hint.WorkspaceId.HasValue)
        {
            ResolvedOAuthClientConfig? direct = await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, hint.WorkspaceId.Value, flow: "login", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (direct is not null)
            {
                return direct;
            }
        }

        PanelUser? user = await panelUserStore.GetByIdAsync(hint.PanelUserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, workspaceId: null, flow: "login", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        IReadOnlyList<Workspace> owned = await workspaceStore.GetByOwnerAsync(user.Id, cancellationToken).ConfigureAwait(false);
        foreach (Workspace ws in owned)
        {
            ResolvedOAuthClientConfig? resolved = await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, ws.Id, flow: "login", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<AccessibleWorkspace> accessible = await workspaceAccessService
            .GetAccessibleWorkspacesAsync(user, identities, oidcGroupClaims: null, authProvider: normalizedPlatform, isMfaVerified: true, cancellationToken)
            .ConfigureAwait(false);

        foreach (AccessibleWorkspace ws in accessible)
        {
            ResolvedOAuthClientConfig? resolved = await oauthClientConfigResolver
                .ResolveAsync(normalizedPlatform, ws.Workspace.Id, flow: "login", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    /// <summary>
    /// Looks up a workspace and its custom OAuth credentials for <paramref name="normalizedPlatform"/> matching a Tier 3 login identifier
    /// (workspace slug, workspace name, platform channel ID, or a workspace owned by or accessible to a user with matching <see cref="UserLoginIdentity.ProviderUsername"/>).
    /// </summary>
    /// <param name="normalizedPlatform">The normalized lowercase platform identifier.</param>
    /// <param name="identifier">The user-submitted workspace or channel identifier.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matched workspace and decrypted credentials, or <see langword="null"/> if not found.</returns>
    private static async ValueTask<(Workspace Workspace, PlatformOAuthAppCredentials Credentials)?> LookupWorkspaceOAuthForIdentifierAsync(
        string normalizedPlatform,
        string identifier,
        IWorkspaceStore workspaceStore,
        IPanelUserStore panelUserStore,
        IWorkspaceAccessService workspaceAccessService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Workspace> workspaces = await workspaceStore.GetAllAsync(cancellationToken).ConfigureAwait(false);

        foreach (Workspace workspace in workspaces)
        {
            if (string.Equals(workspace.Slug, identifier, StringComparison.OrdinalIgnoreCase))
            {
                PlatformOAuthAppCredentials? creds = await workspaceStore
                    .GetCustomOAuthAppAsync(workspace.Id, normalizedPlatform, cancellationToken)
                    .ConfigureAwait(false);
                if (creds is not null)
                {
                    return (workspace, creds);
                }
            }
        }

        foreach (Workspace workspace in workspaces)
        {
            if (string.Equals(workspace.Name, identifier, StringComparison.OrdinalIgnoreCase))
            {
                PlatformOAuthAppCredentials? creds = await workspaceStore
                    .GetCustomOAuthAppAsync(workspace.Id, normalizedPlatform, cancellationToken)
                    .ConfigureAwait(false);
                if (creds is not null)
                {
                    return (workspace, creds);
                }
            }
        }

        foreach (Workspace workspace in workspaces)
        {
            WorkspacePlatform? platform = await workspaceStore
                .GetPlatformAsync(workspace.Id, normalizedPlatform, cancellationToken)
                .ConfigureAwait(false);
            if (platform is not null && string.Equals(platform.PlatformChannelId, identifier, StringComparison.OrdinalIgnoreCase))
            {
                PlatformOAuthAppCredentials? creds = await workspaceStore
                    .GetCustomOAuthAppAsync(workspace.Id, normalizedPlatform, cancellationToken)
                    .ConfigureAwait(false);
                if (creds is not null)
                {
                    return (workspace, creds);
                }
            }
        }

        IReadOnlyList<PanelUser> users = await panelUserStore.GetAllUsersAsync(cancellationToken).ConfigureAwait(false);
        foreach (PanelUser user in users)
        {
            IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
                .GetLoginIdentitiesAsync(user.Id, cancellationToken)
                .ConfigureAwait(false);

            bool matchesUsername = identities.Any(i =>
                string.Equals(i.ProviderId, normalizedPlatform, StringComparison.OrdinalIgnoreCase)
                && string.Equals(i.ProviderUsername, identifier, StringComparison.OrdinalIgnoreCase))
                || identities.Any(i => string.Equals(i.ProviderUsername, identifier, StringComparison.OrdinalIgnoreCase));

            if (!matchesUsername)
            {
                continue;
            }

            IReadOnlyList<Workspace> owned = await workspaceStore.GetByOwnerAsync(user.Id, cancellationToken).ConfigureAwait(false);
            foreach (Workspace ws in owned)
            {
                PlatformOAuthAppCredentials? creds = await workspaceStore
                    .GetCustomOAuthAppAsync(ws.Id, normalizedPlatform, cancellationToken)
                    .ConfigureAwait(false);
                if (creds is not null)
                {
                    return (ws, creds);
                }
            }

            IReadOnlyList<AccessibleWorkspace> accessible = await workspaceAccessService
                .GetAccessibleWorkspacesAsync(user, identities, oidcGroupClaims: null, authProvider: normalizedPlatform, isMfaVerified: true, cancellationToken)
                .ConfigureAwait(false);
            foreach (AccessibleWorkspace ws in accessible)
            {
                PlatformOAuthAppCredentials? creds = await workspaceStore
                    .GetCustomOAuthAppAsync(ws.Workspace.Id, normalizedPlatform, cancellationToken)
                    .ConfigureAwait(false);
                if (creds is not null)
                {
                    return (ws.Workspace, creds);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Verifies post-callback identity binding for Tier 3 DB-lookup OAuth logins: the authenticated platform identity must own
    /// or evaluate to <see cref="PermissionState.Allow"/> for <see cref="BuiltInPermissionKeys.UiPanelAccess"/> in at least one
    /// workspace configured with <paramref name="lookupClientId"/> for <paramref name="platformId"/>.
    /// </summary>
    /// <param name="platformId">The normalized platform identifier.</param>
    /// <param name="platformUserId">The authenticated platform user identifier.</param>
    /// <param name="platformUsername">The authenticated platform username.</param>
    /// <param name="lookupClientId">The custom OAuth client identifier used from DB lookup.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if bound to at least one matching workspace; otherwise, <see langword="false"/>.</returns>
    private static async ValueTask<bool> VerifyDbLookupIdentityBindingAsync(
        string platformId,
        string platformUserId,
        string platformUsername,
        string lookupClientId,
        IPanelUserStore panelUserStore,
        IWorkspaceStore workspaceStore,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        PanelUser? existingUser = await panelUserStore
            .GetByLoginIdentityAsync(platformId, platformUserId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<UserLoginIdentity> existingIdentities = existingUser is not null
            ? await panelUserStore.GetLoginIdentitiesAsync(existingUser.Id, cancellationToken).ConfigureAwait(false)
            : [];

        IReadOnlyList<Workspace> workspaces = await workspaceStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (Workspace workspace in workspaces)
        {
            WorkspacePlatform? platform = await workspaceStore
                .GetPlatformAsync(workspace.Id, platformId, cancellationToken)
                .ConfigureAwait(false);
            if (platform is null || !string.Equals(platform.CustomClientId, lookupClientId, StringComparison.Ordinal))
            {
                continue;
            }

            if (existingUser is not null && (existingUser.IsRoot || existingUser.IsGlobalAdmin || workspace.OwnerUserId == existingUser.Id))
            {
                return true;
            }

            if (existingUser is not null && existingIdentities.Count > 0)
            {
                bool anyAllow = false;
                bool anyDeny = false;
                foreach (UserLoginIdentity identity in existingIdentities)
                {
                    PlatformUserIdentity actor = new(
                        identity.ProviderId,
                        identity.ProviderUserId,
                        identity.ProviderUsername,
                        PlatformUserDesignations.None,
                        PanelUserId: existingUser.Id);
                    PermissionEvaluationResult result = await permissionService
                        .EvaluateAsync(workspace.Id, actor, BuiltInPermissionKeys.UiPanelAccess, cancellationToken)
                        .ConfigureAwait(false);

                    if (result.EffectiveState == PermissionState.Deny)
                    {
                        anyDeny = true;
                        break;
                    }

                    if (result.IsAllowed)
                    {
                        anyAllow = true;
                    }
                }

                if (!anyDeny && anyAllow)
                {
                    return true;
                }
            }
            else
            {
                PlatformUserIdentity actor = new(
                    platformId,
                    platformUserId,
                    platformUsername,
                    PlatformUserDesignations.None);
                PermissionEvaluationResult result = await permissionService
                    .EvaluateAsync(workspace.Id, actor, BuiltInPermissionKeys.UiPanelAccess, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsAllowed)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether every workspace in <paramref name="workspaces"/> requires External OIDC authentication.
    /// </summary>
    /// <param name="settings">The instance settings.</param>
    /// <param name="workspaces">The workspaces to inspect.</param>
    /// <param name="oidcProviderStore">Optional external OIDC provider store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if <paramref name="workspaces"/> is non-empty and every entry requires external OIDC; otherwise, <see langword="false"/>.</returns>
    private static async ValueTask<bool> AllWorkspacesRequireExternalOidcAsync(
        InstanceSettings settings,
        List<Workspace> workspaces,
        IExternalOidcProviderStore? oidcProviderStore,
        CancellationToken cancellationToken)
    {
        if (workspaces.Count == 0 || settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Disabled)
        {
            return false;
        }

        if (settings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required)
        {
            return true;
        }

        foreach (Workspace workspace in workspaces)
        {
            if (workspace.RequireExternalOidcLogin)
            {
                continue;
            }

            if (oidcProviderStore is not null)
            {
                IReadOnlyList<ExternalOidcProvider> providers = await oidcProviderStore
                    .GetProvidersAsync(workspace.Id, enabledOnly: true, cancellationToken)
                    .ConfigureAwait(false);
                if (providers.Count > 0)
                {
                    continue;
                }
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Selects the preferred <see cref="WorkspaceSessionAuthStatus.Ready"/> workspace identifier for setting the initial workspace cookie.
    /// </summary>
    /// <param name="accessible">The evaluated accessible workspaces.</param>
    /// <param name="preferredWorkspaceId">An explicit target workspace identifier, if any.</param>
    /// <param name="preferredOwnedId">An owned workspace identifier matching the sign-in platform, if any.</param>
    /// <returns>The selected ready workspace identifier, or <see langword="null"/> if none is ready.</returns>
    private static Guid? SelectPreferredReadyWorkspaceId(
        IReadOnlyList<AccessibleWorkspace> accessible,
        Guid? preferredWorkspaceId,
        Guid? preferredOwnedId)
    {
        if (preferredWorkspaceId.HasValue)
        {
            AccessibleWorkspace? explicitMatch = accessible.FirstOrDefault(w =>
                w.Workspace.Id == preferredWorkspaceId.Value && w.AuthStatus == WorkspaceSessionAuthStatus.Ready);
            if (explicitMatch is not null)
            {
                return explicitMatch.Workspace.Id;
            }
        }

        if (preferredOwnedId.HasValue)
        {
            AccessibleWorkspace? ownedMatch = accessible.FirstOrDefault(w =>
                w.Workspace.Id == preferredOwnedId.Value && w.AuthStatus == WorkspaceSessionAuthStatus.Ready);
            if (ownedMatch is not null)
            {
                return ownedMatch.Workspace.Id;
            }
        }

        AccessibleWorkspace? firstReady = accessible.FirstOrDefault(static w => w.AuthStatus == WorkspaceSessionAuthStatus.Ready);
        return firstReady?.Workspace.Id;
    }

    /// <summary>
    /// Issues or refreshes the 90-day sliding <c>StreamActions.LoginHint</c> cookie for <paramref name="user"/>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The user's linked external login identities.</param>
    /// <param name="authProvider">The authentication provider used for the session.</param>
    /// <param name="workspaceId">The active or preferred workspace identifier, if known.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Platform identifiers are normalized to lowercase invariant.")]
    internal static async ValueTask IssueLoginHintForUserAsync(
        HttpContext httpContext,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        string? authProvider,
        Guid? workspaceId,
        IWorkspaceStore workspaceStore,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        Guid? resolvedWorkspaceId = workspaceId;
        string? resolvedPlatformId = null;

        if (!string.IsNullOrWhiteSpace(authProvider)
            && !authProvider.StartsWith("oidc:", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(authProvider, "passkey", StringComparison.OrdinalIgnoreCase))
        {
            resolvedPlatformId = authProvider.Trim().ToLowerInvariant();
        }

        if (!resolvedWorkspaceId.HasValue)
        {
            IReadOnlyList<Workspace> owned = await workspaceStore.GetByOwnerAsync(user.Id, cancellationToken).ConfigureAwait(false);
            Workspace? fallbackOwned = owned.Count > 0 ? owned[0] : null;
            Workspace? preferred = resolvedPlatformId is not null
                ? owned.FirstOrDefault(w => string.Equals(w.PrimaryPlatformId, resolvedPlatformId, StringComparison.OrdinalIgnoreCase)) ?? fallbackOwned
                : fallbackOwned;
            resolvedWorkspaceId = preferred?.Id;
            resolvedPlatformId ??= preferred?.PrimaryPlatformId;
        }
        else if (resolvedPlatformId is null)
        {
            Workspace? ws = await workspaceStore.GetByIdAsync(resolvedWorkspaceId.Value, cancellationToken).ConfigureAwait(false);
            resolvedPlatformId = ws?.PrimaryPlatformId;
        }

        if (resolvedPlatformId is null)
        {
            UserLoginIdentity? platformIdentity = identities.FirstOrDefault(static i =>
                !i.ProviderId.StartsWith("oidc:", StringComparison.OrdinalIgnoreCase));
            resolvedPlatformId = platformIdentity?.ProviderId;
        }

        AuthClaims.WriteLoginHintCookie(
            httpContext,
            dataProtectionProvider,
            new LoginHintPayload(user.Id, resolvedWorkspaceId, resolvedPlatformId, user.DisplayName));
    }

    /// <summary>
    /// Builds an MFA challenge or enrollment redirect URL preserving optional <paramref name="workspaceId"/> and <paramref name="safeReturnUrl"/>.
    /// </summary>
    /// <param name="basePath">The base MFA path (<c>/auth/mfa</c> or <c>/account/mfa</c>).</param>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="safeReturnUrl">The validated local return URL, if any.</param>
    /// <param name="enrollRequired">Whether <c>enrollRequired=true</c> should be included.</param>
    /// <returns>The formatted local redirect URL.</returns>
    private static string BuildMfaChallengeUrl(
        string basePath,
        Guid? workspaceId,
        string? safeReturnUrl,
        bool enrollRequired)
    {
        List<string> queryParts = [];
        if (enrollRequired)
        {
            queryParts.Add("enrollRequired=true");
        }

        if (workspaceId.HasValue && workspaceId.Value != Guid.Empty)
        {
            queryParts.Add($"workspaceId={workspaceId.Value:D}");
        }

        if (!string.IsNullOrWhiteSpace(safeReturnUrl))
        {
            queryParts.Add($"returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
        }

        return queryParts.Count == 0
            ? basePath
            : $"{basePath}?{string.Join('&', queryParts)}";
    }

    /// <summary>
    /// Ensures WebAuthn request options JSON specifies <c>"userVerification": "required"</c>.
    /// </summary>
    /// <param name="requestOptionsJson">The raw JSON emitted by <see cref="IPasskeyHandler{TUser}.MakeRequestOptionsAsync"/>.</param>
    /// <returns>The updated request options JSON string.</returns>
    private static string EnsureRequiredUserVerification(string requestOptionsJson)
    {
        JsonNode? root = JsonNode.Parse(requestOptionsJson);
        if (root is JsonObject obj)
        {
            obj["userVerification"] = "required";
            return obj.ToJsonString();
        }

        return requestOptionsJson;
    }

    /// <summary>
    /// Parses and validates an optional absolute HTTP or HTTPS redirect URI string.
    /// </summary>
    /// <param name="rawUri">The raw URI string.</param>
    /// <param name="parsedUri">When this method returns <see langword="true"/>, contains the parsed <see cref="Uri"/>.</param>
    /// <returns><see langword="true"/> if valid HTTP/HTTPS absolute URI; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseHttpRedirectUri(string rawUri, out Uri? parsedUri)
    {
        if (Uri.TryCreate(rawUri.Trim(), UriKind.Absolute, out Uri? candidate)
            && (string.Equals(candidate.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            parsedUri = candidate;
            return true;
        }

        parsedUri = null;
        return false;
    }

    /// <summary>
    /// Parses a discoverable passkey-first login submission from JSON or an antiforgery-validated form.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating parse success, an optional error result, and the parsed submission.</returns>
    private static async Task<(bool Succeeded, IResult? ErrorResult, PasskeyLoginSubmission? Request)> TryReadPasskeyLoginRequestAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.HasFormContentType)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                return (false, Results.BadRequest(), null);
            }

            IFormCollection form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return (true, null, new PasskeyLoginSubmission
            {
                CredentialJson = form["credentialJson"].FirstOrDefault() ?? form["CredentialJson"].FirstOrDefault(),
                ReturnUrl = form["returnUrl"].FirstOrDefault() ?? form["ReturnUrl"].FirstOrDefault(),
            });
        }

        if (httpContext.Request.HasJsonContentType())
        {
            using StreamReader reader = new(httpContext.Request.Body);
            string rawBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return (false, Results.BadRequest(), null);
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(rawBody);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return (false, Results.BadRequest(), null);
                }

                if (TryGetJsonStringProperty(document.RootElement, "credentialJson", out string? wrappedCredentialJson))
                {
                    _ = TryGetJsonStringProperty(document.RootElement, "returnUrl", out string? returnUrl);
                    return (true, null, new PasskeyLoginSubmission
                    {
                        CredentialJson = wrappedCredentialJson,
                        ReturnUrl = returnUrl,
                    });
                }

                _ = TryGetJsonStringProperty(document.RootElement, "returnUrl", out string? rawReturnUrl);
                return (true, null, new PasskeyLoginSubmission
                {
                    CredentialJson = rawBody,
                    ReturnUrl = rawReturnUrl,
                });
            }
            catch (JsonException)
            {
                return (false, Results.BadRequest(), null);
            }
        }

        return (false, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType), null);
    }

    /// <summary>
    /// Attempts to read a case-insensitive string property from a JSON object element.
    /// </summary>
    /// <param name="element">The JSON object element.</param>
    /// <param name="propertyName">The target property name.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the string value.</param>
    /// <returns><see langword="true"/> if found; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetJsonStringProperty(JsonElement element, string propertyName, out string? value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                value = property.Value.GetString();
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Encrypts and writes the <c>StreamActions.PasskeyLogin</c> <see langword="HttpOnly"/> state cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="payload">The passkey login state payload to protect.</param>
    private static void WritePasskeyLoginStateCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider,
        PasskeyLoginStatePayload payload)
    {
        string json = JsonSerializer.Serialize(payload);
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(AuthClaims.PasskeyLoginStateProtectorPurpose)
            .ToTimeLimitedDataProtector();
        string protectedValue = protector.Protect(json, AuthFlowLifetime);

        httpContext.Response.Cookies.Append(
            AuthClaims.PasskeyLoginStateCookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                IsEssential = true,
                MaxAge = AuthFlowLifetime,
                Path = "/",
            });
    }

    /// <summary>
    /// Reads and immediately deletes the <c>StreamActions.PasskeyLogin</c> state cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <returns>The unprotected <see cref="PasskeyLoginStatePayload"/>, or <see langword="null"/> if absent, expired, or invalid.</returns>
    private static PasskeyLoginStatePayload? ReadAndDeletePasskeyLoginStateCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider)
    {
        bool hasCookie = httpContext.Request.Cookies.TryGetValue(AuthClaims.PasskeyLoginStateCookieName, out string? protectedValue);
        DeleteCookie(httpContext, AuthClaims.PasskeyLoginStateCookieName);

        if (!hasCookie || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(AuthClaims.PasskeyLoginStateProtectorPurpose)
                .ToTimeLimitedDataProtector();
            string json = protector.Unprotect(protectedValue);
            PasskeyLoginStatePayload? payload = JsonSerializer.Deserialize<PasskeyLoginStatePayload>(json);
            if (payload is null || string.IsNullOrWhiteSpace(payload.State))
            {
                return null;
            }

            TimeProvider timeProvider = GetTimeProvider(httpContext);
            return payload.ExpiresAtUtc is DateTimeOffset expiresAtUtc && timeProvider.GetUtcNow() >= expiresAtUtc
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
    /// Creates an error response when External OIDC policy rejects a non-OIDC login attempt.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="errorCode">The login error code query value.</param>
    /// <returns>A redirect for HTML requests or a <c>403 Forbidden</c> JSON payload for API callers.</returns>
    private static IResult CreateOidcRejectionResult(HttpContext httpContext, string errorCode)
    {
        string redirectUrl = $"/login?error={Uri.EscapeDataString(errorCode)}";
        return PrefersHtmlResponse(httpContext.Request)
            ? Results.Redirect(redirectUrl)
            : Results.Json(
                new OidcRejectionResponse
                {
                    Error = errorCode,
                    RedirectUrl = redirectUrl,
                },
                statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Determines whether the request explicitly accepts an HTML response.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns><see langword="true"/> if the <c>Accept</c> header contains <c>text/html</c>; otherwise, <see langword="false"/>.</returns>
    private static bool PrefersHtmlResponse(HttpRequest request)
    {
        string acceptHeader = request.Headers.Accept.ToString();
        return !string.IsNullOrWhiteSpace(acceptHeader)
            && acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase);
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
    /// Generates an RFC 7636 PKCE code verifier using 32 cryptographically random bytes encoded as unpadded base64url (43 characters).
    /// </summary>
    /// <returns>A base64url-encoded RFC 7636 PKCE code verifier.</returns>
    private static string GeneratePkceCodeVerifier() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Computes an RFC 7636 PKCE <c>S256</c> code challenge from the specified code verifier.
    /// </summary>
    /// <param name="codeVerifier">The RFC 7636 PKCE code verifier.</param>
    /// <returns>The unpadded base64url-encoded SHA-256 hash of <paramref name="codeVerifier"/>.</returns>
    private static string ComputePkceS256Challenge(string codeVerifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

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
    /// <param name="provider">The matched platform OAuth provider supplying default scopes.</param>
    /// <param name="normalizedFlow">The normalized lowercase flow name.</param>
    /// <param name="rawScopes">Optional space- or comma-delimited custom scope string.</param>
    /// <returns>The scope collection to request from the OAuth provider.</returns>
    private static IReadOnlyList<string> ResolveRequestedScopes(
        IPlatformOAuthProvider provider,
        string normalizedFlow,
        string? rawScopes)
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
            "setup" or "broadcaster" => provider.DefaultBroadcasterScopes,
            "bot" or "instance-bot" => provider.DefaultBotScopes,
            _ => provider.DefaultLoginScopes,
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

    /// <summary>
    /// Creates and persists a new <see cref="PanelUserSession"/> when <see cref="IPanelSessionService"/> is registered in request services.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserId">The authenticated panel user identifier.</param>
    /// <param name="providerId">The authentication provider identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created session identifier, or <see langword="null"/> if <see cref="IPanelSessionService"/> is not registered.</returns>
    private static async ValueTask<Guid?> CreatePanelSessionIfRegisteredAsync(
        HttpContext httpContext,
        Guid panelUserId,
        string providerId,
        CancellationToken cancellationToken)
    {
        if (httpContext.RequestServices.GetService<IPanelSessionService>() is not IPanelSessionService sessionService)
        {
            return null;
        }

        string? remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
        PanelUserSession session = await sessionService
            .CreateSessionAsync(panelUserId, providerId, remoteIp, userAgent, cancellationToken)
            .ConfigureAwait(false);
        return session.Id;
    }
}

/// <summary>
/// Represents the JSON response returned by <c>POST /account/sessions/revoke-others</c>.
/// </summary>
internal sealed record RevokeOtherSessionsResponse
{
    /// <summary>
    /// Gets the number of other active sessions revoked for the signed-in user.
    /// </summary>
    public int? RevokedCount { get; init; }
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
    /// Gets the bound panel user identifier for <c>flow=stepup</c> forced re-authentication requests.
    /// </summary>
    public Guid? StepUpUserId { get; init; }

    /// <summary>
    /// Gets the PKCE code verifier for platform OAuth and external OIDC flows.
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
    /// Gets the optional explicit custom OAuth redirect URI submitted for a new workspace under <c>RequireStreamerOAuthApp</c>.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Serialized inside the DataProtection cookie payload.")]
    public string? CustomRedirectUri { get; init; }

    /// <summary>
    /// Gets a value indicating whether the login flow resolved OAuth credentials from a workspace database lookup requiring post-callback identity binding.
    /// </summary>
    public bool UsedDbLookupOAuthApp { get; init; }

    /// <summary>
    /// Gets the OAuth client identifier used from database lookup when <see cref="UsedDbLookupOAuthApp"/> is <see langword="true"/>.
    /// </summary>
    public string? LookupClientId { get; init; }

    /// <summary>
    /// Gets the UTC expiration timestamp for this auth flow payload.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

/// <summary>
/// Represents the protected payload stored inside the <c>StreamActions.PasskeyLogin</c> cookie.
/// </summary>
internal sealed record PasskeyLoginStatePayload
{
    /// <summary>
    /// Gets the serialized ASP.NET Core Identity passkey assertion state.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the UTC expiration timestamp for this passkey state payload.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

/// <summary>
/// Represents a submitted discoverable passkey-first sign-in assertion request.
/// </summary>
internal sealed record PasskeyLoginSubmission
{
    /// <summary>
    /// Gets the serialized WebAuthn assertion credential JSON string.
    /// </summary>
    public string? CredentialJson { get; init; }

    /// <summary>
    /// Gets the optional safe local redirect target after sign-in.
    /// </summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// Represents the JSON response returned by <c>POST /auth/passkey/login</c>.
/// </summary>
internal sealed record PasskeyLoginResponse
{
    /// <summary>
    /// Gets a value indicating whether passkey sign-in succeeded.
    /// </summary>
    public bool? Verified { get; init; }

    /// <summary>
    /// Gets the validated safe local redirect target.
    /// </summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// Represents the JSON error payload returned when External OIDC policy rejects a non-OIDC login attempt.
/// </summary>
internal sealed record OidcRejectionResponse
{
    /// <summary>
    /// Gets the error code (<c>oidc_required</c> or <c>workspace_oidc_required</c>).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the relative login redirect URL displaying the OIDC prompt.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Relative redirect URL returned to browser fetch client.")]
    public string? RedirectUrl { get; init; }
}
