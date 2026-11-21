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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Http;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Security;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core and Data Protection implementation of <see cref="ISecretVault"/>.
/// </summary>
public sealed partial class SecretVault : ISecretVault
{
    /// <summary>
    /// JSON serializer options used for persisting allowed host pattern lists.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The Data Protection service for encrypting and decrypting workspace secrets.
    /// </summary>
    private readonly SecretProtectionService _secretProtection;

    /// <summary>
    /// The secret redactor used to register plaintext secrets upon write or read.
    /// </summary>
    private readonly ISecretRedactor _secretRedactor;

    /// <summary>
    /// The time provider used for expiration and update timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger instance for key-ring mismatch diagnostics.
    /// </summary>
    private readonly ILogger<SecretVault> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretVault"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="secretProtection">The secret protection service.</param>
    /// <param name="secretRedactor">The secret redactor.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger instance.</param>
    public SecretVault(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        SecretProtectionService secretProtection,
        ISecretRedactor secretRedactor,
        TimeProvider timeProvider,
        ILogger<SecretVault> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(secretProtection);
        ArgumentNullException.ThrowIfNull(secretRedactor);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._dbContextFactory = dbContextFactory;
        this._secretProtection = secretProtection;
        this._secretRedactor = secretRedactor;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetSecretValueAsync(
        Guid workspaceId,
        string secretName,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = NormalizeAndValidateSecretName(secretName);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceSecret? secret = await db.WorkspaceSecrets
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => (x.WorkspaceId == workspaceId) && (x.SecretName == normalizedName),
                    cancellationToken)
                .ConfigureAwait(false);

            if (secret is null)
            {
                return null;
            }

            if (secret.Status is CredentialStatus.Revoked or CredentialStatus.KeyRingMismatchRequiresReauth)
            {
                return null;
            }

            DateTimeOffset now = this._timeProvider.GetUtcNow();
            if ((secret.Status == CredentialStatus.Expired)
                || (secret.ExpiresAtUtc.HasValue && (secret.ExpiresAtUtc.Value <= now)))
            {
                return null;
            }

            (bool success, string? plaintext, CredentialStatus _) = this._secretProtection.TryUnprotectWorkspaceSecret(
                workspaceId,
                normalizedName,
                secret.EncryptedValue);

            if (!success || (plaintext is null))
            {
                secret.Status = CredentialStatus.KeyRingMismatchRequiresReauth;
                secret.UpdatedAtUtc = now;
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                LogSecretKeyRingMismatch(this._logger, workspaceId, normalizedName);
                return null;
            }

            this._secretRedactor.RegisterSecret(plaintext);
            return plaintext;
        }
    }

    /// <inheritdoc/>
    public async ValueTask SetSecretAsync(
        Guid workspaceId,
        string secretName,
        string plaintextValue,
        IReadOnlyCollection<string> allowedHostPatterns,
        DateTimeOffset? expiresAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = NormalizeAndValidateSecretName(secretName);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextValue);
        IReadOnlyList<string> normalizedPatterns = NormalizeAndValidateHostPatterns(allowedHostPatterns);

        string encrypted = this._secretProtection.ProtectWorkspaceSecret(workspaceId, normalizedName, plaintextValue);
        this._secretRedactor.RegisterSecret(plaintextValue);

        string patternsJson = JsonSerializer.Serialize(normalizedPatterns, _jsonOptions);
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceSecret? existing = await db.WorkspaceSecrets
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => (x.WorkspaceId == workspaceId) && (x.SecretName == normalizedName),
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.EncryptedValue = encrypted;
                existing.AllowedHostPatternsJson = patternsJson;
                existing.Status = CredentialStatus.Active;
                existing.ExpiresAtUtc = expiresAtUtc;
                existing.UpdatedAtUtc = now;
            }
            else
            {
                WorkspaceSecret persisted = new()
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    SecretName = normalizedName,
                    EncryptedValue = encrypted,
                    AllowedHostPatternsJson = patternsJson,
                    Status = CredentialStatus.Active,
                    ExpiresAtUtc = expiresAtUtc,
                    UpdatedAtUtc = now,
                };

                _ = db.WorkspaceSecrets.Add(persisted);
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteSecretAsync(
        Guid workspaceId,
        string secretName,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = NormalizeAndValidateSecretName(secretName);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            int deleted = await db.WorkspaceSecrets
                .Where(x => (x.WorkspaceId == workspaceId) && (x.SecretName == normalizedName))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return deleted > 0;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceSecretMetadata>> GetSecretsMetadataAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var rows = await db.WorkspaceSecrets
                .AsNoTracking()
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.SecretName)
                .Select(static x => new
                {
                    x.Id,
                    x.WorkspaceId,
                    x.SecretName,
                    x.AllowedHostPatternsJson,
                    x.Status,
                    x.ExpiresAtUtc,
                    x.UpdatedAtUtc,
                    HasSecretConfigured = !string.IsNullOrEmpty(x.EncryptedValue),
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<WorkspaceSecretMetadata> metadata = new(rows.Count);
            foreach (var row in rows)
            {
                bool isExpired = (row.Status == CredentialStatus.Expired)
                    || (row.ExpiresAtUtc.HasValue && (row.ExpiresAtUtc.Value <= now));

                metadata.Add(new(
                    row.Id,
                    row.WorkspaceId,
                    row.SecretName,
                    DeserializeHostPatterns(row.AllowedHostPatternsJson),
                    row.Status,
                    row.ExpiresAtUtc,
                    isExpired,
                    row.UpdatedAtUtc,
                    row.HasSecretConfigured));
            }

            return metadata;
        }
    }

    /// <inheritdoc/>
    public async ValueTask UpdateSecretPolicyAsync(
        Guid workspaceId,
        string secretName,
        IReadOnlyCollection<string> allowedHostPatterns,
        DateTimeOffset? expiresAtUtc = null,
        string? newPlaintextValue = null,
        bool broadcasterConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = NormalizeAndValidateSecretName(secretName);
        IReadOnlyList<string> normalizedNewPatterns = NormalizeAndValidateHostPatterns(allowedHostPatterns);

        if (newPlaintextValue is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newPlaintextValue);
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceSecret existing = await db.WorkspaceSecrets
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => (x.WorkspaceId == workspaceId) && (x.SecretName == normalizedName),
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Workspace secret '{normalizedName}' was not found in workspace '{workspaceId}'.");

            IReadOnlyList<string> existingPatterns = DeserializeHostPatterns(existing.AllowedHostPatternsJson);
            bool isHostExpansion = !HostPatternMatcher.ArePatternsCoveredBy(normalizedNewPatterns, existingPatterns);
            if (isHostExpansion && (newPlaintextValue is null) && !broadcasterConfirmed)
            {
                throw new InvalidOperationException(
                    $"Expanding AllowedHostPatterns for secret '{normalizedName}' requires rotating the secret value or explicit broadcaster confirmation.");
            }

            DateTimeOffset now = this._timeProvider.GetUtcNow();
            if (newPlaintextValue is not null)
            {
                existing.EncryptedValue = this._secretProtection.ProtectWorkspaceSecret(workspaceId, normalizedName, newPlaintextValue);
                this._secretRedactor.RegisterSecret(newPlaintextValue);
                existing.Status = CredentialStatus.Active;
            }
            else if ((existing.Status == CredentialStatus.Expired) && (!expiresAtUtc.HasValue || (expiresAtUtc.Value > now)))
            {
                existing.Status = CredentialStatus.Active;
            }

            existing.AllowedHostPatternsJson = JsonSerializer.Serialize(normalizedNewPatterns, _jsonOptions);
            existing.ExpiresAtUtc = expiresAtUtc;
            existing.UpdatedAtUtc = now;

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SecretResolutionResult> ResolveForHostAsync(
        Guid workspaceId,
        string secretName,
        Uri targetUri,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = NormalizeAndValidateSecretName(secretName);
        ArgumentNullException.ThrowIfNull(targetUri);

        if (!targetUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Target URI must be an absolute URI.", nameof(targetUri));
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceSecret? secret = await db.WorkspaceSecrets
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => (x.WorkspaceId == workspaceId) && (x.SecretName == normalizedName),
                    cancellationToken)
                .ConfigureAwait(false);

            if (secret is null)
            {
                return new(SecretResolutionStatus.NotFound);
            }

            IReadOnlyList<string> allowedPatterns = DeserializeHostPatterns(secret.AllowedHostPatternsJson);

            if (secret.Status == CredentialStatus.Revoked)
            {
                return new(SecretResolutionStatus.Revoked, AllowedHostPatterns: allowedPatterns);
            }

            DateTimeOffset now = this._timeProvider.GetUtcNow();
            if ((secret.Status == CredentialStatus.Expired)
                || (secret.ExpiresAtUtc.HasValue && (secret.ExpiresAtUtc.Value <= now)))
            {
                return new(SecretResolutionStatus.Expired, AllowedHostPatterns: allowedPatterns);
            }

            if (secret.Status == CredentialStatus.KeyRingMismatchRequiresReauth)
            {
                return new(SecretResolutionStatus.KeyRingMismatch, AllowedHostPatterns: allowedPatterns);
            }

            if (!HostPatternMatcher.IsHostAllowed(allowedPatterns, targetUri.IdnHost))
            {
                return new(SecretResolutionStatus.HostNotAllowed, AllowedHostPatterns: allowedPatterns);
            }

            (bool success, string? plaintext, CredentialStatus _) = this._secretProtection.TryUnprotectWorkspaceSecret(
                workspaceId,
                normalizedName,
                secret.EncryptedValue);

            if (!success || (plaintext is null))
            {
                secret.Status = CredentialStatus.KeyRingMismatchRequiresReauth;
                secret.UpdatedAtUtc = now;
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                LogSecretKeyRingMismatch(this._logger, workspaceId, normalizedName);
                return new(SecretResolutionStatus.KeyRingMismatch, AllowedHostPatterns: allowedPatterns);
            }

            this._secretRedactor.RegisterSecret(plaintext);
            return new(SecretResolutionStatus.Resolved, plaintext, allowedPatterns);
        }
    }

    /// <summary>
    /// Normalizes a secret name to trimmed lowercase invariant form and validates it against <c>[a-z0-9_.-]{1,128}</c>.
    /// </summary>
    /// <param name="secretName">The raw secret name.</param>
    /// <returns>The normalized and validated secret name.</returns>
    private static string NormalizeAndValidateSecretName(string secretName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        string normalized = secretName.Trim().ToLowerInvariant();
        return !SecretNameRegex().IsMatch(normalized)
            ? throw new ArgumentException(
                $"Invalid secret name '{secretName}'. Secret names must be 1 to 128 characters matching [a-z0-9_.-]+.",
                nameof(secretName))
            : normalized;
    }

    /// <summary>
    /// Validates, normalizes, and deduplicates a non-empty collection of allowed host patterns.
    /// </summary>
    /// <param name="allowedHostPatterns">The raw host patterns.</param>
    /// <returns>The normalized, deduplicated list of allowed host patterns.</returns>
    private static List<string> NormalizeAndValidateHostPatterns(IReadOnlyCollection<string> allowedHostPatterns)
    {
        ArgumentNullException.ThrowIfNull(allowedHostPatterns);

        List<string> normalized = new(allowedHostPatterns.Count);
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string pattern in allowedHostPatterns)
        {
            string normalizedPattern = HostPatternMatcher.NormalizeAndValidatePattern(pattern);
            if (seen.Add(normalizedPattern))
            {
                normalized.Add(normalizedPattern);
            }
        }

        return normalized.Count == 0
            ? throw new ArgumentException("At least one allowed host pattern is required.", nameof(allowedHostPatterns))
            : normalized;
    }

    /// <summary>
    /// Deserializes the stored JSON array of normalized allowed host patterns.
    /// </summary>
    /// <param name="json">The persisted JSON string.</param>
    /// <returns>The deserialized list of host patterns, or an empty list if invalid.</returns>
    private static List<string> DeserializeHostPatterns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            List<string>? parsed = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
            return parsed ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Matches a valid normalized workspace secret name (<c>[a-z0-9_.-]{1,128}</c>).
    /// </summary>
    /// <returns>The compiled regular expression for secret names.</returns>
    [GeneratedRegex("^[a-z0-9_.-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretNameRegex();

    /// <summary>
    /// Logs a warning when decrypting a workspace secret fails due to a Data Protection key-ring mismatch.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="secretName">The normalized secret name.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to decrypt workspace secret {SecretName} for workspace {WorkspaceId}; marking as KeyRingMismatchRequiresReauth.")]
    private static partial void LogSecretKeyRingMismatch(ILogger logger, Guid workspaceId, string secretName);
}
