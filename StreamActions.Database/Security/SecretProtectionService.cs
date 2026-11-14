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
