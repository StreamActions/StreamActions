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
/// Describes a registered permission key, its category, metadata, global gating behavior, and default designation grants.
/// </summary>
/// <param name="Key">The unique permission key (for example, <c>feature.custom_commands</c>).</param>
/// <param name="Category">The logical category used for grouping in management interfaces.</param>
/// <param name="DisplayName">The human-readable permission name.</param>
/// <param name="Description">A concise description of what the permission governs.</param>
/// <param name="IsGloballyGated">
/// A value indicating whether the permission requires an explicit <c>Allow</c> grant from an assigned Tier 1 global group.
/// </param>
/// <param name="IsDangerousGlobalFeature">
/// A value indicating whether the globally gated feature is a high-risk feature that defaults to <see cref="PermissionState.Inherit"/>
/// on the default global plan unless explicitly opted in during setup or enabled by a root operator.
/// </param>
/// <param name="DefaultAllowedDesignations">
/// The built-in platform designations that receive an initial <see cref="PermissionState.Allow"/> grant when workspace groups are seeded.
/// </param>
/// <param name="DefaultState">
/// The baseline default <see cref="PermissionState"/> of the permission (<see cref="PermissionState.Inherit"/> or <see cref="PermissionState.Allow"/>; never <see cref="PermissionState.Deny"/>).
/// </param>
public sealed record PermissionDescriptor(
    string Key,
    string Category,
    string DisplayName,
    string Description,
    bool IsGloballyGated = false,
    bool IsDangerousGlobalFeature = false,
    PlatformUserDesignations DefaultAllowedDesignations = PlatformUserDesignations.None,
    PermissionState DefaultState = PermissionState.Inherit);
