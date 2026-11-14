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
/// Exports and imports portable <c>.sabackup</c> workspace archives without leaking secrets.
/// </summary>
public interface IWorkspacePortableArchiveService
{
    /// <summary>
    /// Exports a workspace's configuration and non-secret state into a <see cref="WorkspacePortableArchive"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to export.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The exported <see cref="WorkspacePortableArchive"/>.</returns>
    ValueTask<WorkspacePortableArchive> ExportAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a <see cref="WorkspacePortableArchive"/> into the target workspace.
    /// </summary>
    /// <param name="workspaceId">The target workspace identifier.</param>
    /// <param name="archive">The archive to import.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous import operation.</returns>
    ValueTask ImportAsync(Guid workspaceId, WorkspacePortableArchive archive, CancellationToken cancellationToken = default);
}
