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
using StreamActions.Core.Actions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Scripting;
using StreamActions.Scripting.Runtime;
using Xunit;

namespace StreamActions.Scripting.Tests;

public sealed class ScriptLifecycleAndTaskManagerTests
{
    #region EventBoundResidentScripts

    [Fact]
    [Trait("Member", "DispatchEventAsync")]
    public async Task DispatchEventAsync_SupportsMultiHandlerBindAndOnAndCommandDispatch()
    {
        using ScriptingTestRig rig = new();
        rig.GrantClearScriptFeature(rig.WorkspaceId, includePersistent: true);

        WorkspaceScript resident = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/resident.js",
            IsPermanentlyLoaded = true,
            RegisteredCommands = [new ScriptCommandRegistration("heist")],
            Code = """
                $.bind("command", async function(evt) {
                    if (evt.command === "heist") {
                        await context.sendChat(`${evt.sender} started a heist!`);
                    }
                });
                $.on("subscription", async function(evt) {
                    await context.kv.set("subs", "last", "user", evt.subscriber);
                });
                """,
        }, TestContext.Current.CancellationToken);

        using ClearScriptRuntimeService runtime = rig.CreateRuntimeService();

        ChatCommand cmd = new(
            rig.WorkspaceId,
            "twitch",
            "msg-1",
            new ChatUser("twitch", "u1", "alice", "Alice", PlatformUserDesignations.Subscriber),
            new ChatChannel(rig.WorkspaceId, "twitch", "c1", "streamer"),
            "!heist",
            rig.FakeTime.GetUtcNow(),
            "!",
            "heist",
            [],
            string.Empty);

        ScriptExecutionResult cmdResult = await runtime.DispatchCommandAsync(
            cmd,
            resident,
            resident.RegisteredCommands[0],
            TestContext.Current.CancellationToken);

        _ = cmdResult.Succeeded.Should().BeTrue();
        _ = rig.SentMessages.Should().ContainSingle().Which.Message.Should().Be("alice started a heist!");

        int subHandlers = await runtime.DispatchEventAsync(
            new ScriptEventDispatchRequest(
                rig.WorkspaceId,
                "subscription",
                new Dictionary<string, object?> { ["subscriber"] = "bob" }),
            TestContext.Current.CancellationToken);

        _ = subHandlers.Should().Be(1);
        string? lastSub = await rig.KeyValueStore.GetStringAsync(rig.WorkspaceId, "subs", "last", "user", null, TestContext.Current.CancellationToken);
        _ = lastSub.Should().Be("bob");
    }

    #endregion

    #region LifecycleAutoUnloading

    [Fact]
    [Trait("Member", "EvaluateLifecycleSweepAsync")]
    public async Task EvaluateLifecycleSweepAsync_EnforcesPostStreamGraceIdleTtlAndPersistentMode()
    {
        using ScriptingTestRig rig = new();
        rig.GrantClearScriptFeature(rig.WorkspaceId, includePersistent: true);

        rig.StreamStatusProvider.SetPlatformStatus(rig.WorkspaceId, "twitch", isLive: true);

        WorkspaceScript sessionScript = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/session.js",
            Code = "$.sessionVal = 1;",
        }, TestContext.Current.CancellationToken);

        WorkspaceScript persistentScript = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/permanent.js",
            IsPermanentlyLoaded = true,
            Code = "$.permVal = 1;",
        }, TestContext.Current.CancellationToken);

        using ClearScriptRuntimeService runtime = rig.CreateRuntimeService();

        _ = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = sessionScript.Id }, TestContext.Current.CancellationToken);
        _ = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = persistentScript.Id }, TestContext.Current.CancellationToken);
        _ = rig.ExecutionTracker.GetLoadedScripts(rig.WorkspaceId).Should().HaveCount(2);

        // Stream goes offline -> still within 30m grace window at +20m.
        rig.StreamStatusProvider.SetPlatformStatus(rig.WorkspaceId, "twitch", isLive: false);
        runtime.RecordStreamStatusChange(rig.WorkspaceId, isLive: false);
        rig.FakeTime.Advance(TimeSpan.FromMinutes(20));
        _ = (await runtime.EvaluateLifecycleSweepAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        // Past 30m post-stream grace -> StreamSession script unloads, PermanentResident stays loaded.
        rig.FakeTime.Advance(TimeSpan.FromMinutes(11));
        _ = (await runtime.EvaluateLifecycleSweepAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        _ = rig.ExecutionTracker.GetLoadedScripts(rig.WorkspaceId).Should().ContainSingle()
            .Which.ScriptId.Should().Be(persistentScript.Id);

        // Load an off-stream on-demand script -> unloads after 3m idle TTL.
        WorkspaceScript onDemandScript = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/ondemand.js",
            Code = "$.tmpVal = 1;",
        }, TestContext.Current.CancellationToken);

        _ = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = onDemandScript.Id }, TestContext.Current.CancellationToken);
        _ = rig.ExecutionTracker.GetLoadedScripts(rig.WorkspaceId).Should().HaveCount(2);

        rig.FakeTime.Advance(TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(1)));
        _ = (await runtime.EvaluateLifecycleSweepAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        _ = rig.ExecutionTracker.GetLoadedScripts(rig.WorkspaceId).Should().ContainSingle()
            .Which.ScriptId.Should().Be(persistentScript.Id);
    }

    #endregion

    #region TaskManagerSurgicalKill

    [Fact]
    [Trait("Member", "KillScriptAsync")]
    public async Task KillScriptAsync_RemovesExportsAndHandlersWithoutAffectingOtherScripts()
    {
        using ScriptingTestRig rig = new();
        rig.GrantClearScriptFeature(rig.WorkspaceId, includePersistent: true);

        WorkspaceScript scriptA = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/alpha.js",
            Code = """
                $.alphaExport = function() { return "alpha"; };
                $.bind("ping", function() {});
                """,
        }, TestContext.Current.CancellationToken);

        WorkspaceScript scriptB = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "scripts/beta.js",
            Code = """
                $.betaExport = function() { return "beta"; };
                function checkAlpha() { return typeof $.alphaExport; }
                """,
        }, TestContext.Current.CancellationToken);

        using ClearScriptRuntimeService runtime = rig.CreateRuntimeService();

        _ = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = scriptA.Id }, TestContext.Current.CancellationToken);
        _ = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = scriptB.Id }, TestContext.Current.CancellationToken);

        IReadOnlyList<LoadedScriptInstanceInfo> loadedBefore = rig.ExecutionTracker.GetLoadedScripts(rig.WorkspaceId);
        _ = loadedBefore.Should().HaveCount(2);
        LoadedScriptInstanceInfo alphaSnapshot = loadedBefore.Single(s => s.ScriptId == scriptA.Id);
        _ = alphaSnapshot.Exports.Should().Contain("alphaExport");
        _ = alphaSnapshot.BoundEvents.Should().Contain("ping");

        // Surgically kill script A.
        bool killed = await rig.ExecutionTracker.KillScriptAsync(rig.WorkspaceId, scriptA.Id, TestContext.Current.CancellationToken);
        _ = killed.Should().BeTrue();
        _ = rig.ExecutionTracker.GetLoadedScripts(rig.WorkspaceId).Should().ContainSingle().Which.ScriptId.Should().Be(scriptB.Id);

        // Script B remains operational and Script A's $ export & event binding are gone.
        ScriptExecutionResult checkRes = await runtime.ExecuteScriptAsync(
            new ScriptExecutionRequest { WorkspaceId = rig.WorkspaceId, ScriptId = scriptB.Id, EntryFunction = "checkAlpha" },
            TestContext.Current.CancellationToken);
        _ = checkRes.Succeeded.Should().BeTrue();
        _ = checkRes.ReturnValue.Should().Be("undefined");
        _ = (await runtime.DispatchEventAsync(new ScriptEventDispatchRequest(rig.WorkspaceId, "ping", new Dictionary<string, object?>()), TestContext.Current.CancellationToken)).Should().Be(0);

        // Root KillAllForWorkspaceAsync terminates remaining loaded scripts.
        int killedAll = await rig.ExecutionTracker.KillAllForWorkspaceAsync(rig.WorkspaceId, TestContext.Current.CancellationToken);
        _ = killedAll.Should().Be(1);
        _ = rig.ExecutionTracker.GetLoadedScripts(rig.WorkspaceId).Should().BeEmpty();
    }

    #endregion
}
