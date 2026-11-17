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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Tracks the authenticated panel user's accessible workspaces and active workspace selection for the current session or circuit.
/// </summary>
public interface IWorkspaceSessionContext
{
    /// <summary>
    /// Gets the identifier of the currently active workspace, or <see langword="null"/> if none is active.
    /// </summary>
    Guid? ActiveWorkspaceId { get; }

    /// <summary>
    /// Gets the currently active <see cref="AccessibleWorkspace"/>, or <see langword="null"/> if none is active.
    /// </summary>
    AccessibleWorkspace? ActiveWorkspace { get; }

    /// <summary>
    /// Gets all workspaces accessible to the current authenticated user.
    /// </summary>
    IReadOnlyList<AccessibleWorkspace> AccessibleWorkspaces { get; }

    /// <summary>
    /// Occurs when the active workspace or accessible workspace list changes.
    /// </summary>
    event EventHandler? WorkspaceChanged;

    /// <summary>
    /// Initializes the workspace session context if it has not yet been loaded.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous initialization operation.</returns>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates the specified workspace if it is accessible to the current user.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to select.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the workspace is accessible and was selected; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> SelectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads accessible workspaces and refreshes the active workspace selection.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous refresh operation.</returns>
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
