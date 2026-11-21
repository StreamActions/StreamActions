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
using StreamActions.Core.Models.Entities;
using System.Collections.Concurrent;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IViewerLoyaltyStore"/> with atomic balance updates and per-viewer locking.
/// </summary>
public sealed class ViewerLoyaltyStore : IViewerLoyaltyStore
{
    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The time provider used for UTC timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Per-viewer semaphores serializing concurrent profile creations and balance updates.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _viewerLocks = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewerLoyaltyStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="timeProvider">The time provider used for UTC timestamps.</param>
    public ViewerLoyaltyStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceViewerProfile?> GetProfileAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        (string normalizedPlatformId, string normalizedUserId) = NormalizeIdentity(platformId, platformUserId);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.WorkspaceViewerProfiles
                .FirstOrDefaultAsync(
                    x => (x.WorkspaceId == workspaceId)
                        && (x.PlatformId == normalizedPlatformId)
                        && (x.PlatformUserId == normalizedUserId),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceViewerProfile> GetOrCreateProfileAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        string login,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        (string normalizedPlatformId, string normalizedUserId) = NormalizeIdentity(platformId, platformUserId);
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(displayName);

        string normalizedLogin = login.Trim();
        string normalizedDisplayName = displayName.Trim();

        SemaphoreSlim gate = this.GetViewerLock(workspaceId, normalizedPlatformId, normalizedUserId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();

            StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                WorkspaceViewerProfile? existing = await db.WorkspaceViewerProfiles
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => (x.WorkspaceId == workspaceId)
                            && (x.PlatformId == normalizedPlatformId)
                            && (x.PlatformUserId == normalizedUserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    WorkspaceViewerProfile created = new()
                    {
                        Id = Guid.CreateVersion7(),
                        WorkspaceId = workspaceId,
                        PlatformId = normalizedPlatformId,
                        PlatformUserId = normalizedUserId,
                        LastKnownLogin = normalizedLogin,
                        LastKnownDisplayName = normalizedDisplayName,
                        Points = 0L,
                        WatchTimeSeconds = 0L,
                        ChatXp = 0L,
                        UpdatedAtUtc = now,
                    };

                    _ = db.WorkspaceViewerProfiles.Add(created);
                    _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return created;
                }

                existing.LastKnownLogin = normalizedLogin;
                existing.LastKnownDisplayName = normalizedDisplayName;
                existing.UpdatedAtUtc = now;

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceViewerProfile>> GetTopByPointsAsync(
        Guid workspaceId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0)
        {
            return [];
        }

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<WorkspaceViewerProfile> profiles = await db.WorkspaceViewerProfiles
                .Where(x => x.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return
            [
                .. profiles
                    .OrderByDescending(static x => x.Points)
                    .ThenBy(static x => x.UpdatedAtUtc)
                    .ThenBy(static x => x.PlatformId, StringComparer.Ordinal)
                    .ThenBy(static x => x.PlatformUserId, StringComparer.Ordinal)
                    .Take(count),
            ];
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceViewerProfile> AdjustPointsAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        long delta,
        CancellationToken cancellationToken = default)
    {
        (string normalizedPlatformId, string normalizedUserId) = NormalizeIdentity(platformId, platformUserId);
        SemaphoreSlim gate = this.GetViewerLock(workspaceId, normalizedPlatformId, normalizedUserId);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();

            StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                WorkspaceViewerProfile? existing = await db.WorkspaceViewerProfiles
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => (x.WorkspaceId == workspaceId)
                            && (x.PlatformId == normalizedPlatformId)
                            && (x.PlatformUserId == normalizedUserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    WorkspaceViewerProfile created = new()
                    {
                        Id = Guid.CreateVersion7(),
                        WorkspaceId = workspaceId,
                        PlatformId = normalizedPlatformId,
                        PlatformUserId = normalizedUserId,
                        LastKnownLogin = string.Empty,
                        LastKnownDisplayName = string.Empty,
                        Points = delta,
                        WatchTimeSeconds = 0L,
                        ChatXp = 0L,
                        UpdatedAtUtc = now,
                    };

                    _ = db.WorkspaceViewerProfiles.Add(created);
                    _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return created;
                }

                existing.Points += delta;
                existing.UpdatedAtUtc = now;

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TrySpendPointsAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        long cost,
        CancellationToken cancellationToken = default)
    {
        (string normalizedPlatformId, string normalizedUserId) = NormalizeIdentity(platformId, platformUserId);
        ArgumentOutOfRangeException.ThrowIfNegative(cost);

        if (cost == 0L)
        {
            return true;
        }

        SemaphoreSlim gate = this.GetViewerLock(workspaceId, normalizedPlatformId, normalizedUserId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();

            StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                int rowsAffected = await db.WorkspaceViewerProfiles
                    .Where(x => (x.WorkspaceId == workspaceId)
                        && (x.PlatformId == normalizedPlatformId)
                        && (x.PlatformUserId == normalizedUserId)
                        && (x.Points >= cost))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(static x => x.Points, x => x.Points - cost)
                            .SetProperty(static x => x.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                return rowsAffected > 0;
            }
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask AddWatchTimeAndXpAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        long watchTimeSecondsDelta,
        long chatXpDelta,
        CancellationToken cancellationToken = default)
    {
        (string normalizedPlatformId, string normalizedUserId) = NormalizeIdentity(platformId, platformUserId);
        ArgumentOutOfRangeException.ThrowIfNegative(watchTimeSecondsDelta);
        ArgumentOutOfRangeException.ThrowIfNegative(chatXpDelta);

        SemaphoreSlim gate = this.GetViewerLock(workspaceId, normalizedPlatformId, normalizedUserId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();

            StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                int updated = await db.WorkspaceViewerProfiles
                    .Where(x => (x.WorkspaceId == workspaceId)
                        && (x.PlatformId == normalizedPlatformId)
                        && (x.PlatformUserId == normalizedUserId))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(static x => x.WatchTimeSeconds, x => x.WatchTimeSeconds + watchTimeSecondsDelta)
                            .SetProperty(static x => x.ChatXp, x => x.ChatXp + chatXpDelta)
                            .SetProperty(static x => x.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updated == 0)
                {
                    _ = db.WorkspaceViewerProfiles.Add(new WorkspaceViewerProfile
                    {
                        Id = Guid.CreateVersion7(),
                        WorkspaceId = workspaceId,
                        PlatformId = normalizedPlatformId,
                        PlatformUserId = normalizedUserId,
                        LastKnownLogin = string.Empty,
                        LastKnownDisplayName = string.Empty,
                        Points = 0L,
                        WatchTimeSeconds = watchTimeSecondsDelta,
                        ChatXp = chatXpDelta,
                        UpdatedAtUtc = now,
                    });

                    _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <summary>
    /// Validates and normalizes the platform identifier and platform user identifier.
    /// </summary>
    /// <param name="platformId">The raw platform identifier.</param>
    /// <param name="platformUserId">The raw platform user identifier.</param>
    /// <returns>A tuple containing the normalized platform identifier and user identifier.</returns>
    private static (string PlatformId, string PlatformUserId) NormalizeIdentity(string platformId, string platformUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);

        return (platformId.Trim().ToLowerInvariant(), platformUserId.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Gets or creates the per-viewer semaphore serializing concurrent mutations for a specific viewer key.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="normalizedPlatformId">The normalized platform identifier.</param>
    /// <param name="normalizedUserId">The normalized platform user identifier.</param>
    /// <returns>The <see cref="SemaphoreSlim"/> protecting the viewer key.</returns>
    private SemaphoreSlim GetViewerLock(Guid workspaceId, string normalizedPlatformId, string normalizedUserId)
    {
        string lockKey = $"{workspaceId:N}:{normalizedPlatformId}:{normalizedUserId}";
        return this._viewerLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
    }
}
