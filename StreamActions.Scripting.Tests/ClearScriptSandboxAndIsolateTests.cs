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

using FluentAssertions;
using StreamActions.Core.Scripting;
using StreamActions.Scripting.Runtime;
using Xunit;

namespace StreamActions.Scripting.Tests;

public sealed class ClearScriptSandboxAndIsolateTests
{
    #region OptionAIsolationAndRequire

    [Fact]
    [Trait("Member", "ExecuteScriptAsync")]
    public async Task ExecuteScriptAsync_IsolatesTopLevelVariablesWhileSharingDollarExportsViaRequire()
    {
        using ScriptingTestRig rig = new();
        rig.GrantClearScriptFeature(rig.WorkspaceId);

        _ = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "lib/helpers.js",
            Code = """
                const secretInternal = "only-in-lib";
                let loadCount = (typeof $.libLoadCount === "number" ? $.libLoadCount : 0) + 1;
                $.libLoadCount = loadCount;
                $.addNumbers = function(a, b) {
                    return Number(a) + Number(b);
                };
                """,
        }, TestContext.Current.CancellationToken);

        WorkspaceScript scriptA = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/a.js",
            Code = """
                require("lib/helpers.js");
                require("lib/helpers.js"); // second require is a no-op
                const localSecret = "alpha-only";
                function run() {
                    const leaksLib = (typeof secretInternal !== "undefined");
                    return `${$.addNumbers(4, 5)}:${$.libLoadCount}:${leaksLib}`;
                }
                """,
        }, TestContext.Current.CancellationToken);

        WorkspaceScript scriptB = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/b.js",
            Code = """
                require("lib/helpers.js");
                function run() {
                    return typeof localSecret;
                }
                """,
        }, TestContext.Current.CancellationToken);

        using ClearScriptRuntimeService runtime = rig.CreateRuntimeService();

        ScriptExecutionResult resA = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest
        {
            WorkspaceId = rig.WorkspaceId,
            ScriptId = scriptA.Id,
            EntryFunction = "run",
        }, TestContext.Current.CancellationToken);

        ScriptExecutionResult resB = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest
        {
            WorkspaceId = rig.WorkspaceId,
            ScriptId = scriptB.Id,
            EntryFunction = "run",
        }, TestContext.Current.CancellationToken);

        _ = resA.Succeeded.Should().BeTrue(resA.ErrorMessage);
        _ = resA.ReturnValue.Should().Be("9:1:false");
        _ = resB.Succeeded.Should().BeTrue(resB.ErrorMessage);
        _ = resB.ReturnValue.Should().Be("undefined");
    }

    #endregion

    #region SecurityAndResourceBoundaries

    [Fact]
    [Trait("Member", "ExecuteScriptAsync")]
    public async Task ExecuteScriptAsync_WhenInfiniteLoopRuns_AbortsWithinCpuTimeout()
    {
        using ScriptingTestRig rig = new();
        rig.GrantClearScriptFeature(rig.WorkspaceId);
        rig.InstanceSettingsService.Current.ClearScriptExecutionTimeoutSeconds = 1;

        WorkspaceScript infinite = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/infinite.js",
            Code = "while (true) {}",
        }, TestContext.Current.CancellationToken);

        using ClearScriptRuntimeService runtime = rig.CreateRuntimeService();

        ScriptExecutionResult result = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest
        {
            WorkspaceId = rig.WorkspaceId,
            ScriptId = infinite.Id,
        }, TestContext.Current.CancellationToken);

        _ = result.Succeeded.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("CPU execution timeout");
    }

    [Fact]
    [Trait("Member", "ExecuteScriptAsync")]
    public async Task ExecuteScriptAsync_IsolatesContextKvByWorkspaceAndRespectsHostAndPermissionGates()
    {
        using ScriptingTestRig rig = new();
        Guid workspaceB = Guid.CreateVersion7();
        rig.PermissionStore.SeedWorkspace(new Core.Models.Entities.Workspace
        {
            Id = workspaceB,
            OwnerUserId = rig.OwnerUserId,
            Name = "Workspace B",
            Slug = "ws-b",
            PrimaryPlatformId = "twitch",
        });

        rig.GrantClearScriptFeature(rig.WorkspaceId);
        rig.GrantClearScriptFeature(workspaceB);

        WorkspaceScript scriptA = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/kv.js",
            Code = """
                async function run() {
                    await context.kv.set("t", "s", "k", "secret-A");
                    return await context.kv.getString("t", "s", "k", "none");
                }
                """,
        }, TestContext.Current.CancellationToken);

        WorkspaceScript scriptB = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = workspaceB,
            Path = "scripts/kv.js",
            Code = """
                async function run() {
                    return await context.kv.getString("t", "s", "k", "none");
                }
                """,
        }, TestContext.Current.CancellationToken);

        using ClearScriptRuntimeService runtime = rig.CreateRuntimeService();

        ScriptExecutionResult resA = await runtime.ExecuteScriptAsync(
            new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = scriptA.Id, EntryFunction = "run" },
            TestContext.Current.CancellationToken);

        ScriptExecutionResult resB = await runtime.ExecuteScriptAsync(
            new ScriptExecutionRequest { WorkspaceId = workspaceB, ScriptId = scriptB.Id, EntryFunction = "run" },
            TestContext.Current.CancellationToken);

        _ = resA.ReturnValue.Should().Be("secret-A");
        _ = resB.ReturnValue.Should().Be("none");

        // Host kill-switch Security:DisableClearScript blocks execution immediately.
        rig.BootstrapOptions.Security.DisableClearScript = true;
        ScriptExecutionResult disabledResult = await runtime.ExecuteScriptAsync(
            new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = scriptA.Id, EntryFunction = "run" },
            TestContext.Current.CancellationToken);

        _ = disabledResult.Succeeded.Should().BeFalse();
        _ = disabledResult.ErrorMessage.Should().Contain("DisableClearScript");
    }

    [Fact]
    [Trait("Member", "ExecuteScriptAsync")]
    public async Task ExecuteScriptAsync_WhenMemoryExceedsTierOneMaxRamQuota_RejectsAllocation()
    {
        using ScriptingTestRig rig = new();
        // Set tight 128 KB Tier 1 RAM quota so loading/allocating exceeds MaxClearScriptTotalRamBytes.
        rig.GrantClearScriptFeature(rig.WorkspaceId, ramQuotaBytes: 128L * 1024L);

        WorkspaceScript hog = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/hog.js",
            Code = "const big = new Array(500000).fill('x');",
        }, TestContext.Current.CancellationToken);

        using ClearScriptRuntimeService runtime = rig.CreateRuntimeService();

        ScriptExecutionResult result = await runtime.ExecuteScriptAsync(
            new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = hog.Id },
            TestContext.Current.CancellationToken);

        _ = result.Succeeded.Should().BeFalse();
        _ = result.ErrorMessage.Should().Contain("MaxClearScriptTotalRamBytes");
    }

    #endregion
}

