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

using StreamActions.Core.Abstractions;

namespace StreamActions.Core.Scripting;

/// <summary>
/// Tracks dynamic command permission keys across both custom commands and VFS ClearScript scripts, cleaning up unused permission grants automatically.
/// </summary>
public interface IWorkspaceCommandPermissionTracker
{
    /// <summary>
    /// Synchronizes command permission registrations and seeds initial designation defaults for a workspace script's declared commands,
    /// and cleans up any command permission keys no longer referenced anywhere in the workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="previousTriggers">Normalized command triggers previously associated with the modified/deleted item.</param>
    /// <param name="currentRegistrations">Current script command registrations (empty when deleting).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the synchronization operation.</returns>
    ValueTask SyncScriptCommandPermissionsAsync(
        Guid workspaceId,
        IEnumerable<string> previousTriggers,
        IEnumerable<ScriptCommandRegistration> currentRegistrations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes workspace permission grants, user overrides, and unreferenced registry entries for any candidate command triggers
    /// that now have zero references across custom commands and workspace scripts.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="candidateTriggers">Normalized command triggers to check for remaining references.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The list of orphaned permission keys that were removed.</returns>
    ValueTask<IReadOnlyList<string>> CleanupOrphanedCommandPermissionsAsync(
        Guid workspaceId,
        IEnumerable<string> candidateTriggers,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bridges Action Block flows and event dispatchers in <c>StreamActions.Core</c> to the sandboxed ClearScript V8 runtime without introducing a direct ClearScript dependency in Core.
/// </summary>
public interface IRunJsActionExecutor
{
    /// <summary>
    /// Loads (if needed) and executes a workspace VFS script or exported entry function.
    /// </summary>
    /// <param name="request">The execution request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="ScriptExecutionResult"/>.</returns>
    ValueTask<ScriptExecutionResult> ExecuteScriptAsync(
        ScriptExecutionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches an event to all matching <c>$.bind</c>/<c>$.on</c> handlers in the workspace (auto-loading resident or target scripts when permitted).
    /// </summary>
    /// <param name="request">The event dispatch payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of script handlers invoked.</returns>
    ValueTask<int> DispatchEventAsync(
        ScriptEventDispatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a chat command invocation to the owning workspace VFS script after loading it if necessary.
    /// </summary>
    /// <param name="invocation">The inbound command invocation.</param>
    /// <param name="script">The owning VFS script.</param>
    /// <param name="registration">The matched command registration metadata.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ScriptExecutionResult"/> describing the execution outcome.</returns>
    ValueTask<ScriptExecutionResult> DispatchCommandAsync(
        ICommandInvocation invocation,
        WorkspaceScript script,
        ScriptCommandRegistration registration,
        CancellationToken cancellationToken = default);
}

