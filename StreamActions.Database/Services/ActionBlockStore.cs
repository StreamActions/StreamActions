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
using StreamActions.Core.Actions;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using System.Text.Json;

namespace StreamActions.Database.Services;

/// <summary>
/// EF Core and <see cref="WorkspaceCacheService"/>-backed implementation of <see cref="IActionBlockStore"/>.
/// </summary>
public sealed class ActionBlockStore : IActionBlockStore
{
    /// <summary>
    /// Cache domain for workspace Action Block definitions.
    /// </summary>
    private const string _cacheDomain = "action_blocks";

    /// <summary>
    /// Pooled EF Core context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// Workspace-scoped hybrid cache helper.
    /// </summary>
    private readonly WorkspaceCacheService _cache;

    /// <summary>
    /// Permission store used to resolve Tier 1 <c>MaxActionBlocksPerFlow</c> quotas at task save time.
    /// </summary>
    private readonly IPermissionStore _permissionStore;

    /// <summary>
    /// Time provider for <c>UpdatedAtUtc</c> timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBlockStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">EF Core context factory.</param>
    /// <param name="cache">Workspace cache service.</param>
    /// <param name="permissionStore">Permission store for Tier 1 resource quota enforcement.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    public ActionBlockStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        WorkspaceCacheService cache,
        IPermissionStore permissionStore,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(permissionStore);

        this._dbContextFactory = dbContextFactory;
        this._cache = cache;
        this._permissionStore = permissionStore;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceActionBlockTask>> GetTasksAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        List<WorkspaceActionBlockTask> tasks = await this._cache.GetOrCreateAsync(
            workspaceId,
            _cacheDomain,
            "tasks",
            async ct =>
            {
                StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                await using (db.ConfigureAwait(false))
                {
                    return await db.WorkspaceActionBlockTasks
                        .Where(x => x.WorkspaceId == workspaceId)
                        .OrderBy(static x => x.Name)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return [.. tasks.Select(CloneTask)];
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceActionBlockTask?> GetTaskByIdAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkspaceActionBlockTask> tasks = await this.GetTasksAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return tasks.FirstOrDefault(t => t.Id == taskId);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceActionBlockTask?> GetTaskByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string trimmed = name.Trim();
        IReadOnlyList<WorkspaceActionBlockTask> tasks = await this.GetTasksAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return tasks.FirstOrDefault(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceActionBlockTask> UpsertTaskAsync(WorkspaceActionBlockTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(task.Name);

        IReadOnlyList<PermissionGroup> globalGroups = await this._permissionStore
            .GetGlobalGroupsForWorkspaceAsync(task.WorkspaceId, cancellationToken)
            .ConfigureAwait(false);
        WorkspaceResourceQuotas quotas = WorkspaceResourceQuotas.ResolveEffective(globalGroups);
        int totalBlocks = ActionBlockJson.CountTotalBlocks(task.Blocks);
        if (totalBlocks > quotas.EffectiveMaxActionBlocksPerFlow)
        {
            throw new ActionFlowQuotaExceededException(
                $"Task '{task.Name.Trim()}' contains {totalBlocks} action blocks, exceeding the workspace quota MaxActionBlocksPerFlow ({quotas.EffectiveMaxActionBlocksPerFlow}).");
        }

        string trimmedName = task.Name.Trim();
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        List<TaskParameterDefinition> clonedParams = [.. task.InputParameters];
        List<ActionBlock> clonedBlocks = CloneBlocks(task.Blocks);

        WorkspaceActionBlockTask persisted;
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceActionBlockTask? existing = task.Id != Guid.Empty
                ? await db.WorkspaceActionBlockTasks
                    .AsTracking()
                    .FirstOrDefaultAsync(x => x.Id == task.Id && x.WorkspaceId == task.WorkspaceId, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            if (existing is null)
            {
                persisted = new WorkspaceActionBlockTask
                {
                    Id = task.Id == Guid.Empty ? Guid.CreateVersion7() : task.Id,
                    WorkspaceId = task.WorkspaceId,
                    Name = trimmedName,
                    IsEnabled = task.IsEnabled,
                    InputParameters = clonedParams,
                    Blocks = clonedBlocks,
                    UpdatedAtUtc = now,
                };
                _ = db.WorkspaceActionBlockTasks.Add(persisted);
            }
            else
            {
                existing.Name = trimmedName;
                existing.IsEnabled = task.IsEnabled;
                existing.InputParameters = clonedParams;
                existing.Blocks = clonedBlocks;
                existing.UpdatedAtUtc = now;
                persisted = existing;
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._cache.InvalidateAsync(task.WorkspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
        return CloneTask(persisted);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteTaskAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default)
    {
        int deleted;
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            deleted = await db.WorkspaceActionBlockTasks
                .Where(x => x.WorkspaceId == workspaceId && x.Id == taskId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (deleted > 0)
        {
            await this._cache.InvalidateAsync(workspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetProfilesAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        List<WorkspaceActionBlockProfile> profiles = await this._cache.GetOrCreateAsync(
            workspaceId,
            _cacheDomain,
            "profiles",
            async ct =>
            {
                StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                await using (db.ConfigureAwait(false))
                {
                    return await db.WorkspaceActionBlockProfiles
                        .Where(x => x.WorkspaceId == workspaceId)
                        .OrderBy(static x => x.Name)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);
                }
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return [.. profiles.Select(CloneProfile)];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<WorkspaceActionBlockProfile> all = await db.WorkspaceActionBlockProfiles
                .OrderBy(static x => x.WorkspaceId)
                .ThenBy(static x => x.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. all.Select(CloneProfile)];
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceActionBlockProfile?> GetProfileByIdAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkspaceActionBlockProfile> profiles = await this.GetProfilesAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return profiles.FirstOrDefault(p => p.Id == profileId);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceActionBlockProfile?> GetProfileByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string trimmed = name.Trim();
        IReadOnlyList<WorkspaceActionBlockProfile> profiles = await this.GetProfilesAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return profiles.FirstOrDefault(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceActionBlockProfile> UpsertProfileAsync(WorkspaceActionBlockProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Name);

        string trimmedName = profile.Name.Trim();
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        ProfileTriggerDefinition clonedTrigger = CloneTrigger(profile.Trigger);
        ActionBlockConditionGroup? clonedPredicates = CloneConditionGroup(profile.Predicates);
        Dictionary<string, string> clonedMappings = new(profile.ParameterMappings, StringComparer.OrdinalIgnoreCase);

        WorkspaceActionBlockProfile persisted;
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspaceActionBlockProfile? existing = profile.Id != Guid.Empty
                ? await db.WorkspaceActionBlockProfiles
                    .AsTracking()
                    .FirstOrDefaultAsync(x => x.Id == profile.Id && x.WorkspaceId == profile.WorkspaceId, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            if (existing is null)
            {
                persisted = new WorkspaceActionBlockProfile
                {
                    Id = profile.Id == Guid.Empty ? Guid.CreateVersion7() : profile.Id,
                    WorkspaceId = profile.WorkspaceId,
                    Name = trimmedName,
                    IsEnabled = profile.IsEnabled,
                    Trigger = clonedTrigger,
                    Predicates = clonedPredicates,
                    AttachedTaskId = profile.AttachedTaskId,
                    ParameterMappings = clonedMappings,
                    UpdatedAtUtc = now,
                };
                _ = db.WorkspaceActionBlockProfiles.Add(persisted);
            }
            else
            {
                existing.Name = trimmedName;
                existing.IsEnabled = profile.IsEnabled;
                existing.Trigger = clonedTrigger;
                existing.Predicates = clonedPredicates;
                existing.AttachedTaskId = profile.AttachedTaskId;
                existing.ParameterMappings = clonedMappings;
                existing.UpdatedAtUtc = now;
                persisted = existing;
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._cache.InvalidateAsync(profile.WorkspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
        return CloneProfile(persisted);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> SetProfileEnabledAsync(Guid workspaceId, Guid profileId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        int updated;
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            updated = await db.WorkspaceActionBlockProfiles
                .Where(x => x.WorkspaceId == workspaceId && x.Id == profileId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(static p => p.IsEnabled, isEnabled)
                        .SetProperty(static p => p.UpdatedAtUtc, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (updated > 0)
        {
            await this._cache.InvalidateAsync(workspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteProfileAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default)
    {
        int deleted;
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            deleted = await db.WorkspaceActionBlockProfiles
                .Where(x => x.WorkspaceId == workspaceId && x.Id == profileId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (deleted > 0)
        {
            await this._cache.InvalidateAsync(workspaceId, _cacheDomain, cancellationToken).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static WorkspaceActionBlockTask CloneTask(WorkspaceActionBlockTask source) => new()
    {
        Id = source.Id,
        WorkspaceId = source.WorkspaceId,
        Name = source.Name,
        IsEnabled = source.IsEnabled,
        InputParameters = [.. source.InputParameters],
        Blocks = CloneBlocks(source.Blocks),
        UpdatedAtUtc = source.UpdatedAtUtc,
    };

    private static WorkspaceActionBlockProfile CloneProfile(WorkspaceActionBlockProfile source) => new()
    {
        Id = source.Id,
        WorkspaceId = source.WorkspaceId,
        Name = source.Name,
        IsEnabled = source.IsEnabled,
        Trigger = CloneTrigger(source.Trigger),
        Predicates = CloneConditionGroup(source.Predicates),
        AttachedTaskId = source.AttachedTaskId,
        ParameterMappings = new Dictionary<string, string>(source.ParameterMappings, StringComparer.OrdinalIgnoreCase),
        UpdatedAtUtc = source.UpdatedAtUtc,
    };

    private static List<ActionBlock> CloneBlocks(List<ActionBlock> blocks)
    {
        string json = JsonSerializer.Serialize(blocks, ActionBlockJson.Default);
        return JsonSerializer.Deserialize<List<ActionBlock>>(json, ActionBlockJson.Default) ?? [];
    }

    /// <summary>
    /// Clones a <see cref="ProfileTriggerDefinition"/> to prevent external mutation of cached or tracked instances.
    /// </summary>
    /// <param name="trigger">The trigger definition to clone.</param>
    /// <returns>A cloned <see cref="ProfileTriggerDefinition"/>.</returns>
    private static ProfileTriggerDefinition CloneTrigger(ProfileTriggerDefinition trigger) => new()
    {
        TriggerId = trigger.TriggerId,
        PlatformId = trigger.PlatformId,
        CommandTrigger = trigger.CommandTrigger,
        KeywordPattern = trigger.KeywordPattern,
        ScheduleMode = trigger.ScheduleMode,
        IntervalSeconds = trigger.IntervalSeconds,
        CronExpression = trigger.CronExpression,
        ScheduledAtUtc = trigger.ScheduledAtUtc,
        OneShotFiredAtUtc = trigger.OneShotFiredAtUtc,
        Settings = new Dictionary<string, string>(trigger.Settings, StringComparer.OrdinalIgnoreCase),
    };

    private static ActionBlockConditionGroup? CloneConditionGroup(ActionBlockConditionGroup? group) =>
        group is null
            ? null
            : new ActionBlockConditionGroup(
                [.. group.Clauses],
                group.FreeformExpression,
                group.UseFreeform);
}

