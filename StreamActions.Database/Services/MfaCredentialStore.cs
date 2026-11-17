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
/// Entity Framework Core implementation of <see cref="IMfaCredentialStore"/>.
/// </summary>
public sealed class MfaCredentialStore : IMfaCredentialStore
{
    /// <summary>
    /// JSON serializer options used for serializing and deserializing MFA credential payloads and recovery code hashes.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The secret protection service used to encrypt and decrypt MFA payloads.
    /// </summary>
    private readonly SecretProtectionService _secretProtection;

    /// <summary>
    /// The TOTP service used to verify one-time recovery codes.
    /// </summary>
    private readonly ITotpService _totpService;

    /// <summary>
    /// The time provider used for UTC timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfaCredentialStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="secretProtection">The secret protection service.</param>
    /// <param name="totpService">The TOTP service.</param>
    /// <param name="timeProvider">The time provider.</param>
    public MfaCredentialStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        SecretProtectionService secretProtection,
        ITotpService totpService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(secretProtection);
        ArgumentNullException.ThrowIfNull(totpService);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this._dbContextFactory = dbContextFactory;
        this._secretProtection = secretProtection;
        this._totpService = totpService;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasAnyCredentialsAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            return false;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PanelUserMfaCredentials
                .AnyAsync(x => x.PanelUserId == panelUserId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<MfaCredentialSummary>> GetSummariesAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            return [];
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PanelUserMfaCredential> rows = await db.PanelUserMfaCredentials
                .Where(x => x.PanelUserId == panelUserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<MfaCredentialSummary> summaries = new(rows.Count);
            foreach (PanelUserMfaCredential row in rows.OrderBy(static x => x.CreatedAtUtc).ThenBy(static x => x.Id))
            {
                if (row.Method == MfaMethod.Totp)
                {
                    TotpSecretPayload? totpPayload = this.TryUnprotectPayload<TotpSecretPayload>(
                        panelUserId,
                        MfaMethod.Totp,
                        row.CredentialDataEncrypted);

                    string displayName = string.IsNullOrWhiteSpace(totpPayload?.Label)
                        ? "Authenticator App"
                        : totpPayload.Label;
                    int remainingCodes = ParseRecoveryCodeHashes(row.RecoveryCodesHashesJson).Count;
                    summaries.Add(new MfaCredentialSummary(row.Id, MfaMethod.Totp, displayName, row.CreatedAtUtc, remainingCodes));
                }
                else if (row.Method == MfaMethod.Passkey)
                {
                    PasskeyCredentialPayload? passkeyPayload = this.TryUnprotectPayload<PasskeyCredentialPayload>(
                        panelUserId,
                        MfaMethod.Passkey,
                        row.CredentialDataEncrypted);

                    string displayName = string.IsNullOrWhiteSpace(passkeyPayload?.Name)
                        ? "Passkey"
                        : passkeyPayload.Name;
                    summaries.Add(new MfaCredentialSummary(row.Id, MfaMethod.Passkey, displayName, row.CreatedAtUtc, 0));
                }
            }

            return summaries;
        }
    }

    /// <inheritdoc />
    public async ValueTask<Guid> SaveTotpAsync(
        Guid panelUserId,
        TotpSecretPayload payload,
        IReadOnlyList<string> recoveryCodeHashes,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            throw new ArgumentException("Panel user identifier must not be empty.", nameof(panelUserId));
        }

        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.SecretBase32);
        ArgumentNullException.ThrowIfNull(recoveryCodeHashes);

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        TotpSecretPayload normalizedPayload = payload.EnrolledAtUtc == default
            ? payload with { EnrolledAtUtc = now }
            : payload;

        string payloadJson = JsonSerializer.Serialize(normalizedPayload, _jsonOptions);
        string encryptedPayload = this._secretProtection.ProtectUserMfa(panelUserId, MfaMethod.Totp, payloadJson);
        List<string> hashesList = [.. recoveryCodeHashes];
        string hashesJson = JsonSerializer.Serialize(hashesList, _jsonOptions);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PanelUserMfaCredential> existingTotpRows = await db.PanelUserMfaCredentials
                .AsTracking()
                .Where(x => x.PanelUserId == panelUserId && x.Method == MfaMethod.Totp)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existingTotpRows.Count > 0)
            {
                db.PanelUserMfaCredentials.RemoveRange(existingTotpRows);
            }

            PanelUserMfaCredential persisted = new()
            {
                Id = Guid.CreateVersion7(),
                PanelUserId = panelUserId,
                Method = MfaMethod.Totp,
                CredentialDataEncrypted = encryptedPayload,
                RecoveryCodesHashesJson = hashesJson,
                CreatedAtUtc = normalizedPayload.EnrolledAtUtc,
            };

            _ = db.PanelUserMfaCredentials.Add(persisted);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return persisted.Id;
        }
    }

    /// <inheritdoc />
    public async ValueTask<TotpSecretPayload?> GetTotpAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            return null;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PanelUserMfaCredential? row = await db.PanelUserMfaCredentials
                .FirstOrDefaultAsync(x => x.PanelUserId == panelUserId && x.Method == MfaMethod.Totp, cancellationToken)
                .ConfigureAwait(false);

            return row is null
                ? null
                : this.TryUnprotectPayload<TotpSecretPayload>(panelUserId, MfaMethod.Totp, row.CredentialDataEncrypted);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> VerifyAndConsumeRecoveryCodeAsync(
        Guid panelUserId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PanelUserMfaCredential? row = await db.PanelUserMfaCredentials
                .AsTracking()
                .FirstOrDefaultAsync(x => x.PanelUserId == panelUserId && x.Method == MfaMethod.Totp, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                return false;
            }

            List<string> hashes = ParseRecoveryCodeHashes(row.RecoveryCodesHashesJson);
            int matchedIndex = -1;
            for (int i = 0; i < hashes.Count; i++)
            {
                if (this._totpService.VerifyRecoveryCode(code, hashes[i]) && matchedIndex < 0)
                {
                    matchedIndex = i;
                }
            }

            if (matchedIndex < 0)
            {
                return false;
            }

            hashes.RemoveAt(matchedIndex);
            row.RecoveryCodesHashesJson = JsonSerializer.Serialize(hashes, _jsonOptions);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> GetRemainingRecoveryCodesCountAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            return 0;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PanelUserMfaCredential? row = await db.PanelUserMfaCredentials
                .FirstOrDefaultAsync(x => x.PanelUserId == panelUserId && x.Method == MfaMethod.Totp, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? 0 : ParseRecoveryCodeHashes(row.RecoveryCodesHashesJson).Count;
        }
    }

    /// <inheritdoc />
    public async ValueTask UpsertPasskeyAsync(
        Guid panelUserId,
        PasskeyCredentialPayload passkey,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            throw new ArgumentException("Panel user identifier must not be empty.", nameof(panelUserId));
        }

        ArgumentNullException.ThrowIfNull(passkey);
        if (passkey.CredentialId is null || passkey.CredentialId.Length == 0)
        {
            throw new ArgumentException("Passkey credential identifier must not be empty.", nameof(passkey));
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        PasskeyCredentialPayload normalizedPasskey = passkey.CreatedAt == default
            ? passkey with { CreatedAt = now }
            : passkey;

        string payloadJson = JsonSerializer.Serialize(normalizedPasskey, _jsonOptions);
        string encryptedPayload = this._secretProtection.ProtectUserMfa(panelUserId, MfaMethod.Passkey, payloadJson);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PanelUserMfaCredential> existingRows = await db.PanelUserMfaCredentials
                .AsTracking()
                .Where(x => x.PanelUserId == panelUserId && x.Method == MfaMethod.Passkey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (PanelUserMfaCredential existingRow in existingRows)
            {
                PasskeyCredentialPayload? existingPayload = this.TryUnprotectPayload<PasskeyCredentialPayload>(
                    panelUserId,
                    MfaMethod.Passkey,
                    existingRow.CredentialDataEncrypted);

                if (existingPayload is not null && existingPayload.CredentialId.SequenceEqual(normalizedPasskey.CredentialId))
                {
                    existingRow.CredentialDataEncrypted = encryptedPayload;
                    _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            PanelUserMfaCredential persisted = new()
            {
                Id = Guid.CreateVersion7(),
                PanelUserId = panelUserId,
                Method = MfaMethod.Passkey,
                CredentialDataEncrypted = encryptedPayload,
                RecoveryCodesHashesJson = null,
                CreatedAtUtc = normalizedPasskey.CreatedAt,
            };

            _ = db.PanelUserMfaCredentials.Add(persisted);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PasskeyCredentialPayload>> GetPasskeysAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            return [];
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PanelUserMfaCredential> rows = await db.PanelUserMfaCredentials
                .Where(x => x.PanelUserId == panelUserId && x.Method == MfaMethod.Passkey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<PasskeyCredentialPayload> passkeys = [];
            foreach (PanelUserMfaCredential row in rows.OrderBy(static x => x.CreatedAtUtc).ThenBy(static x => x.Id))
            {
                PasskeyCredentialPayload? payload = this.TryUnprotectPayload<PasskeyCredentialPayload>(
                    panelUserId,
                    MfaMethod.Passkey,
                    row.CredentialDataEncrypted);

                if (payload is not null)
                {
                    passkeys.Add(payload);
                }
            }

            return passkeys;
        }
    }

    /// <inheritdoc />
    public async ValueTask<PasskeyCredentialPayload?> FindPasskeyAsync(
        Guid panelUserId,
        byte[] credentialId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        if (panelUserId == Guid.Empty || credentialId.Length == 0)
        {
            return null;
        }

        IReadOnlyList<PasskeyCredentialPayload> passkeys = await this.GetPasskeysAsync(panelUserId, cancellationToken).ConfigureAwait(false);
        foreach (PasskeyCredentialPayload passkey in passkeys)
        {
            if (passkey.CredentialId.SequenceEqual(credentialId))
            {
                return passkey;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<(Guid PanelUserId, PasskeyCredentialPayload Passkey)?> FindPasskeyByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        if (credentialId.Length == 0)
        {
            return null;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PanelUserMfaCredential> rows = await db.PanelUserMfaCredentials
                .Where(x => x.Method == MfaMethod.Passkey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (PanelUserMfaCredential row in rows)
            {
                PasskeyCredentialPayload? payload = this.TryUnprotectPayload<PasskeyCredentialPayload>(
                    row.PanelUserId,
                    MfaMethod.Passkey,
                    row.CredentialDataEncrypted);

                if (payload is not null && payload.CredentialId.SequenceEqual(credentialId))
                {
                    return (row.PanelUserId, payload);
                }
            }

            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemovePasskeyAsync(
        Guid panelUserId,
        byte[] credentialId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        if (panelUserId == Guid.Empty || credentialId.Length == 0)
        {
            return false;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PanelUserMfaCredential> rows = await db.PanelUserMfaCredentials
                .AsTracking()
                .Where(x => x.PanelUserId == panelUserId && x.Method == MfaMethod.Passkey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (PanelUserMfaCredential row in rows)
            {
                PasskeyCredentialPayload? payload = this.TryUnprotectPayload<PasskeyCredentialPayload>(
                    panelUserId,
                    MfaMethod.Passkey,
                    row.CredentialDataEncrypted);

                if (payload is not null && payload.CredentialId.SequenceEqual(credentialId))
                {
                    _ = db.PanelUserMfaCredentials.Remove(row);
                    _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return true;
                }
            }

            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteCredentialAsync(
        Guid panelUserId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty || credentialId == Guid.Empty)
        {
            return false;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            int deleted = await db.PanelUserMfaCredentials
                .Where(x => x.PanelUserId == panelUserId && x.Id == credentialId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return deleted > 0;
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ClearAllForUserAsync(
        Guid panelUserId,
        CancellationToken cancellationToken = default)
    {
        if (panelUserId == Guid.Empty)
        {
            return 0;
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PanelUserMfaCredentials
                .Where(x => x.PanelUserId == panelUserId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Attempts to decrypt and deserialize an MFA credential payload, returning <see langword="null"/> if decryption or JSON parsing fails.
    /// </summary>
    /// <typeparam name="TPayload">The target payload type.</typeparam>
    /// <param name="panelUserId">The owning panel user identifier.</param>
    /// <param name="method">The MFA method.</param>
    /// <param name="encryptedPayload">The Data Protection protected payload string.</param>
    /// <returns>The deserialized payload, or <see langword="null"/> if undecryptable or malformed.</returns>
    private TPayload? TryUnprotectPayload<TPayload>(
        Guid panelUserId,
        MfaMethod method,
        string encryptedPayload)
        where TPayload : class
    {
        if (string.IsNullOrWhiteSpace(encryptedPayload))
        {
            return null;
        }

        (bool success, string? plaintext, CredentialStatus _) = this._secretProtection.TryUnprotectUserMfa(
            panelUserId,
            method,
            encryptedPayload);

        if (!success || string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TPayload>(plaintext, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a JSON-serialized list of SHA-256 recovery code hashes, returning an empty list if absent or malformed.
    /// </summary>
    /// <param name="json">The serialized recovery code hashes JSON.</param>
    /// <returns>A mutable list of recovery code hash strings.</returns>
    private static List<string> ParseRecoveryCodeHashes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
