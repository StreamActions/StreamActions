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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Permissions;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

namespace StreamActions.Web.Endpoints;

/// <summary>
/// Maps bootstrap verification and token-request HTTP endpoints for initial setup and root recovery.
/// </summary>
internal static class SetupEndpoints
{
    /// <summary>
    /// Name of the rate-limiter policy protecting setup verification endpoints.
    /// </summary>
    public const string RateLimitPolicyName = "setup-verify";

    /// <summary>
    /// Name of the protected setup session cookie issued after verifying a bootstrap token.
    /// </summary>
    public const string SetupTicketCookieName = "StreamActions.SetupTicket";

    /// <summary>
    /// Data protection purpose string for <c>StreamActions.SetupTicket</c> payloads.
    /// </summary>
    public const string SetupTicketProtectorPurpose = "StreamActions.SetupTicket.v1";

    /// <summary>
    /// Default platform identifier used when no platform is explicitly supplied.
    /// </summary>
    private const string _defaultPlatformId = "twitch";

    /// <summary>
    /// Lifetime of an issued setup session ticket and its accompanying cookie.
    /// </summary>
    public static readonly TimeSpan SetupTicketLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Maps <c>POST /setup/verify</c> and <c>POST /setup/request-token</c> endpoints protected by rate limiting.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The configured <see cref="IEndpointRouteBuilder"/>.</returns>
    internal static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapPost("/setup/verify", HandleVerifyAsync)
            .RequireRateLimiting(RateLimitPolicyName);

        _ = endpoints.MapPost("/setup/request-token", HandleRequestToken)
            .RequireRateLimiting(RateLimitPolicyName);

        return endpoints;
    }

    /// <summary>
    /// Attempts to read and validate the protected <c>StreamActions.SetupTicket</c> cookie from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect the ticket.</param>
    /// <param name="payload">When this method returns <see langword="true"/>, contains the unprotected ticket payload.</param>
    /// <returns><see langword="true"/> if a valid, non-expired setup ticket cookie is present; otherwise, <see langword="false"/>.</returns>
    internal static bool TryReadSetupTicket(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider,
        out SetupTicketPayload? payload)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        payload = null;
        if (!httpContext.Request.Cookies.TryGetValue(SetupTicketCookieName, out string? protectedTicket)
            || string.IsNullOrWhiteSpace(protectedTicket))
        {
            return false;
        }

        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(SetupTicketProtectorPurpose)
                .ToTimeLimitedDataProtector();
            string ticketJson = protector.Unprotect(protectedTicket);
            SetupTicketPayload? parsed = JsonSerializer.Deserialize<SetupTicketPayload>(ticketJson);
            if ((parsed is null) || string.IsNullOrWhiteSpace(parsed.Nonce))
            {
                return false;
            }

            TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
            if ((parsed.ExpiresAtUtc is DateTimeOffset expiresAtUtc) && (timeProvider.GetUtcNow() >= expiresAtUtc))
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a submitted bootstrap token, optionally saves instance OAuth credentials, burns the token, and issues a protected setup ticket cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="bootstrapTokenService">The bootstrap token service.</param>
    /// <param name="oauthConfigService">The instance OAuth configuration service.</param>
    /// <param name="panelUserStore">The panel user store used to detect root recovery mode.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect the setup ticket cookie.</param>
    /// <param name="antiforgery">The antiforgery service used to validate browser form submissions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IResult"/> representing the verification outcome.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Platform identifiers are normalized to lowercase invariant.")]
    private static async Task<IResult> HandleVerifyAsync(
        HttpContext httpContext,
        IBootstrapTokenService bootstrapTokenService,
        IInstanceOAuthConfigService oauthConfigService,
        IPanelUserStore panelUserStore,
        IDataProtectionProvider dataProtectionProvider,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        (bool parsed, IResult? errorResult, SetupVerifyRequest? request) = await TryReadVerifyRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || (request is null))
        {
            return errorResult ?? Results.BadRequest();
        }

        if (string.IsNullOrWhiteSpace(request.BootstrapToken)
            || !bootstrapTokenService.ValidateToken(request.BootstrapToken))
        {
            return Results.Unauthorized();
        }

        string selectedPlatformId = string.IsNullOrWhiteSpace(request.PlatformId)
            ? _defaultPlatformId
            : request.PlatformId.Trim().ToLowerInvariant();

        bool oauthCredentialsSaved = false;
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            string? clientSecret = string.IsNullOrWhiteSpace(request.ClientSecret)
                ? null
                : request.ClientSecret.Trim();
            PlatformOAuthAppCredentials credentials = new(
                request.ClientId.Trim(),
                clientSecret);
            await oauthConfigService.SetAsync(selectedPlatformId, credentials, cancellationToken).ConfigureAwait(false);
            oauthCredentialsSaved = true;
        }

        bootstrapTokenService.Invalidate();

        bool isRootRecovery = await panelUserStore.HasRootUserAsync(cancellationToken).ConfigureAwait(false);
        string[]? enabledDangerousFeatures = null;
        if (!isRootRecovery)
        {
            IPermissionRegistry permissionRegistry = httpContext.RequestServices.GetService<IPermissionRegistry>() ?? new PermissionRegistry();
            enabledDangerousFeatures = ResolveEnabledDangerousFeatures(request, permissionRegistry);

            IPermissionStore? permissionStore = httpContext.RequestServices.GetService<IPermissionStore>();
            if (permissionStore is not null)
            {
                _ = await permissionStore
                    .SeedDefaultGlobalGroupAsync(enabledDangerousFeatures, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        DateTimeOffset issuedAtUtc = timeProvider.GetUtcNow();
        DateTimeOffset expiresAtUtc = issuedAtUtc + SetupTicketLifetime;

        SetupTicketPayload ticketPayload = new()
        {
            Nonce = RandomNumberGenerator.GetHexString(32, lowercase: true),
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            IsRootRecovery = isRootRecovery,
            PlatformId = selectedPlatformId,
            ResetRootMfa = isRootRecovery ? (request.ResetMfa ?? true) : null,
            EnabledDangerousFeatures = isRootRecovery ? null : enabledDangerousFeatures,
        };

        string ticketJson = JsonSerializer.Serialize(ticketPayload);
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(SetupTicketProtectorPurpose)
            .ToTimeLimitedDataProtector();
        string protectedTicket = protector.Protect(ticketJson, SetupTicketLifetime);

        CookieOptions cookieOptions = new()
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            IsEssential = true,
            MaxAge = SetupTicketLifetime,
            Path = "/",
        };
        httpContext.Response.Cookies.Append(SetupTicketCookieName, protectedTicket, cookieOptions);

        return PrefersHtmlResponse(httpContext.Request)
            ? Results.Redirect("/setup?verified=true")
            : Results.Ok(new SetupVerifyResponse
            {
                Verified = true,
                IsRootRecovery = isRootRecovery,
                OAuthCredentialsSaved = oauthCredentialsSaved,
            });
    }

    /// <summary>
    /// Requests or reuses an active console bootstrap token without returning the raw token over HTTP.
    /// </summary>
    /// <param name="bootstrapTokenService">The bootstrap token service.</param>
    /// <returns>An <see cref="IResult"/> indicating whether a token is active or in cooldown.</returns>
    private static IResult HandleRequestToken(IBootstrapTokenService bootstrapTokenService)
    {
        BootstrapTokenRequestResult result = bootstrapTokenService.RequestOrReuseActiveToken();
        int? cooldownRemainingSeconds = result.CooldownRemaining is TimeSpan remaining
            ? Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))
            : null;

        return Results.Ok(new SetupTokenRequestResponse
        {
            Succeeded = result.Succeeded,
            CooldownRemainingSeconds = cooldownRemainingSeconds,
        });
    }

    /// <summary>
    /// Parses a setup verification request from either a form submission (with antiforgery validation) or a JSON body.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery service used to validate form requests.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating parse success, an optional failure <see cref="IResult"/>, and the parsed <see cref="SetupVerifyRequest"/>.</returns>
    private static async Task<(bool Succeeded, IResult? ErrorResult, SetupVerifyRequest? Request)> TryReadVerifyRequestAsync(
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
            string? rawResetMfaSubmitted = form["resetMfaSubmitted"].FirstOrDefault() ?? form["ResetMfaSubmitted"].FirstOrDefault();
            string? rawResetMfa = form["resetMfa"].FirstOrDefault() ?? form["ResetMfa"].FirstOrDefault();
            bool? resetMfa = null;
            if (string.Equals(rawResetMfaSubmitted, "true", StringComparison.OrdinalIgnoreCase))
            {
                resetMfa = string.Equals(rawResetMfa, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rawResetMfa, "on", StringComparison.OrdinalIgnoreCase);
            }
            else if (!string.IsNullOrWhiteSpace(rawResetMfa) && bool.TryParse(rawResetMfa, out bool parsedResetMfa))
            {
                resetMfa = parsedResetMfa;
            }

            string? rawEnableAll = form["enableAllDangerousFeatures"].FirstOrDefault() ?? form["EnableAllDangerousFeatures"].FirstOrDefault();
            bool? enableAllDangerousFeatures = null;
            if (!string.IsNullOrWhiteSpace(rawEnableAll))
            {
                enableAllDangerousFeatures = string.Equals(rawEnableAll, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rawEnableAll, "on", StringComparison.OrdinalIgnoreCase);
            }

            string[] rawDangerousFeatures = [.. form["enabledDangerousFeatures"]
                .Concat(form["EnabledDangerousFeatures"])
                .Where(static k => !string.IsNullOrWhiteSpace(k))
                .Select(static k => k!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            SetupVerifyRequest formRequest = new()
            {
                BootstrapToken = form["bootstrapToken"].FirstOrDefault() ?? form["BootstrapToken"].FirstOrDefault(),
                PlatformId = form["platformId"].FirstOrDefault() ?? form["PlatformId"].FirstOrDefault(),
                ClientId = form["clientId"].FirstOrDefault() ?? form["ClientId"].FirstOrDefault(),
                ClientSecret = form["clientSecret"].FirstOrDefault() ?? form["ClientSecret"].FirstOrDefault(),
                ResetMfa = resetMfa,
                EnableAllDangerousFeatures = enableAllDangerousFeatures,
                EnabledDangerousFeatures = rawDangerousFeatures,
            };
            return (true, null, formRequest);
        }

        if (httpContext.Request.HasJsonContentType())
        {
            try
            {
                SetupVerifyRequest? jsonRequest = await httpContext.Request
                    .ReadFromJsonAsync<SetupVerifyRequest>(cancellationToken)
                    .ConfigureAwait(false);
                if (jsonRequest is null)
                {
                    return (false, Results.BadRequest(), null);
                }

                return (true, null, jsonRequest);
            }
            catch (JsonException)
            {
                return (false, Results.BadRequest(), null);
            }
        }

        return (false, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType), null);
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
    /// Resolves and normalizes the list of dangerous global feature keys opted in by the root operator during initial setup.
    /// </summary>
    /// <param name="request">The parsed setup verification request.</param>
    /// <param name="permissionRegistry">The permission descriptor registry.</param>
    /// <returns>The normalized array of opted-in dangerous global feature keys.</returns>
    private static string[] ResolveEnabledDangerousFeatures(
        SetupVerifyRequest request,
        IPermissionRegistry permissionRegistry)
    {
        IReadOnlyList<PermissionDescriptor> allDangerous = [.. permissionRegistry
            .GetAll()
            .Where(static d => d.IsGloballyGated && d.IsDangerousGlobalFeature)];

        if (request.EnableAllDangerousFeatures == true)
        {
            return [.. allDangerous.Select(static d => d.Key)];
        }

        if (request.EnabledDangerousFeatures is null || request.EnabledDangerousFeatures.Count == 0)
        {
            return [];
        }

        List<string> resolved = [];
        foreach (string candidate in request.EnabledDangerousFeatures)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            PermissionDescriptor? descriptor = permissionRegistry.GetByKey(candidate);
            if (descriptor is not null && descriptor.IsGloballyGated && descriptor.IsDangerousGlobalFeature)
            {
                if (!resolved.Contains(descriptor.Key, StringComparer.OrdinalIgnoreCase))
                {
                    resolved.Add(descriptor.Key);
                }
            }
        }

        return [.. resolved];
    }
}

/// <summary>
/// Represents a request payload submitted to <c>POST /setup/verify</c>.
/// </summary>
internal sealed record SetupVerifyRequest
{
    /// <summary>
    /// Gets the candidate bootstrap token submitted by the operator.
    /// </summary>
    public string? BootstrapToken { get; init; }

    /// <summary>
    /// Gets the target streaming platform identifier.
    /// </summary>
    public string? PlatformId { get; init; }

    /// <summary>
    /// Gets the optional OAuth application client identifier.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the optional OAuth application client secret.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Gets a value indicating whether root recovery should reset the root operator's enrolled MFA credentials.
    /// </summary>
    public bool? ResetMfa { get; init; }

    /// <summary>
    /// Gets a value indicating whether initial setup should enable all 5 high-risk global features on the default global plan.
    /// </summary>
    public bool? EnableAllDangerousFeatures { get; init; }

    /// <summary>
    /// Gets the list of specific dangerous global feature keys opted in during initial setup.
    /// </summary>
    public IReadOnlyList<string>? EnabledDangerousFeatures { get; init; }
}

/// <summary>
/// Represents the JSON response returned by <c>POST /setup/verify</c>.
/// </summary>
internal sealed record SetupVerifyResponse
{
    /// <summary>
    /// Gets a value indicating whether bootstrap verification succeeded.
    /// </summary>
    public bool? Verified { get; init; }

    /// <summary>
    /// Gets a value indicating whether the node is in root recovery mode.
    /// </summary>
    public bool? IsRootRecovery { get; init; }

    /// <summary>
    /// Gets a value indicating whether OAuth application credentials were saved during verification.
    /// </summary>
    public bool? OAuthCredentialsSaved { get; init; }
}

/// <summary>
/// Represents the JSON response returned by <c>POST /setup/request-token</c>.
/// </summary>
internal sealed record SetupTokenRequestResponse
{
    /// <summary>
    /// Gets a value indicating whether an active bootstrap token is available on the console.
    /// </summary>
    public bool? Succeeded { get; init; }

    /// <summary>
    /// Gets the remaining cooldown duration in seconds when token generation is blocked.
    /// </summary>
    public int? CooldownRemainingSeconds { get; init; }
}

/// <summary>
/// Represents the data-protected payload stored inside the <c>StreamActions.SetupTicket</c> cookie.
/// </summary>
internal sealed record SetupTicketPayload
{
    /// <summary>
    /// Gets the unique random ticket identifier.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the setup ticket was issued.
    /// </summary>
    public DateTimeOffset? IssuedAtUtc { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the setup ticket expires.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <summary>
    /// Gets a value indicating whether the node already had a root user when the ticket was issued.
    /// </summary>
    public bool? IsRootRecovery { get; init; }

    /// <summary>
    /// Gets the selected platform identifier for continuing setup OAuth.
    /// </summary>
    public string? PlatformId { get; init; }

    /// <summary>
    /// Gets a value indicating whether root recovery should clear the root operator's enrolled MFA credentials.
    /// </summary>
    public bool? ResetRootMfa { get; init; }

    /// <summary>
    /// Gets the optional dangerous global feature keys opted in during initial setup.
    /// </summary>
    public IReadOnlyList<string>? EnabledDangerousFeatures { get; init; }
}
