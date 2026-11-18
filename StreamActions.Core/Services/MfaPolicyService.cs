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

namespace StreamActions.Core.Services;

/// <summary>
/// Evaluates two-tier (Instance + Workspace) multi-factor authentication policies where the strictest policy always wins.
/// </summary>
public sealed class MfaPolicyService : IMfaPolicyService
{
    /// <summary>
    /// The instance settings service.
    /// </summary>
    private readonly IInstanceSettingsService _instanceSettingsService;

    /// <summary>
    /// The workspace persistence store.
    /// </summary>
    private readonly IWorkspaceStore _workspaceStore;

    /// <summary>
    /// The permission persistence store.
    /// </summary>
    private readonly IPermissionStore _permissionStore;

    /// <summary>
    /// The workspace access evaluation service.
    /// </summary>
    private readonly IWorkspaceAccessService _workspaceAccessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfaPolicyService"/> class.
    /// </summary>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="workspaceStore">The workspace persistence store.</param>
    /// <param name="permissionStore">The permission persistence store.</param>
    /// <param name="workspaceAccessService">The workspace access evaluation service.</param>
    public MfaPolicyService(
        IInstanceSettingsService instanceSettingsService,
        IWorkspaceStore workspaceStore,
        IPermissionStore permissionStore,
        IWorkspaceAccessService workspaceAccessService)
    {
        ArgumentNullException.ThrowIfNull(instanceSettingsService);
        ArgumentNullException.ThrowIfNull(workspaceStore);
        ArgumentNullException.ThrowIfNull(permissionStore);
        ArgumentNullException.ThrowIfNull(workspaceAccessService);

        this._instanceSettingsService = instanceSettingsService;
        this._workspaceStore = workspaceStore;
        this._permissionStore = permissionStore;
        this._workspaceAccessService = workspaceAccessService;
    }

    /// <inheritdoc/>
    public EffectiveMfaPolicy ComputeEffectivePolicy(InstanceSettings instanceSettings, Workspace? workspace = null) =>
        CombinePolicies(instanceSettings, workspace);

    /// <summary>
    /// Evaluates whether login MFA is required for <paramref name="user"/> within a specific <paramref name="workspace"/> without invoking <see cref="IWorkspaceAccessService"/>.
    /// </summary>
    /// <param name="settings">The global instance settings.</param>
    /// <param name="workspace">The target workspace.</param>
    /// <param name="permissionStore">The permission persistence store.</param>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The external login identities linked to <paramref name="user"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if login MFA is required for <paramref name="user"/> in <paramref name="workspace"/>; otherwise, <see langword="false"/>.</returns>
    internal static ValueTask<bool> IsWorkspaceLoginMfaRequiredAsync(
        InstanceSettings settings,
        Workspace workspace,
        IPermissionStore permissionStore,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(permissionStore);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identities);

        EffectiveMfaPolicy explicitPolicy = CombinePolicies(settings, workspace);
        return EvaluateLoginRequirementAsync(
            explicitPolicy,
            permissionStore,
            user,
            identities,
            [workspace],
            cancellationToken);
    }

    /// <summary>
    /// Combines <paramref name="instanceSettings"/> and an optional <paramref name="workspace"/> into an <see cref="EffectiveMfaPolicy"/>.
    /// </summary>
    /// <param name="instanceSettings">The global instance settings.</param>
    /// <param name="workspace">An optional workspace whose stricter overrides apply.</param>
    /// <returns>The combined <see cref="EffectiveMfaPolicy"/>.</returns>
    private static EffectiveMfaPolicy CombinePolicies(InstanceSettings instanceSettings, Workspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(instanceSettings);

        MfaMethodAvailability passkeyAvailability = CombineWithInstance(
            instanceSettings.MfaPasskeyAvailability,
            workspace?.MfaPasskeyAvailability);

        MfaMethodAvailability totpAvailability = CombineWithInstance(
            instanceSettings.MfaTotpAvailability,
            workspace?.MfaTotpAvailability);

        PanelLoginMfaRequirement loginRequirement = workspace is null
            ? instanceSettings.LoginMfaRequirement
            : (PanelLoginMfaRequirement)Math.Max((int)instanceSettings.LoginMfaRequirement, (int)workspace.LoginMfaRequirement);

        StepUpSudoPolicy stepUpPolicy = workspace is null
            ? instanceSettings.StepUpSudoPolicy
            : (StepUpSudoPolicy)Math.Max((int)instanceSettings.StepUpSudoPolicy, (int)workspace.StepUpSudoPolicy);

        return new EffectiveMfaPolicy(passkeyAvailability, totpAvailability, loginRequirement, stepUpPolicy);
    }

    /// <inheritdoc/>
    public async ValueTask<EffectiveMfaPolicy> GetEffectivePolicyAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        Workspace? workspace = workspaceId.HasValue && workspaceId.Value != Guid.Empty
            ? await this._workspaceStore.GetByIdAsync(workspaceId.Value, cancellationToken).ConfigureAwait(false)
            : null;

        return this.ComputeEffectivePolicy(settings, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<EffectiveMfaPolicy> GetEffectivePolicyForUserAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identities);

        if (workspaceId.HasValue && workspaceId.Value != Guid.Empty)
        {
            return await this.GetEffectivePolicyAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        }

        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        EffectiveMfaPolicy effective = this.ComputeEffectivePolicy(settings, workspace: null);

        IReadOnlyList<Workspace> relevantWorkspaces = await this
            .GetRelevantWorkspacesAsync(user, identities, oidcGroupClaims, cancellationToken)
            .ConfigureAwait(false);

        foreach (Workspace workspace in relevantWorkspaces)
        {
            EffectiveMfaPolicy workspacePolicy = this.ComputeEffectivePolicy(settings, workspace);
            effective = CombineEffectivePolicies(effective, workspacePolicy);
        }

        return effective;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsLoginMfaRequiredAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identities);

        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        if (workspaceId.HasValue && workspaceId.Value != Guid.Empty)
        {
            Workspace? workspace = await this._workspaceStore.GetByIdAsync(workspaceId.Value, cancellationToken).ConfigureAwait(false);
            EffectiveMfaPolicy explicitPolicy = this.ComputeEffectivePolicy(settings, workspace);
            return await EvaluateLoginRequirementAsync(
                explicitPolicy,
                this._permissionStore,
                user,
                identities,
                workspace is null ? [] : [workspace],
                cancellationToken).ConfigureAwait(false);
        }

        EffectiveMfaPolicy instancePolicy = this.ComputeEffectivePolicy(settings, workspace: null);
        IReadOnlyList<Workspace> relevantWorkspaces = await this
            .GetRelevantWorkspacesAsync(user, identities, oidcGroupClaims, cancellationToken)
            .ConfigureAwait(false);

        if (await EvaluateLoginRequirementAsync(
            instancePolicy,
            this._permissionStore,
            user,
            identities,
            relevantWorkspaces,
            cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        foreach (Workspace workspace in relevantWorkspaces)
        {
            EffectiveMfaPolicy workspacePolicy = this.ComputeEffectivePolicy(settings, workspace);
            if (await EvaluateLoginRequirementAsync(
                workspacePolicy,
                this._permissionStore,
                user,
                identities,
                [workspace],
                cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Combines an instance-level method availability with an optional workspace-level setting, ensuring workspace overrides cannot weaken instance restrictions.
    /// </summary>
    /// <param name="instanceValue">The instance-level availability.</param>
    /// <param name="workspaceValue">The optional workspace-level availability.</param>
    /// <returns>The effective <see cref="MfaMethodAvailability"/>.</returns>
    private static MfaMethodAvailability CombineWithInstance(
        MfaMethodAvailability instanceValue,
        MfaMethodAvailability? workspaceValue) =>
        instanceValue == MfaMethodAvailability.Disabled
            ? MfaMethodAvailability.Disabled
            : instanceValue == MfaMethodAvailability.Required
            ? MfaMethodAvailability.Required
            : workspaceValue ?? MfaMethodAvailability.Allowed;

    /// <summary>
    /// Merges two effective policies across multiple relevant workspaces using strictest-wins precedence.
    /// </summary>
    /// <param name="left">The first effective policy.</param>
    /// <param name="right">The second effective policy.</param>
    /// <returns>The strictest combined <see cref="EffectiveMfaPolicy"/>.</returns>
    private static EffectiveMfaPolicy CombineEffectivePolicies(EffectiveMfaPolicy left, EffectiveMfaPolicy right) =>
        new(
            CombineSiblingMethodAvailability(left.PasskeyAvailability, right.PasskeyAvailability),
            CombineSiblingMethodAvailability(left.TotpAvailability, right.TotpAvailability),
            (PanelLoginMfaRequirement)Math.Max((int)left.LoginMfaRequirement, (int)right.LoginMfaRequirement),
            (StepUpSudoPolicy)Math.Max((int)left.StepUpSudoPolicy, (int)right.StepUpSudoPolicy));

    /// <summary>
    /// Combines two method availabilities across sibling workspaces where <see cref="MfaMethodAvailability.Disabled"/> and <see cref="MfaMethodAvailability.Required"/> supersede <see cref="MfaMethodAvailability.Allowed"/>.
    /// </summary>
    /// <param name="first">The first availability value.</param>
    /// <param name="second">The second availability value.</param>
    /// <returns>The strictest availability value.</returns>
    private static MfaMethodAvailability CombineSiblingMethodAvailability(
        MfaMethodAvailability first,
        MfaMethodAvailability second) =>
        first == MfaMethodAvailability.Disabled || second == MfaMethodAvailability.Disabled
            ? MfaMethodAvailability.Disabled
            : first == MfaMethodAvailability.Required || second == MfaMethodAvailability.Required
            ? MfaMethodAvailability.Required
            : MfaMethodAvailability.Allowed;

    /// <summary>
    /// Retrieves the workspaces owned by or accessible to <paramref name="user"/> without letting unrelated workspaces override root/global-admin settings.
    /// </summary>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The user's linked login identities.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The list of relevant <see cref="Workspace"/> entities.</returns>
    private async ValueTask<IReadOnlyList<Workspace>> GetRelevantWorkspacesAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims,
        CancellationToken cancellationToken)
    {
        if (user.IsRoot || user.IsGlobalAdmin)
        {
            return await this._workspaceStore.GetByOwnerAsync(user.Id, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<AccessibleWorkspace> accessible = await this._workspaceAccessService
            .GetAccessibleWorkspacesAsync(user, identities, oidcGroupClaims, ct: cancellationToken)
            .ConfigureAwait(false);

        return [.. accessible.Select(static aw => aw.Workspace)];
    }

    /// <summary>
    /// Evaluates whether a specific <see cref="EffectiveMfaPolicy"/> requires login MFA for <paramref name="user"/> across the supplied workspaces.
    /// </summary>
    /// <param name="policy">The effective MFA policy to evaluate.</param>
    /// <param name="permissionStore">The permission persistence store.</param>
    /// <param name="user">The panel user.</param>
    /// <param name="identities">The user's linked login identities.</param>
    /// <param name="workspaces">The workspaces in scope for role inspection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if login MFA is required; otherwise, <see langword="false"/>.</returns>
    private static async ValueTask<bool> EvaluateLoginRequirementAsync(
        EffectiveMfaPolicy policy,
        IPermissionStore permissionStore,
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyList<Workspace> workspaces,
        CancellationToken cancellationToken)
    {
        if (policy.IsMfaDisabled || policy.LoginMfaRequirement == PanelLoginMfaRequirement.Optional)
        {
            return false;
        }

        if (policy.LoginMfaRequirement == PanelLoginMfaRequirement.RequiredForAll)
        {
            return true;
        }

        bool isRootOrAdmin = user.IsRoot || user.IsGlobalAdmin;
        bool isWorkspaceOwner = workspaces.Any(w => w.OwnerUserId == user.Id);
        if (isRootOrAdmin || isWorkspaceOwner)
        {
            return true;
        }

        if (policy.LoginMfaRequirement == PanelLoginMfaRequirement.RequiredForRootAndStreamers)
        {
            return false;
        }

        foreach (Workspace workspace in workspaces)
        {
            if (await IsModeratorInWorkspaceAsync(permissionStore, workspace.Id, identities, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether any linked identity holds a moderator or broadcaster designation in the specified workspace.
    /// </summary>
    /// <param name="permissionStore">The permission persistence store.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="identities">The user's linked login identities.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the user has moderator or higher designation in the workspace; otherwise, <see langword="false"/>.</returns>
    private static async ValueTask<bool> IsModeratorInWorkspaceAsync(
        IPermissionStore permissionStore,
        Guid workspaceId,
        IReadOnlyList<UserLoginIdentity> identities,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionGroup>? groups = null;

        foreach (UserLoginIdentity identity in identities)
        {
            WorkspacePlatformRoleRoster? roster = await permissionStore
                .GetPlatformRoleRosterAsync(workspaceId, identity.ProviderId, identity.ProviderUserId, cancellationToken)
                .ConfigureAwait(false);

            if (roster is not null
                && (roster.Designations.HasFlag(PlatformUserDesignations.Moderator)
                    || roster.Designations.HasFlag(PlatformUserDesignations.Broadcaster)))
            {
                return true;
            }

            IReadOnlyList<Guid> groupIds = await permissionStore
                .GetUserGroupIdsAsync(workspaceId, identity.ProviderId, identity.ProviderUserId, cancellationToken)
                .ConfigureAwait(false);

            if (groupIds.Count == 0)
            {
                continue;
            }

            groups ??= await permissionStore.GetGroupsAsync(workspaceId, cancellationToken).ConfigureAwait(false);
            foreach (PermissionGroup group in groups)
            {
                if (!groupIds.Contains(group.Id))
                {
                    continue;
                }

                if (string.Equals(group.MappedPlatformDesignation, nameof(PlatformUserDesignations.Moderator), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(group.MappedPlatformDesignation, nameof(PlatformUserDesignations.Broadcaster), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
