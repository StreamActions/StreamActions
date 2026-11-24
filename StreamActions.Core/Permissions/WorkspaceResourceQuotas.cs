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

namespace StreamActions.Core.Permissions;

/// <summary>
/// Defines Tier 1 global resource consumption quotas applied to a workspace across Action Block flows and ClearScript V8 isolates.
/// </summary>
/// <param name="MaxClearScriptTotalRamBytes">
/// The maximum combined V8 heap memory in bytes across all loaded ClearScript script isolates for the workspace, or <see langword="null"/> when inheriting the baseline quota.
/// </param>
/// <param name="MaxActionBlocksPerFlow">
/// The maximum number of Action Blocks allowed per Task and per root flow execution, or <see langword="null"/> when inheriting the baseline quota.
/// </param>
/// <param name="MaxActionBlockFlowRuntimeSeconds">
/// The maximum wall-clock execution time in seconds for a single root Action Block flow, or <see langword="null"/> when inheriting the baseline quota.
/// </param>
public sealed record WorkspaceResourceQuotas(
    long? MaxClearScriptTotalRamBytes = null,
    int? MaxActionBlocksPerFlow = null,
    int? MaxActionBlockFlowRuntimeSeconds = null)
{
    /// <summary>
    /// Default ClearScript V8 total workspace RAM quota in bytes (16 MB).
    /// </summary>
    public const long DefaultMaxClearScriptTotalRamBytes = 16L * 1024L * 1024L;

    /// <summary>
    /// Default maximum number of Action Blocks allowed per flow (25 blocks).
    /// </summary>
    public const int DefaultMaxActionBlocksPerFlow = 25;

    /// <summary>
    /// Default maximum runtime in seconds for a root Action Block flow (15 seconds).
    /// </summary>
    public const int DefaultMaxActionBlockFlowRuntimeSeconds = 15;

    /// <summary>
    /// Gets the seeded baseline resource quotas used by the <c>Default Workspace Plan</c> and as the fallback when no assigned global group specifies a positive limit.
    /// </summary>
    public static WorkspaceResourceQuotas DefaultBaseline { get; } = new(
        MaxClearScriptTotalRamBytes: DefaultMaxClearScriptTotalRamBytes,
        MaxActionBlocksPerFlow: DefaultMaxActionBlocksPerFlow,
        MaxActionBlockFlowRuntimeSeconds: DefaultMaxActionBlockFlowRuntimeSeconds);

    /// <summary>
    /// Gets the resolved ClearScript total RAM quota in bytes, falling back to <see cref="DefaultMaxClearScriptTotalRamBytes"/> when unset or non-positive.
    /// </summary>
    public long EffectiveMaxClearScriptTotalRamBytes =>
        this.MaxClearScriptTotalRamBytes is > 0 ? this.MaxClearScriptTotalRamBytes.Value : DefaultMaxClearScriptTotalRamBytes;

    /// <summary>
    /// Gets the resolved Action Blocks per flow quota, falling back to <see cref="DefaultMaxActionBlocksPerFlow"/> when unset or non-positive.
    /// </summary>
    public int EffectiveMaxActionBlocksPerFlow =>
        this.MaxActionBlocksPerFlow is > 0 ? this.MaxActionBlocksPerFlow.Value : DefaultMaxActionBlocksPerFlow;

    /// <summary>
    /// Gets the resolved Action Block flow runtime ceiling in seconds, falling back to <see cref="DefaultMaxActionBlockFlowRuntimeSeconds"/> when unset or non-positive.
    /// </summary>
    public int EffectiveMaxActionBlockFlowRuntimeSeconds =>
        this.MaxActionBlockFlowRuntimeSeconds is > 0 ? this.MaxActionBlockFlowRuntimeSeconds.Value : DefaultMaxActionBlockFlowRuntimeSeconds;

    /// <summary>
    /// Resolves effective workspace resource quotas using the most-permissive (<c>MAX</c>) non-null positive quota independently across all assigned Tier 1 global groups, falling back to <see cref="DefaultBaseline"/>.
    /// </summary>
    /// <param name="globalGroups">The Tier 1 global permission groups assigned to the workspace.</param>
    /// <returns>The resolved <see cref="WorkspaceResourceQuotas"/> with all fields populated.</returns>
    public static WorkspaceResourceQuotas ResolveEffective(IEnumerable<PermissionGroup> globalGroups)
    {
        ArgumentNullException.ThrowIfNull(globalGroups);

        long? maxRam = null;
        int? maxBlocks = null;
        int? maxRuntimeSeconds = null;

        foreach (PermissionGroup group in globalGroups)
        {
            if (group is null)
            {
                continue;
            }

            if (group.MaxClearScriptTotalRamBytes is > 0)
            {
                maxRam = maxRam.HasValue
                    ? Math.Max(maxRam.Value, group.MaxClearScriptTotalRamBytes.Value)
                    : group.MaxClearScriptTotalRamBytes.Value;
            }

            if (group.MaxActionBlocksPerFlow is > 0)
            {
                maxBlocks = maxBlocks.HasValue
                    ? Math.Max(maxBlocks.Value, group.MaxActionBlocksPerFlow.Value)
                    : group.MaxActionBlocksPerFlow.Value;
            }

            if (group.MaxActionBlockFlowRuntimeSeconds is > 0)
            {
                maxRuntimeSeconds = maxRuntimeSeconds.HasValue
                    ? Math.Max(maxRuntimeSeconds.Value, group.MaxActionBlockFlowRuntimeSeconds.Value)
                    : group.MaxActionBlockFlowRuntimeSeconds.Value;
            }
        }

        return new WorkspaceResourceQuotas(
            MaxClearScriptTotalRamBytes: maxRam ?? DefaultMaxClearScriptTotalRamBytes,
            MaxActionBlocksPerFlow: maxBlocks ?? DefaultMaxActionBlocksPerFlow,
            MaxActionBlockFlowRuntimeSeconds: maxRuntimeSeconds ?? DefaultMaxActionBlockFlowRuntimeSeconds);
    }
}

