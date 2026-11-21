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
using StreamActions.Core.Counters;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using System.Collections.Concurrent;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="ICounterStore"/> with per-counter locking and event bus notifications.
/// </summary>
public sealed class CounterStore : ICounterStore
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
    /// The event bus used to publish counter change events.
    /// </summary>
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Per-counter semaphores serializing atomic read-modify-write and delete operations.
    /// </summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _counterLocks = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="timeProvider">The time provider used for UTC timestamps.</param>
    /// <param name="eventBus">The event bus used to publish counter change events.</param>
    public CounterStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        TimeProvider timeProvider,
        IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(eventBus);

        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
        this._eventBus = eventBus;
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceCounter?> GetAsync(
        Guid workspaceId,
        string counterName,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default)
    {
        (string normalizedName, string normalizedCategory) = NormalizeInputs(counterName, scopeMode, gameCategory);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.WorkspaceCounters
                .FirstOrDefaultAsync(
                    x => (x.WorkspaceId == workspaceId)
                        && (x.CounterName == normalizedName)
                        && (x.ScopeMode == scopeMode)
                        && (x.GameCategory == normalizedCategory),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceCounter>> GetAllAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.WorkspaceCounters
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.CounterName)
                .ThenBy(static x => x.ScopeMode)
                .ThenBy(static x => x.GameCategory)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceCounter> AdjustAsync(
        Guid workspaceId,
        string counterName,
        long delta,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default)
    {
        (string normalizedName, string normalizedCategory) = NormalizeInputs(counterName, scopeMode, gameCategory);
        SemaphoreSlim gate = this.GetCounterLock(workspaceId, normalizedName, scopeMode, normalizedCategory);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            long? previousValue;
            WorkspaceCounter counter;

            StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                WorkspaceCounter? existing = await db.WorkspaceCounters
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => (x.WorkspaceId == workspaceId)
                            && (x.CounterName == normalizedName)
                            && (x.ScopeMode == scopeMode)
                            && (x.GameCategory == normalizedCategory),
                        cancellationToken)
                    .ConfigureAwait(false);

                previousValue = existing?.CurrentValue;

                if (existing is null)
                {
                    counter = new WorkspaceCounter
                    {
                        Id = Guid.CreateVersion7(),
                        WorkspaceId = workspaceId,
                        CounterName = normalizedName,
                        ScopeMode = scopeMode,
                        GameCategory = normalizedCategory,
                        CurrentValue = delta,
                        UpdatedAtUtc = now,
                    };

                    _ = db.WorkspaceCounters.Add(counter);
                }
                else
                {
                    existing.CurrentValue += delta;
                    existing.UpdatedAtUtc = now;
                    counter = existing;
                }

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await this._eventBus.PublishAsync(
                new CounterUpdatedEvent(
                    workspaceId,
                    normalizedName,
                    scopeMode,
                    normalizedCategory,
                    previousValue,
                    counter.CurrentValue,
                    now),
                cancellationToken).ConfigureAwait(false);

            return counter;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceCounter> SetAsync(
        Guid workspaceId,
        string counterName,
        long value,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default)
    {
        (string normalizedName, string normalizedCategory) = NormalizeInputs(counterName, scopeMode, gameCategory);
        SemaphoreSlim gate = this.GetCounterLock(workspaceId, normalizedName, scopeMode, normalizedCategory);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            long? previousValue;
            WorkspaceCounter counter;

            StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                WorkspaceCounter? existing = await db.WorkspaceCounters
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => (x.WorkspaceId == workspaceId)
                            && (x.CounterName == normalizedName)
                            && (x.ScopeMode == scopeMode)
                            && (x.GameCategory == normalizedCategory),
                        cancellationToken)
                    .ConfigureAwait(false);

                previousValue = existing?.CurrentValue;

                if (existing is null)
                {
                    counter = new WorkspaceCounter
                    {
                        Id = Guid.CreateVersion7(),
                        WorkspaceId = workspaceId,
                        CounterName = normalizedName,
                        ScopeMode = scopeMode,
                        GameCategory = normalizedCategory,
                        CurrentValue = value,
                        UpdatedAtUtc = now,
                    };

                    _ = db.WorkspaceCounters.Add(counter);
                }
                else
                {
                    existing.CurrentValue = value;
                    existing.UpdatedAtUtc = now;
                    counter = existing;
                }

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await this._eventBus.PublishAsync(
                new CounterUpdatedEvent(
                    workspaceId,
                    normalizedName,
                    scopeMode,
                    normalizedCategory,
                    previousValue,
                    counter.CurrentValue,
                    now),
                cancellationToken).ConfigureAwait(false);

            return counter;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(
        Guid workspaceId,
        string counterName,
        CounterScopeMode scopeMode = CounterScopeMode.Global,
        string gameCategory = "",
        CancellationToken cancellationToken = default)
    {
        (string normalizedName, string normalizedCategory) = NormalizeInputs(counterName, scopeMode, gameCategory);
        SemaphoreSlim gate = this.GetCounterLock(workspaceId, normalizedName, scopeMode, normalizedCategory);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long? previousValue;
            DateTimeOffset now = this._timeProvider.GetUtcNow();

            StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                WorkspaceCounter? existing = await db.WorkspaceCounters
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => (x.WorkspaceId == workspaceId)
                            && (x.CounterName == normalizedName)
                            && (x.ScopeMode == scopeMode)
                            && (x.GameCategory == normalizedCategory),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    return false;
                }

                previousValue = existing.CurrentValue;
                _ = db.WorkspaceCounters.Remove(existing);
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await this._eventBus.PublishAsync(
                new CounterUpdatedEvent(
                    workspaceId,
                    normalizedName,
                    scopeMode,
                    normalizedCategory,
                    previousValue,
                    null,
                    now),
                cancellationToken).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <summary>
    /// Validates and normalizes the counter name, scope mode, and game category.
    /// </summary>
    /// <param name="counterName">The raw counter name.</param>
    /// <param name="scopeMode">The counter scope mode.</param>
    /// <param name="gameCategory">The raw game or category name.</param>
    /// <returns>A tuple containing the normalized counter name and game category.</returns>
    private static (string CounterName, string GameCategory) NormalizeInputs(
        string counterName,
        CounterScopeMode scopeMode,
        string? gameCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(counterName);

        if (!Enum.IsDefined(scopeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(scopeMode), scopeMode, "Unsupported counter scope mode.");
        }

        string normalizedName = counterName.Trim().ToLowerInvariant();
        string normalizedCategory = scopeMode == CounterScopeMode.Global
            ? string.Empty
            : (gameCategory ?? string.Empty).Trim();

        return (normalizedName, normalizedCategory);
    }

    /// <summary>
    /// Gets or creates the per-counter semaphore serializing mutations for a specific counter key.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="normalizedName">The normalized counter name.</param>
    /// <param name="scopeMode">The counter scope mode.</param>
    /// <param name="normalizedCategory">The normalized game category.</param>
    /// <returns>The <see cref="SemaphoreSlim"/> protecting the counter key.</returns>
    private SemaphoreSlim GetCounterLock(
        Guid workspaceId,
        string normalizedName,
        CounterScopeMode scopeMode,
        string normalizedCategory)
    {
        string lockKey = $"{workspaceId:N}:{normalizedName}:{(int)scopeMode}:{normalizedCategory}";
        return this._counterLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
    }
}
