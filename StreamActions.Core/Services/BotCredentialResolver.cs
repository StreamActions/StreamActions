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
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Services;

/// <summary>
/// Resolves chat bot credentials for a workspace platform using <see cref="InstanceBotAccountPolicy"/>,
/// <see cref="BuiltInPermissionKeys.FeatureCustomBotAccount"/>, <see cref="Workspace.AllowCustomBotAccountOverride"/>,
/// and <see cref="Models.Metadata.PlatformMetadata.UseBroadcasterAsBot"/>.
/// </summary>
public sealed class BotCredentialResolver : IBotCredentialResolver
{
    /// <summary>
    /// The instance settings service used to read <see cref="InstanceSettings.InstanceBotAccountPolicy"/>.
    /// </summary>
    private readonly IInstanceSettingsService _instanceSettingsService;

    /// <summary>
    /// The workspace store used to read workspace override flags and platform metadata.
    /// </summary>
    private readonly IWorkspaceStore _workspaceStore;

    /// <summary>
    /// The encrypted platform credential store.
    /// </summary>
    private readonly IPlatformCredentialStore _credentialStore;

    /// <summary>
    /// The permission service used to evaluate <see cref="BuiltInPermissionKeys.FeatureCustomBotAccount"/>.
    /// </summary>
    private readonly IPermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotCredentialResolver"/> class.
    /// </summary>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="workspaceStore">The workspace store.</param>
    /// <param name="credentialStore">The encrypted platform credential store.</param>
    /// <param name="permissionService">The permission evaluation service.</param>
    public BotCredentialResolver(
        IInstanceSettingsService instanceSettingsService,
        IWorkspaceStore workspaceStore,
        IPlatformCredentialStore credentialStore,
        IPermissionService permissionService)
    {
        ArgumentNullException.ThrowIfNull(instanceSettingsService);
        ArgumentNullException.ThrowIfNull(workspaceStore);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(permissionService);

        this._instanceSettingsService = instanceSettingsService;
        this._workspaceStore = workspaceStore;
        this._credentialStore = credentialStore;
        this._permissionService = permissionService;
    }

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    public async ValueTask<BotCredentialResolutionResult> ResolveAsync(
        Guid workspaceId,
        string platformId,
        PlatformUserIdentity? actor = null,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace identifier must not be empty.", nameof(workspaceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = platformId.Trim().ToLowerInvariant();
        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        InstanceBotAccountPolicy policy = AuthPolicyParser.ParseBotAccountPolicy(settings.InstanceBotAccountPolicy);

        if (policy == InstanceBotAccountPolicy.DefaultOnly)
        {
            return await this.ResolveInstanceDefaultAsync(
                normalizedPlatformId,
                policy,
                isCustomOverrideAllowed: false,
                cancellationToken).ConfigureAwait(false);
        }

        Workspace? workspace = await this._workspaceStore.GetByIdAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        WorkspacePlatform? workspacePlatform = await this._workspaceStore
            .GetPlatformAsync(workspaceId, normalizedPlatformId, cancellationToken)
            .ConfigureAwait(false);
        bool useBroadcasterAsBot = workspacePlatform?.Metadata.UseBroadcasterAsBot == true;

        if (policy == InstanceBotAccountPolicy.DefaultWithOptionalOverride)
        {
            bool customOverrideAllowed = await this.IsCustomBotOverrideAllowedAsync(
                workspaceId,
                normalizedPlatformId,
                workspace,
                actor,
                cancellationToken).ConfigureAwait(false);

            if (customOverrideAllowed)
            {
                BotCredentialResolutionResult? workspaceResolution = await this.TryResolveWorkspaceBotOrFallbackAsync(
                    workspaceId,
                    normalizedPlatformId,
                    policy,
                    customOverrideAllowed,
                    useBroadcasterAsBot,
                    cancellationToken).ConfigureAwait(false);

                if (workspaceResolution is not null)
                {
                    return workspaceResolution;
                }
            }

            return await this.ResolveInstanceDefaultAsync(
                normalizedPlatformId,
                policy,
                isCustomOverrideAllowed: customOverrideAllowed,
                cancellationToken).ConfigureAwait(false);
        }

        if (policy == InstanceBotAccountPolicy.RequireStreamerBotAccount)
        {
            BotCredentialResolutionResult? requiredResolution = await this.TryResolveWorkspaceBotOrFallbackAsync(
                workspaceId,
                normalizedPlatformId,
                policy,
                isCustomOverrideAllowed: true,
                useBroadcasterAsBot,
                cancellationToken).ConfigureAwait(false);

            return requiredResolution ?? new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.AwaitingBotAccountAuth,
                BotCredentialSource.None,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: true,
                CredentialWorkspaceId: workspaceId,
                CredentialTokenRole: TokenRole.BotChat,
                Reason: "Workspace requires a streamer bot account or broadcaster fallback authorization.");
        }

        throw new InvalidOperationException($"Unsupported bot account policy: {policy}.");
    }

    /// <summary>
    /// Determines whether the workspace is permitted to override the instance default bot account.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="normalizedPlatformId">The normalized platform identifier.</param>
    /// <param name="workspace">The workspace entity, if found.</param>
    /// <param name="actor">The optional actor identity for permission evaluation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if custom bot override is enabled and permitted; otherwise, <see langword="false"/>.</returns>
    private async ValueTask<bool> IsCustomBotOverrideAllowedAsync(
        Guid workspaceId,
        string normalizedPlatformId,
        Workspace? workspace,
        PlatformUserIdentity? actor,
        CancellationToken cancellationToken)
    {
        if (workspace is null || !workspace.AllowCustomBotAccountOverride)
        {
            return false;
        }

        PlatformUserIdentity effectiveActor = actor ?? new PlatformUserIdentity(
            normalizedPlatformId,
            "__workspace__",
            "workspace",
            PlatformUserDesignations.Broadcaster);

        return await this._permissionService
            .IsAllowedAsync(workspaceId, effectiveActor, BuiltInPermissionKeys.FeatureCustomBotAccount, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to resolve a workspace-scoped custom bot token or broadcaster fallback token.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="normalizedPlatformId">The normalized platform identifier.</param>
    /// <param name="policy">The active instance bot account policy.</param>
    /// <param name="isCustomOverrideAllowed">Whether custom override is allowed for this workspace.</param>
    /// <param name="useBroadcasterAsBot">Whether broadcaster fallback is enabled on the workspace platform.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A resolution result if a usable token or key-ring mismatch was found; otherwise, <see langword="null"/>.</returns>
    private async ValueTask<BotCredentialResolutionResult?> TryResolveWorkspaceBotOrFallbackAsync(
        Guid workspaceId,
        string normalizedPlatformId,
        InstanceBotAccountPolicy policy,
        bool isCustomOverrideAllowed,
        bool useBroadcasterAsBot,
        CancellationToken cancellationToken)
    {
        (bool botFound, CredentialStatus botStatus, PlatformTokenPayload? botPayload) = await this._credentialStore
            .TryGetTokenAsync(workspaceId, normalizedPlatformId, TokenRole.BotChat, cancellationToken)
            .ConfigureAwait(false);

        if (botFound && botStatus == CredentialStatus.Active && IsUsablePayload(botPayload))
        {
            return new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.Resolved,
                BotCredentialSource.WorkspaceCustomBot,
                botPayload,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: isCustomOverrideAllowed,
                CredentialWorkspaceId: workspaceId,
                CredentialTokenRole: TokenRole.BotChat,
                Reason: "Resolved workspace custom bot account credential.");
        }

        if (botFound && botStatus == CredentialStatus.KeyRingMismatchRequiresReauth)
        {
            return new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.KeyRingMismatchRequiresReauth,
                BotCredentialSource.None,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: isCustomOverrideAllowed,
                CredentialWorkspaceId: workspaceId,
                CredentialTokenRole: TokenRole.BotChat,
                Reason: "Workspace custom bot credential cannot be decrypted with the active key ring.");
        }

        if (!useBroadcasterAsBot)
        {
            return null;
        }

        (bool broadcasterFound, CredentialStatus broadcasterStatus, PlatformTokenPayload? broadcasterPayload) = await this._credentialStore
            .TryGetTokenAsync(workspaceId, normalizedPlatformId, TokenRole.Broadcaster, cancellationToken)
            .ConfigureAwait(false);

        return broadcasterFound && broadcasterStatus == CredentialStatus.Active && IsUsablePayload(broadcasterPayload)
            ? new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.Resolved,
                BotCredentialSource.WorkspaceBroadcasterFallback,
                broadcasterPayload,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: isCustomOverrideAllowed,
                CredentialWorkspaceId: workspaceId,
                CredentialTokenRole: TokenRole.Broadcaster,
                Reason: "Resolved workspace broadcaster credential as chat bot identity fallback.")
            : broadcasterFound && broadcasterStatus == CredentialStatus.KeyRingMismatchRequiresReauth
            ? new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.KeyRingMismatchRequiresReauth,
                BotCredentialSource.None,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: isCustomOverrideAllowed,
                CredentialWorkspaceId: workspaceId,
                CredentialTokenRole: TokenRole.Broadcaster,
                Reason: "Workspace broadcaster credential cannot be decrypted with the active key ring.")
            : null;
    }

    /// <summary>
    /// Resolves the instance-wide root default bot account credential.
    /// </summary>
    /// <param name="normalizedPlatformId">The normalized platform identifier.</param>
    /// <param name="policy">The active instance bot account policy.</param>
    /// <param name="isCustomOverrideAllowed">Whether custom override is allowed for this workspace.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolution result for the instance default bot account.</returns>
    private async ValueTask<BotCredentialResolutionResult> ResolveInstanceDefaultAsync(
        string normalizedPlatformId,
        InstanceBotAccountPolicy policy,
        bool isCustomOverrideAllowed,
        CancellationToken cancellationToken)
    {
        (bool found, CredentialStatus status, PlatformTokenPayload? payload) = await this._credentialStore
            .TryGetTokenAsync(workspaceId: null, normalizedPlatformId, TokenRole.BotChat, cancellationToken)
            .ConfigureAwait(false);

        return found && status == CredentialStatus.Active && IsUsablePayload(payload)
            ? new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.Resolved,
                BotCredentialSource.InstanceDefaultBot,
                payload,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: isCustomOverrideAllowed,
                CredentialWorkspaceId: null,
                CredentialTokenRole: TokenRole.BotChat,
                Reason: "Resolved instance default bot account credential.")
            : found && status == CredentialStatus.KeyRingMismatchRequiresReauth
            ? new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.KeyRingMismatchRequiresReauth,
                BotCredentialSource.None,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: isCustomOverrideAllowed,
                CredentialWorkspaceId: null,
                CredentialTokenRole: TokenRole.BotChat,
                Reason: "Instance default bot credential cannot be decrypted with the active key ring.")
            : new BotCredentialResolutionResult(
                BotCredentialResolutionStatus.AwaitingBotAccountAuth,
                BotCredentialSource.None,
                EffectivePolicy: policy,
                IsCustomOverrideAllowed: isCustomOverrideAllowed,
                CredentialWorkspaceId: null,
                CredentialTokenRole: TokenRole.BotChat,
                Reason: "Instance default bot credential is absent or unusable.");
    }

    /// <summary>
    /// Determines whether a decrypted token payload contains a non-empty access token.
    /// </summary>
    /// <param name="payload">The decrypted payload to inspect.</param>
    /// <returns><see langword="true"/> if usable; otherwise, <see langword="false"/>.</returns>
    private static bool IsUsablePayload(PlatformTokenPayload? payload) =>
        payload is not null && !string.IsNullOrWhiteSpace(payload.AccessToken);
}
