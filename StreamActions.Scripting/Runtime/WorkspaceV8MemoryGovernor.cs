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

namespace StreamActions.Scripting.Runtime;

/// <summary>
/// Exception thrown when a workspace's ClearScript V8 isolates exceed its Tier 1 <c>MaxClearScriptTotalRamBytes</c> quota.
/// </summary>
public sealed class ScriptMemoryQuotaExceededException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptMemoryQuotaExceededException"/> class.
    /// </summary>
    public ScriptMemoryQuotaExceededException()
        : base("Workspace ClearScript V8 memory quota exceeded.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptMemoryQuotaExceededException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ScriptMemoryQuotaExceededException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptMemoryQuotaExceededException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ScriptMemoryQuotaExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Monitors total V8 heap memory across all loaded <see cref="ScriptIsolateInstance"/> isolates in a workspace
/// against the Tier 1 <c>MaxClearScriptTotalRamBytes</c> ceiling.
/// </summary>
internal sealed class WorkspaceV8MemoryGovernor
{
    /// <summary>
    /// Computes the combined V8 used heap bytes across all loaded isolates in the workspace.
    /// </summary>
    /// <param name="isolates">Currently loaded script isolates.</param>
    /// <returns>Total used heap bytes.</returns>
    public static long GetTotalUsedHeapBytes(IEnumerable<ScriptIsolateInstance> isolates)
    {
        ArgumentNullException.ThrowIfNull(isolates);

        long total = 0;
        foreach (ScriptIsolateInstance isolate in isolates)
        {
            total += isolate.GetUsedHeapBytes();
        }

        return total;
    }

    /// <summary>
    /// Validates whether the workspace has remaining memory headroom before loading a new isolate
    /// and returns the clamped per-isolate heap limit.
    /// </summary>
    /// <param name="isolates">Currently loaded script isolates.</param>
    /// <param name="workspaceQuotaBytes">Effective Tier 1 <c>MaxClearScriptTotalRamBytes</c> limit.</param>
    /// <returns>The clamped <see cref="UIntPtr"/> heap limit for a new V8 isolate.</returns>
    public static nuint ComputeNextIsolateHeapLimit(
        IEnumerable<ScriptIsolateInstance> isolates,
        long workspaceQuotaBytes)
    {
        long used = GetTotalUsedHeapBytes(isolates);
        long remaining = workspaceQuotaBytes - used;
        return remaining <= 0
            ? throw new ScriptMemoryQuotaExceededException(
                $"Workspace ClearScript RAM usage ({used} bytes) has reached Tier 1 MaxClearScriptTotalRamBytes ({workspaceQuotaBytes} bytes).")
            : (nuint)remaining;
    }

    /// <summary>
    /// Recomputes and clamps heap limits across all active isolates and verifies total workspace heap stays within quota.
    /// </summary>
    /// <param name="isolates">Currently loaded script isolates.</param>
    /// <param name="workspaceQuotaBytes">Effective Tier 1 <c>MaxClearScriptTotalRamBytes</c> limit.</param>
    public static void EnforceWorkspaceQuota(
        IEnumerable<ScriptIsolateInstance> isolates,
        long workspaceQuotaBytes)
    {
        List<ScriptIsolateInstance> snapshot = [.. isolates];
        long totalUsed = GetTotalUsedHeapBytes(snapshot);
        if (totalUsed > workspaceQuotaBytes)
        {
            throw new ScriptMemoryQuotaExceededException(
                $"Workspace ClearScript RAM usage ({totalUsed} bytes) exceeded Tier 1 MaxClearScriptTotalRamBytes ({workspaceQuotaBytes} bytes).");
        }

        foreach (ScriptIsolateInstance isolate in snapshot)
        {
            long otherUsed = totalUsed - isolate.GetUsedHeapBytes();
            long maxForIsolate = Math.Max(isolate.GetUsedHeapBytes(), workspaceQuotaBytes - otherUsed);
            if (maxForIsolate > 0)
            {
                isolate.UpdateMaxRuntimeHeapSize((nuint)maxForIsolate);
            }
        }
    }
}

