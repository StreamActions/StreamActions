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
    /// The optional instance settings service used for external OIDC and MFA policy evaluation.
    /// </summary>
    private readonly IInstanceSettingsService? _instanceSettingsService;

    /// <summary>
    /// The optional external OIDC provider store used to check workspace-scoped and instance-wide OIDC providers.
    /// </summary>
    private readonly IExternalOidcProviderStore? _oidcProviderStore;

    /// <summary>
    /// The optional permission persistence store used for workspace login MFA requirement evaluation.
    /// </summary>
    private readonly IPermissionStore? _permissionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceAccessService"/> class.
    /// </summary>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    /// <param name="instanceSettingsService">Optional instance settings service.</param>
    /// <param name="oidcProviderStore">Optional external OIDC provider store.</param>
    /// <param name="permissionStore">Optional permission persistence store.</param>
    public WorkspaceAccessService(
        IWorkspaceStore workspaceStore,
        IPermissionService permissionService,
        IInstanceSettingsService? instanceSettingsService = null,
        IExternalOidcProviderStore? oidcProviderStore = null,
        IPermissionStore? permissionStore = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceStore);
        ArgumentNullException.ThrowIfNull(permissionService);

        this._workspaceStore = workspaceStore;
        this._permissionService = permissionService;
        this._instanceSettingsService = instanceSettingsService;
        this._oidcProviderStore = oidcProviderStore;
        this._permissionStore = permissionStore;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AccessibleWorkspace>> GetAccessibleWorkspacesAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        string? authProvider = null,
        bool isMfaVerified = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identities);

        IReadOnlyList<Workspace> workspaces = await this._workspaceStore.GetAllAsync(ct).ConfigureAwait(false);
        InstanceSettings? settings = this._instanceSettingsService is not null
            ? await this._instanceSettingsService.GetAsync(ct).ConfigureAwait(false)
            : null;
        IReadOnlyList<ExternalOidcProvider>? instanceOidcProviders = null;
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

            (WorkspaceSessionAuthStatus authStatus, string? requiredOidcSlug, instanceOidcProviders) = await this
                .EvaluateWorkspaceAuthStatusAsync(
                    workspace,
                    user,
                    identities,
                    authProvider,
                    isMfaVerified,
                    settings,
                    instanceOidcProviders,
                    ct)
                .ConfigureAwait(false);

            accessible.Add(new AccessibleWorkspace(
                workspace,
                workspace.OwnerUserId == user.Id,
                platforms,
                authStatus,
                requiredOidcSlug));
        }

        return accessible;
    }

    /// <inheritdoc />
    public async ValueTask<bool> CanAccessWorkspaceAsync(
        Guid workspaceId,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        string? authProvider = null,
        bool isMfaVerified = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identities);

        if (workspaceId == Guid.Empty)
        {
            return false;
        }

        Workspace? workspace = await this._workspaceStore.GetByIdAsync(workspaceId, ct).ConfigureAwait(false);
        if (workspace is null)
        {
            return user.IsRoot || user.IsGlobalAdmin;
        }

        bool allowed = await this.EvaluateWorkspacePermissionAsync(
            workspace,
            user,
            identities,
            BuiltInPermissionKeys.UiPanelAccess,
            oidcGroupClaims,
            ct).ConfigureAwait(false);

        if (!allowed)
        {
            return false;
        }

        InstanceSettings? settings = this._instanceSettingsService is not null
            ? await this._instanceSettingsService.GetAsync(ct).ConfigureAwait(false)
            : null;

        (WorkspaceSessionAuthStatus authStatus, _, _) = await this
            .EvaluateWorkspaceAuthStatusAsync(
                workspace,
                user,
                identities,
                authProvider,
                isMfaVerified,
                settings,
                instanceOidcProviders: null,
                ct)
            .ConfigureAwait(false);

        return authStatus == WorkspaceSessionAuthStatus.Ready;
    }

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

    /// <summary>
    /// Evaluates external OIDC and MFA readiness requirements for a <see cref="PanelUser"/> accessing <paramref name="workspace"/>.
    /// </summary>
    /// <param name="workspace">The target workspace.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The user's linked external login identities.</param>
    /// <param name="authProvider">The authentication provider identifier for the current session.</param>
    /// <param name="isMfaVerified"><see langword="true"/> if the current session has completed MFA verification.</param>
    /// <param name="instanceSettings">The loaded instance settings, if available.</param>
    /// <param name="instanceOidcProviders">Cached enabled instance-wide OIDC providers, if already loaded.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The resulting <see cref="WorkspaceSessionAuthStatus"/>, optional required OIDC provider slug, and cached instance OIDC providers.</returns>
    private async ValueTask<(WorkspaceSessionAuthStatus AuthStatus, string? RequiredOidcProviderSlug, IReadOnlyList<ExternalOidcProvider>? InstanceOidcProviders)> EvaluateWorkspaceAuthStatusAsync(
        Workspace workspace,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        string? authProvider,
        bool isMfaVerified,
        InstanceSettings? instanceSettings,
        IReadOnlyList<ExternalOidcProvider>? instanceOidcProviders,
        CancellationToken ct)
    {
        if (instanceSettings is not null
            && !user.IsRoot
            && !user.IsGlobalAdmin
            && instanceSettings.ExternalOidcLoginMode != ExternalOidcLoginMode.Disabled)
        {
            IReadOnlyList<ExternalOidcProvider> workspaceProviders = this._oidcProviderStore is not null
                ? await this._oidcProviderStore.GetProvidersAsync(workspace.Id, enabledOnly: true, ct).ConfigureAwait(false)
                : [];

            bool requiresExternalOidc = instanceSettings.ExternalOidcLoginMode == ExternalOidcLoginMode.Required
                || (instanceSettings.ExternalOidcLoginMode == ExternalOidcLoginMode.Allowed
                    && (workspace.RequireExternalOidcLogin || workspaceProviders.Count > 0));

            if (requiresExternalOidc)
            {
                string? sessionOidcSlug = ExtractOidcSlug(authProvider);
                if (workspaceProviders.Count > 0)
                {
                    bool matchesWorkspaceProvider = sessionOidcSlug is not null
                        && workspaceProviders.Any(p => string.Equals(p.Slug, sessionOidcSlug, StringComparison.OrdinalIgnoreCase));

                    if (!matchesWorkspaceProvider)
                    {
                        return (WorkspaceSessionAuthStatus.RequiresOidcReauth, workspaceProviders[0].Slug, instanceOidcProviders);
                    }
                }
                else if (sessionOidcSlug is null)
                {
                    if (instanceOidcProviders is null && this._oidcProviderStore is not null)
                    {
                        instanceOidcProviders = await this._oidcProviderStore
                            .GetProvidersAsync(workspaceId: null, enabledOnly: true, ct)
                            .ConfigureAwait(false);
                    }

                    string? fallbackSlug = instanceOidcProviders is { Count: > 0 }
                        ? instanceOidcProviders[0].Slug
                        : null;

                    return (WorkspaceSessionAuthStatus.RequiresOidcReauth, fallbackSlug, instanceOidcProviders);
                }
            }
        }

        if (!isMfaVerified && instanceSettings is not null && this._permissionStore is not null)
        {
            bool mfaRequired = await MfaPolicyService
                .IsWorkspaceLoginMfaRequiredAsync(instanceSettings, workspace, this._permissionStore, user, identities, ct)
                .ConfigureAwait(false);

            if (mfaRequired)
            {
                return (WorkspaceSessionAuthStatus.RequiresMfaChallenge, null, instanceOidcProviders);
            }
        }

        return (WorkspaceSessionAuthStatus.Ready, null, instanceOidcProviders);
    }

    /// <summary>
    /// Extracts the external OIDC provider slug from an <c>oidc:&lt;slug&gt;</c> authentication provider claim.
    /// </summary>
    /// <param name="authProvider">The raw authentication provider identifier.</param>
    /// <returns>The trimmed OIDC provider slug, or <see langword="null"/> if the session did not authenticate via external OIDC.</returns>
    private static string? ExtractOidcSlug(string? authProvider)
    {
        if (string.IsNullOrWhiteSpace(authProvider))
        {
            return null;
        }

        string trimmed = authProvider.Trim();
        if (!trimmed.StartsWith(_oidcIdentityPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string slug = trimmed[_oidcIdentityPrefix.Length..].Trim();
        return slug.Length == 0 ? null : slug;
    }
}
