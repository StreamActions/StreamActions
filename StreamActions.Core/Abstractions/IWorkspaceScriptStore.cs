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

using StreamActions.Core.Scripting;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides persistence operations for ClearScript V8 JavaScript files stored in the Workspace Virtual File System (<c>WorkspaceScripts</c>).
/// </summary>
public interface IWorkspaceScriptStore
{
    /// <summary>
    /// Retrieves all scripts stored in the workspace VFS.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="WorkspaceScript"/> records.</returns>
    ValueTask<IReadOnlyList<WorkspaceScript>> GetScriptsAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permanently loaded scripts across all workspaces for startup pre-loading.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of enabled permanently-loaded <see cref="WorkspaceScript"/> records.</returns>
    ValueTask<IReadOnlyList<WorkspaceScript>> GetPermanentlyLoadedScriptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a script by its immutable identifier within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="scriptId">The script identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching script, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceScript?> GetByIdAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a script by its normalized VFS path within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="path">The VFS path (for example, <c>lib/helpers.js</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching script, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceScript?> GetByPathAsync(Guid workspaceId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a workspace script and synchronizes command permission registrations/cleanups.
    /// </summary>
    /// <param name="script">The script entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="WorkspaceScript"/>.</returns>
    ValueTask<WorkspaceScript> UpsertAsync(WorkspaceScript script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace script and cleans up any unreferenced command permission grants.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="scriptId">The script identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default);
}

