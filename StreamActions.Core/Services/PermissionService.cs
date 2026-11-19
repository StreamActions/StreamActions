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
    /// Redacted source name displayed for Tier 1 global groups when viewed by non-root and non-global-admin operators.
    /// </summary>
    private const string _redactedGlobalGroupName = "Global Permissions";

    /// <summary>
    /// Source name displayed for the mandatory default state contribution entry.
    /// </summary>
    private const string _defaultStateSourceName = "Default State";

    /// <summary>
    /// Human-readable display label for a permission whose default state is <see cref="PermissionState.Inherit"/>.
    /// </summary>
    private const string _implicitDenyDefaultDisplay = "Implicit Deny (Overridable Deny)";

    /// <summary>
    /// Human-readable display label for a permission whose default state is <see cref="PermissionState.Allow"/>.
    /// </summary>
    private const string _defaultAllowDisplay = "Default State: Allow";

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
                if (!MatchesPermissionKey(grant.PermissionKey, permissionKey))
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

        PermissionDescriptor? descriptor = this._registry.GetByKey(permissionKey);
        bool isGloballyGated = descriptor?.IsGloballyGated == true
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
            if (!MatchesGroup(entry.Group, effectiveDesignations, userGroupIds, actor.OidcGroupClaims))
            {
                continue;
            }

            foreach (PermissionGrant grant in entry.Grants)
            {
                if (!MatchesPermissionKey(grant.PermissionKey, permissionKey))
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
            if (!MatchesPermissionKey(userOverride.PermissionKey, permissionKey))
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

    /// <inheritdoc />
    public async ValueTask<PermissionAuditBreakdown> ExplainWorkspacePermissionAsync(
        Guid workspaceId,
        string permissionKey,
        bool viewerIsRootOrGlobalAdmin = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        IReadOnlyList<GroupWithGrants> globalGroups = await this.GetGlobalGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        PermissionDescriptor descriptor = this.ResolveDescriptor(permissionKey);
        return BuildWorkspaceBreakdown(descriptor, globalGroups, viewerIsRootOrGlobalAdmin);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PermissionAuditBreakdown>> ExplainWorkspacePermissionsAsync(
        Guid workspaceId,
        bool viewerIsRootOrGlobalAdmin = true,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GroupWithGrants> globalGroups = await this.GetGlobalGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PermissionDescriptor> descriptors = this._registry.GetAll();
        List<PermissionAuditBreakdown> results = new(descriptors.Count);

        foreach (PermissionDescriptor descriptor in descriptors)
        {
            results.Add(BuildWorkspaceBreakdown(descriptor, globalGroups, viewerIsRootOrGlobalAdmin));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<PermissionAuditBreakdown> ExplainUserPermissionAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        string permissionKey,
        bool viewerIsRootOrGlobalAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        IReadOnlyList<GroupWithGrants> globalGroups = await this.GetGlobalGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        Workspace? workspace = await this.GetWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        WorkspacePlatformRoleRoster? roster = await this.GetPlatformRoleRosterAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);
        PlatformUserDesignations effectiveDesignations = actor.Designations | (roster?.Designations ?? PlatformUserDesignations.None);
        IReadOnlyList<Guid> userGroupIds = await this.GetUserGroupIdsAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<GroupWithGrants> workspaceGroups = await this.GetWorkspaceGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<UserPermissionOverride> userOverrides = await this.GetUserOverridesAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);

        PermissionDescriptor descriptor = this.ResolveDescriptor(permissionKey);
        return BuildUserBreakdown(
            descriptor,
            actor,
            workspace,
            effectiveDesignations,
            userGroupIds,
            globalGroups,
            workspaceGroups,
            userOverrides,
            viewerIsRootOrGlobalAdmin);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PermissionAuditBreakdown>> ExplainUserPermissionsAsync(
        Guid workspaceId,
        PlatformUserIdentity actor,
        bool viewerIsRootOrGlobalAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        IReadOnlyList<GroupWithGrants> globalGroups = await this.GetGlobalGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        Workspace? workspace = await this.GetWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        WorkspacePlatformRoleRoster? roster = await this.GetPlatformRoleRosterAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);
        PlatformUserDesignations effectiveDesignations = actor.Designations | (roster?.Designations ?? PlatformUserDesignations.None);
        IReadOnlyList<Guid> userGroupIds = await this.GetUserGroupIdsAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<GroupWithGrants> workspaceGroups = await this.GetWorkspaceGroupsWithGrantsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<UserPermissionOverride> userOverrides = await this.GetUserOverridesAsync(workspaceId, actor.PlatformId, actor.PlatformUserId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PermissionDescriptor> descriptors = this._registry.GetAll();
        List<PermissionAuditBreakdown> results = new(descriptors.Count);

        foreach (PermissionDescriptor descriptor in descriptors)
        {
            results.Add(BuildUserBreakdown(
                descriptor,
                actor,
                workspace,
                effectiveDesignations,
                userGroupIds,
                globalGroups,
                workspaceGroups,
                userOverrides,
                viewerIsRootOrGlobalAdmin));
        }

        return results;
    }

    /// <summary>
    /// Builds a Tier 1 workspace-level <see cref="PermissionAuditBreakdown"/> for a single permission descriptor.
    /// </summary>
    /// <param name="descriptor">The permission descriptor.</param>
    /// <param name="globalGroups">The assigned Tier 1 global groups and their grants.</param>
    /// <param name="viewerIsRootOrGlobalAdmin">Whether global group names should be preserved (<see langword="true"/>) or redacted (<see langword="false"/>).</param>
    /// <returns>The computed <see cref="PermissionAuditBreakdown"/>.</returns>
    private static PermissionAuditBreakdown BuildWorkspaceBreakdown(
        PermissionDescriptor descriptor,
        IReadOnlyList<GroupWithGrants> globalGroups,
        bool viewerIsRootOrGlobalAdmin)
    {
        List<PermissionContribution> contributions = [];
        bool hasGlobalDeny = false;
        bool hasGlobalAllow = false;
        string? firstGlobalDenyName = null;
        string? firstGlobalAllowName = null;

        foreach (GroupWithGrants entry in globalGroups)
        {
            foreach (PermissionGrant grant in entry.Grants)
            {
                if (!MatchesPermissionKey(grant.PermissionKey, descriptor.Key)
                    || grant.State == PermissionState.Inherit)
                {
                    continue;
                }

                string sourceName = viewerIsRootOrGlobalAdmin ? entry.Group.Name : _redactedGlobalGroupName;
                Guid? groupId = viewerIsRootOrGlobalAdmin ? entry.Group.Id : null;
                string displayState = grant.State == PermissionState.Allow ? "Allow" : "Deny";

                contributions.Add(new(
                    SourceName: sourceName,
                    State: grant.State,
                    DisplayState: displayState,
                    IsGlobalTier: true,
                    IsDefaultState: false,
                    GroupId: groupId));

                if (grant.State == PermissionState.Deny)
                {
                    hasGlobalDeny = true;
                    firstGlobalDenyName ??= sourceName;
                }
                else if (grant.State == PermissionState.Allow)
                {
                    hasGlobalAllow = true;
                    firstGlobalAllowName ??= sourceName;
                }
            }
        }

        (PermissionState defaultState, string defaultStateDisplay, PermissionContribution defaultContribution) = BuildDefaultStateContribution(descriptor);
        contributions.Add(defaultContribution);

        if (hasGlobalDeny)
        {
            string denyReason = viewerIsRootOrGlobalAdmin && !string.IsNullOrWhiteSpace(firstGlobalDenyName)
                ? $"Explicitly denied by Tier 1 global group '{firstGlobalDenyName}'."
                : "Explicitly denied by Global Permissions.";
            return new(
                Descriptor: descriptor,
                IsAllowed: false,
                EffectiveState: PermissionState.Deny,
                DeterminedByTier: PermissionDeterminingTier.GlobalTier1,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.GlobalTier1),
                SummaryReason: denyReason,
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        if (hasGlobalAllow)
        {
            string allowReason = viewerIsRootOrGlobalAdmin && !string.IsNullOrWhiteSpace(firstGlobalAllowName)
                ? $"Allowed by Tier 1 global group '{firstGlobalAllowName}'."
                : "Allowed by Global Permissions.";
            return new(
                Descriptor: descriptor,
                IsAllowed: true,
                EffectiveState: PermissionState.Allow,
                DeterminedByTier: PermissionDeterminingTier.GlobalTier1,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.GlobalTier1),
                SummaryReason: allowReason,
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        if (descriptor.IsGloballyGated)
        {
            return new(
                Descriptor: descriptor,
                IsAllowed: false,
                EffectiveState: PermissionState.Deny,
                DeterminedByTier: PermissionDeterminingTier.DefaultState,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.DefaultState),
                SummaryReason: "Globally gated feature is not enabled by any assigned global group (Implicit Deny).",
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        bool allowedByDefault = defaultState == PermissionState.Allow;
        return new(
            Descriptor: descriptor,
            IsAllowed: allowedByDefault,
            EffectiveState: defaultState,
            DeterminedByTier: PermissionDeterminingTier.DefaultState,
            DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.DefaultState),
            SummaryReason: allowedByDefault
                ? "Not restricted by Tier 1 global groups; allowed by default state."
                : "Not restricted by Tier 1 global groups; governed by Tier 2 workspace groups (Default State: Implicit Deny).",
            DefaultState: defaultState,
            DefaultStateDisplay: defaultStateDisplay,
            Contributions: contributions);
    }

    /// <summary>
    /// Builds a two-tier user-level <see cref="PermissionAuditBreakdown"/> for a single permission descriptor and actor.
    /// </summary>
    /// <param name="descriptor">The permission descriptor.</param>
    /// <param name="actor">The target actor identity.</param>
    /// <param name="workspace">The workspace entity, if found.</param>
    /// <param name="effectiveDesignations">The actor's combined live and roster designations.</param>
    /// <param name="userGroupIds">The custom workspace group identifiers the actor belongs to.</param>
    /// <param name="globalGroups">The assigned Tier 1 global groups and their grants.</param>
    /// <param name="workspaceGroups">The Tier 2 workspace groups and their grants.</param>
    /// <param name="userOverrides">The per-user permission overrides for the actor.</param>
    /// <param name="viewerIsRootOrGlobalAdmin">Whether global group names should be preserved (<see langword="true"/>) or redacted (<see langword="false"/>).</param>
    /// <returns>The computed <see cref="PermissionAuditBreakdown"/>.</returns>
    private static PermissionAuditBreakdown BuildUserBreakdown(
        PermissionDescriptor descriptor,
        PlatformUserIdentity actor,
        Workspace? workspace,
        PlatformUserDesignations effectiveDesignations,
        IReadOnlyList<Guid> userGroupIds,
        IReadOnlyList<GroupWithGrants> globalGroups,
        IReadOnlyList<GroupWithGrants> workspaceGroups,
        IReadOnlyList<UserPermissionOverride> userOverrides,
        bool viewerIsRootOrGlobalAdmin)
    {
        List<PermissionContribution> contributions = [];
        bool hasGlobalDeny = false;
        bool hasGlobalAllow = false;
        string? firstGlobalDenyName = null;
        string? firstGlobalAllowName = null;

        foreach (GroupWithGrants entry in globalGroups)
        {
            foreach (PermissionGrant grant in entry.Grants)
            {
                if (!MatchesPermissionKey(grant.PermissionKey, descriptor.Key)
                    || grant.State == PermissionState.Inherit)
                {
                    continue;
                }

                string sourceName = viewerIsRootOrGlobalAdmin ? entry.Group.Name : _redactedGlobalGroupName;
                Guid? groupId = viewerIsRootOrGlobalAdmin ? entry.Group.Id : null;
                string displayState = grant.State == PermissionState.Allow ? "Allow" : "Deny";

                contributions.Add(new(
                    SourceName: sourceName,
                    State: grant.State,
                    DisplayState: displayState,
                    IsGlobalTier: true,
                    IsDefaultState: false,
                    GroupId: groupId));

                if (grant.State == PermissionState.Deny)
                {
                    hasGlobalDeny = true;
                    firstGlobalDenyName ??= sourceName;
                }
                else if (grant.State == PermissionState.Allow)
                {
                    hasGlobalAllow = true;
                    firstGlobalAllowName ??= sourceName;
                }
            }
        }

        bool hasWorkspaceDeny = false;
        bool hasWorkspaceAllow = false;
        string? firstWorkspaceDenyName = null;
        string? firstWorkspaceAllowName = null;

        foreach (GroupWithGrants entry in workspaceGroups)
        {
            if (!MatchesGroup(entry.Group, effectiveDesignations, userGroupIds, actor.OidcGroupClaims))
            {
                continue;
            }

            foreach (PermissionGrant grant in entry.Grants)
            {
                if (!MatchesPermissionKey(grant.PermissionKey, descriptor.Key)
                    || grant.State == PermissionState.Inherit)
                {
                    continue;
                }

                string displayState = grant.State == PermissionState.Allow ? "Allow" : "Deny";
                contributions.Add(new(
                    SourceName: entry.Group.Name,
                    State: grant.State,
                    DisplayState: displayState,
                    IsGlobalTier: false,
                    IsDefaultState: false,
                    GroupId: entry.Group.Id));

                if (grant.State == PermissionState.Deny)
                {
                    hasWorkspaceDeny = true;
                    firstWorkspaceDenyName ??= entry.Group.Name;
                }
                else if (grant.State == PermissionState.Allow)
                {
                    hasWorkspaceAllow = true;
                    firstWorkspaceAllowName ??= entry.Group.Name;
                }
            }
        }

        foreach (UserPermissionOverride userOverride in userOverrides)
        {
            if (!MatchesPermissionKey(userOverride.PermissionKey, descriptor.Key)
                || userOverride.State == PermissionState.Inherit)
            {
                continue;
            }

            const string overrideSourceName = "User Override";
            string displayState = userOverride.State == PermissionState.Allow ? "Allow" : "Deny";
            contributions.Add(new(
                SourceName: overrideSourceName,
                State: userOverride.State,
                DisplayState: displayState,
                IsGlobalTier: false,
                IsDefaultState: false,
                GroupId: null));

            if (userOverride.State == PermissionState.Deny)
            {
                hasWorkspaceDeny = true;
                firstWorkspaceDenyName ??= overrideSourceName;
            }
            else if (userOverride.State == PermissionState.Allow)
            {
                hasWorkspaceAllow = true;
                firstWorkspaceAllowName ??= overrideSourceName;
            }
        }

        (PermissionState defaultState, string defaultStateDisplay, PermissionContribution defaultContribution) = BuildDefaultStateContribution(descriptor);
        contributions.Add(defaultContribution);

        // 1. Root or Global Admin bypass.
        if (actor.IsRoot || actor.IsGlobalAdmin)
        {
            return new(
                Descriptor: descriptor,
                IsAllowed: true,
                EffectiveState: PermissionState.Allow,
                DeterminedByTier: PermissionDeterminingTier.RootOrOwnerBypass,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.RootOrOwnerBypass),
                SummaryReason: "Root or global administrator bypass.",
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        // 2. Tier 1 Global Deny always wins.
        if (hasGlobalDeny)
        {
            string denyReason = viewerIsRootOrGlobalAdmin && !string.IsNullOrWhiteSpace(firstGlobalDenyName)
                ? $"Denied by Tier 1 global group '{firstGlobalDenyName}'."
                : "Denied by Global Permissions.";
            return new(
                Descriptor: descriptor,
                IsAllowed: false,
                EffectiveState: PermissionState.Deny,
                DeterminedByTier: PermissionDeterminingTier.GlobalTier1,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.GlobalTier1),
                SummaryReason: denyReason,
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        // 3. Globally gated feature without Tier 1 Global Allow.
        if (descriptor.IsGloballyGated && !hasGlobalAllow)
        {
            return new(
                Descriptor: descriptor,
                IsAllowed: false,
                EffectiveState: PermissionState.Deny,
                DeterminedByTier: PermissionDeterminingTier.DefaultState,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.DefaultState),
                SummaryReason: "Globally gated permission is not allowed by any assigned global group (Implicit Deny).",
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        // 4. Workspace Owner or Broadcaster bypass (unless Tier 2 explicitly granted via Broadcaster group, or owner bypass).
        bool isOwnerOrBroadcaster = effectiveDesignations.HasFlag(PlatformUserDesignations.Broadcaster)
            || (workspace is not null && actor.PanelUserId.HasValue && workspace.OwnerUserId == actor.PanelUserId.Value);

        if (isOwnerOrBroadcaster)
        {
            if (descriptor.IsGloballyGated && hasGlobalAllow && !hasWorkspaceAllow)
            {
                string globalAllowReason = viewerIsRootOrGlobalAdmin && !string.IsNullOrWhiteSpace(firstGlobalAllowName)
                    ? $"Allowed by Tier 1 global group '{firstGlobalAllowName}' for workspace owner/broadcaster."
                    : "Allowed by Global Permissions for workspace owner/broadcaster.";
                return new(
                    Descriptor: descriptor,
                    IsAllowed: true,
                    EffectiveState: PermissionState.Allow,
                    DeterminedByTier: PermissionDeterminingTier.GlobalTier1,
                    DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.GlobalTier1),
                    SummaryReason: globalAllowReason,
                    DefaultState: defaultState,
                    DefaultStateDisplay: defaultStateDisplay,
                    Contributions: contributions);
            }

            return hasWorkspaceAllow && !hasWorkspaceDeny
                ? new(
                    Descriptor: descriptor,
                    IsAllowed: true,
                    EffectiveState: PermissionState.Allow,
                    DeterminedByTier: PermissionDeterminingTier.WorkspaceTier2,
                    DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.WorkspaceTier2),
                    SummaryReason: $"Allowed by workspace group '{firstWorkspaceAllowName}'.",
                    DefaultState: defaultState,
                    DefaultStateDisplay: defaultStateDisplay,
                    Contributions: contributions)
                : new(
                Descriptor: descriptor,
                IsAllowed: true,
                EffectiveState: PermissionState.Allow,
                DeterminedByTier: PermissionDeterminingTier.RootOrOwnerBypass,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.RootOrOwnerBypass),
                SummaryReason: "Workspace owner or broadcaster bypass.",
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        // 5. Tier 2 Workspace Deny.
        if (hasWorkspaceDeny)
        {
            return new(
                Descriptor: descriptor,
                IsAllowed: false,
                EffectiveState: PermissionState.Deny,
                DeterminedByTier: PermissionDeterminingTier.WorkspaceTier2,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.WorkspaceTier2),
                SummaryReason: $"Denied by workspace group or override '{firstWorkspaceDenyName}'.",
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        // 6. Tier 2 Workspace Allow.
        if (hasWorkspaceAllow)
        {
            return new(
                Descriptor: descriptor,
                IsAllowed: true,
                EffectiveState: PermissionState.Allow,
                DeterminedByTier: PermissionDeterminingTier.WorkspaceTier2,
                DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.WorkspaceTier2),
                SummaryReason: $"Allowed by workspace group or override '{firstWorkspaceAllowName}'.",
                DefaultState: defaultState,
                DefaultStateDisplay: defaultStateDisplay,
                Contributions: contributions);
        }

        // 7. No Tier 2 group or override granted Allow -> falls back to Default State.
        return new(
            Descriptor: descriptor,
            IsAllowed: false,
            EffectiveState: PermissionState.Inherit,
            DeterminedByTier: PermissionDeterminingTier.DefaultState,
            DeterminedByDisplay: GetTierDisplay(PermissionDeterminingTier.DefaultState),
            SummaryReason: "No explicit allow grant found; denied by default state (Implicit Deny).",
            DefaultState: defaultState,
            DefaultStateDisplay: defaultStateDisplay,
            Contributions: contributions);
    }

    /// <summary>
    /// Builds the mandatory Default State <see cref="PermissionContribution"/> and display strings for a permission descriptor.
    /// </summary>
    /// <param name="descriptor">The permission descriptor.</param>
    /// <returns>A tuple containing the baseline <see cref="PermissionState"/>, display label, and <see cref="PermissionContribution"/>.</returns>
    private static (PermissionState DefaultState, string DefaultStateDisplay, PermissionContribution Contribution) BuildDefaultStateContribution(
        PermissionDescriptor descriptor)
    {
        PermissionState defaultState = descriptor.DefaultState == PermissionState.Allow
            ? PermissionState.Allow
            : PermissionState.Inherit;

        string display = defaultState == PermissionState.Allow
            ? _defaultAllowDisplay
            : _implicitDenyDefaultDisplay;

        PermissionContribution contribution = new(
            SourceName: _defaultStateSourceName,
            State: defaultState,
            DisplayState: display,
            IsGlobalTier: descriptor.IsGloballyGated,
            IsDefaultState: true,
            GroupId: null);

        return (defaultState, display, contribution);
    }

    /// <summary>
    /// Formats a human-readable display string for a <see cref="PermissionDeterminingTier"/>.
    /// </summary>
    /// <param name="tier">The determining tier.</param>
    /// <returns>The human-readable tier label.</returns>
    private static string GetTierDisplay(PermissionDeterminingTier tier) => tier switch
    {
        PermissionDeterminingTier.GlobalTier1 => "Global Permissions (Tier 1)",
        PermissionDeterminingTier.WorkspaceTier2 => "Workspace Permissions (Tier 2)",
        PermissionDeterminingTier.RootOrOwnerBypass => "Root / Workspace Owner Bypass",
        PermissionDeterminingTier.DefaultState => "Default State",
        _ => "Default State",
    };

    /// <summary>
    /// Determines whether a stored grant or override permission key matches a requested permission key.
    /// </summary>
    /// <param name="storedKey">The permission key stored on a grant or override.</param>
    /// <param name="requestedKey">The permission key being evaluated or explained.</param>
    /// <returns><see langword="true"/> if the keys match; otherwise, <see langword="false"/>.</returns>
    private static bool MatchesPermissionKey(string storedKey, string requestedKey) => string.Equals(storedKey, requestedKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves a <see cref="PermissionDescriptor"/> from the registry or constructs a fallback descriptor for ad-hoc keys.
    /// </summary>
    /// <param name="permissionKey">The permission key.</param>
    /// <returns>The resolved or synthesized <see cref="PermissionDescriptor"/>.</returns>
    private PermissionDescriptor ResolveDescriptor(string permissionKey)
    {
        string trimmed = permissionKey.Trim();
        PermissionDescriptor? registered = this._registry.GetByKey(trimmed);
        if (registered is not null)
        {
            return registered;
        }

        bool isGloballyGated = trimmed.StartsWith("feature.", StringComparison.OrdinalIgnoreCase);
        return new(
            Key: trimmed,
            Category: isGloballyGated ? PermissionCategories.Features : PermissionCategories.Actions,
            DisplayName: trimmed,
            Description: trimmed,
            IsGloballyGated: isGloballyGated);
    }

    /// <summary>
    /// Builds the cache tag for a specific workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>The workspace cache tag.</returns>
    private static string GetWorkspaceCacheTag(Guid workspaceId) => $"perm:ws:{workspaceId:N}";

    /// <summary>
    /// Determines whether a permission group applies to an actor based on group membership, mapped designation, or mapped OIDC group claim.
    /// </summary>
    /// <param name="group">The permission group to test.</param>
    /// <param name="effectiveDesignations">The actor's combined platform designations.</param>
    /// <param name="userGroupIds">The identifiers of custom groups the actor explicitly belongs to.</param>
    /// <param name="oidcGroupClaims">The optional OIDC group claim values from the actor's session.</param>
    /// <returns><see langword="true"/> if the group matches the actor; otherwise, <see langword="false"/>.</returns>
    private static bool MatchesGroup(
        PermissionGroup group,
        PlatformUserDesignations effectiveDesignations,
        IReadOnlyList<Guid> userGroupIds,
        IReadOnlyCollection<string>? oidcGroupClaims)
    {
        if (userGroupIds.Contains(group.Id))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(group.MappedPlatformDesignation)
            && (string.Equals(group.MappedPlatformDesignation, _everyoneDesignation, StringComparison.OrdinalIgnoreCase)
                || (Enum.TryParse<PlatformUserDesignations>(group.MappedPlatformDesignation, ignoreCase: true, out PlatformUserDesignations mappedDesignation)
                    && mappedDesignation != PlatformUserDesignations.None
                    && effectiveDesignations.HasFlag(mappedDesignation))))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(group.MappedOidcGroupClaim) && oidcGroupClaims is not null)
        {
            string expectedClaim = group.MappedOidcGroupClaim.Trim();
            foreach (string claim in oidcGroupClaims)
            {
                if (!string.IsNullOrWhiteSpace(claim)
                    && string.Equals(claim.Trim(), expectedClaim, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

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
