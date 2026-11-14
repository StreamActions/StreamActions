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
/// Provides persistence operations for <see cref="CustomCommand"/> entities within a workspace.
/// </summary>
public interface ICommandRepository
{
    /// <summary>
    /// Retrieves all custom commands configured for a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="CustomCommand"/> entities.</returns>
    ValueTask<IReadOnlyList<CustomCommand>> GetCommandsAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a custom command by its primary trigger or alias within a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="trigger">The command trigger to look up.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="CustomCommand"/>, or <see langword="null"/> if not found.</returns>
    ValueTask<CustomCommand?> GetByTriggerAsync(Guid workspaceId, string trigger, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a custom command.
    /// </summary>
    /// <param name="command">The command entity to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous upsert operation.</returns>
    ValueTask UpsertAsync(CustomCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a custom command from a workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous delete operation.</returns>
    ValueTask DeleteAsync(Guid workspaceId, Guid commandId, CancellationToken cancellationToken = default);
}
