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

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using StreamActions.Core.Models.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace StreamActions.Web.Security;

/// <summary>
/// Defines authentication cookie, authorization policy, and claim constants plus principal helpers for the StreamActions web panel.
/// </summary>
public static class AuthClaims
{
    /// <summary>
    /// Cookie name for the authenticated web panel session.
    /// </summary>
    public const string AuthCookieName = "StreamActions.Auth";

    /// <summary>
    /// Cookie name for short-lived protected OAuth/OIDC authorization flow state.
    /// </summary>
    public const string AuthFlowCookieName = "StreamActions.AuthFlow";

    /// <summary>
    /// Cookie name storing the authenticated user's selected active workspace identifier.
    /// </summary>
    public const string WorkspaceCookieName = "StreamActions.Workspace";

    /// <summary>
    /// Cookie name for short-lived protected MFA challenge or enrollment state payloads.
    /// </summary>
    public const string MfaStateCookieName = "StreamActions.MfaState";

    /// <summary>
    /// Cookie name for short-lived protected discoverable passkey-first sign-in state payloads.
    /// </summary>
    public const string PasskeyLoginStateCookieName = "StreamActions.PasskeyLogin";

    /// <summary>
    /// Cookie name storing the DataProtection-signed 90-day login hint payload for Tier 2 returning-user sign-in.
    /// </summary>
    public const string LoginHintCookieName = "StreamActions.LoginHint";

    /// <summary>
    /// Data protection purpose string for <see cref="AuthFlowCookieName"/> payloads.
    /// </summary>
    public const string AuthFlowProtectorPurpose = "StreamActions.AuthFlow.v1";

    /// <summary>
    /// Data protection purpose string for <see cref="MfaStateCookieName"/> payloads.
    /// </summary>
    public const string MfaStateProtectorPurpose = "StreamActions.MfaState.v1";

    /// <summary>
    /// Data protection purpose string for <see cref="PasskeyLoginStateCookieName"/> payloads.
    /// </summary>
    public const string PasskeyLoginStateProtectorPurpose = "StreamActions.PasskeyLogin.v1";

    /// <summary>
    /// Data protection purpose string for <see cref="LoginHintCookieName"/> payloads.
    /// </summary>
    public const string LoginHintProtectorPurpose = "StreamActions.LoginHint.v1";

    /// <summary>
    /// Authorization policy allowing authenticated users whose MFA challenge or enrollment is still pending.
    /// </summary>
    public const string MfaPendingPolicy = "MfaPending";

    /// <summary>
    /// Authorization policy requiring an authenticated user whose MFA challenge is complete.
    /// </summary>
    public const string PanelAuthenticatedPolicy = "PanelAuthenticated";

    /// <summary>
    /// Authorization policy requiring <see cref="PanelAuthenticatedPolicy"/> plus root or global administrator privilege.
    /// </summary>
    public const string RootOrGlobalAdminPolicy = "RootOrGlobalAdmin";

    /// <summary>
    /// Authorization policy requiring <see cref="PanelAuthenticatedPolicy"/> plus root operator privilege.
    /// </summary>
    public const string RootOnlyPolicy = "RootOnly";

    /// <summary>
    /// Claim type indicating whether the signed-in user is the instance root operator.
    /// </summary>
    public const string IsRootClaimType = "sa:is_root";

    /// <summary>
    /// Claim type indicating whether the signed-in user is a global administrator.
    /// </summary>
    public const string IsGlobalAdminClaimType = "sa:is_global_admin";

    /// <summary>
    /// Claim type indicating whether multi-factor authentication verification is still pending.
    /// </summary>
    public const string MfaPendingClaimType = "sa:mfa_pending";

    /// <summary>
    /// Claim type indicating whether the user must enroll a multi-factor authentication method before continuing.
    /// </summary>
    public const string MfaEnrollmentRequiredClaimType = "sa:mfa_enrollment_required";

    /// <summary>
    /// Claim type storing the UTC expiration timestamp for step-up ("Sudo Mode") MFA elevation.
    /// </summary>
    public const string MfaElevatedUntilClaimType = "sa:mfa_elevated_until";

    /// <summary>
    /// Claim type storing external OIDC group memberships associated with the current session.
    /// </summary>
    public const string OidcGroupsClaimType = "sa:oidc_groups";

    /// <summary>
    /// Claim type recording the authentication provider identifier used to establish the current session.
    /// </summary>
    public const string AuthProviderClaimType = "sa:auth_provider";

    /// <summary>
    /// Claim type indicating that MFA verification has been completed in the current session.
    /// </summary>
    public const string MfaVerifiedClaimType = "sa:mfa_verified";

    /// <summary>
    /// Claim type storing the unique <see cref="PanelUserSession.Id"/> identifier for the active session.
    /// </summary>
    public const string SessionIdClaimType = "sid";

    /// <summary>
    /// Alias for <see cref="SessionIdClaimType"/> storing the unique <see cref="PanelUserSession.Id"/> identifier.
    /// </summary>
    public const string SessionIdType = "sid";

    /// <summary>
    /// Duration for which a completed MFA challenge or enrollment grants step-up ("Sudo Mode") elevation.
    /// </summary>
    public static readonly TimeSpan StepUpWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Sliding lifetime for the DataProtection-signed <see cref="LoginHintCookieName"/> cookie.
    /// </summary>
    public static readonly TimeSpan LoginHintLifetime = TimeSpan.FromDays(90);

    /// <summary>
    /// Builds a <see cref="ClaimsPrincipal"/> for the cookie authentication scheme from a <see cref="PanelUser"/>.
    /// </summary>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="oidcGroups">Optional OIDC group claims to attach to the session.</param>
    /// <param name="authProvider">Optional authentication provider identifier.</param>
    /// <param name="mfaPending"><see langword="true"/> if an MFA challenge is pending; otherwise, <see langword="false"/>.</param>
    /// <param name="mfaEnrollmentRequired"><see langword="true"/> if the user must enroll MFA before completing authentication; otherwise, <see langword="false"/>.</param>
    /// <param name="mfaElevatedUntilUtc">Optional UTC expiration timestamp for step-up MFA elevation.</param>
    /// <param name="mfaVerified"><see langword="true"/> if MFA has been verified in this session; otherwise, <see langword="false"/>.</param>
    /// <param name="sessionId">Optional <see cref="PanelUserSession.Id"/> to embed as the <see cref="SessionIdClaimType"/> claim.</param>
    /// <returns>The constructed <see cref="ClaimsPrincipal"/>.</returns>
    public static ClaimsPrincipal BuildClaimsPrincipal(
        PanelUser user,
        IReadOnlyCollection<string>? oidcGroups = null,
        string? authProvider = null,
        bool mfaPending = false,
        bool mfaEnrollmentRequired = false,
        DateTimeOffset? mfaElevatedUntilUtc = null,
        bool mfaVerified = false,
        Guid? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new(ClaimTypes.Name, user.DisplayName),
            new(IsRootClaimType, user.IsRoot ? "true" : "false"),
            new(IsGlobalAdminClaimType, user.IsGlobalAdmin ? "true" : "false"),
        ];

        if (sessionId.HasValue && sessionId.Value != Guid.Empty)
        {
            claims.Add(new Claim(SessionIdClaimType, sessionId.Value.ToString("D")));
        }

        if (mfaPending)
        {
            claims.Add(new Claim(MfaPendingClaimType, "true"));
        }

        if (mfaEnrollmentRequired)
        {
            claims.Add(new Claim(MfaEnrollmentRequiredClaimType, "true"));
        }

        if (mfaVerified)
        {
            claims.Add(new Claim(MfaVerifiedClaimType, "true"));
        }

        if (mfaElevatedUntilUtc.HasValue)
        {
            claims.Add(new Claim(
                MfaElevatedUntilClaimType,
                mfaElevatedUntilUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(authProvider))
        {
            claims.Add(new Claim(AuthProviderClaimType, authProvider.Trim()));
        }

        if (oidcGroups is not null)
        {
            foreach (string group in oidcGroups)
            {
                if (!string.IsNullOrWhiteSpace(group))
                {
                    claims.Add(new Claim(OidcGroupsClaimType, group.Trim()));
                }
            }
        }

        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Determines whether <paramref name="principal"/> currently holds an active step-up ("Sudo Mode") MFA elevation window.
    /// </summary>
    /// <param name="principal">The claims principal to inspect.</param>
    /// <param name="timeProvider">The time provider used to evaluate the current UTC timestamp.</param>
    /// <returns><see langword="true"/> if <see cref="MfaElevatedUntilClaimType"/> is strictly greater than the current UTC time; otherwise, <see langword="false"/>.</returns>
    public static bool IsStepUpElevated(ClaimsPrincipal? principal, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (principal is null)
        {
            return false;
        }

        string? rawElevatedUntil = principal.FindFirstValue(MfaElevatedUntilClaimType);
        return !string.IsNullOrWhiteSpace(rawElevatedUntil)
            && DateTimeOffset.TryParse(
                rawElevatedUntil,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset elevatedUntilUtc)
            && elevatedUntilUtc > timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Produces a new <see cref="ClaimsPrincipal"/> preserving identity, role, OIDC, and provider claims while clearing pending MFA flags and setting <see cref="MfaElevatedUntilClaimType"/>.
    /// </summary>
    /// <param name="existing">The existing authenticated <see cref="ClaimsPrincipal"/>.</param>
    /// <param name="elevatedUntilUtc">The UTC expiration timestamp for the granted step-up window.</param>
    /// <returns>The updated <see cref="ClaimsPrincipal"/>.</returns>
    public static ClaimsPrincipal WithMfaCompletedAndElevated(ClaimsPrincipal existing, DateTimeOffset elevatedUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(existing);

        List<Claim> claims = [];
        foreach (Claim claim in existing.Claims)
        {
            if (string.Equals(claim.Type, MfaPendingClaimType, StringComparison.Ordinal)
                || string.Equals(claim.Type, MfaEnrollmentRequiredClaimType, StringComparison.Ordinal)
                || string.Equals(claim.Type, MfaElevatedUntilClaimType, StringComparison.Ordinal)
                || string.Equals(claim.Type, MfaVerifiedClaimType, StringComparison.Ordinal))
            {
                continue;
            }

            claims.Add(new Claim(claim.Type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer));
        }

        claims.Add(new Claim(MfaVerifiedClaimType, "true"));
        claims.Add(new Claim(
            MfaElevatedUntilClaimType,
            elevatedUntilUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

        ClaimsIdentity? existingIdentity = existing.Identity as ClaimsIdentity;
        string authenticationType = existingIdentity?.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme;
        string nameClaimType = existingIdentity?.NameClaimType ?? ClaimsIdentity.DefaultNameClaimType;
        string roleClaimType = existingIdentity?.RoleClaimType ?? ClaimsIdentity.DefaultRoleClaimType;

        ClaimsIdentity updatedIdentity = new(claims, authenticationType, nameClaimType, roleClaimType);
        return new ClaimsPrincipal(updatedIdentity);
    }

    /// <summary>
    /// Produces a new <see cref="ClaimsPrincipal"/> updating <see cref="MfaElevatedUntilClaimType"/> while preserving pending MFA and enrollment-required claims.
    /// </summary>
    /// <param name="existing">The existing authenticated <see cref="ClaimsPrincipal"/>.</param>
    /// <param name="elevatedUntilUtc">The UTC expiration timestamp for the granted step-up window.</param>
    /// <returns>The updated <see cref="ClaimsPrincipal"/>.</returns>
    public static ClaimsPrincipal WithStepUpElevated(ClaimsPrincipal existing, DateTimeOffset elevatedUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(existing);

        List<Claim> claims = [];
        foreach (Claim claim in existing.Claims)
        {
            if (string.Equals(claim.Type, MfaElevatedUntilClaimType, StringComparison.Ordinal))
            {
                continue;
            }

            claims.Add(new Claim(claim.Type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer));
        }

        claims.Add(new Claim(
            MfaElevatedUntilClaimType,
            elevatedUntilUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

        ClaimsIdentity? existingIdentity = existing.Identity as ClaimsIdentity;
        string authenticationType = existingIdentity?.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme;
        string nameClaimType = existingIdentity?.NameClaimType ?? ClaimsIdentity.DefaultNameClaimType;
        string roleClaimType = existingIdentity?.RoleClaimType ?? ClaimsIdentity.DefaultRoleClaimType;

        ClaimsIdentity updatedIdentity = new(claims, authenticationType, nameClaimType, roleClaimType);
        return new ClaimsPrincipal(updatedIdentity);
    }

    /// <summary>
    /// Extracts the <see cref="PanelUser.Id"/> from a <see cref="ClaimsPrincipal"/>, if present and valid.
    /// </summary>
    /// <param name="principal">The claims principal to inspect.</param>
    /// <returns>The parsed <see cref="Guid"/> user identifier, or <see langword="null"/> if absent or invalid.</returns>
    public static Guid? GetPanelUserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        string? rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out Guid userId) && userId != Guid.Empty
            ? userId
            : null;
    }

    /// <summary>
    /// Extracts the <see cref="PanelUserSession.Id"/> (<see cref="SessionIdClaimType"/>) from a <see cref="ClaimsPrincipal"/>, if present and valid.
    /// </summary>
    /// <param name="principal">The claims principal to inspect.</param>
    /// <returns>The parsed <see cref="Guid"/> session identifier, or <see langword="null"/> if absent or invalid.</returns>
    public static Guid? GetSessionId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        string? rawId = principal.FindFirstValue(SessionIdClaimType);
        return Guid.TryParse(rawId, out Guid sessionId) && sessionId != Guid.Empty
            ? sessionId
            : null;
    }

    /// <summary>
    /// Extracts all <see cref="OidcGroupsClaimType"/> claim values from a <see cref="ClaimsPrincipal"/>.
    /// </summary>
    /// <param name="principal">The claims principal to inspect.</param>
    /// <returns>A read-only list of OIDC group values.</returns>
    public static IReadOnlyList<string> GetOidcGroups(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return [];
        }

        List<string> groups = [];
        foreach (Claim claim in principal.FindAll(OidcGroupsClaimType))
        {
            if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                groups.Add(claim.Value.Trim());
            }
        }

        return groups;
    }

    /// <summary>
    /// Determines whether a <see cref="ClaimsPrincipal"/> has an affirmative claim value (<c>"true"</c>).
    /// </summary>
    /// <param name="principal">The claims principal to inspect.</param>
    /// <param name="claimType">The claim type to check.</param>
    /// <returns><see langword="true"/> if the claim exists with value <c>"true"</c>; otherwise, <see langword="false"/>.</returns>
    public static bool HasTrueClaim(ClaimsPrincipal? principal, string claimType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);

        return principal is not null
            && principal.HasClaim(c => string.Equals(c.Type, claimType, StringComparison.Ordinal)
                && string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates that a redirect target is a safe relative local path, preventing open redirects.
    /// </summary>
    /// <param name="returnUrl">The candidate redirect URL.</param>
    /// <returns><see langword="true"/> if <paramref name="returnUrl"/> is a safe relative local URL; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Accepts raw query-string returnUrl values for local-path validation.")]
    public static bool IsSafeLocalRedirect(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        string trimmed = returnUrl.Trim();
        return trimmed.StartsWith('/')
            && !trimmed.StartsWith("//", StringComparison.Ordinal)
            && !trimmed.StartsWith("/\\", StringComparison.Ordinal)
            && !trimmed.Contains('\r', StringComparison.Ordinal)
            && !trimmed.Contains('\n', StringComparison.Ordinal)
            && Uri.IsWellFormedUriString(trimmed, UriKind.Relative);
    }

    /// <summary>
    /// Returns <paramref name="returnUrl"/> if it is a safe local redirect target; otherwise, returns <paramref name="fallback"/>.
    /// </summary>
    /// <param name="returnUrl">The candidate redirect URL.</param>
    /// <param name="fallback">The safe fallback relative URL.</param>
    /// <returns>A safe local redirect URL.</returns>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Accepts raw query-string returnUrl values for ASP.NET Core Results.Redirect.")]
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Results.Redirect accepts a string URL.")]
    public static string SafeReturnUrlOrDefault(string? returnUrl, string fallback = "/") =>
        IsSafeLocalRedirect(returnUrl) ? returnUrl!.Trim() : fallback;

    /// <summary>
    /// Encrypts and writes the 90-day sliding <see cref="LoginHintCookieName"/> cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="payload">The login hint payload to protect.</param>
    internal static void WriteLoginHintCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider,
        LoginHintPayload payload)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(payload);

        string json = JsonSerializer.Serialize(payload);
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(LoginHintProtectorPurpose)
            .ToTimeLimitedDataProtector();
        string protectedValue = protector.Protect(json, LoginHintLifetime);

        httpContext.Response.Cookies.Append(
            LoginHintCookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                IsEssential = true,
                MaxAge = LoginHintLifetime,
                Path = "/",
            });
    }

    /// <summary>
    /// Attempts to read and validate the DataProtection-signed <see cref="LoginHintCookieName"/> cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <returns>The validated <see cref="LoginHintPayload"/>, or <see langword="null"/> if absent, expired, or invalid.</returns>
    internal static LoginHintPayload? TryReadLoginHint(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        if (!httpContext.Request.Cookies.TryGetValue(LoginHintCookieName, out string? protectedValue)
            || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(LoginHintProtectorPurpose)
                .ToTimeLimitedDataProtector();
            string json = protector.Unprotect(protectedValue);
            LoginHintPayload? payload = JsonSerializer.Deserialize<LoginHintPayload>(json);
            return payload is null || payload.PanelUserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.DisplayName)
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
    /// Deletes the <see cref="LoginHintCookieName"/> cookie with matching security and path attributes.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    internal static void DeleteLoginHintCookie(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Cookies.Delete(LoginHintCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            IsEssential = true,
            Path = "/",
        });
    }
}

/// <summary>
/// Represents the DataProtection-signed payload stored inside the <c>StreamActions.LoginHint</c> cookie.
/// </summary>
/// <param name="PanelUserId">The remembered panel user identifier.</param>
/// <param name="WorkspaceId">The optional last active workspace identifier.</param>
/// <param name="PlatformId">The optional platform identifier associated with the workspace or user.</param>
/// <param name="DisplayName">The user's display name shown on the returning-user sign-in button.</param>
internal sealed record LoginHintPayload(
    Guid PanelUserId,
    Guid? WorkspaceId,
    string? PlatformId,
    string DisplayName);
