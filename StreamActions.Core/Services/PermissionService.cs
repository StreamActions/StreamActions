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

using Microsoft.Extensions.Caching.Hybrid;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;

namespace StreamActions.Core.Services;

/// <summary>
/// Implements the two-tier SMF and PhantomBot permission evaluation pipeline with <see cref="HybridCache"/> caching.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    /// <summary>
    /// Cache tag applied to entries that depend on global permission groups.
    /// </summary>
    private const string _globalCacheTag = "perm:global";

    /// <summary>
    /// Designation name matching all users regardless of platform roles.
    /// </summary>
    private const string _everyoneDesignation = "Everyone";

    /// <summary>
    /// The permission persistence store.
    /// </summary>
    private readonly IPermissionStore _store;

    /// <summary>
    /// The permission descriptor registry.
    /// </summary>
    private readonly IPermissionRegistry _registry;

    /// <summary>
    /// The hybrid cache instance.
    /// </summary>
    private readonly HybridCache _cache;

    /// <summary>
    /// The event bus used to publish permission invalidation events.
    /// </summary>
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionService"/> class.
    /// </summary>
    /// <param name="store">The permission persistence store.</param>
    /// <param name="registry">The permission descriptor registry.</param>
    /// <param name="cache">The hybrid cache instance.</param>
    /// <param name="eventBus">The event bus used to publish invalidation events.</param>
    public PermissionService(
        IPermissionStore store,
        IPermissionRegistry registry,
        HybridCache cache,
        IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(eventBus);

        this._store = store;
        this._registry = registry;
        this._cache = cache;
        this._eventBus = eventBus;
    }

    /// <inheritdoc />
    public async ValueTask<PermissionEvaluationResult> EvaluateAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        // 1. Root or Global Admin bypass.
        if (actor.IsRoot || actor.IsGlobalAdmin)
        {
            return new(true, PermissionState.Allow, "Root or global administrator bypass.");
        }

        // 2. Tier 1: Global Groups assigned to workspace.
        IReadOnlyList<GroupWithGrants> globalGroups = await this.GetGlobalGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        bool hasGlobalAllow = false;

        foreach (GroupWithGrants entry in globalGroups)
        {
            foreach (PermissionGrant grant in entry.Grants)
            {
                if (!string.Equals(grant.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (grant.State == PermissionState.Deny)
                {
                    return new(false, PermissionState.Deny, "Denied by assigned global group.", entry.Group.Id, entry.Group.Name);
                }

                if (grant.State == PermissionState.Allow)
                {
                    hasGlobalAllow = true;
                }
            }
        }

        bool isGloballyGated = this._registry.GetByKey(permissionKey)?.IsGloballyGated == true
            || permissionKey.StartsWith("feature.", StringComparison.OrdinalIgnoreCase);

        if (isGloballyGated && !hasGlobalAllow)
        {
            return new(false, PermissionState.Deny, "Globally gated permission is not allowed by any assigned global group.");
        }

        // 3. Broadcaster or Workspace Owner bypass.
        Workspace? workspace = await this.GetWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        if (actor.Designations.HasFlag(PlatformUserDesignations.Broadcaster)
            || (workspace is not null && actor.PanelUserId.HasValue && workspace.OwnerUserId == actor.PanelUserId.Value))
        {
            return new(true, PermissionState.Allow, "Workspace owner or broadcaster bypass.");
        }

        // 4. Tier 2: Workspace Groups (designations + roster + custom groups) + User Overrides.
        WorkspacePlatformRoleRoster? roster = await this.GetPlatformRoleRosterAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);
        PlatformUserDesignations effectiveDesignations = actor.Designations | (roster?.Designations ?? PlatformUserDesignations.None);

        IReadOnlyList<Guid> userGroupIds = await this.GetUserGroupIdsAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<GroupWithGrants> workspaceGroups = await this.GetWorkspaceGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<UserPermissionOverride> userOverrides = await this.GetUserOverridesAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);

        bool hasWorkspaceAllow = false;

        foreach (GroupWithGrants entry in workspaceGroups)
        {
            if (!MatchesGroup(entry.Group, effectiveDesignations, userGroupIds))
            {
                continue;
            }

            foreach (PermissionGrant grant in entry.Grants)
            {
                if (!string.Equals(grant.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (grant.State == PermissionState.Deny)
                {
                    return new(false, PermissionState.Deny, "Denied by workspace group.", entry.Group.Id, entry.Group.Name);
                }

                if (grant.State == PermissionState.Allow)
                {
                    hasWorkspaceAllow = true;
                }
            }
        }

        foreach (UserPermissionOverride userOverride in userOverrides)
        {
            if (!string.Equals(userOverride.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (userOverride.State == PermissionState.Deny)
            {
                return new(false, PermissionState.Deny, "Denied by user permission override.");
            }

            if (userOverride.State == PermissionState.Allow)
            {
                hasWorkspaceAllow = true;
            }
        }

        return hasWorkspaceAllow
            ? new(true, PermissionState.Allow, "Allowed by workspace group or user override.")
            : new(false, PermissionState.Inherit, "No explicit allow grant found.");
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsAllowedAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        PermissionEvaluationResult result = await this.EvaluateAsync(workspaceId, actor, permissionKey, cancellationToken).ConfigureAwait(false);
        return result.IsAllowed;
    }

    /// <inheritdoc />
    public async ValueTask InvalidateWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        await this._cache.RemoveByTagAsync(GetWorkspaceCacheTag(workspaceId), cancellationToken).ConfigureAwait(false);
        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask InvalidateGlobalAsync(CancellationToken cancellationToken = default)
    {
        await this._cache.RemoveByTagAsync(_globalCacheTag, cancellationToken).ConfigureAwait(false);
        await this._eventBus.PublishAsync(new PermissionsInvalidatedEvent(null), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the cache tag for a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>The workspace cache tag.</returns>
    private static string GetWorkspaceCacheTag(Guid workspaceId) => $"perm:ws:{workspaceId:N}";

    /// <summary>
    /// Determines whether a permission group applies to an actor based on group membership or mapped designation.
    /// </summary>
    /// <param name="group">The permission group to test.</param>
    /// <param name="effectiveDesignations">The actor's combined platform designations.</param>
    /// <param name="userGroupIds">The identifiers of custom groups the actor explicitly belongs to.</param>
    /// <returns><see langword="true"/> if the group matches the actor; otherwise, <see langword="false"/>.</returns>
    private static bool MatchesGroup(
        PermissionGroup group,
        PlatformUserDesignations effectiveDesignations,
        IReadOnlyList<Guid> userGroupIds) =>
        userGroupIds.Contains(group.Id)
            || (!string.IsNullOrWhiteSpace(group.MappedPlatformDesignation)
                && (string.Equals(group.MappedPlatformDesignation, _everyoneDesignation, StringComparison.OrdinalIgnoreCase)
                    || (Enum.TryParse<PlatformUserDesignations>(group.MappedPlatformDesignation, ignoreCase: true, out PlatformUserDesignations mappedDesignation)
                        && mappedDesignation != PlatformUserDesignations.None
                        && effectiveDesignations.HasFlag(mappedDesignation))));

    /// <summary>
    /// Retrieves cached global permission groups and their grants for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The global groups assigned to the workspace paired with their grants.</returns>
    private async ValueTask<IReadOnlyList<GroupWithGrants>> GetGlobalGroupsWithGrantsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        string cacheKey = $"perm:global-groups:{workspaceId:N}";
        string[] tags = [_globalCacheTag, GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                IReadOnlyList<PermissionGroup> groups = await this._store.GetGlobalGroupsForWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
                List<GroupWithGrants> result = new(groups.Count);

                foreach (PermissionGroup group in groups)
                {
                    IReadOnlyList<PermissionGrant> grants = await this._store.GetGrantsAsync(group.Id, ct).ConfigureAwait(false);
                    result.Add(new(group, grants));
                }

                return result;
            },
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves cached workspace-scoped permission groups and their grants.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The workspace permission groups paired with their grants.</returns>
    private async ValueTask<IReadOnlyList<GroupWithGrants>> GetWorkspaceGroupsWithGrantsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        string cacheKey = $"perm:ws-groups:{workspaceId:N}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                IReadOnlyList<PermissionGroup> groups = await this._store.GetGroupsAsync(workspaceId, ct).ConfigureAwait(false);
                List<GroupWithGrants> result = new(groups.Count);

                foreach (PermissionGroup group in groups)
                {
                    IReadOnlyList<PermissionGrant> grants = await this._store.GetGrantsAsync(group.Id, ct).ConfigureAwait(false);
                    result.Add(new(group, grants));
                }

                return result;
            },
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the cached workspace metadata for ownership checks.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The workspace entity, or <see langword="null"/> if not found.</returns>
    private async ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        string cacheKey = $"perm:ws:{workspaceId:N}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct => await this._store.GetWorkspaceAsync(workspaceId, ct).ConfigureAwait(false),
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the cached platform role roster entry for an actor in a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The role roster entry, or <see langword="null"/> if none exists.</returns>
    private async ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"perm:roster:{workspaceId:N}:{platformId}:{platformUserId}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct => await this._store.GetPlatformRoleRosterAsync(workspaceId, platformId, platformUserId, ct).ConfigureAwait(false),
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the cached custom permission group identifiers assigned to an actor in a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The list of custom group identifiers.</returns>
    private async ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"perm:user-groups:{workspaceId:N}:{platformId}:{platformUserId}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct => await this._store.GetUserGroupIdsAsync(workspaceId, platformId, platformUserId, ct).ConfigureAwait(false),
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the cached per-user permission overrides for an actor in a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="platformUserId">The platform-specific user identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The list of user permission overrides.</returns>
    private async ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(
        Guid workspaceId,
        string platformId,
        string platformUserId,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"perm:user-overrides:{workspaceId:N}:{platformId}:{platformUserId}";
        string[] tags = [GetWorkspaceCacheTag(workspaceId)];

        return await this._cache.GetOrCreateAsync(
            cacheKey,
            async ct => await this._store.GetUserOverridesAsync(workspaceId, platformId, platformUserId, ct).ConfigureAwait(false),
            tags: tags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Represents a cached permission group paired with its grants.
    /// </summary>
    /// <param name="Group">The permission group.</param>
    /// <param name="Grants">The grants assigned to the group.</param>
    private sealed record GroupWithGrants(PermissionGroup Group, IReadOnlyList<PermissionGrant> Grants);
}
