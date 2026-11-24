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
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Scripting;

namespace StreamActions.Database.Services;

/// <summary>
/// Tracks dynamic command permission keys across both <see cref="CustomCommand"/> and <see cref="WorkspaceScript"/> records,
/// registering descriptors and deleting orphaned workspace permission grants when zero references remain.
/// </summary>
public sealed class WorkspaceCommandPermissionTracker : IWorkspaceCommandPermissionTracker
{
    /// <summary>
    /// Pooled EF Core context factory.
    /// </summary>
    private readonly IDbContextFactory<StreamActionsDbContext> _dbContextFactory;

    /// <summary>
    /// Permission persistence store.
    /// </summary>
    private readonly IPermissionStore _permissionStore;

    /// <summary>
    /// Dynamic permission descriptor registry.
    /// </summary>
    private readonly IPermissionRegistry _permissionRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceCommandPermissionTracker"/> class.
    /// </summary>
    /// <param name="dbContextFactory">EF Core context factory.</param>
    /// <param name="permissionStore">Permission persistence store.</param>
    /// <param name="permissionRegistry">Permission descriptor registry.</param>
    public WorkspaceCommandPermissionTracker(
        IDbContextFactory<StreamActionsDbContext> dbContextFactory,
        IPermissionStore permissionStore,
        IPermissionRegistry permissionRegistry)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(permissionStore);
        ArgumentNullException.ThrowIfNull(permissionRegistry);

        this._dbContextFactory = dbContextFactory;
        this._permissionStore = permissionStore;
        this._permissionRegistry = permissionRegistry;
    }

    /// <inheritdoc/>
    public async ValueTask SyncScriptCommandPermissionsAsync(
        Guid workspaceId,
        IEnumerable<string> previousTriggers,
        IEnumerable<ScriptCommandRegistration> currentRegistrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previousTriggers);
        ArgumentNullException.ThrowIfNull(currentRegistrations);

        HashSet<string> currentSet = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<PermissionGroup> workspaceGroups = await this._permissionStore
            .GetGroupsAsync(workspaceId, cancellationToken)
            .ConfigureAwait(false);

        foreach (ScriptCommandRegistration registration in currentRegistrations)
        {
            string normalizedTrigger = BuiltInCommandRegistry.NormalizeTrigger(registration.Trigger);
            if (string.IsNullOrEmpty(normalizedTrigger))
            {
                continue;
            }

            _ = currentSet.Add(normalizedTrigger);
            string permissionKey = BuildCommandPermissionKey(normalizedTrigger);

            if (this._permissionRegistry.GetByKey(permissionKey) is null)
            {
                this._permissionRegistry.Register(new PermissionDescriptor(
                    Key: permissionKey,
                    Category: "Commands",
                    DisplayName: $"!{normalizedTrigger}",
                    Description: $"Allows invoking the !{normalizedTrigger} command.",
                    IsGloballyGated: false,
                    IsDangerousGlobalFeature: false,
                    DefaultAllowedDesignations: registration.DefaultAllowedDesignations));
            }

            await this.SeedInitialCommandGrantsIfUnconfiguredAsync(
                workspaceGroups,
                permissionKey,
                registration.DefaultAllowedDesignations,
                cancellationToken).ConfigureAwait(false);
        }

        List<string> removedTriggers = [];
        foreach (string previousTrigger in previousTriggers)
        {
            string normalized = BuiltInCommandRegistry.NormalizeTrigger(previousTrigger);
            if (!string.IsNullOrEmpty(normalized) && !currentSet.Contains(normalized))
            {
                removedTriggers.Add(normalized);
            }
        }

        if (removedTriggers.Count > 0)
        {
            _ = await this.CleanupOrphanedCommandPermissionsAsync(workspaceId, removedTriggers, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>> CleanupOrphanedCommandPermissionsAsync(
        Guid workspaceId,
        IEnumerable<string> candidateTriggers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateTriggers);

        List<string> normalizedCandidates = [.. candidateTriggers
            .Select(static t => BuiltInCommandRegistry.NormalizeTrigger(t))
            .Where(static t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        if (normalizedCandidates.Count == 0)
        {
            return [];
        }

        HashSet<string> activeWorkspaceTriggers = await this.GetActiveCommandTriggersInWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        List<string> removedKeys = [];

        foreach (string trigger in normalizedCandidates)
        {
            if (activeWorkspaceTriggers.Contains(trigger))
            {
                continue;
            }

            string permissionKey = BuildCommandPermissionKey(trigger);
            await this._permissionStore
                .DeleteWorkspacePermissionKeyAsync(workspaceId, permissionKey, cancellationToken)
                .ConfigureAwait(false);

            bool referencedElsewhere = await this.IsTriggerReferencedInAnyWorkspaceAsync(trigger, cancellationToken).ConfigureAwait(false);
            if (!referencedElsewhere)
            {
                _ = this._permissionRegistry.Unregister(permissionKey);
            }

            removedKeys.Add(permissionKey);
        }

        return removedKeys;
    }

    /// <summary>
    /// Builds the canonical permission key (<c>command.&lt;trigger&gt;</c>) for a normalized trigger.
    /// </summary>
    internal static string BuildCommandPermissionKey(string normalizedTrigger) => $"command.{normalizedTrigger}";

    /// <summary>
    /// Seeds initial <see cref="PermissionState.Allow"/> grants on matching built-in designation groups only if no group in the workspace already has an explicit grant for <paramref name="permissionKey"/>.
    /// </summary>
    private async ValueTask SeedInitialCommandGrantsIfUnconfiguredAsync(
        IReadOnlyList<PermissionGroup> workspaceGroups,
        string permissionKey,
        PlatformUserDesignations defaultDesignations,
        CancellationToken cancellationToken)
    {
        if (workspaceGroups.Count == 0 || defaultDesignations == PlatformUserDesignations.None)
        {
            return;
        }

        foreach (PermissionGroup group in workspaceGroups)
        {
            IReadOnlyList<PermissionGrant> existingGrants = await this._permissionStore
                .GetGrantsAsync(group.Id, cancellationToken)
                .ConfigureAwait(false);

            if (existingGrants.Any(g => string.Equals(g.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
        }

        foreach (PermissionGroup group in workspaceGroups)
        {
            if (string.IsNullOrWhiteSpace(group.MappedPlatformDesignation)
                || !Enum.TryParse<PlatformUserDesignations>(group.MappedPlatformDesignation, ignoreCase: true, out PlatformUserDesignations flag)
                || flag == PlatformUserDesignations.None
                || !defaultDesignations.HasFlag(flag))
            {
                continue;
            }

            await this._permissionStore
                .SetGrantAsync(group.Id, permissionKey, PermissionState.Allow, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Collects all normalized command triggers referenced by either <see cref="CustomCommand"/>s or <see cref="WorkspaceScript"/>s in <paramref name="workspaceId"/>.
    /// </summary>
    private async ValueTask<HashSet<string>> GetActiveCommandTriggersInWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        HashSet<string> triggers = new(StringComparer.OrdinalIgnoreCase);

        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<string> customTriggers = await db.CustomCommands
                .Where(x => x.WorkspaceId == workspaceId)
                .Select(static x => x.Trigger)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (string rawTrigger in customTriggers)
            {
                string normalized = BuiltInCommandRegistry.NormalizeTrigger(rawTrigger);
                if (!string.IsNullOrEmpty(normalized))
                {
                    _ = triggers.Add(normalized);
                }
            }

            List<List<ScriptCommandRegistration>> scriptCommandLists = await db.WorkspaceScripts
                .Where(x => x.WorkspaceId == workspaceId)
                .Select(static x => x.RegisteredCommands)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (List<ScriptCommandRegistration> list in scriptCommandLists)
            {
                foreach (ScriptCommandRegistration cmd in list)
                {
                    string normalized = BuiltInCommandRegistry.NormalizeTrigger(cmd.Trigger);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        _ = triggers.Add(normalized);
                    }
                }
            }
        }

        return triggers;
    }

    /// <summary>
    /// Checks whether any workspace across the instance still declares <paramref name="normalizedTrigger"/>.
    /// </summary>
    private async ValueTask<bool> IsTriggerReferencedInAnyWorkspaceAsync(string normalizedTrigger, CancellationToken cancellationToken)
    {
        StreamActionsDbContext db = await this._dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            List<string> allCustomTriggers = await db.CustomCommands
                .Select(static x => x.Trigger)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (allCustomTriggers.Any(t => string.Equals(BuiltInCommandRegistry.NormalizeTrigger(t), normalizedTrigger, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            List<List<ScriptCommandRegistration>> allScriptLists = await db.WorkspaceScripts
                .Select(static x => x.RegisteredCommands)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (List<ScriptCommandRegistration> list in allScriptLists)
            {
                if (list.Any(cmd => string.Equals(BuiltInCommandRegistry.NormalizeTrigger(cmd.Trigger), normalizedTrigger, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

