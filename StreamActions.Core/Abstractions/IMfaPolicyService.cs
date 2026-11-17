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

using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Evaluates two-tier (Instance + Workspace) multi-factor authentication policies where the strictest setting always wins.
/// </summary>
public interface IMfaPolicyService
{
    /// <summary>
    /// Computes the <see cref="EffectiveMfaPolicy"/> by combining <paramref name="instanceSettings"/> and an optional <paramref name="workspace"/>.
    /// </summary>
    /// <param name="instanceSettings">The global instance settings.</param>
    /// <param name="workspace">An optional workspace whose stricter overrides apply.</param>
    /// <returns>The combined <see cref="EffectiveMfaPolicy"/>.</returns>
    EffectiveMfaPolicy ComputeEffectivePolicy(InstanceSettings instanceSettings, Workspace? workspace = null);

    /// <summary>
    /// Loads instance settings and an optional workspace to compute the <see cref="EffectiveMfaPolicy"/>.
    /// </summary>
    /// <param name="workspaceId">The optional workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The combined <see cref="EffectiveMfaPolicy"/>.</returns>
    ValueTask<EffectiveMfaPolicy> GetEffectivePolicyAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the <see cref="EffectiveMfaPolicy"/> applicable to a <see cref="PanelUser"/>, taking into account an explicit workspace or all relevant workspaces.
    /// </summary>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The external login identities linked to <paramref name="user"/>.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims from the current session.</param>
    /// <param name="workspaceId">An optional explicit workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The effective <see cref="EffectiveMfaPolicy"/> for the user.</returns>
    ValueTask<EffectiveMfaPolicy> GetEffectivePolicyForUserAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether login MFA enrollment/verification is required for the specified <see cref="PanelUser"/> based on their role and applicable policies.
    /// </summary>
    /// <param name="user">The authenticated panel user.</param>
    /// <param name="identities">The external login identities linked to <paramref name="user"/>.</param>
    /// <param name="oidcGroupClaims">Optional OIDC group claims from the current session.</param>
    /// <param name="workspaceId">An optional explicit workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if login MFA is required for the user; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsLoginMfaRequiredAsync(
        PanelUser user,
        IReadOnlyList<UserLoginIdentity> identities,
        IReadOnlyCollection<string>? oidcGroupClaims = null,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);
}
