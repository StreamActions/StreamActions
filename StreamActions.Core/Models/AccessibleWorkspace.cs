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

using StreamActions.Core.Models.Entities;

namespace StreamActions.Core.Models;

/// <summary>
/// Represents a <see cref="Entities.Workspace"/> accessible to an authenticated <see cref="PanelUser"/> along with its attached platforms.
/// </summary>
/// <param name="Workspace">The accessible workspace.</param>
/// <param name="IsOwner">A value indicating whether the user owns this workspace.</param>
/// <param name="Platforms">The platforms attached to this workspace.</param>
/// <param name="AuthStatus">The authentication readiness status of this workspace for the current session.</param>
/// <param name="RequiredOidcProviderSlug">
/// The slug of the external OIDC provider required to reauthenticate for this workspace, when <paramref name="AuthStatus"/> is <see cref="WorkspaceSessionAuthStatus.RequiresOidcReauth"/>.
/// </param>
public sealed record AccessibleWorkspace(
    Workspace Workspace,
    bool IsOwner,
    IReadOnlyList<WorkspacePlatform> Platforms,
    WorkspaceSessionAuthStatus AuthStatus = WorkspaceSessionAuthStatus.Ready,
    string? RequiredOidcProviderSlug = null);
