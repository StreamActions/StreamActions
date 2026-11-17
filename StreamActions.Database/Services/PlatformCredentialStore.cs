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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Security;
using System.Text.Json;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IPlatformCredentialStore"/>.
/// </summary>
public sealed class PlatformCredentialStore : IPlatformCredentialStore
{
    /// <summary>
    /// JSON serializer options used for serializing and deserializing <see cref="PlatformTokenPayload"/>.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The secret protection service used to encrypt and decrypt token payloads.
    /// </summary>
    private readonly SecretProtectionService _secretProtection;

    /// <summary>
    /// The time provider used for UTC timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformCredentialStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="secretProtection">The secret protection service.</param>
    /// <param name="timeProvider">The time provider.</param>
    public PlatformCredentialStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        SecretProtectionService secretProtection,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(secretProtection);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this._dbContextFactory = dbContextFactory;
        this._secretProtection = secretProtection;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async ValueTask<PlatformCredential?> GetCredentialRecordAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PlatformCredentials
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId
                        && x.PlatformId == normalizedPlatformId
                        && x.TokenRole == tokenRole,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PlatformCredential>> GetCredentialsForWorkspaceAsync(
        Guid? workspaceId,
        CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PlatformCredentials
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.PlatformId)
                .ThenBy(static x => x.TokenRole)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PlatformCredential>> GetAllCredentialsAsync(
        CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PlatformCredentials
                .OrderBy(static x => x.WorkspaceId)
                .ThenBy(static x => x.PlatformId)
                .ThenBy(static x => x.TokenRole)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<(bool Found, CredentialStatus Status, PlatformTokenPayload? Payload)> TryGetTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        Guid protectionWorkspaceId = workspaceId ?? Guid.Empty;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PlatformCredential? existing = await db.PlatformCredentials
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId
                        && x.PlatformId == normalizedPlatformId
                        && x.TokenRole == tokenRole,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return (false, CredentialStatus.Revoked, null);
            }

            if (existing.Status is CredentialStatus.Revoked or CredentialStatus.KeyRingMismatchRequiresReauth)
            {
                return (true, existing.Status, null);
            }

            (bool success, string? plaintext, CredentialStatus _) = this._secretProtection.TryUnprotectWorkspaceToken(
                protectionWorkspaceId,
                normalizedPlatformId,
                tokenRole,
                existing.EncryptedTokenJson);

            if (!success || string.IsNullOrWhiteSpace(plaintext))
            {
                existing.Status = CredentialStatus.KeyRingMismatchRequiresReauth;
                existing.UpdatedAtUtc = this._timeProvider.GetUtcNow();
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return (true, CredentialStatus.KeyRingMismatchRequiresReauth, null);
            }

            PlatformTokenPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<PlatformTokenPayload>(plaintext, _jsonOptions);
            }
            catch (JsonException)
            {
                existing.Status = CredentialStatus.KeyRingMismatchRequiresReauth;
                existing.UpdatedAtUtc = this._timeProvider.GetUtcNow();
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return (true, CredentialStatus.KeyRingMismatchRequiresReauth, null);
            }

            if (payload is null)
            {
                existing.Status = CredentialStatus.KeyRingMismatchRequiresReauth;
                existing.UpdatedAtUtc = this._timeProvider.GetUtcNow();
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return (true, CredentialStatus.KeyRingMismatchRequiresReauth, null);
            }

            return (true, existing.Status, payload);
        }
    }

    /// <inheritdoc />
    public async ValueTask<PlatformCredential> SaveTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        PlatformTokenPayload payload,
        CredentialStatus status = CredentialStatus.Active,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.AccessToken);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        Guid protectionWorkspaceId = workspaceId ?? Guid.Empty;
        string json = JsonSerializer.Serialize(payload, _jsonOptions);
        string encryptedJson = this._secretProtection.ProtectWorkspaceToken(
            protectionWorkspaceId,
            normalizedPlatformId,
            tokenRole,
            json);
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PlatformCredential? existing = await db.PlatformCredentials
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId
                        && x.PlatformId == normalizedPlatformId
                        && x.TokenRole == tokenRole,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.EncryptedTokenJson = encryptedJson;
                existing.Status = status;
                existing.ExpiresAtUtc = payload.ExpiresAtUtc;
                existing.UpdatedAtUtc = now;

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }

            PlatformCredential persisted = new()
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                PlatformId = normalizedPlatformId,
                TokenRole = tokenRole,
                EncryptedTokenJson = encryptedJson,
                Status = status,
                ExpiresAtUtc = payload.ExpiresAtUtc,
                UpdatedAtUtc = now,
            };

            _ = db.PlatformCredentials.Add(persisted);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return persisted;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> UpdateStatusAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CredentialStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PlatformCredential? existing = await db.PlatformCredentials
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId
                        && x.PlatformId == normalizedPlatformId
                        && x.TokenRole == tokenRole,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return false;
            }

            existing.Status = status;
            existing.UpdatedAtUtc = this._timeProvider.GetUtcNow();
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteTokenAsync(
        Guid? workspaceId,
        string platformId,
        TokenRole tokenRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            int deleted = await db.PlatformCredentials
                .Where(x => x.WorkspaceId == workspaceId
                    && x.PlatformId == normalizedPlatformId
                    && x.TokenRole == tokenRole)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return deleted > 0;
        }
    }

    /// <summary>
    /// Normalizes a platform identifier to trimmed lowercase invariant form.
    /// </summary>
    /// <param name="platformId">The raw platform identifier.</param>
    /// <returns>The normalized platform identifier.</returns>
    private static string NormalizePlatformId(string platformId) => platformId.Trim().ToLowerInvariant();
}
