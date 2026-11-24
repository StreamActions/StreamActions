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
using StreamActions.Core.Permissions;

namespace StreamActions.Database.Services;

/// <summary>
/// Entity Framework Core implementation of <see cref="IPermissionStore"/>.
/// </summary>
public sealed class PermissionStore : IPermissionStore
{
    /// <summary>
    /// Built-in workspace permission groups and their mapped platform designations.
    /// </summary>
    private static readonly (string Name, string Designation, PlatformUserDesignations Flag)[] _builtInWorkspaceGroups =
    [
        ("Broadcaster", "broadcaster", PlatformUserDesignations.Broadcaster),
        ("Moderator", "moderator", PlatformUserDesignations.Moderator),
        ("VIP", "vip", PlatformUserDesignations.Vip),
        ("Subscriber", "subscriber", PlatformUserDesignations.Subscriber),
        ("Regular", "regular", PlatformUserDesignations.Regular),
        ("Follower", "follower", PlatformUserDesignations.Follower),
        ("Everyone", "everyone", PlatformUserDesignations.Everyone),
    ];

    /// <summary>
    /// The database context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// The time provider used for UTC timestamps.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The event bus used to publish permission invalidation events.
    /// </summary>
    private readonly IEventBus _eventBus;

    /// <summary>
    /// The permission descriptor registry used to resolve default global and workspace permission grants.
    /// </summary>
    private readonly IPermissionRegistry _permissionRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="timeProvider">The time provider used for UTC timestamps.</param>
    /// <param name="eventBus">The event bus used to publish permission invalidation events.</param>
    /// <param name="permissionRegistry">The optional permission registry (defaults to a new <see cref="PermissionRegistry"/> if <see langword="null"/>).</param>
    public PermissionStore(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        TimeProvider timeProvider,
        IEventBus eventBus,
        IPermissionRegistry? permissionRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(eventBus);

        this._dbContextFactory = dbContextFactory;
        this._timeProvider = timeProvider;
        this._eventBus = eventBus;
        this._permissionRegistry = permissionRegistry ?? new PermissionRegistry();
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PermissionGroups
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderBy(static x => x.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PermissionGrants
                .Where(x => x.GroupId == groupId)
                .OrderBy(static x => x.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PermissionState> EvaluatePermissionAsync(
        Guid workspaceId,
        IReadOnlyCollection<Guid> groupIds,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        if (groupIds.Count == 0)
        {
            return PermissionState.Inherit;
        }

        string normalizedKey = NormalizeValue(permissionKey);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<PermissionState> states = await (
                from grant in db.PermissionGrants
                join grp in db.PermissionGroups on grant.GroupId equals grp.Id
                where groupIds.Contains(grant.GroupId)
                    && grant.PermissionKey == normalizedKey
                    && (grp.WorkspaceId == workspaceId || grp.WorkspaceId == null)
                select grant.State)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return states.Contains(PermissionState.Deny)
                ? PermissionState.Deny
                : states.Contains(PermissionState.Allow) ? PermissionState.Allow : PermissionState.Inherit;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.Workspaces
                .FirstOrDefaultAsync(x => x.Id == workspaceId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await (
                from assignment in db.WorkspaceGlobalGroupAssignments
                join grp in db.PermissionGroups on assignment.GlobalGroupId equals grp.Id
                where assignment.WorkspaceId == workspaceId && grp.WorkspaceId == null
                orderby grp.Name
                select grp)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default)
    {
        bool changed = false;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            bool exists = await db.WorkspaceGlobalGroupAssignments
                .AnyAsync(x => x.WorkspaceId == workspaceId && x.GlobalGroupId == globalGroupId, cancellationToken)
                .ConfigureAwait(false);

            if (!exists)
            {
                _ = db.WorkspaceGlobalGroupAssignments.Add(new WorkspaceGlobalGroupAssignment
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    GlobalGroupId = globalGroupId,
                    AssignedAtUtc = this._timeProvider.GetUtcNow(),
                });

                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                changed = true;
            }
        }

        if (changed)
        {
            await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default)
    {
        int deleted;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            deleted = await db.WorkspaceGlobalGroupAssignments
                .Where(x => x.WorkspaceId == workspaceId && x.GlobalGroupId == globalGroupId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (deleted > 0)
        {
            await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);

        string normalizedPlatformId = NormalizeValue(platformId);
        string normalizedPlatformUserId = NormalizeValue(platformUserId);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await (
                from member in db.PermissionGroupMembers
                join grp in db.PermissionGroups on member.GroupId equals grp.Id
                where grp.WorkspaceId == workspaceId
                    && member.PlatformId == normalizedPlatformId
                    && member.PlatformUserId == normalizedPlatformUserId
                select grp.Id)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);

        string normalizedPlatformId = NormalizeValue(platformId);
        string normalizedPlatformUserId = NormalizeValue(platformUserId);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.UserPermissionOverrides
                .Where(x => x.WorkspaceId == workspaceId
                    && x.PlatformId == normalizedPlatformId
                    && x.PlatformUserId == normalizedPlatformUserId)
                .OrderBy(static x => x.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);

        string normalizedPlatformId = NormalizeValue(platformId);
        string normalizedPlatformUserId = NormalizeValue(platformUserId);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.WorkspacePlatformRoleRosters
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == workspaceId
                        && x.PlatformId == normalizedPlatformId
                        && x.PlatformUserId == normalizedPlatformUserId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(group.Name);

        Guid id = group.Id == Guid.Empty ? Guid.CreateVersion7() : group.Id;
        string trimmedName = group.Name.Trim();
        string? trimmedDesignation = string.IsNullOrWhiteSpace(group.MappedPlatformDesignation)
            ? null
            : group.MappedPlatformDesignation.Trim();
        string? trimmedOidcClaim = string.IsNullOrWhiteSpace(group.MappedOidcGroupClaim)
            ? null
            : group.MappedOidcGroupClaim.Trim();

        PermissionGroup persisted;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            PermissionGroup? existing = await db.PermissionGroups
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.WorkspaceId = group.WorkspaceId;
                existing.Name = trimmedName;
                existing.MappedPlatformDesignation = trimmedDesignation;
                existing.MappedOidcGroupClaim = trimmedOidcClaim;
                existing.MaxClearScriptTotalRamBytes = group.MaxClearScriptTotalRamBytes;
                existing.MaxActionBlocksPerFlow = group.MaxActionBlocksPerFlow;
                existing.MaxActionBlockFlowRuntimeSeconds = group.MaxActionBlockFlowRuntimeSeconds;
                persisted = existing;
            }
            else
            {
                persisted = new PermissionGroup
                {
                    Id = id,
                    WorkspaceId = group.WorkspaceId,
                    Name = trimmedName,
                    MappedPlatformDesignation = trimmedDesignation,
                    MappedOidcGroupClaim = trimmedOidcClaim,
                    MaxClearScriptTotalRamBytes = group.MaxClearScriptTotalRamBytes,
                    MaxActionBlocksPerFlow = group.MaxActionBlocksPerFlow,
                    MaxActionBlockFlowRuntimeSeconds = group.MaxActionBlockFlowRuntimeSeconds,
                };
                _ = db.PermissionGroups.Add(persisted);
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(persisted.WorkspaceId), cancellationToken).ConfigureAwait(false);
        return persisted;
    }

    /// <inheritdoc/>
    public async ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        PermissionGroup? existing;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            existing = await db.PermissionGroups
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                _ = db.PermissionGroups.Remove(existing);
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (existing is not null)
        {
            await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(existing.WorkspaceId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        string normalizedKey = NormalizeValue(permissionKey);
        Guid? workspaceId;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            workspaceId = await db.PermissionGroups
                .Where(x => x.Id == groupId)
                .Select(static x => x.WorkspaceId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            PermissionGrant? existing = await db.PermissionGrants
                .AsTracking()
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.PermissionKey == normalizedKey, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.State = state;
            }
            else
            {
                _ = db.PermissionGrants.Add(new PermissionGrant
                {
                    Id = Guid.CreateVersion7(),
                    GroupId = groupId,
                    PermissionKey = normalizedKey,
                    State = state,
                });
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SetGrantsBatchAsync(
        Guid groupId,
        IReadOnlyDictionary<string, PermissionState> grants,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grants);

        if (grants.Count == 0)
        {
            return;
        }

        Guid? workspaceId;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            workspaceId = await db.PermissionGroups
                .Where(x => x.Id == groupId)
                .Select(static x => x.WorkspaceId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            List<PermissionGrant> existingGrants = await db.PermissionGrants
                .AsTracking()
                .Where(x => x.GroupId == groupId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            Dictionary<string, PermissionGrant> existingByKey = existingGrants
                .ToDictionary(static x => x.PermissionKey, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, PermissionState> pair in grants)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);

                string normalizedKey = NormalizeValue(pair.Key);
                if (existingByKey.TryGetValue(normalizedKey, out PermissionGrant? existing))
                {
                    existing.State = pair.Value;
                }
                else
                {
                    PermissionGrant created = new()
                    {
                        Id = Guid.CreateVersion7(),
                        GroupId = groupId,
                        PermissionKey = normalizedKey,
                        State = pair.Value,
                    };
                    _ = db.PermissionGrants.Add(created);
                    existingByKey[normalizedKey] = created;
                }
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<PermissionGroup> SeedDefaultGlobalGroupAsync(
        IReadOnlyCollection<string>? enabledDangerousFeatureKeys = null,
        CancellationToken cancellationToken = default)
    {
        PermissionGroup defaultGroup;
        bool changed;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            (defaultGroup, changed) = await this.EnsureDefaultGlobalGroupInContextAsync(
                db,
                enabledDangerousFeatureKeys,
                cancellationToken).ConfigureAwait(false);

            if (changed)
            {
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (changed)
        {
            await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(null), cancellationToken).ConfigureAwait(false);
        }

        return defaultGroup;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PermissionGroupMembers
                .Where(x => x.GroupId == groupId)
                .OrderBy(static x => x.PlatformId)
                .ThenBy(static x => x.PlatformUserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentException.ThrowIfNullOrWhiteSpace(member.PlatformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(member.PlatformUserId);

        string normalizedPlatformId = NormalizeValue(member.PlatformId);
        string normalizedPlatformUserId = NormalizeValue(member.PlatformUserId);
        string? normalizedUsername = NormalizeOptionalValue(member.LastKnownUsername);
        Guid? workspaceId;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            workspaceId = await db.PermissionGroups
                .Where(x => x.Id == member.GroupId)
                .Select(static x => x.WorkspaceId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            PermissionGroupMember? existing = await db.PermissionGroupMembers
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.GroupId == member.GroupId
                        && x.PlatformId == normalizedPlatformId
                        && x.PlatformUserId == normalizedPlatformUserId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.LastKnownUsername = normalizedUsername;
            }
            else
            {
                _ = db.PermissionGroupMembers.Add(new PermissionGroupMember
                {
                    Id = member.Id == Guid.Empty ? Guid.CreateVersion7() : member.Id,
                    GroupId = member.GroupId,
                    PlatformId = normalizedPlatformId,
                    PlatformUserId = normalizedPlatformUserId,
                    LastKnownUsername = normalizedUsername,
                    AddedAtUtc = member.AddedAtUtc == default ? this._timeProvider.GetUtcNow() : member.AddedAtUtc,
                });
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveGroupMemberAsync(
        Guid groupId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformUserId);

        string normalizedPlatformId = NormalizeValue(platformId);
        string normalizedPlatformUserId = NormalizeValue(platformUserId);
        int deleted;
        Guid? workspaceId = null;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            deleted = await db.PermissionGroupMembers
                .Where(x => x.GroupId == groupId
                    && x.PlatformId == normalizedPlatformId
                    && x.PlatformUserId == normalizedPlatformUserId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            if (deleted > 0)
            {
                workspaceId = await db.PermissionGroups
                    .Where(x => x.Id == groupId)
                    .Select(static x => x.WorkspaceId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (deleted > 0)
        {
            await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userOverride);
        ArgumentException.ThrowIfNullOrWhiteSpace(userOverride.PlatformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userOverride.PlatformUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userOverride.PermissionKey);

        string normalizedPlatformId = NormalizeValue(userOverride.PlatformId);
        string normalizedPlatformUserId = NormalizeValue(userOverride.PlatformUserId);
        string normalizedPermissionKey = NormalizeValue(userOverride.PermissionKey);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            UserPermissionOverride? existing = await db.UserPermissionOverrides
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == userOverride.WorkspaceId
                        && x.PlatformId == normalizedPlatformId
                        && x.PlatformUserId == normalizedPlatformUserId
                        && x.PermissionKey == normalizedPermissionKey,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.State = userOverride.State;
            }
            else
            {
                _ = db.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    Id = userOverride.Id == Guid.Empty ? Guid.CreateVersion7() : userOverride.Id,
                    WorkspaceId = userOverride.WorkspaceId,
                    PlatformId = normalizedPlatformId,
                    PlatformUserId = normalizedPlatformUserId,
                    PermissionKey = normalizedPermissionKey,
                    State = userOverride.State,
                });
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(userOverride.WorkspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.PlatformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.PlatformUserId);

        string normalizedPlatformId = NormalizeValue(entry.PlatformId);
        string normalizedPlatformUserId = NormalizeValue(entry.PlatformUserId);
        string? normalizedUsername = NormalizeOptionalValue(entry.Username);
        DateTimeOffset updatedAt = entry.UpdatedAtUtc == default ? this._timeProvider.GetUtcNow() : entry.UpdatedAtUtc;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            WorkspacePlatformRoleRoster? existing = await db.WorkspacePlatformRoleRosters
                .AsTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkspaceId == entry.WorkspaceId
                        && x.PlatformId == normalizedPlatformId
                        && x.PlatformUserId == normalizedPlatformUserId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Username = normalizedUsername;
                existing.Designations = entry.Designations;
                existing.UpdatedAtUtc = updatedAt;
            }
            else
            {
                _ = db.WorkspacePlatformRoleRosters.Add(new WorkspacePlatformRoleRoster
                {
                    Id = entry.Id == Guid.Empty ? Guid.CreateVersion7() : entry.Id,
                    WorkspaceId = entry.WorkspaceId,
                    PlatformId = normalizedPlatformId,
                    PlatformUserId = normalizedPlatformUserId,
                    Username = normalizedUsername,
                    Designations = entry.Designations,
                    UpdatedAtUtc = updatedAt,
                });
            }

            _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(entry.WorkspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        bool addedAny = false;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            (PermissionGroup defaultGlobalGroup, bool defaultGlobalChanged) = await this.EnsureDefaultGlobalGroupInContextAsync(
                db,
                enabledDangerousFeatureKeys: null,
                cancellationToken).ConfigureAwait(false);

            bool assignedToDefaultGlobal = await db.WorkspaceGlobalGroupAssignments
                .AnyAsync(
                    x => x.WorkspaceId == workspaceId && x.GlobalGroupId == defaultGlobalGroup.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!assignedToDefaultGlobal)
            {
                _ = db.WorkspaceGlobalGroupAssignments.Add(new WorkspaceGlobalGroupAssignment
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    GlobalGroupId = defaultGlobalGroup.Id,
                    AssignedAtUtc = this._timeProvider.GetUtcNow(),
                });
                addedAny = true;
            }

            List<PermissionGroup> existingGroups = await db.PermissionGroups
                .AsTracking()
                .Where(x => x.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            Dictionary<string, PermissionGroup> groupsByDesignation = [];
            foreach (PermissionGroup grp in existingGroups)
            {
                if (!string.IsNullOrWhiteSpace(grp.MappedPlatformDesignation))
                {
                    groupsByDesignation[grp.MappedPlatformDesignation] = grp;
                }
            }

            HashSet<string> existingNames = existingGroups
                .Select(static x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach ((string name, string designation, PlatformUserDesignations _) in _builtInWorkspaceGroups)
            {
                if (groupsByDesignation.ContainsKey(designation) || existingNames.Contains(name))
                {
                    if (!groupsByDesignation.ContainsKey(designation))
                    {
                        PermissionGroup? byName = existingGroups.FirstOrDefault(
                            g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (byName is not null)
                        {
                            groupsByDesignation[designation] = byName;
                        }
                    }

                    continue;
                }

                PermissionGroup createdGroup = new()
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    Name = name,
                    MappedPlatformDesignation = designation,
                };
                _ = db.PermissionGroups.Add(createdGroup);
                existingGroups.Add(createdGroup);
                groupsByDesignation[designation] = createdGroup;
                addedAny = true;
            }

            List<Guid> builtInGroupIds = [.. groupsByDesignation.Values.Select(static g => g.Id)];
            List<PermissionGrant> existingGrants = builtInGroupIds.Count == 0
                ? []
                : await db.PermissionGrants
                    .Where(x => builtInGroupIds.Contains(x.GroupId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            HashSet<(Guid GroupId, string PermissionKey)> existingGrantKeys = [.. existingGrants.Select(static g => (g.GroupId, NormalizeValue(g.PermissionKey)))];

            IReadOnlyList<PermissionDescriptor> allDescriptors = this._permissionRegistry.GetAll();

            foreach ((string _, string designation, PlatformUserDesignations flag) in _builtInWorkspaceGroups)
            {
                if (!groupsByDesignation.TryGetValue(designation, out PermissionGroup? targetGroup))
                {
                    continue;
                }

                foreach (PermissionDescriptor descriptor in allDescriptors)
                {
                    if (descriptor.IsGloballyGated
                        || descriptor.DefaultAllowedDesignations == PlatformUserDesignations.None
                        || !descriptor.DefaultAllowedDesignations.HasFlag(flag))
                    {
                        continue;
                    }

                    string normalizedKey = NormalizeValue(descriptor.Key);
                    if (!existingGrantKeys.Add((targetGroup.Id, normalizedKey)))
                    {
                        continue;
                    }

                    _ = db.PermissionGrants.Add(new PermissionGrant
                    {
                        Id = Guid.CreateVersion7(),
                        GroupId = targetGroup.Id,
                        PermissionKey = normalizedKey,
                        State = PermissionState.Allow,
                    });
                    addedAny = true;
                }
            }

            if (addedAny || defaultGlobalChanged)
            {
                _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (addedAny)
        {
            await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ensures the built-in <c>Default Workspace Plan</c> global group and its default grants exist within the given <paramref name="db"/> context without calling <c>SaveChangesAsync</c>.
    /// </summary>
    /// <param name="db">The active database context.</param>
    /// <param name="enabledDangerousFeatureKeys">Optional dangerous global feature keys to explicitly grant <see cref="PermissionState.Allow"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing the <c>Default Workspace Plan</c> group and a flag indicating whether any entities were added or modified.</returns>
    private async ValueTask<(PermissionGroup Group, bool Changed)> EnsureDefaultGlobalGroupInContextAsync(
        StreamActionsDbContext db,
        IReadOnlyCollection<string>? enabledDangerousFeatureKeys,
        CancellationToken cancellationToken)
    {
        bool changed = false;

        List<PermissionGroup> globalGroups = await db.PermissionGroups
            .AsTracking()
            .Where(x => x.WorkspaceId == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        PermissionGroup? defaultGroup = globalGroups.FirstOrDefault(
            static g => string.Equals(g.Name, BuiltInPermissionKeys.DefaultWorkspacePlanGroupName, StringComparison.OrdinalIgnoreCase));

        if (defaultGroup is null)
        {
            defaultGroup = new PermissionGroup
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = null,
                Name = BuiltInPermissionKeys.DefaultWorkspacePlanGroupName,
                MaxClearScriptTotalRamBytes = WorkspaceResourceQuotas.DefaultMaxClearScriptTotalRamBytes,
                MaxActionBlocksPerFlow = WorkspaceResourceQuotas.DefaultMaxActionBlocksPerFlow,
                MaxActionBlockFlowRuntimeSeconds = WorkspaceResourceQuotas.DefaultMaxActionBlockFlowRuntimeSeconds,
            };
            _ = db.PermissionGroups.Add(defaultGroup);
            changed = true;
        }
        else if (!defaultGroup.MaxClearScriptTotalRamBytes.HasValue
            || !defaultGroup.MaxActionBlocksPerFlow.HasValue
            || !defaultGroup.MaxActionBlockFlowRuntimeSeconds.HasValue)
        {
            defaultGroup.MaxClearScriptTotalRamBytes ??= WorkspaceResourceQuotas.DefaultMaxClearScriptTotalRamBytes;
            defaultGroup.MaxActionBlocksPerFlow ??= WorkspaceResourceQuotas.DefaultMaxActionBlocksPerFlow;
            defaultGroup.MaxActionBlockFlowRuntimeSeconds ??= WorkspaceResourceQuotas.DefaultMaxActionBlockFlowRuntimeSeconds;
            changed = true;
        }

        List<PermissionGrant> existingGrants = await db.PermissionGrants
            .AsTracking()
            .Where(x => x.GroupId == defaultGroup.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, PermissionGrant> grantsByKey = existingGrants
            .ToDictionary(static g => NormalizeValue(g.PermissionKey), StringComparer.OrdinalIgnoreCase);

        HashSet<string> dangerousOptInSet = [];
        if (enabledDangerousFeatureKeys is not null)
        {
            foreach (string rawKey in enabledDangerousFeatureKeys)
            {
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    continue;
                }

                PermissionDescriptor? resolved = this._permissionRegistry.GetByKey(rawKey);
                string canonicalKey = resolved is not null ? NormalizeValue(resolved.Key) : NormalizeValue(rawKey);
                _ = dangerousOptInSet.Add(canonicalKey);
            }
        }

        foreach (PermissionDescriptor descriptor in this._permissionRegistry.GetAll())
        {
            if (!descriptor.IsGloballyGated)
            {
                continue;
            }

            string normalizedKey = NormalizeValue(descriptor.Key);

            if (!descriptor.IsDangerousGlobalFeature)
            {
                if (!grantsByKey.ContainsKey(normalizedKey))
                {
                    PermissionGrant created = new()
                    {
                        Id = Guid.CreateVersion7(),
                        GroupId = defaultGroup.Id,
                        PermissionKey = normalizedKey,
                        State = PermissionState.Allow,
                    };
                    _ = db.PermissionGrants.Add(created);
                    grantsByKey[normalizedKey] = created;
                    changed = true;
                }
            }
            else if (dangerousOptInSet.Contains(normalizedKey))
            {
                if (grantsByKey.TryGetValue(normalizedKey, out PermissionGrant? existingDangerous))
                {
                    if (existingDangerous.State != PermissionState.Allow)
                    {
                        existingDangerous.State = PermissionState.Allow;
                        changed = true;
                    }
                }
                else
                {
                    PermissionGrant created = new()
                    {
                        Id = Guid.CreateVersion7(),
                        GroupId = defaultGroup.Id,
                        PermissionKey = normalizedKey,
                        State = PermissionState.Allow,
                    };
                    _ = db.PermissionGrants.Add(created);
                    grantsByKey[normalizedKey] = created;
                    changed = true;
                }
            }
        }

        return (defaultGroup, changed);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteWorkspacePermissionKeyAsync(
        Guid workspaceId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        string normalizedKey = NormalizeValue(permissionKey);
        int deletedCount = 0;

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<Guid> groupIds = await db.PermissionGroups
                .Where(x => x.WorkspaceId == workspaceId)
                .Select(static x => x.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (groupIds.Count > 0)
            {
                deletedCount += await db.PermissionGrants
                    .Where(x => groupIds.Contains(x.GroupId) && x.PermissionKey == normalizedKey)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            deletedCount += await db.UserPermissionOverrides
                .Where(x => x.WorkspaceId == workspaceId && x.PermissionKey == normalizedKey)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (deletedCount > 0)
        {
            await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Trims and lowercases a required string key or identifier using invariant culture.
    /// </summary>
    /// <param name="value">The input string.</param>
    /// <returns>The normalized string.</returns>
    private static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();

    /// <summary>
    /// Trims and lowercases an optional string value using invariant culture, returning <see langword="null"/> if blank.
    /// </summary>
    /// <param name="value">The optional input string.</param>
    /// <returns>The normalized string, or <see langword="null"/>.</returns>
    private static string? NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
