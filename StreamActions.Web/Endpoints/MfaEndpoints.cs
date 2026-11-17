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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Web.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace StreamActions.Web.Endpoints;

/// <summary>
/// Maps WebAuthn passkey, TOTP authenticator, recovery code, and credential management HTTP endpoints for MFA challenge, enrollment, and step-up elevation.
/// </summary>
internal static class MfaEndpoints
{
    /// <summary>
    /// Lifetime of the protected <c>StreamActions.MfaState</c> cookie.
    /// </summary>
    public static readonly TimeSpan MfaStateLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// State discriminator for passkey registration options.
    /// </summary>
    private const string _passkeyCreateKind = "passkey-create";

    /// <summary>
    /// State discriminator for passkey assertion request options.
    /// </summary>
    private const string _passkeyRequestKind = "passkey-request";

    /// <summary>
    /// State discriminator for pending TOTP enrollment secrets.
    /// </summary>
    private const string _totpEnrollKind = "totp-enroll";

    /// <summary>
    /// Issuer label displayed in generated <c>otpauth://</c> provisioning URIs.
    /// </summary>
    private const string _totpIssuer = "StreamActions";

    /// <summary>
    /// Number of one-time recovery codes generated during TOTP enrollment.
    /// </summary>
    private const int _recoveryCodeCount = 10;

    /// <summary>
    /// Maps <c>/auth/mfa/*</c> endpoints under <see cref="AuthClaims.MfaPendingPolicy"/> and <see cref="AuthClaims.PanelAuthenticatedPolicy"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The configured <see cref="IEndpointRouteBuilder"/>.</returns>
    internal static IEndpointRouteBuilder MapMfaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder mfaPendingGroup = endpoints.MapGroup(string.Empty)
            .RequireAuthorization(AuthClaims.MfaPendingPolicy);

        _ = mfaPendingGroup.MapPost("/auth/mfa/passkey/create-options", HandlePasskeyCreateOptionsAsync);
        _ = mfaPendingGroup.MapPost("/auth/mfa/passkey/register", HandlePasskeyRegisterAsync);
        _ = mfaPendingGroup.MapPost("/auth/mfa/passkey/request-options", HandlePasskeyRequestOptionsAsync);
        _ = mfaPendingGroup.MapPost("/auth/mfa/passkey/verify", HandlePasskeyVerifyAsync);
        _ = mfaPendingGroup.MapPost("/auth/mfa/totp/enroll/begin", HandleTotpEnrollBeginAsync);
        _ = mfaPendingGroup.MapPost("/auth/mfa/totp/enroll/confirm", HandleTotpEnrollConfirmAsync);
        _ = mfaPendingGroup.MapPost("/auth/mfa/totp/verify", HandleTotpVerifyAsync);
        _ = mfaPendingGroup.MapPost("/auth/mfa/recovery/verify", HandleRecoveryVerifyAsync);

        _ = endpoints.MapPost("/auth/mfa/delete", HandleDeleteCredentialAsync)
            .RequireAuthorization(AuthClaims.PanelAuthenticatedPolicy);

        return endpoints;
    }

    /// <summary>
    /// Generates WebAuthn passkey creation options for the current user and stores attestation state in <c>StreamActions.MfaState</c>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="passkeyHandler">The ASP.NET Core Identity passkey handler.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The WebAuthn creation options JSON, or <c>401</c>/<c>403</c> if unauthorized or disallowed by policy.</returns>
    private static async Task<IResult> HandlePasskeyCreateOptionsAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        IPasskeyHandler<PanelUser> passkeyHandler,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        (PanelUser? user, EffectiveMfaPolicy? policy) = await LoadUserAndPolicyAsync(
            httpContext,
            panelUserStore,
            mfaPolicyService,
            cancellationToken).ConfigureAwait(false);
        if (user is null || policy is null)
        {
            return Results.Unauthorized();
        }

        if (!policy.IsPasskeyAllowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        PasskeyUserEntity userEntity = new()
        {
            Id = user.Id.ToString("D"),
            Name = user.DisplayName,
            DisplayName = user.DisplayName,
        };

        PasskeyCreationOptionsResult optionsResult = await passkeyHandler
            .MakeCreationOptionsAsync(userEntity, httpContext)
            .ConfigureAwait(false);

        WriteMfaStateCookie(
            httpContext,
            dataProtectionProvider,
            new MfaStateCookiePayload
            {
                Kind = _passkeyCreateKind,
                PanelUserId = user.Id,
                State = optionsResult.AttestationState,
                ExpiresAtUtc = GetTimeProvider(httpContext).GetUtcNow() + MfaStateLifetime,
            });

        return Results.Content(optionsResult.CreationOptionsJson, "application/json");
    }

    /// <summary>
    /// Completes WebAuthn passkey attestation, stores the credential, and signs in an elevated principal.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="passkeyHandler">The ASP.NET Core Identity passkey handler.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>200 OK</c> on success, or an error status result.</returns>
    private static async Task<IResult> HandlePasskeyRegisterAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        IPasskeyHandler<PanelUser> passkeyHandler,
        UserManager<PanelUser> userManager,
        IDataProtectionProvider dataProtectionProvider,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        (PanelUser? user, EffectiveMfaPolicy? policy) = await LoadUserAndPolicyAsync(
            httpContext,
            panelUserStore,
            mfaPolicyService,
            cancellationToken).ConfigureAwait(false);
        if (user is null || policy is null)
        {
            return Results.Unauthorized();
        }

        if (!policy.IsPasskeyAllowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        (bool parsed, IResult? errorResult, PasskeySubmissionRequest? request) = await TryReadPasskeyRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || request is null || string.IsNullOrWhiteSpace(request.CredentialJson))
        {
            return errorResult ?? Results.BadRequest("Credential payload is required.");
        }

        MfaStateCookiePayload? statePayload = ReadAndDeleteMfaStateCookie(httpContext, dataProtectionProvider);
        if (statePayload is null
            || statePayload.PanelUserId != user.Id
            || !string.Equals(statePayload.Kind, _passkeyCreateKind, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(statePayload.State))
        {
            return Results.BadRequest("Invalid or expired MFA state.");
        }

        PasskeyAttestationResult attestationResult = await passkeyHandler
            .PerformAttestationAsync(new PasskeyAttestationContext
            {
                HttpContext = httpContext,
                CredentialJson = request.CredentialJson,
                AttestationState = statePayload.State,
            })
            .ConfigureAwait(false);

        if (!attestationResult.Succeeded || attestationResult.Passkey is null)
        {
            return Results.BadRequest(attestationResult.Failure?.Message ?? "Passkey attestation failed.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            attestationResult.Passkey.Name = request.Name.Trim();
        }

        IdentityResult saveResult = await userManager
            .AddOrUpdatePasskeyAsync(user, attestationResult.Passkey)
            .ConfigureAwait(false);
        if (!saveResult.Succeeded)
        {
            return Results.BadRequest("Failed to persist passkey credential.");
        }

        await SignInElevatedAsync(httpContext).ConfigureAwait(false);
        return Results.Ok(new MfaActionResponse { Succeeded = true });
    }

    /// <summary>
    /// Generates WebAuthn passkey assertion request options for the current user and stores assertion state in <c>StreamActions.MfaState</c>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="passkeyHandler">The ASP.NET Core Identity passkey handler.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The WebAuthn request options JSON, or <c>401</c>/<c>403</c> if unauthorized or disallowed by policy.</returns>
    private static async Task<IResult> HandlePasskeyRequestOptionsAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        IPasskeyHandler<PanelUser> passkeyHandler,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        (PanelUser? user, EffectiveMfaPolicy? policy) = await LoadUserAndPolicyAsync(
            httpContext,
            panelUserStore,
            mfaPolicyService,
            cancellationToken).ConfigureAwait(false);
        if (user is null || policy is null)
        {
            return Results.Unauthorized();
        }

        if (!policy.IsPasskeyAllowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        PasskeyRequestOptionsResult optionsResult = await passkeyHandler
            .MakeRequestOptionsAsync(user, httpContext)
            .ConfigureAwait(false);

        WriteMfaStateCookie(
            httpContext,
            dataProtectionProvider,
            new MfaStateCookiePayload
            {
                Kind = _passkeyRequestKind,
                PanelUserId = user.Id,
                State = optionsResult.AssertionState,
                ExpiresAtUtc = GetTimeProvider(httpContext).GetUtcNow() + MfaStateLifetime,
            });

        return Results.Content(optionsResult.RequestOptionsJson, "application/json");
    }

    /// <summary>
    /// Verifies a WebAuthn passkey assertion, updates passkey metadata, and signs in an elevated principal.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="passkeyHandler">The ASP.NET Core Identity passkey handler.</param>
    /// <param name="userManager">The ASP.NET Core Identity user manager.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect or JSON verification response, or an error result.</returns>
    private static async Task<IResult> HandlePasskeyVerifyAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        IPasskeyHandler<PanelUser> passkeyHandler,
        UserManager<PanelUser> userManager,
        IDataProtectionProvider dataProtectionProvider,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        (PanelUser? user, EffectiveMfaPolicy? policy) = await LoadUserAndPolicyAsync(
            httpContext,
            panelUserStore,
            mfaPolicyService,
            cancellationToken).ConfigureAwait(false);
        if (user is null || policy is null)
        {
            return Results.Unauthorized();
        }

        if (!policy.IsPasskeyAllowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        (bool parsed, IResult? errorResult, PasskeySubmissionRequest? request) = await TryReadPasskeyRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || request is null || string.IsNullOrWhiteSpace(request.CredentialJson))
        {
            return errorResult ?? Results.BadRequest("Credential payload is required.");
        }

        MfaStateCookiePayload? statePayload = ReadAndDeleteMfaStateCookie(httpContext, dataProtectionProvider);
        if (statePayload is null
            || statePayload.PanelUserId != user.Id
            || !string.Equals(statePayload.Kind, _passkeyRequestKind, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(statePayload.State))
        {
            return Results.BadRequest("Invalid or expired MFA state.");
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
            || assertionResult.User.Id != user.Id)
        {
            return Results.Unauthorized();
        }

        UserPasskeyInfo? existingPasskey = await userManager
            .GetPasskeyAsync(assertionResult.User, assertionResult.Passkey.CredentialId)
            .ConfigureAwait(false);
        if (existingPasskey is not null)
        {
            existingPasskey.SignCount = assertionResult.Passkey.SignCount;
            existingPasskey.IsBackedUp = assertionResult.Passkey.IsBackedUp;
            existingPasskey.IsUserVerified = assertionResult.Passkey.IsUserVerified;
            _ = await userManager.AddOrUpdatePasskeyAsync(assertionResult.User, existingPasskey).ConfigureAwait(false);
        }
        else
        {
            _ = await userManager.AddOrUpdatePasskeyAsync(assertionResult.User, assertionResult.Passkey).ConfigureAwait(false);
        }

        await SignInElevatedAsync(httpContext).ConfigureAwait(false);
        return CreateVerificationSuccessResult(httpContext, request.ReturnUrl);
    }

    /// <summary>
    /// Begins TOTP enrollment by generating a Base32 secret, storing it in <c>StreamActions.MfaState</c>, and returning provisioning details.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="totpService">The TOTP cryptography service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A JSON response containing the Base32 secret and <c>otpauth://</c> URI, or <c>401</c>/<c>403</c>.</returns>
    private static async Task<IResult> HandleTotpEnrollBeginAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        ITotpService totpService,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        (PanelUser? user, EffectiveMfaPolicy? policy) = await LoadUserAndPolicyAsync(
            httpContext,
            panelUserStore,
            mfaPolicyService,
            cancellationToken).ConfigureAwait(false);
        if (user is null || policy is null)
        {
            return Results.Unauthorized();
        }

        if (!policy.IsTotpAllowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        string secretBase32 = totpService.GenerateSecretBase32();
        Uri otpAuthUri = totpService.BuildOtpAuthUri(_totpIssuer, user.DisplayName, secretBase32);

        WriteMfaStateCookie(
            httpContext,
            dataProtectionProvider,
            new MfaStateCookiePayload
            {
                Kind = _totpEnrollKind,
                PanelUserId = user.Id,
                TotpSecretBase32 = secretBase32,
                ExpiresAtUtc = GetTimeProvider(httpContext).GetUtcNow() + MfaStateLifetime,
            });

        return Results.Ok(new TotpEnrollBeginResponse
        {
            SecretBase32 = secretBase32,
            OtpAuthUri = otpAuthUri,
        });
    }

    /// <summary>
    /// Confirms TOTP enrollment by verifying the initial 6-digit code, generating 10 recovery codes, persisting the credential, and signing in an elevated principal.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="totpService">The TOTP cryptography service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated plaintext recovery codes on success, or an error response.</returns>
    private static async Task<IResult> HandleTotpEnrollConfirmAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        ITotpService totpService,
        IDataProtectionProvider dataProtectionProvider,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        (PanelUser? user, EffectiveMfaPolicy? policy) = await LoadUserAndPolicyAsync(
            httpContext,
            panelUserStore,
            mfaPolicyService,
            cancellationToken).ConfigureAwait(false);
        if (user is null || policy is null)
        {
            return Results.Unauthorized();
        }

        if (!policy.IsTotpAllowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        (bool parsed, IResult? errorResult, MfaCodeRequest? request) = await TryReadCodeRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || request is null)
        {
            return errorResult ?? Results.BadRequest();
        }

        MfaStateCookiePayload? statePayload = TryReadMfaStateCookie(httpContext, dataProtectionProvider);
        if (statePayload is null
            || statePayload.PanelUserId != user.Id
            || !string.Equals(statePayload.Kind, _totpEnrollKind, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(statePayload.TotpSecretBase32))
        {
            DeleteMfaStateCookie(httpContext);
            return Results.BadRequest("Invalid or expired TOTP enrollment state.");
        }

        if (string.IsNullOrWhiteSpace(request.Code)
            || !totpService.VerifyCode(statePayload.TotpSecretBase32, request.Code))
        {
            return Results.BadRequest("Invalid TOTP verification code.");
        }

        DeleteMfaStateCookie(httpContext);

        IReadOnlyList<string> recoveryCodes = totpService.GenerateRecoveryCodes(_recoveryCodeCount);
        List<string> recoveryCodeHashes = new(recoveryCodes.Count);
        foreach (string recoveryCode in recoveryCodes)
        {
            recoveryCodeHashes.Add(totpService.HashRecoveryCode(recoveryCode));
        }

        TotpSecretPayload secretPayload = new(
            statePayload.TotpSecretBase32,
            Label: user.DisplayName,
            EnrolledAtUtc: GetTimeProvider(httpContext).GetUtcNow());

        _ = await mfaCredentialStore
            .SaveTotpAsync(user.Id, secretPayload, recoveryCodeHashes, cancellationToken)
            .ConfigureAwait(false);

        await SignInElevatedAsync(httpContext).ConfigureAwait(false);
        return Results.Ok(new TotpEnrollConfirmResponse
        {
            RecoveryCodes = recoveryCodes,
        });
    }

    /// <summary>
    /// Verifies a TOTP authenticator code for login MFA or step-up elevation when TOTP is permitted by policy.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="totpService">The TOTP cryptography service.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect or JSON verification response on success, <c>403</c> if TOTP is disallowed, or <c>401</c> on invalid code.</returns>
    private static async Task<IResult> HandleTotpVerifyAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        IMfaCredentialStore mfaCredentialStore,
        ITotpService totpService,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        (PanelUser? user, EffectiveMfaPolicy? policy) = await LoadUserAndPolicyAsync(
            httpContext,
            panelUserStore,
            mfaPolicyService,
            cancellationToken).ConfigureAwait(false);
        if (user is null || policy is null)
        {
            return Results.Unauthorized();
        }

        if (!policy.IsTotpAllowed)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        (bool parsed, IResult? errorResult, MfaCodeRequest? request) = await TryReadCodeRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || request is null)
        {
            return errorResult ?? Results.BadRequest();
        }

        TotpSecretPayload? totpPayload = await mfaCredentialStore
            .GetTotpAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (totpPayload is null
            || string.IsNullOrWhiteSpace(request.Code)
            || !totpService.VerifyCode(totpPayload.SecretBase32, request.Code))
        {
            return Results.Unauthorized();
        }

        await SignInElevatedAsync(httpContext).ConfigureAwait(false);
        return CreateVerificationSuccessResult(httpContext, request.ReturnUrl);
    }

    /// <summary>
    /// Verifies and consumes a one-time recovery code for login MFA or step-up elevation.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A redirect or JSON verification response on success, or <c>401</c> on invalid code.</returns>
    private static async Task<IResult> HandleRecoveryVerifyAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaCredentialStore mfaCredentialStore,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        Guid? userId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        PanelUser? user = await panelUserStore.GetByIdAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        (bool parsed, IResult? errorResult, MfaCodeRequest? request) = await TryReadCodeRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || request is null)
        {
            return errorResult ?? Results.BadRequest();
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.Unauthorized();
        }

        bool consumed = await mfaCredentialStore
            .VerifyAndConsumeRecoveryCodeAsync(user.Id, request.Code, cancellationToken)
            .ConfigureAwait(false);
        if (!consumed)
        {
            return Results.Unauthorized();
        }

        await SignInElevatedAsync(httpContext).ConfigureAwait(false);
        return CreateVerificationSuccessResult(httpContext, request.ReturnUrl);
    }

    /// <summary>
    /// Deletes an MFA credential owned by the currently authenticated user.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaCredentialStore">The MFA credential persistence store.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>200 OK</c> or redirect on success, <c>404</c> if not found, or <c>400</c>/<c>401</c>.</returns>
    private static async Task<IResult> HandleDeleteCredentialAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaCredentialStore mfaCredentialStore,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        Guid? userId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        PanelUser? user = await panelUserStore.GetByIdAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        (bool parsed, IResult? errorResult, MfaDeleteRequest? request) = await TryReadDeleteRequestAsync(
            httpContext,
            antiforgery,
            cancellationToken).ConfigureAwait(false);
        if (!parsed || request is null)
        {
            return errorResult ?? Results.BadRequest();
        }

        if (!Guid.TryParse(request.CredentialId, out Guid credentialId) || credentialId == Guid.Empty)
        {
            return Results.BadRequest("Invalid credential identifier.");
        }

        bool deleted = await mfaCredentialStore
            .DeleteCredentialAsync(user.Id, credentialId, cancellationToken)
            .ConfigureAwait(false);
        return !deleted
            ? Results.NotFound()
            : PrefersHtmlResponse(httpContext.Request)
            ? Results.Redirect("/account/mfa")
            : Results.Ok(new MfaActionResponse { Succeeded = true });
    }

    /// <summary>
    /// Loads the authenticated <see cref="PanelUser"/> and computes their effective <see cref="EffectiveMfaPolicy"/>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="panelUserStore">The panel user persistence store.</param>
    /// <param name="mfaPolicyService">The effective MFA policy evaluator.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved user and policy, or nulls when the user cannot be resolved.</returns>
    private static async ValueTask<(PanelUser? User, EffectiveMfaPolicy? Policy)> LoadUserAndPolicyAsync(
        HttpContext httpContext,
        IPanelUserStore panelUserStore,
        IMfaPolicyService mfaPolicyService,
        CancellationToken cancellationToken)
    {
        Guid? userId = AuthClaims.GetPanelUserId(httpContext.User);
        if (!userId.HasValue)
        {
            return (null, null);
        }

        PanelUser? user = await panelUserStore.GetByIdAsync(userId.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return (null, null);
        }

        IReadOnlyList<UserLoginIdentity> identities = await panelUserStore
            .GetLoginIdentitiesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<string> oidcGroups = AuthClaims.GetOidcGroups(httpContext.User);
        EffectiveMfaPolicy policy = await mfaPolicyService
            .GetEffectivePolicyForUserAsync(user, identities, oidcGroups, workspaceId: null, cancellationToken)
            .ConfigureAwait(false);

        return (user, policy);
    }

    /// <summary>
    /// Signs in an updated <see cref="ClaimsPrincipal"/> with pending MFA flags removed and step-up elevation granted.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    private static async Task SignInElevatedAsync(HttpContext httpContext)
    {
        DateTimeOffset elevatedUntilUtc = GetTimeProvider(httpContext).GetUtcNow() + AuthClaims.StepUpWindow;
        ClaimsPrincipal elevatedPrincipal = AuthClaims.WithMfaCompletedAndElevated(httpContext.User, elevatedUntilUtc);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            elevatedPrincipal).ConfigureAwait(false);
    }

    /// <summary>
    /// Produces an HTML redirect or JSON verification response depending on the request <c>Accept</c> header.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="returnUrl">The optional return URL.</param>
    /// <returns>An <see cref="IResult"/> representing verification success.</returns>
    private static IResult CreateVerificationSuccessResult(HttpContext httpContext, string? returnUrl)
    {
        string safeReturnUrl = AuthClaims.SafeReturnUrlOrDefault(returnUrl, "/");
        return PrefersHtmlResponse(httpContext.Request)
            ? Results.Redirect(safeReturnUrl)
            : Results.Ok(new MfaVerifyResponse
            {
                Verified = true,
                ReturnUrl = safeReturnUrl,
            });
    }

    /// <summary>
    /// Parses a WebAuthn passkey submission from JSON (wrapped or raw <c>PublicKeyCredential</c>) or an antiforgery-validated form.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating parse success, an optional failure <see cref="IResult"/>, and the parsed request.</returns>
    private static async Task<(bool Succeeded, IResult? ErrorResult, PasskeySubmissionRequest? Request)> TryReadPasskeyRequestAsync(
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
            return (true, null, new PasskeySubmissionRequest
            {
                CredentialJson = form["credentialJson"].FirstOrDefault() ?? form["CredentialJson"].FirstOrDefault(),
                Name = form["name"].FirstOrDefault() ?? form["Name"].FirstOrDefault(),
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

                if (TryGetStringProperty(document.RootElement, "credentialJson", out string? wrappedCredentialJson))
                {
                    _ = TryGetStringProperty(document.RootElement, "name", out string? name);
                    _ = TryGetStringProperty(document.RootElement, "returnUrl", out string? returnUrl);
                    return (true, null, new PasskeySubmissionRequest
                    {
                        CredentialJson = wrappedCredentialJson,
                        Name = name,
                        ReturnUrl = returnUrl,
                    });
                }

                _ = TryGetStringProperty(document.RootElement, "name", out string? rawName);
                _ = TryGetStringProperty(document.RootElement, "returnUrl", out string? rawReturnUrl);
                return (true, null, new PasskeySubmissionRequest
                {
                    CredentialJson = rawBody,
                    Name = rawName,
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
    /// Parses a TOTP or recovery code submission from an antiforgery-validated form or JSON request body.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating parse success, an optional failure <see cref="IResult"/>, and the parsed request.</returns>
    private static async Task<(bool Succeeded, IResult? ErrorResult, MfaCodeRequest? Request)> TryReadCodeRequestAsync(
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
            return (true, null, new MfaCodeRequest
            {
                Code = form["code"].FirstOrDefault() ?? form["Code"].FirstOrDefault(),
                ReturnUrl = form["returnUrl"].FirstOrDefault() ?? form["ReturnUrl"].FirstOrDefault(),
            });
        }

        if (httpContext.Request.HasJsonContentType())
        {
            try
            {
                MfaCodeRequest? jsonRequest = await httpContext.Request
                    .ReadFromJsonAsync<MfaCodeRequest>(cancellationToken)
                    .ConfigureAwait(false);
                return jsonRequest is null
                    ? (false, Results.BadRequest(), null)
                    : (true, null, jsonRequest);
            }
            catch (JsonException)
            {
                return (false, Results.BadRequest(), null);
            }
        }

        return (false, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType), null);
    }

    /// <summary>
    /// Parses an MFA credential deletion request from an antiforgery-validated form or JSON request body.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery validation service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple indicating parse success, an optional failure <see cref="IResult"/>, and the parsed request.</returns>
    private static async Task<(bool Succeeded, IResult? ErrorResult, MfaDeleteRequest? Request)> TryReadDeleteRequestAsync(
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
            return (true, null, new MfaDeleteRequest
            {
                CredentialId = form["credentialId"].FirstOrDefault() ?? form["CredentialId"].FirstOrDefault(),
            });
        }

        if (httpContext.Request.HasJsonContentType())
        {
            try
            {
                MfaDeleteRequest? jsonRequest = await httpContext.Request
                    .ReadFromJsonAsync<MfaDeleteRequest>(cancellationToken)
                    .ConfigureAwait(false);
                return jsonRequest is null
                    ? (false, Results.BadRequest(), null)
                    : (true, null, jsonRequest);
            }
            catch (JsonException)
            {
                return (false, Results.BadRequest(), null);
            }
        }

        return (false, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType), null);
    }

    /// <summary>
    /// Encrypts and writes the <c>StreamActions.MfaState</c> <see langword="HttpOnly"/> cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="payload">The MFA state payload to protect.</param>
    private static void WriteMfaStateCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider,
        MfaStateCookiePayload payload)
    {
        string json = JsonSerializer.Serialize(payload);
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(AuthClaims.MfaStateProtectorPurpose)
            .ToTimeLimitedDataProtector();
        string protectedValue = protector.Protect(json, MfaStateLifetime);

        httpContext.Response.Cookies.Append(
            AuthClaims.MfaStateCookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                IsEssential = true,
                MaxAge = MfaStateLifetime,
                Path = "/",
            });
    }

    /// <summary>
    /// Reads and immediately deletes the <c>StreamActions.MfaState</c> cookie.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <returns>The unprotected <see cref="MfaStateCookiePayload"/>, or <see langword="null"/> if absent, expired, or invalid.</returns>
    private static MfaStateCookiePayload? ReadAndDeleteMfaStateCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider)
    {
        MfaStateCookiePayload? payload = TryReadMfaStateCookie(httpContext, dataProtectionProvider);
        DeleteMfaStateCookie(httpContext);
        return payload;
    }

    /// <summary>
    /// Attempts to read and validate the <c>StreamActions.MfaState</c> cookie without deleting it.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <returns>The unprotected <see cref="MfaStateCookiePayload"/>, or <see langword="null"/> if absent, expired, or invalid.</returns>
    private static MfaStateCookiePayload? TryReadMfaStateCookie(
        HttpContext httpContext,
        IDataProtectionProvider dataProtectionProvider)
    {
        if (!httpContext.Request.Cookies.TryGetValue(AuthClaims.MfaStateCookieName, out string? protectedValue)
            || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(AuthClaims.MfaStateProtectorPurpose)
                .ToTimeLimitedDataProtector();
            string json = protector.Unprotect(protectedValue);
            MfaStateCookiePayload? payload = JsonSerializer.Deserialize<MfaStateCookiePayload>(json);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Kind) || !payload.PanelUserId.HasValue)
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
    /// Deletes the <c>StreamActions.MfaState</c> cookie with matching security and path attributes.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    private static void DeleteMfaStateCookie(HttpContext httpContext) =>
        httpContext.Response.Cookies.Delete(AuthClaims.MfaStateCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            IsEssential = true,
            Path = "/",
        });

    /// <summary>
    /// Attempts to read a case-insensitive string property from a JSON object element.
    /// </summary>
    /// <param name="element">The JSON object element.</param>
    /// <param name="propertyName">The target property name.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the string value.</param>
    /// <returns><see langword="true"/> if a matching string property was found; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
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
    /// Resolves <see cref="TimeProvider"/> from request services, falling back to <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The resolved <see cref="TimeProvider"/>.</returns>
    private static TimeProvider GetTimeProvider(HttpContext httpContext) =>
        httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
}

/// <summary>
/// Represents the protected payload stored inside the <c>StreamActions.MfaState</c> cookie.
/// </summary>
internal sealed record MfaStateCookiePayload
{
    /// <summary>
    /// Gets the MFA state flow discriminator.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Gets the bound panel user identifier.
    /// </summary>
    public Guid? PanelUserId { get; init; }

    /// <summary>
    /// Gets the serialized ASP.NET Core Identity passkey attestation or assertion state.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the pending Base32 TOTP secret during TOTP enrollment.
    /// </summary>
    public string? TotpSecretBase32 { get; init; }

    /// <summary>
    /// Gets the UTC expiration timestamp for this MFA state payload.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

/// <summary>
/// Represents a submitted WebAuthn passkey attestation or assertion request.
/// </summary>
internal sealed record PasskeySubmissionRequest
{
    /// <summary>
    /// Gets the serialized WebAuthn credential JSON string.
    /// </summary>
    public string? CredentialJson { get; init; }

    /// <summary>
    /// Gets the optional friendly passkey display name during registration.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the optional safe local redirect target after verification.
    /// </summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// Represents a TOTP or recovery code verification request.
/// </summary>
internal sealed record MfaCodeRequest
{
    /// <summary>
    /// Gets the submitted TOTP or recovery code.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Gets the optional safe local redirect target after verification.
    /// </summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// Represents an MFA credential deletion request.
/// </summary>
internal sealed record MfaDeleteRequest
{
    /// <summary>
    /// Gets the database identifier of the credential to delete.
    /// </summary>
    public string? CredentialId { get; init; }
}

/// <summary>
/// Represents the JSON response returned by <c>POST /auth/mfa/totp/enroll/begin</c>.
/// </summary>
internal sealed record TotpEnrollBeginResponse
{
    /// <summary>
    /// Gets the Base32-encoded TOTP secret key.
    /// </summary>
    public string? SecretBase32 { get; init; }

    /// <summary>
    /// Gets the <c>otpauth://totp/...</c> provisioning URI.
    /// </summary>
    public Uri? OtpAuthUri { get; init; }
}

/// <summary>
/// Represents the JSON response returned by <c>POST /auth/mfa/totp/enroll/confirm</c>.
/// </summary>
internal sealed record TotpEnrollConfirmResponse
{
    /// <summary>
    /// Gets the newly generated plaintext one-time recovery codes.
    /// </summary>
    public IReadOnlyList<string>? RecoveryCodes { get; init; }
}

/// <summary>
/// Represents a JSON verification success response returned by MFA challenge endpoints.
/// </summary>
internal sealed record MfaVerifyResponse
{
    /// <summary>
    /// Gets a value indicating whether MFA verification succeeded.
    /// </summary>
    public bool? Verified { get; init; }

    /// <summary>
    /// Gets the validated safe local redirect target.
    /// </summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>
/// Represents a generic JSON success response for MFA enrollment or deletion actions.
/// </summary>
internal sealed record MfaActionResponse
{
    /// <summary>
    /// Gets a value indicating whether the action succeeded.
    /// </summary>
    public bool? Succeeded { get; init; }
}
