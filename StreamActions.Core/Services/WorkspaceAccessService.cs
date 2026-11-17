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

using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;

namespace StreamActions.Core.Services;

/// <summary>
/// Evaluates workspace visibility and permissions for a <see cref="PanelUser"/> across all linked login identities and OIDC groups.
/// </summary>
public sealed class WorkspaceAccessService : IWorkspaceAccessService
{
    /// <summary>
    /// Provider prefix identifying external OIDC login identities.
    /// </summary>
    private const string _oidcIdentityPrefix = "oidc:";

    /// <summary>
    /// Synthetic platform identifier used for OIDC-backed user permission evaluation.
    /// </summary>
    private const string _syntheticOidcPlatformId = "oidc";

    /// <summary>
    /// The workspace persistence store.
    /// </summary>
    private readonly IWorkspaceStore _workspaceStore;

    /// <summary>
    /// The permission evaluation service.
    /// </summary>
    private readonly IPermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceAccessService"/> class.
    /// </summary>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    public WorkspaceAccessService(
        IWorkspaceStore workspaceStore,
        IPermissionService permissionService)
    {
        ArgumentNullException.ThrowIfNull(workspaceStore);
        ArgumentNullException.ThrowIfNull(permissionService);

        this._workspaceStore = workspaceStore;
        this._permissionService = permissionService;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AccessibleWorkspace>> GetAccessibleWorkspacesAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identities);

        IReadOnlyList<Workspace> workspaces = await this._workspaceStore.GetAllAsync(ct).ConfigureAwait(false);
        List<AccessibleWorkspace> accessible = [];

        foreach (Workspace workspace in workspaces)
        {
            bool allowed = await this.EvaluateWorkspacePermissionAsync(
                workspace,
                user,
                identities,
                BuiltInPermissionKeys.UiPanelAccess,
                oidcGroupClaims,
                ct).ConfigureAwait(false);

            if (!allowed)
            {
                continue;
            }

            IReadOnlyList<WorkspacePlatform> platforms = await this._workspaceStore
                .GetPlatformsAsync(workspace.Id, ct)
                .ConfigureAwait(false);

            accessible.Add(new AccessibleWorkspace(
                workspace,
                workspace.OwnerUserId == user.Id,
                platforms));
        }

        return accessible;
    }

    /// <inheritdoc />
    public ValueTask<bool> CanAccessWorkspaceAsync(
        Guid workspaceId,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        CancellationToken ct = default) =>
        this.HasPermissionInWorkspaceAsync(
            workspaceId,
            user,
            identities,
            BuiltInPermissionKeys.UiPanelAccess,
            oidcGroupClaims,
            ct);

    /// <inheritdoc />
    public async ValueTask<bool> HasPermissionInWorkspaceAsync(
        Guid workspaceId,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        string permissionKey,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        if (workspaceId == Guid.Empty)
        {
            return false;
        }

        if (user.IsRoot || user.IsGlobalAdmin)
        {
            return true;
        }

        Workspace? workspace = await this._workspaceStore.GetByIdAsync(workspaceId, ct).ConfigureAwait(false);
        return workspace is not null
            && await this.EvaluateWorkspacePermissionAsync(
                workspace,
                user,
                identities,
                permissionKey,
                oidcGroupClaims,
                ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates a permission key for a <see cref="PanelUser"/> within a known <see cref="Workspace"/>.
    /// </summary>
    /// <param name="workspace">The target workspace.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The external login identities linked to <paramref name="user"/>.</param>
    /// <param name="permissionKey">The permission key to evaluate.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims from the user's current session.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if permitted; otherwise, <see langword="false"/>.</returns>
    private async ValueTask<bool> EvaluateWorkspacePermissionAsync(
        Workspace workspace,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        string permissionKey,
        IReadOnlyCollection<string>? oidcGroupClaims,
        CancellationToken ct)
    {
        if (user.IsRoot || user.IsGlobalAdmin)
        {
            return true;
        }

        if (workspace.OwnerUserId == user.Id)
        {
            return true;
        }

        List<PlatformUserIdentity> actors = BuildCandidateActors(user, identities, oidcGroupClaims);
        if (actors.Count == 0)
        {
            return false;
        }

        bool anyAllow = false;
        foreach (PlatformUserIdentity actor in actors)
        {
            PermissionEvaluationResult result = await this._permissionService
                .EvaluateAsync(workspace.Id, actor, permissionKey, ct)
                .ConfigureAwait(false);

            if (result.EffectiveState == PermissionState.Deny)
            {
                return false;
            }

            if (result.IsAllowed)
            {
                anyAllow = true;
            }
        }

        return anyAllow;
    }

    /// <summary>
    /// Builds candidate <see cref="PlatformUserIdentity"/> actors from the user's linked identities and optional OIDC claims.
    /// </summary>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The user's linked external login identities.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims from the current session.</param>
    /// <returns>The list of actors to evaluate.</returns>
    private static List<PlatformUserIdentity> BuildCandidateActors(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims)
    {
        List<PlatformUserIdentity> actors = new(identities.Count + 1);
        string syntheticOidcUserId = user.Id.ToString("N");
        bool hasOidcIdentity = false;
        bool hasSyntheticOidcIdentity = false;

        foreach (UserLoginIdentity identity in identities)
        {
            if (identity.ProviderId.StartsWith(_oidcIdentityPrefix, StringComparison.OrdinalIgnoreCase))
            {
                hasOidcIdentity = true;
            }

            if (string.Equals(identity.ProviderId, _syntheticOidcPlatformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(identity.ProviderUserId, syntheticOidcUserId, StringComparison.OrdinalIgnoreCase))
            {
                hasSyntheticOidcIdentity = true;
            }

            actors.Add(new PlatformUserIdentity(
                identity.ProviderId,
                identity.ProviderUserId,
                identity.ProviderUsername,
                PlatformUserDesignations.None,
                PanelUserId: user.Id,
                IsRoot: user.IsRoot,
                IsGlobalAdmin: user.IsGlobalAdmin,
                OidcGroupClaims: oidcGroupClaims));
        }

        if (!hasSyntheticOidcIdentity && (hasOidcIdentity || (oidcGroupClaims is { Count: > 0 })))
        {
            actors.Add(new PlatformUserIdentity(
                _syntheticOidcPlatformId,
                syntheticOidcUserId,
                user.DisplayName,
                PlatformUserDesignations.None,
                PanelUserId: user.Id,
                IsRoot: user.IsRoot,
                IsGlobalAdmin: user.IsGlobalAdmin,
                OidcGroupClaims: oidcGroupClaims));
        }

        return actors;
    }
}
