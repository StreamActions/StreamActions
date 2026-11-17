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
/// Resolves effective OAuth application credentials using the instance policy, workspace custom credentials, and feature permissions.
/// </summary>
public sealed class OAuthClientConfigResolver : IOAuthClientConfigResolver
{
    /// <summary>
    /// The instance settings service used to read <see cref="InstanceSettings.InstanceOAuthAppPolicy"/>.
    /// </summary>
    private readonly IInstanceSettingsService _instanceSettingsService;

    /// <summary>
    /// The instance-wide OAuth application configuration service.
    /// </summary>
    private readonly IInstanceOAuthConfigService _instanceOAuthConfigService;

    /// <summary>
    /// The workspace persistence store used to retrieve workspace custom OAuth credentials.
    /// </summary>
    private readonly IWorkspaceStore _workspaceStore;

    /// <summary>
    /// The permission evaluation service used to verify <see cref="BuiltInPermissionKeys.FeatureCustomOAuthApp"/>.
    /// </summary>
    private readonly IPermissionService _permissionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthClientConfigResolver"/> class.
    /// </summary>
    /// <param name="instanceSettingsService">The instance settings service.</param>
    /// <param name="instanceOAuthConfigService">The instance OAuth configuration service.</param>
    /// <param name="workspaceStore">The workspace store.</param>
    /// <param name="permissionService">The permission service.</param>
    public OAuthClientConfigResolver(
        IInstanceSettingsService instanceSettingsService,
        IInstanceOAuthConfigService instanceOAuthConfigService,
        IWorkspaceStore workspaceStore,
        IPermissionService permissionService)
    {
        ArgumentNullException.ThrowIfNull(instanceSettingsService);
        ArgumentNullException.ThrowIfNull(instanceOAuthConfigService);
        ArgumentNullException.ThrowIfNull(workspaceStore);
        ArgumentNullException.ThrowIfNull(permissionService);

        this._instanceSettingsService = instanceSettingsService;
        this._instanceOAuthConfigService = instanceOAuthConfigService;
        this._workspaceStore = workspaceStore;
        this._permissionService = permissionService;
    }

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes platform identifiers to lowercase invariant.")]
    public async ValueTask<ResolvedOAuthClientConfig?> ResolveAsync(
        string platformId,
        Guid? workspaceId = null,
        PlatformUserIdentity? actor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformId);

        string normalizedPlatformId = platformId.Trim().ToLowerInvariant();
        InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        InstanceOAuthAppPolicy policy = AuthPolicyParser.ParseOAuthAppPolicy(settings.InstanceOAuthAppPolicy);

        switch (policy)
        {
            case InstanceOAuthAppPolicy.InstanceOnly:
                return await this.ResolveInstanceAsync(normalizedPlatformId, workspaceId, cancellationToken).ConfigureAwait(false);

            case InstanceOAuthAppPolicy.InstanceWithOptionalOverride:
                if (workspaceId.HasValue)
                {
                    PlatformOAuthAppCredentials? workspaceCredentials = await this._workspaceStore
                        .GetCustomOAuthAppAsync(workspaceId.Value, normalizedPlatformId, cancellationToken)
                        .ConfigureAwait(false);

                    if (workspaceCredentials is not null)
                    {
                        PlatformUserIdentity effectiveActor = actor ?? new PlatformUserIdentity(
                            normalizedPlatformId,
                            "__workspace__",
                            "workspace",
                            PlatformUserDesignations.Broadcaster);

                        bool customAppAllowed = await this._permissionService
                            .IsAllowedAsync(workspaceId.Value, effectiveActor, BuiltInPermissionKeys.FeatureCustomOAuthApp, cancellationToken)
                            .ConfigureAwait(false);

                        if (customAppAllowed)
                        {
                            return new ResolvedOAuthClientConfig(
                                normalizedPlatformId,
                                workspaceCredentials.ClientId,
                                workspaceCredentials.ClientSecret,
                                workspaceCredentials.RedirectUri,
                                IsWorkspaceOverride: true,
                                WorkspaceId: workspaceId);
                        }
                    }
                }

                return await this.ResolveInstanceAsync(normalizedPlatformId, workspaceId, cancellationToken).ConfigureAwait(false);

            case InstanceOAuthAppPolicy.RequireStreamerOAuthApp:
                if (!workspaceId.HasValue)
                {
                    return null;
                }

                PlatformOAuthAppCredentials? requiredCredentials = await this._workspaceStore
                    .GetCustomOAuthAppAsync(workspaceId.Value, normalizedPlatformId, cancellationToken)
                    .ConfigureAwait(false);

                return requiredCredentials is null
                    ? null
                    : new ResolvedOAuthClientConfig(
                        normalizedPlatformId,
                        requiredCredentials.ClientId,
                        requiredCredentials.ClientSecret,
                        requiredCredentials.RedirectUri,
                        IsWorkspaceOverride: true,
                        WorkspaceId: workspaceId);

            default:
                throw new InvalidOperationException($"Unsupported OAuth application policy: {policy}.");
        }
    }

    /// <summary>
    /// Resolves the instance-wide OAuth application credentials for the specified platform.
    /// </summary>
    /// <param name="normalizedPlatformId">The normalized lowercase platform identifier.</param>
    /// <param name="workspaceId">The optional workspace identifier for context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved instance-wide <see cref="ResolvedOAuthClientConfig"/>, or <see langword="null"/> if not configured.</returns>
    private async ValueTask<ResolvedOAuthClientConfig?> ResolveInstanceAsync(
        string normalizedPlatformId,
        Guid? workspaceId,
        CancellationToken cancellationToken)
    {
        PlatformOAuthAppCredentials? instanceCredentials = await this._instanceOAuthConfigService
            .GetAsync(normalizedPlatformId, cancellationToken)
            .ConfigureAwait(false);

        return instanceCredentials is null
            ? null
            : new ResolvedOAuthClientConfig(
                normalizedPlatformId,
                instanceCredentials.ClientId,
                instanceCredentials.ClientSecret,
                instanceCredentials.RedirectUri,
                IsWorkspaceOverride: false,
                WorkspaceId: workspaceId);
    }
}
