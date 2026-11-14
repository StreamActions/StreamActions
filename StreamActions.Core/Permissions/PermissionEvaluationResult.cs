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
/// Represents the outcome of evaluating a permission key for an actor within a workspace.
/// </summary>
/// <param name="IsAllowed">A value indicating whether the actor is permitted to perform the action.</param>
/// <param name="EffectiveState">The resolved tri-state permission outcome.</param>
/// <param name="Reason">A human-readable explanation of why the permission was allowed or denied, safe for public display.</param>
/// <param name="DenyingGroupId">The identifier of the global or workspace group that explicitly denied the permission, if applicable; intended for logging and admin diagnostics.</param>
/// <param name="DenyingGroupName">The name of the global or workspace group that explicitly denied the permission, if applicable; intended for logging and admin diagnostics.</param>
public sealed record PermissionEvaluationResult(
    bool IsAllowed,
    PermissionState EffectiveState,
    string Reason,
    Guid? DenyingGroupId = null,
    string? DenyingGroupName = null);
