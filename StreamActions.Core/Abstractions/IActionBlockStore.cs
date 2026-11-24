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

using StreamActions.Core.Actions;

namespace StreamActions.Core.Abstractions;

/// <summary>
/// Provides persistence operations for Action Block <see cref="WorkspaceActionBlockTask"/> and <see cref="WorkspaceActionBlockProfile"/> definitions.
/// </summary>
public interface IActionBlockStore
{
    /// <summary>
    /// Retrieves all tasks belonging to a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="WorkspaceActionBlockTask"/> records.</returns>
    ValueTask<IReadOnlyList<WorkspaceActionBlockTask>> GetTasksAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a task by its immutable identifier within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching task, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceActionBlockTask?> GetTaskByIdAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a task by its operator-assigned name within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="name">The task name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching task, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceActionBlockTask?> GetTaskByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates an Action Block task, validating its block count against Tier 1 <c>MaxActionBlocksPerFlow</c> quotas.
    /// </summary>
    /// <param name="task">The task entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="WorkspaceActionBlockTask"/>.</returns>
    ValueTask<WorkspaceActionBlockTask> UpsertTaskAsync(WorkspaceActionBlockTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an Action Block task from a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteTaskAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all Action Block profiles belonging to a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="WorkspaceActionBlockProfile"/> records.</returns>
    ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetProfilesAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all Action Block profiles across all workspaces (used by background schedule coordinators).
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of all <see cref="WorkspaceActionBlockProfile"/> records.</returns>
    ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an Action Block profile by its immutable identifier within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="profileId">The profile identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching profile, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceActionBlockProfile?> GetProfileByIdAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an Action Block profile by its operator-assigned name within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="name">The profile name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching profile, or <see langword="null"/> if not found.</returns>
    ValueTask<WorkspaceActionBlockProfile?> GetProfileByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates an Action Block profile.
    /// </summary>
    /// <param name="profile">The profile entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="WorkspaceActionBlockProfile"/>.</returns>
    ValueTask<WorkspaceActionBlockProfile> UpsertProfileAsync(WorkspaceActionBlockProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the enabled state of an Action Block profile by its immutable identifier.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="profileId">The profile identifier.</param>
    /// <param name="isEnabled">Desired enabled state.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a matching profile was updated; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> SetProfileEnabledAsync(Guid workspaceId, Guid profileId, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an Action Block profile from a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="profileId">The profile identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteProfileAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default);
}

