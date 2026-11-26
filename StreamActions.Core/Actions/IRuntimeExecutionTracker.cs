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

namespace StreamActions.Core.Actions;

/// <summary>
/// Snapshot of an actively running Action Block flow displayed in the Scoped Task Manager.
/// </summary>
/// <param name="ExecutionId">The unique execution identifier.</param>
/// <param name="WorkspaceId">The owning workspace identifier.</param>
/// <param name="TaskId">The executing task identifier.</param>
/// <param name="TaskName">The executing task display name.</param>
/// <param name="ProfileId">Optional triggering profile identifier.</param>
/// <param name="ProfileName">Optional triggering profile display name.</param>
/// <param name="CurrentStepIndex">Current zero-based step index.</param>
/// <param name="CurrentStepName">Human-readable name of the active step.</param>
/// <param name="StartedAtUtc">UTC start timestamp.</param>
/// <param name="MaxRuntimeSeconds">Effective Tier 1 flow runtime budget in seconds.</param>
public sealed record RunningActionFlowInfo(
    Guid ExecutionId,
    Guid WorkspaceId,
    Guid TaskId,
    string TaskName,
    Guid? ProfileId,
    string? ProfileName,
    int CurrentStepIndex,
    string CurrentStepName,
    DateTimeOffset StartedAtUtc,
    int MaxRuntimeSeconds);

/// <summary>
/// Snapshot of a loaded ClearScript V8 per-script isolate displayed in the Scoped Task Manager.
/// </summary>
/// <param name="InstanceId">Unique runtime isolate instance identifier.</param>
/// <param name="WorkspaceId">Owning workspace identifier.</param>
/// <param name="ScriptId">VFS script identifier.</param>
/// <param name="ScriptPath">Normalized VFS script path (for example, <c>scripts/heist.js</c>).</param>
/// <param name="LifecycleMode">Current automatic unloading lifecycle mode.</param>
/// <param name="UsedHeapBytes">Current V8 heap memory used by this isolate in bytes.</param>
/// <param name="WorkspaceRamQuotaBytes">Effective Tier 1 workspace ClearScript RAM quota in bytes.</param>
/// <param name="CumulativeRuntime">Cumulative execution runtime consumed by this isolate.</param>
/// <param name="Exports">Names of properties and functions exported onto <c>$</c> by this script.</param>
/// <param name="BoundEvents">Event names bound via <c>$.bind</c> / <c>$.on</c> by this script.</param>
/// <param name="LoadedAtUtc">UTC timestamp when the isolate was loaded.</param>
/// <param name="LastActivityAtUtc">UTC timestamp of the most recent invocation.</param>
public sealed record LoadedScriptInstanceInfo(
    Guid InstanceId,
    Guid WorkspaceId,
    Guid ScriptId,
    string ScriptPath,
    ScriptLifecycleMode LifecycleMode,
    long UsedHeapBytes,
    long WorkspaceRamQuotaBytes,
    TimeSpan CumulativeRuntime,
    IReadOnlyList<string> Exports,
    IReadOnlyList<string> BoundEvents,
    DateTimeOffset LoadedAtUtc,
    DateTimeOffset LastActivityAtUtc);

/// <summary>
/// Aggregate runtime resource summary per workspace for Root/Global Admin Task Manager dashboards.
/// </summary>
/// <param name="WorkspaceId">The workspace identifier.</param>
/// <param name="TotalUsedHeapBytes">Combined V8 heap bytes across all loaded isolates in this workspace.</param>
/// <param name="RamQuotaBytes">Effective Tier 1 workspace ClearScript RAM quota in bytes.</param>
/// <param name="ActiveFlowCount">Number of currently executing Action Block flows.</param>
/// <param name="LoadedScriptCount">Number of currently loaded ClearScript V8 isolates.</param>
public sealed record WorkspaceRuntimeSummary(
    Guid WorkspaceId,
    long TotalUsedHeapBytes,
    long RamQuotaBytes,
    int ActiveFlowCount,
    int LoadedScriptCount);

/// <summary>
/// Provides real-time observability and surgical termination controls over running Action Block flows and loaded ClearScript V8 isolates.
/// </summary>
public interface IRuntimeExecutionTracker
{
    /// <summary>
    /// Registers a running Action Block flow with its cancellation hook.
    /// </summary>
    /// <param name="info">Initial flow metadata.</param>
    /// <param name="cancelAction">Callback invoked when the flow is terminated.</param>
    void RegisterFlow(RunningActionFlowInfo info, Action cancelAction);

    /// <summary>
    /// Updates the current step progress of a running Action Block flow.
    /// </summary>
    /// <param name="executionId">The flow execution identifier.</param>
    /// <param name="currentStepIndex">Current zero-based step index.</param>
    /// <param name="currentStepName">Current step name.</param>
    void UpdateFlowStep(Guid executionId, int currentStepIndex, string currentStepName);

    /// <summary>
    /// Unregisters a completed or terminated Action Block flow.
    /// </summary>
    /// <param name="executionId">The flow execution identifier.</param>
    void UnregisterFlow(Guid executionId);

    /// <summary>
    /// Registers or replaces the active metrics/termination delegate for a loaded ClearScript V8 isolate.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="scriptId">The VFS script identifier.</param>
    /// <param name="snapshotProvider">Delegate returning current live metrics for the isolate.</param>
    /// <param name="terminateAsync">Delegate performing surgical interrupt, detachment, and disposal of the isolate.</param>
    void RegisterScriptIsolate(
        Guid workspaceId,
        Guid scriptId,
        Func<LoadedScriptInstanceInfo?> snapshotProvider,
        Func<CancellationToken, ValueTask> terminateAsync);

    /// <summary>
    /// Unregisters a ClearScript V8 isolate after normal unload or termination.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="scriptId">The VFS script identifier.</param>
    void UnregisterScriptIsolate(Guid workspaceId, Guid scriptId);

    /// <summary>
    /// Retrieves currently running Action Block flows, optionally filtered to a single workspace.
    /// </summary>
    /// <param name="workspaceId">Optional workspace identifier filter (<see langword="null"/> returns all workspaces).</param>
    /// <returns>Snapshot list of running flows.</returns>
    IReadOnlyList<RunningActionFlowInfo> GetRunningFlows(Guid? workspaceId = null);

    /// <summary>
    /// Retrieves currently loaded ClearScript V8 script isolates, optionally filtered to a single workspace.
    /// </summary>
    /// <param name="workspaceId">Optional workspace identifier filter (<see langword="null"/> returns all workspaces).</param>
    /// <returns>Snapshot list of loaded script isolates with live RAM and runtime metrics.</returns>
    IReadOnlyList<LoadedScriptInstanceInfo> GetLoadedScripts(Guid? workspaceId = null);

    /// <summary>
    /// Retrieves aggregate per-workspace runtime summaries across all workspaces with active flows or loaded scripts.
    /// </summary>
    /// <returns>Per-workspace runtime summaries.</returns>
    IReadOnlyList<WorkspaceRuntimeSummary> GetWorkspaceSummaries();

    /// <summary>
    /// Surgically terminates a specific running Action Block flow.
    /// </summary>
    /// <param name="executionId">The execution identifier to terminate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a matching running flow was found and cancelled; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> TerminateFlowAsync(Guid executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Surgically interrupts, detaches (<c>$</c> exports and <c>$.bind</c>/<c>$.on</c> handlers), and disposes a specific ClearScript V8 isolate without disturbing other scripts.
    /// </summary>
    /// <param name="workspaceId">The owning workspace identifier.</param>
    /// <param name="scriptId">The VFS script identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a matching loaded isolate was found and terminated; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> TerminateScriptAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates all running Action Block flows and unloads/disposes all ClearScript V8 isolates within <paramref name="workspaceId"/>.
    /// </summary>
    /// <param name="workspaceId">The target workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The total number of flows and script isolates terminated.</returns>
    ValueTask<int> TerminateAllForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}

