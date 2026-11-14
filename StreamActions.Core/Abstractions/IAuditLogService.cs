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

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Records and queries immutable <see cref="WorkspaceAuditLog"/> entries.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Appends a new audit log entry.
    /// </summary>
    /// <param name="entry">The audit log entry to record.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous write operation.</returns>
    ValueTask RecordAsync(WorkspaceAuditLog entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent audit log entries for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of recent <see cref="WorkspaceAuditLog"/> entries.</returns>
    ValueTask<IReadOnlyList<WorkspaceAuditLog>> GetRecentAsync(Guid workspaceId, int count = 50, CancellationToken cancellationToken = default);
}
