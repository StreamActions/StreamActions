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
    private static readonly (string Name, string Designation)[] _builtInWorkspaceGroups =
    [
        ("Broadcaster", "broadcaster"),
        ("Moderator", "moderator"),
        ("VIP", "vip"),
        ("Subscriber", "subscriber"),
        ("Regular", "regular"),
        ("Follower", "follower"),
        ("Everyone", "everyone"),
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
    /// Initializes a new instance of the <see cref="PermissionStore"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="timeProvider">The time provider used for UTC timestamps.</param>
    /// <param name="eventBus">The event bus used to publish permission invalidation events.</param>
    public PermissionStore(
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
            List<PermissionGroup> existingGroups = await db.PermissionGroups
                .Where(x => x.WorkspaceId == workspaceId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            HashSet<string> existingNames = existingGroups
                .Select(static x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            HashSet<string> existingDesignations = existingGroups
                .Where(static x => !string.IsNullOrWhiteSpace(x.MappedPlatformDesignation))
                .Select(static x => x.MappedPlatformDesignation!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach ((string name, string designation) in _builtInWorkspaceGroups)
            {
                if (existingNames.Contains(name) || existingDesignations.Contains(designation))
                {
                    continue;
                }

                _ = db.PermissionGroups.Add(new PermissionGroup
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    Name = name,
                    MappedPlatformDesignation = designation,
                });
                addedAny = true;
            }

            if (addedAny)
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
