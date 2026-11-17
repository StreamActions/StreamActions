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
using StreamActions.Core.Models.Metadata;
using StreamActions.Database.Security;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IWorkspaceStore"/>.
/// </summary>
public sealed class WorkspaceStore : IWorkspaceStore
{
    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The secret protection service used to encrypt and decrypt workspace custom OAuth client secrets.
    /// </summary>
    private readonly SecretProtectionService _secretProtection;

    /// <summary>
    /// The permission store used to seed built-in workspace permission groups on creation.
    /// </summary>
    private readonly IPermissionStore _permissionStore;

    /// <summary>
    /// The time provider used for UTC timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="secretProtection">The secret protection service.</param>
    /// <param name="permissionStore">The permission store.</param>
    /// <param name="timeProvider">The time provider.</param>
    public WorkspaceStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        SecretProtectionService secretProtection,
        IPermissionStore permissionStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(secretProtection);
        ArgumentNullException.ThrowIfNull(permissionStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this._dbContextFactory = dbContextFactory;
        this._secretProtection = secretProtection;
        this._permissionStore = permissionStore;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.Workspaces
                .FirstOrDefaultAsync(x => x.Id == workspaceId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<Workspace> workspaces = await db.Workspaces
                .Where(x => x.OwnerUserId == ownerUserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return [.. workspaces.OrderBy(static x => x.CreatedAtUtc).ThenBy(static x => x.Id)];
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<Workspace> workspaces = await db.Workspaces
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return [.. workspaces.OrderBy(static x => x.CreatedAtUtc).ThenBy(static x => x.Id)];
        }
    }

    /// <inheritdoc />
    public async ValueTask<Workspace> CreateWorkspaceAsync(
        Workspace workspace,
        WorkspacePlatform? primaryPlatform = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace.PrimaryPlatformId);

        if (workspace.OwnerUserId == Guid.Empty)
        {
            throw new ArgumentException("Workspace owner user identifier must not be empty.", nameof(workspace));
        }

        if (primaryPlatform is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(primaryPlatform.PlatformId);
            ArgumentException.ThrowIfNullOrWhiteSpace(primaryPlatform.PlatformChannelId);
        }

        Guid workspaceId = workspace.Id == Guid.Empty ? Guid.CreateVersion7() : workspace.Id;
        Workspace persisted = new()
        {
            Id = workspaceId,
            OwnerUserId = workspace.OwnerUserId,
            Name = workspace.Name.Trim(),
            PrimaryPlatformId = NormalizePlatformId(workspace.PrimaryPlatformId),
            AllowCustomBotAccountOverride = workspace.AllowCustomBotAccountOverride,
            MfaPasskeyAvailability = workspace.MfaPasskeyAvailability,
            MfaTotpAvailability = workspace.MfaTotpAvailability,
            LoginMfaRequirement = workspace.LoginMfaRequirement,
            StepUpSudoPolicy = workspace.StepUpSudoPolicy,
            CreatedAtUtc = workspace.CreatedAtUtc == default ? this._timeProvider.GetUtcNow() : workspace.CreatedAtUtc,
        };

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            _ = db.Workspaces.Add(persisted);

            if (primaryPlatform is not null)
            {
                string normalizedPlatformId = NormalizePlatformId(primaryPlatform.PlatformId);
                _ = db.WorkspacePlatforms.Add(new WorkspacePlatform
                {
                    Id = primaryPlatform.Id == Guid.Empty ? Guid.CreateVersion7() : primaryPlatform.Id,
                    WorkspaceId = workspaceId,
                    PlatformId = normalizedPlatformId,
                    Role = primaryPlatform.Role,
                    PlatformChannelId = primaryPlatform.PlatformChannelId.Trim(),
                    CustomClientId = NormalizeOptionalClientId(primaryPlatform.CustomClientId),
                    EncryptedCustomClientSecret = null,
                    Metadata = CloneMetadata(primaryPlatform.Metadata),
                });
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._permissionStore.SeedBuiltInWorkspaceGroupsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return persisted;
    }

    /// <inheritdoc />
    public async ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace.PrimaryPlatformId);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            Workspace? existing = await db.Workspaces
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == workspace.Id, cancellationToken)
                .ConfigureAwait(false) ?? throw new KeyNotFoundException($"Workspace '{workspace.Id}' was not found.");

            existing.Name = workspace.Name.Trim();
            existing.PrimaryPlatformId = NormalizePlatformId(workspace.PrimaryPlatformId);
            existing.AllowCustomBotAccountOverride = workspace.AllowCustomBotAccountOverride;
            existing.MfaPasskeyAvailability = workspace.MfaPasskeyAvailability;
            existing.MfaTotpAvailability = workspace.MfaTotpAvailability;
            existing.LoginMfaRequirement = workspace.LoginMfaRequirement;
            existing.StepUpSudoPolicy = workspace.StepUpSudoPolicy;

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            int deleted = await db.Workspaces
                .Where(x => x.Id == workspaceId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return deleted > 0;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.WorkspacePlatforms
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.PlatformId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkspacePlatform?> GetPlatformAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.WorkspacePlatforms
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId && x.PlatformId == normalizedPlatformId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkspacePlatform> UpsertPlatformAsync(
        WorkspacePlatform platform,
        string? plaintextCustomClientSecret = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform.PlatformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform.PlatformChannelId);

        if (platform.WorkspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace identifier must not be empty.", nameof(platform));
        }

        string normalizedPlatformId = NormalizePlatformId(platform.PlatformId);
        string? normalizedClientId = NormalizeOptionalClientId(platform.CustomClientId);
        string? encryptedSecret = plaintextCustomClientSecret is null
            ? null
            : string.IsNullOrWhiteSpace(plaintextCustomClientSecret)
                ? string.Empty
                : this._secretProtection.ProtectWorkspaceOAuth(platform.WorkspaceId, normalizedPlatformId, plaintextCustomClientSecret.Trim());

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspacePlatform? existing = await db.WorkspacePlatforms
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == platform.WorkspaceId && x.PlatformId == normalizedPlatformId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Role = platform.Role;
                existing.PlatformChannelId = platform.PlatformChannelId.Trim();
                existing.CustomClientId = normalizedClientId;
                existing.Metadata = CloneMetadata(platform.Metadata);

                if (encryptedSecret is not null)
                {
                    existing.EncryptedCustomClientSecret = encryptedSecret.Length == 0 ? null : encryptedSecret;
                }

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }

            WorkspacePlatform persisted = new()
            {
                Id = platform.Id == Guid.Empty ? Guid.CreateVersion7() : platform.Id,
                WorkspaceId = platform.WorkspaceId,
                PlatformId = normalizedPlatformId,
                Role = platform.Role,
                PlatformChannelId = platform.PlatformChannelId.Trim(),
                CustomClientId = normalizedClientId,
                EncryptedCustomClientSecret = encryptedSecret is { Length: > 0 } ? encryptedSecret : null,
                Metadata = CloneMetadata(platform.Metadata),
            };

            _ = db.WorkspacePlatforms.Add(persisted);
            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return persisted;
        }
    }

    /// <inheritdoc />
    public async ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(
        Guid workspaceId,
        string platformId,
        string? clientId,
        string? clientSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspacePlatform? existing = await db.WorkspacePlatforms
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId && x.PlatformId == normalizedPlatformId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Platform '{normalizedPlatformId}' is not attached to workspace '{workspaceId}'.");

            if (string.IsNullOrWhiteSpace(clientId))
            {
                existing.CustomClientId = null;
                existing.EncryptedCustomClientSecret = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(existing.EncryptedCustomClientSecret))
                {
                    throw new ArgumentException("A custom OAuth client secret is required when setting custom credentials.", nameof(clientSecret));
                }

                existing.CustomClientId = clientId.Trim();
                if (!string.IsNullOrWhiteSpace(clientSecret))
                {
                    existing.EncryptedCustomClientSecret = this._secretProtection.ProtectWorkspaceOAuth(
                        workspaceId,
                        normalizedPlatformId,
                        clientSecret.Trim());
                }
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
    }

    /// <inheritdoc />
    public async ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        WorkspacePlatform? platform = await this.GetPlatformAsync(workspaceId, platformId, cancellationToken).ConfigureAwait(false);
        if (platform is null
            || string.IsNullOrWhiteSpace(platform.CustomClientId)
            || string.IsNullOrWhiteSpace(platform.EncryptedCustomClientSecret))
        {
            return null;
        }

        (bool success, string? plaintextSecret, CredentialStatus _) = this._secretProtection.TryUnprotectWorkspaceOAuth(
            workspaceId,
            platform.PlatformId,
            platform.EncryptedCustomClientSecret);

        return success && !string.IsNullOrWhiteSpace(plaintextSecret)
            ? new PlatformOAuthAppCredentials(platform.CustomClientId, plaintextSecret)
            : null;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemovePlatformAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = NormalizePlatformId(platformId);
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            int deleted = await db.WorkspacePlatforms
                .Where(x => x.WorkspaceId == workspaceId && x.PlatformId == normalizedPlatformId)
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

    /// <summary>
    /// Normalizes an optional custom client identifier by trimming whitespace or returning <see langword="null"/>.
    /// </summary>
    /// <param name="clientId">The raw client identifier.</param>
    /// <returns>The trimmed client identifier, or <see langword="null"/>.</returns>
    private static string? NormalizeOptionalClientId(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();

    /// <summary>
    /// Creates a detached copy of <see cref="PlatformMetadata"/> to avoid sharing mutable dictionaries across calls.
    /// </summary>
    /// <param name="metadata">The source metadata.</param>
    /// <returns>A cloned <see cref="PlatformMetadata"/> instance.</returns>
    private static PlatformMetadata CloneMetadata(PlatformMetadata? metadata) =>
        metadata is null
            ? new()
            : new()
            {
                AutoJoinChat = metadata.AutoJoinChat,
                UseBroadcasterAsBot = metadata.UseBroadcasterAsBot,
                BotUsername = metadata.BotUsername,
                ExtraSettings = new Dictionary<string, string>(metadata.ExtraSettings, StringComparer.Ordinal),
            };
}
