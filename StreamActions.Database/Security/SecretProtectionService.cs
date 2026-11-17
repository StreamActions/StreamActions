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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using StreamActions.Core.Models;
using System.Security.Cryptography;

namespace StreamActions.Database.Security;

/// <summary>
/// Encrypts and decrypts workspace tokens, workspace secrets, and instance OAuth configurations using ASP.NET Core Data Protection with purpose-bound AAD.
/// </summary>
public sealed partial class SecretProtectionService
{
    /// <summary>
    /// Root Data Protection purpose string for StreamActions secrets.
    /// </summary>
    private const string _rootPurpose = "StreamActions.Secrets.v1";

    /// <summary>
    /// The Data Protection provider used to create purpose-bound protectors.
    /// </summary>
    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// The logger instance for key-ring mismatch warnings.
    /// </summary>
    private readonly ILogger<SecretProtectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretProtectionService"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The Data Protection provider.</param>
    /// <param name="logger">The logger instance.</param>
    public SecretProtectionService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SecretProtectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._dataProtectionProvider = dataProtectionProvider;
        this._logger = logger;
    }

    /// <summary>
    /// Encrypts a workspace-scoped platform token bound to the workspace, platform, and token role.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="role">The token role.</param>
    /// <param name="plaintext">The plaintext token payload.</param>
    /// <returns>The Data Protection protected payload.</returns>
    public string ProtectWorkspaceToken(Guid workspaceId, string platformId, TokenRole role, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentNullException.ThrowIfNull(plaintext);

        IDataProtector protector = this.CreateWorkspaceTokenProtector(workspaceId, platformId, role);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Attempts to decrypt a workspace-scoped platform token.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="role">The token role.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <returns>A tuple indicating success, the decrypted plaintext if successful, and the resulting <see cref="CredentialStatus"/>.</returns>
    public (bool Success, string? Plaintext, CredentialStatus Status) TryUnprotectWorkspaceToken(
        Guid workspaceId,
        string platformId,
        TokenRole role,
        string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentNullException.ThrowIfNull(protectedPayload);

        IDataProtector protector = this.CreateWorkspaceTokenProtector(workspaceId, platformId, role);
        try
        {
            string plaintext = protector.Unprotect(protectedPayload);
            return (true, plaintext, CredentialStatus.Active);
        }
        catch (CryptographicException ex)
        {
            LogWorkspaceTokenKeyRingMismatch(this._logger, workspaceId, platformId, role, ex);
            return (false, null, CredentialStatus.KeyRingMismatchRequiresReauth);
        }
    }

    /// <summary>
    /// Encrypts a workspace-scoped custom secret bound to the workspace and secret name.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The secret name.</param>
    /// <param name="plaintext">The plaintext secret value.</param>
    /// <returns>The Data Protection protected payload.</returns>
    public string ProtectWorkspaceSecret(Guid workspaceId, string secretName, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentNullException.ThrowIfNull(plaintext);

        IDataProtector protector = this.CreateWorkspaceSecretProtector(workspaceId, secretName);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Attempts to decrypt a workspace-scoped custom secret.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The secret name.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <returns>A tuple indicating success, the decrypted plaintext if successful, and the resulting <see cref="CredentialStatus"/>.</returns>
    public (bool Success, string? Plaintext, CredentialStatus Status) TryUnprotectWorkspaceSecret(
        Guid workspaceId,
        string secretName,
        string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentNullException.ThrowIfNull(protectedPayload);

        IDataProtector protector = this.CreateWorkspaceSecretProtector(workspaceId, secretName);
        try
        {
            string plaintext = protector.Unprotect(protectedPayload);
            return (true, plaintext, CredentialStatus.Active);
        }
        catch (CryptographicException ex)
        {
            LogWorkspaceSecretKeyRingMismatch(this._logger, workspaceId, secretName, ex);
            return (false, null, CredentialStatus.KeyRingMismatchRequiresReauth);
        }
    }

    /// <summary>
    /// Encrypts an instance-wide OAuth client configuration payload bound to the provider identifier.
    /// </summary>
    /// <param name="providerId">The OAuth/OIDC provider identifier.</param>
    /// <param name="plaintext">The plaintext configuration JSON.</param>
    /// <returns>The Data Protection protected payload.</returns>
    public string ProtectInstanceOAuth(string providerId, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(plaintext);

        IDataProtector protector = this.CreateInstanceOAuthProtector(providerId);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Attempts to decrypt an instance-wide OAuth client configuration payload.
    /// </summary>
    /// <param name="providerId">The OAuth/OIDC provider identifier.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <returns>A tuple indicating success, the decrypted plaintext if successful, and the resulting <see cref="CredentialStatus"/>.</returns>
    public (bool Success, string? Plaintext, CredentialStatus Status) TryUnprotectInstanceOAuth(
        string providerId,
        string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(protectedPayload);

        IDataProtector protector = this.CreateInstanceOAuthProtector(providerId);
        try
        {
            string plaintext = protector.Unprotect(protectedPayload);
            return (true, plaintext, CredentialStatus.Active);
        }
        catch (CryptographicException ex)
        {
            LogInstanceOAuthKeyRingMismatch(this._logger, providerId, ex);
            return (false, null, CredentialStatus.KeyRingMismatchRequiresReauth);
        }
    }

    /// <summary>
    /// Encrypts a workspace-scoped custom OAuth client secret bound to the workspace and platform.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="plaintext">The plaintext custom OAuth client secret.</param>
    /// <returns>The Data Protection protected payload.</returns>
    public string ProtectWorkspaceOAuth(Guid workspaceId, string platformId, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentNullException.ThrowIfNull(plaintext);

        IDataProtector protector = this.CreateWorkspaceOAuthProtector(workspaceId, platformId);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Attempts to decrypt a workspace-scoped custom OAuth client secret.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <returns>A tuple indicating success, the decrypted plaintext if successful, and the resulting <see cref="CredentialStatus"/>.</returns>
    public (bool Success, string? Plaintext, CredentialStatus Status) TryUnprotectWorkspaceOAuth(
        Guid workspaceId,
        string platformId,
        string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentNullException.ThrowIfNull(protectedPayload);

        IDataProtector protector = this.CreateWorkspaceOAuthProtector(workspaceId, platformId);
        try
        {
            string plaintext = protector.Unprotect(protectedPayload);
            return (true, plaintext, CredentialStatus.Active);
        }
        catch (CryptographicException ex)
        {
            LogWorkspaceOAuthKeyRingMismatch(this._logger, workspaceId, platformId, ex);
            return (false, null, CredentialStatus.KeyRingMismatchRequiresReauth);
        }
    }

    /// <summary>
    /// Encrypts an external OIDC provider client secret bound to its scope (instance or workspace) and slug.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier, or <see langword="null"/> for an instance-wide provider.</param>
    /// <param name="slug">The provider slug.</param>
    /// <param name="plaintext">The plaintext client secret.</param>
    /// <returns>The Data Protection protected payload.</returns>
    public string ProtectExternalOidcSecret(Guid? workspaceId, string slug, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(plaintext);

        IDataProtector protector = this.CreateExternalOidcProtector(workspaceId, slug);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Attempts to decrypt an external OIDC provider client secret.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier, or <see langword="null"/> for an instance-wide provider.</param>
    /// <param name="slug">The provider slug.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <returns>A tuple indicating success, the decrypted plaintext if successful, and the resulting <see cref="CredentialStatus"/>.</returns>
    public (bool Success, string? Plaintext, CredentialStatus Status) TryUnprotectExternalOidcSecret(
        Guid? workspaceId,
        string slug,
        string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(protectedPayload);

        IDataProtector protector = this.CreateExternalOidcProtector(workspaceId, slug);
        try
        {
            string plaintext = protector.Unprotect(protectedPayload);
            return (true, plaintext, CredentialStatus.Active);
        }
        catch (CryptographicException ex)
        {
            LogExternalOidcKeyRingMismatch(this._logger, workspaceId, slug, ex);
            return (false, null, CredentialStatus.KeyRingMismatchRequiresReauth);
        }
    }

    /// <summary>
    /// Encrypts a user-scoped MFA credential payload bound to the panel user and MFA method.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="method">The MFA method.</param>
    /// <param name="plaintext">The plaintext MFA credential payload.</param>
    /// <returns>The Data Protection protected payload.</returns>
    public string ProtectUserMfa(Guid panelUserId, MfaMethod method, string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        IDataProtector protector = this.CreateUserMfaProtector(panelUserId, method);
        return protector.Protect(plaintext);
    }

    /// <summary>
    /// Attempts to decrypt a user-scoped MFA credential payload.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="method">The MFA method.</param>
    /// <param name="protectedPayload">The protected payload.</param>
    /// <returns>A tuple indicating success, the decrypted plaintext if successful, and the resulting <see cref="CredentialStatus"/>.</returns>
    public (bool Success, string? Plaintext, CredentialStatus Status) TryUnprotectUserMfa(
        Guid panelUserId,
        MfaMethod method,
        string protectedPayload)
    {
        ArgumentNullException.ThrowIfNull(protectedPayload);

        IDataProtector protector = this.CreateUserMfaProtector(panelUserId, method);
        try
        {
            string plaintext = protector.Unprotect(protectedPayload);
            return (true, plaintext, CredentialStatus.Active);
        }
        catch (CryptographicException ex)
        {
            LogUserMfaKeyRingMismatch(this._logger, panelUserId, method, ex);
            return (false, null, CredentialStatus.KeyRingMismatchRequiresReauth);
        }
    }

    /// <summary>
    /// Creates a purpose-bound <see cref="IDataProtector"/> for a workspace platform token.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="role">The token role.</param>
    /// <returns>A purpose-bound <see cref="IDataProtector"/>.</returns>
    private IDataProtector CreateWorkspaceTokenProtector(Guid workspaceId, string platformId, TokenRole role) =>
        this._dataProtectionProvider.CreateProtector(
            _rootPurpose,
            $"Workspace:{workspaceId:N}",
            $"Platform:{platformId.ToLowerInvariant()}",
            $"TokenRole:{role}");

    /// <summary>
    /// Creates a purpose-bound <see cref="IDataProtector"/> for a workspace custom secret.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The secret name.</param>
    /// <returns>A purpose-bound <see cref="IDataProtector"/>.</returns>
    private IDataProtector CreateWorkspaceSecretProtector(Guid workspaceId, string secretName) =>
        this._dataProtectionProvider.CreateProtector(
            _rootPurpose,
            $"Workspace:{workspaceId:N}",
            $"Secret:{secretName.ToLowerInvariant()}");

    /// <summary>
    /// Creates a purpose-bound <see cref="IDataProtector"/> for an instance-wide OAuth configuration.
    /// </summary>
    /// <param name="providerId">The OAuth/OIDC provider identifier.</param>
    /// <returns>A purpose-bound <see cref="IDataProtector"/>.</returns>
    private IDataProtector CreateInstanceOAuthProtector(string providerId) =>
        this._dataProtectionProvider.CreateProtector(
            _rootPurpose,
            "Instance",
            $"Provider:{providerId.ToLowerInvariant()}");

    /// <summary>
    /// Creates a purpose-bound <see cref="IDataProtector"/> for a workspace custom OAuth client secret.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <returns>A purpose-bound <see cref="IDataProtector"/>.</returns>
    private IDataProtector CreateWorkspaceOAuthProtector(Guid workspaceId, string platformId) =>
        this._dataProtectionProvider.CreateProtector(
            _rootPurpose,
            $"Workspace:{workspaceId:N}",
            $"PlatformOAuth:{platformId.Trim().ToLowerInvariant()}");

    /// <summary>
    /// Creates a purpose-bound <see cref="IDataProtector"/> for an external OIDC provider client secret.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier, or <see langword="null"/> for instance scope.</param>
    /// <param name="slug">The provider slug.</param>
    /// <returns>A purpose-bound <see cref="IDataProtector"/>.</returns>
    private IDataProtector CreateExternalOidcProtector(Guid? workspaceId, string slug)
    {
        string scopeSegment = workspaceId.HasValue ? $"Workspace:{workspaceId.Value:N}" : "Instance";
        return this._dataProtectionProvider.CreateProtector(
            _rootPurpose,
            scopeSegment,
            $"ExternalOidc:{slug.Trim().ToLowerInvariant()}");
    }

    /// <summary>
    /// Creates a purpose-bound <see cref="IDataProtector"/> for a panel user MFA credential.
    /// </summary>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="method">The MFA method.</param>
    /// <returns>A purpose-bound <see cref="IDataProtector"/>.</returns>
    private IDataProtector CreateUserMfaProtector(Guid panelUserId, MfaMethod method) =>
        this._dataProtectionProvider.CreateProtector(
            _rootPurpose,
            $"PanelUser:{panelUserId:N}",
            $"MfaMethod:{method}");

    /// <summary>
    /// Logs a warning when decrypting a panel user MFA credential fails due to a Data Protection key-ring mismatch.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="panelUserId">The panel user identifier.</param>
    /// <param name="method">The MFA method.</param>
    /// <param name="exception">The cryptographic exception thrown during unprotection.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to decrypt user MFA credential for panel user {PanelUserId}, method {Method}; marking as KeyRingMismatchRequiresReauth.")]
    private static partial void LogUserMfaKeyRingMismatch(
        ILogger logger,
        Guid panelUserId,
        MfaMethod method,
        Exception exception);

    /// <summary>
    /// Logs a warning when decrypting a workspace custom OAuth secret fails due to a Data Protection key-ring mismatch.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="exception">The cryptographic exception thrown during unprotection.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to decrypt workspace custom OAuth secret for workspace {WorkspaceId}, platform {PlatformId}; marking as KeyRingMismatchRequiresReauth.")]
    private static partial void LogWorkspaceOAuthKeyRingMismatch(
        ILogger logger,
        Guid workspaceId,
        string platformId,
        Exception exception);

    /// <summary>
    /// Logs a warning when decrypting an external OIDC client secret fails due to a Data Protection key-ring mismatch.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="slug">The provider slug.</param>
    /// <param name="exception">The cryptographic exception thrown during unprotection.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to decrypt external OIDC client secret for workspace {WorkspaceId}, slug {Slug}; marking as KeyRingMismatchRequiresReauth.")]
    private static partial void LogExternalOidcKeyRingMismatch(
        ILogger logger,
        Guid? workspaceId,
        string slug,
        Exception exception);

    /// <summary>
    /// Logs a warning when decrypting a workspace token fails due to a Data Protection key-ring mismatch.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="role">The token role.</param>
    /// <param name="exception">The cryptographic exception thrown during unprotection.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to decrypt workspace token for workspace {WorkspaceId}, platform {PlatformId}, role {Role}; marking as KeyRingMismatchRequiresReauth.")]
    private static partial void LogWorkspaceTokenKeyRingMismatch(
        ILogger logger,
        Guid workspaceId,
        string platformId,
        TokenRole role,
        Exception exception);

    /// <summary>
    /// Logs a warning when decrypting a workspace secret fails due to a Data Protection key-ring mismatch.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The secret name.</param>
    /// <param name="exception">The cryptographic exception thrown during unprotection.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to decrypt workspace secret {SecretName} for workspace {WorkspaceId}; marking as KeyRingMismatchRequiresReauth.")]
    private static partial void LogWorkspaceSecretKeyRingMismatch(
        ILogger logger,
        Guid workspaceId,
        string secretName,
        Exception exception);

    /// <summary>
    /// Logs a warning when decrypting an instance OAuth configuration fails due to a Data Protection key-ring mismatch.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="providerId">The OAuth/OIDC provider identifier.</param>
    /// <param name="exception">The cryptographic exception thrown during unprotection.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to decrypt instance OAuth configuration for provider {ProviderId}; marking as KeyRingMismatchRequiresReauth.")]
    private static partial void LogInstanceOAuthKeyRingMismatch(
        ILogger logger,
        string providerId,
        Exception exception);
}
