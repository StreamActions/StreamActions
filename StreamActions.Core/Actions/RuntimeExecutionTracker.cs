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

using StreamActions.Core.Permissions;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Actions;

/// <summary>
/// Thread-safe default implementation of <see cref="IRuntimeExecutionTracker"/> backing Workspace and Root Scoped Task Manager views.
/// </summary>
public sealed class RuntimeExecutionTracker : IRuntimeExecutionTracker
{
    /// <summary>
    /// Active Action Block flow executions keyed by execution identifier.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, TrackedFlowEntry> _flows = new();

    /// <summary>
    /// Active ClearScript V8 isolate instances keyed by (WorkspaceId, ScriptId).
    /// </summary>
    private readonly ConcurrentDictionary<ScriptTrackerKey, TrackedScriptEntry> _scripts = new();

    /// <inheritdoc/>
    public void RegisterFlow(RunningActionFlowInfo info, Action cancelAction)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(cancelAction);

        this._flows[info.ExecutionId] = new TrackedFlowEntry(info, cancelAction);
    }

    /// <inheritdoc/>
    public void UpdateFlowStep(Guid executionId, int currentStepIndex, string currentStepName)
    {
        if (this._flows.TryGetValue(executionId, out TrackedFlowEntry? entry))
        {
            entry.Info = entry.Info with
            {
                CurrentStepIndex = currentStepIndex,
                CurrentStepName = currentStepName ?? string.Empty,
            };
        }
    }

    /// <inheritdoc/>
    public void UnregisterFlow(Guid executionId) => _ = this._flows.TryRemove(executionId, out _);

    /// <inheritdoc/>
    public void RegisterScriptIsolate(
        Guid workspaceId,
        Guid scriptId,
        Func<LoadedScriptInstanceInfo?> snapshotProvider,
        Func<CancellationToken, ValueTask> killAsync)
    {
        ArgumentNullException.ThrowIfNull(snapshotProvider);
        ArgumentNullException.ThrowIfNull(killAsync);

        ScriptTrackerKey key = new(workspaceId, scriptId);
        this._scripts[key] = new TrackedScriptEntry(snapshotProvider, killAsync);
    }

    /// <inheritdoc/>
    public void UnregisterScriptIsolate(Guid workspaceId, Guid scriptId)
    {
        ScriptTrackerKey key = new(workspaceId, scriptId);
        _ = this._scripts.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public IReadOnlyList<RunningActionFlowInfo> GetRunningFlows(Guid? workspaceId = null) =>
        [.. this._flows.Values
            .Select(static entry => entry.Info)
            .Where(info => !workspaceId.HasValue || info.WorkspaceId == workspaceId.Value)
            .OrderBy(static info => info.StartedAtUtc)];

    /// <inheritdoc/>
    public IReadOnlyList<LoadedScriptInstanceInfo> GetLoadedScripts(Guid? workspaceId = null)
    {
        List<LoadedScriptInstanceInfo> results = [];
        foreach (KeyValuePair<ScriptTrackerKey, TrackedScriptEntry> kvp in this._scripts)
        {
            if (workspaceId.HasValue && kvp.Key.WorkspaceId != workspaceId.Value)
            {
                continue;
            }

            LoadedScriptInstanceInfo? snapshot = kvp.Value.SnapshotProvider();
            if (snapshot is not null)
            {
                results.Add(snapshot);
            }
        }

        return [.. results.OrderBy(static s => s.ScriptPath, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<WorkspaceRuntimeSummary> GetWorkspaceSummaries()
    {
        Dictionary<Guid, (long UsedBytes, long QuotaBytes, int FlowCount, int ScriptCount)> byWorkspace = [];

        foreach (TrackedFlowEntry entry in this._flows.Values)
        {
            Guid wsId = entry.Info.WorkspaceId;
            _ = byWorkspace.TryGetValue(wsId, out (long UsedBytes, long QuotaBytes, int FlowCount, int ScriptCount) current);
            byWorkspace[wsId] = (
                current.UsedBytes,
                current.QuotaBytes > 0 ? current.QuotaBytes : WorkspaceResourceQuotas.DefaultMaxClearScriptTotalRamBytes,
                current.FlowCount + 1,
                current.ScriptCount);
        }

        foreach (TrackedScriptEntry scriptEntry in this._scripts.Values)
        {
            LoadedScriptInstanceInfo? snapshot = scriptEntry.SnapshotProvider();
            if (snapshot is null)
            {
                continue;
            }

            Guid wsId = snapshot.WorkspaceId;
            _ = byWorkspace.TryGetValue(wsId, out (long UsedBytes, long QuotaBytes, int FlowCount, int ScriptCount) current);
            byWorkspace[wsId] = (
                current.UsedBytes + snapshot.UsedHeapBytes,
                snapshot.WorkspaceRamQuotaBytes > 0 ? snapshot.WorkspaceRamQuotaBytes : current.QuotaBytes,
                current.FlowCount,
                current.ScriptCount + 1);
        }

        return [.. byWorkspace
            .Select(static kvp => new WorkspaceRuntimeSummary(
                kvp.Key,
                kvp.Value.UsedBytes,
                kvp.Value.QuotaBytes,
                kvp.Value.FlowCount,
                kvp.Value.ScriptCount))
            .OrderBy(static summary => summary.WorkspaceId)];
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cancel callbacks on already-disposed flows must not prevent Task Manager cleanup.")]
    public ValueTask<bool> KillFlowAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        if (!this._flows.TryRemove(executionId, out TrackedFlowEntry? entry))
        {
            return ValueTask.FromResult(false);
        }

        try
        {
            entry.CancelAction();
        }
        catch (ObjectDisposedException)
        {
            // Flow completed concurrently with kill request.
        }
        catch (Exception)
        {
            // Ignore disposal races.
        }

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> KillScriptAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default)
    {
        ScriptTrackerKey key = new(workspaceId, scriptId);
        if (!this._scripts.TryRemove(key, out TrackedScriptEntry? entry))
        {
            return false;
        }

        await entry.KillAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<int> KillAllForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        int killedCount = 0;

        List<Guid> flowIds = [.. this._flows.Values
            .Where(entry => entry.Info.WorkspaceId == workspaceId)
            .Select(static entry => entry.Info.ExecutionId)];

        foreach (Guid flowId in flowIds)
        {
            if (await this.KillFlowAsync(flowId, cancellationToken).ConfigureAwait(false))
            {
                killedCount++;
            }
        }

        List<ScriptTrackerKey> scriptKeys = [.. this._scripts.Keys.Where(k => k.WorkspaceId == workspaceId)];
        foreach (ScriptTrackerKey scriptKey in scriptKeys)
        {
            if (await this.KillScriptAsync(scriptKey.WorkspaceId, scriptKey.ScriptId, cancellationToken).ConfigureAwait(false))
            {
                killedCount++;
            }
        }

        return killedCount;
    }

    /// <summary>
    /// Composite key identifying a tracked script isolate.
    /// </summary>
    /// <param name="WorkspaceId">Workspace identifier.</param>
    /// <param name="ScriptId">Script identifier.</param>
    private readonly record struct ScriptTrackerKey(Guid WorkspaceId, Guid ScriptId);

    /// <summary>
    /// Mutable tracking record for an executing flow.
    /// </summary>
    /// <param name="info">Current flow snapshot.</param>
    /// <param name="cancelAction">Cancellation callback.</param>
    private sealed class TrackedFlowEntry(RunningActionFlowInfo info, Action cancelAction)
    {
        /// <summary>
        /// Gets or sets the current flow snapshot.
        /// </summary>
        public RunningActionFlowInfo Info { get; set; } = info;

        /// <summary>
        /// Gets the cancellation callback.
        /// </summary>
        public Action CancelAction { get; } = cancelAction;
    }

    /// <summary>
    /// Tracking record for a loaded ClearScript V8 isolate.
    /// </summary>
    /// <param name="SnapshotProvider">Live metrics provider.</param>
    /// <param name="KillAsync">Surgical kill callback.</param>
    private sealed record TrackedScriptEntry(
        Func<LoadedScriptInstanceInfo?> SnapshotProvider,
        Func<CancellationToken, ValueTask> KillAsync);
}

