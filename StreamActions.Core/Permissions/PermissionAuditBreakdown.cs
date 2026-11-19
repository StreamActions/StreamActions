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

namespace StreamActions.Core.Permissions;

/// <summary>
/// Identifies the permission evaluation tier that determined the final effective permission outcome.
/// </summary>
public enum PermissionDeterminingTier
{
    /// <summary>
    /// The outcome was determined by the permission's baseline default state (no overriding group grant or override).
    /// </summary>
    DefaultState = 0,

    /// <summary>
    /// The outcome was determined by a Tier 1 Global Permission Group assigned to the workspace.
    /// </summary>
    GlobalTier1 = 1,

    /// <summary>
    /// The outcome was determined by a Tier 2 Workspace Permission Group or per-user permission override.
    /// </summary>
    WorkspaceTier2 = 2,

    /// <summary>
    /// The outcome was determined by a root operator, global administrator, or workspace owner/broadcaster bypass.
    /// </summary>
    RootOrOwnerBypass = 3,
}

/// <summary>
/// Represents a single non-inheriting contribution (from a Tier 1 global group, Tier 2 workspace group, user override, or the permission's default state)
/// toward a permission's effective audit outcome.
/// </summary>
/// <param name="SourceName">
/// The display name of the contributing group, user override, or <c>Default State</c>
/// (redacted to <c>Global Permissions</c> for Tier 1 global groups when viewed by a non-root/non-global-admin operator).
/// </param>
/// <param name="State">The tri-state <see cref="PermissionState"/> contributed by this source.</param>
/// <param name="DisplayState">The human-readable label for the contributed state (for example, <c>Allow</c>, <c>Deny</c>, <c>Implicit Deny (Overridable Deny)</c>, or <c>Default State: Allow</c>).</param>
/// <param name="IsGlobalTier">A value indicating whether this contribution originates from a Tier 1 global permission group.</param>
/// <param name="IsDefaultState">A value indicating whether this entry represents the permission's baseline default state.</param>
/// <param name="GroupId">The identifier of the contributing group when applicable and visible to the viewer; otherwise, <see langword="null"/>.</param>
public sealed record PermissionContribution(
    string SourceName,
    PermissionState State,
    string DisplayState,
    bool IsGlobalTier,
    bool IsDefaultState = false,
    Guid? GroupId = null);

/// <summary>
/// Represents the complete effective permission audit and explain breakdown for a single permission key.
/// </summary>
/// <param name="Descriptor">The permission descriptor being audited.</param>
/// <param name="IsAllowed">A value indicating whether the permission is effectively allowed for the audited workspace or user.</param>
/// <param name="EffectiveState">The resolved tri-state <see cref="PermissionState"/>.</param>
/// <param name="DeterminedByTier">The <see cref="PermissionDeterminingTier"/> that produced the final outcome.</param>
/// <param name="DeterminedByDisplay">A human-readable label for <paramref name="DeterminedByTier"/> (for example, <c>Global Permissions (Tier 1)</c>, <c>Workspace Permissions (Tier 2)</c>, or <c>Default State</c>).</param>
/// <param name="SummaryReason">A human-readable summary explaining why the permission is allowed or denied (with global group names redacted when required).</param>
/// <param name="DefaultState">The baseline default <see cref="PermissionState"/> of the permission.</param>
/// <param name="DefaultStateDisplay">The human-readable display string for the permission's default state (<c>Implicit Deny (Overridable Deny)</c> or <c>Default State: Allow</c>).</param>
/// <param name="Contributions">The ordered list of non-<see cref="PermissionState.Inherit"/> group/override contributions plus the mandatory Default State contribution.</param>
public sealed record PermissionAuditBreakdown(
    PermissionDescriptor Descriptor,
    bool IsAllowed,
    PermissionState EffectiveState,
    PermissionDeterminingTier DeterminedByTier,
    string DeterminedByDisplay,
    string SummaryReason,
    PermissionState DefaultState,
    string DefaultStateDisplay,
    IReadOnlyList<PermissionContribution> Contributions);
