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
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Actions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using StreamActions.Core.Templates;
using StreamActions.Core.Tests.Commands;
using System.Collections.Concurrent;
using System.Text.Json;
using Xunit;

namespace StreamActions.Core.Tests.Actions;

public sealed class ActionFlowEngineTests
{
    #region Serialization

    [Fact]
    [Trait("Member", "ActionBlockJson")]
    public void ActionBlockJsonWhenRoundTrippingAllPolymorphicTypesPreservesTypesAndProperties()
    {
        Guid subTaskId = Guid.CreateVersion7();
        Guid profileId = Guid.CreateVersion7();
        Guid scriptId = Guid.CreateVersion7();

        List<ActionBlock> blocks =
        [
            new SendMessageActionBlock("Hello {sender}", TargetScope: OutboundTargetScope.AllActiveStreamPlatforms) { IsEnabled = false },
            new DelayActionBlock(250, "{var delayMs}"),
            new SetVariableActionBlock("greeting", "hi {sender}")
            {
                InlineCondition = new ActionBlockConditionGroup([new("Stream.IsOnline", ActionBlockComparisonOperator.Equals, "true")]),
            },
            new IfActionBlock(
                new ActionBlockConditionGroup([new("{var greeting}", ActionBlockComparisonOperator.Contains, "hi")]),
                ThenBlocks: [new PerformTaskActionBlock(subTaskId, new Dictionary<string, string> { ["arg1"] = "val1" }, "sub")],
                ElseBlocks: [new EnableProfileActionBlock(profileId)]),
            new DisableProfileActionBlock(profileId),
            new PlatformActionBlock("core.kv.set", new Dictionary<string, string> { ["key"] = "k", ["value"] = "v" }, "outVar"),
            new RunJsActionBlock(scriptId, "onTrigger", new Dictionary<string, string> { ["x"] = "1" }, "jsOut"),
        ];

        string json = JsonSerializer.Serialize<IReadOnlyList<ActionBlock>>(blocks, ActionBlockJson.Default);
        List<ActionBlock>? roundTripped = JsonSerializer.Deserialize<List<ActionBlock>>(json, ActionBlockJson.Default);

        _ = roundTripped.Should().NotBeNull();
        _ = roundTripped!.Should().HaveCount(7);
        _ = roundTripped[0].Should().BeOfType<SendMessageActionBlock>().Which.IsEnabled.Should().BeFalse();
        _ = roundTripped[2].Should().BeOfType<SetVariableActionBlock>().Which.InlineCondition.Should().NotBeNull();
        _ = roundTripped[3].Should().BeOfType<IfActionBlock>();
        _ = ((IfActionBlock)roundTripped[3]).ThenBlocks[0].Should().BeOfType<PerformTaskActionBlock>().Which.TargetTaskId.Should().Be(subTaskId);
        _ = ((IfActionBlock)roundTripped[3]).ElseBlocks![0].Should().BeOfType<EnableProfileActionBlock>().Which.TargetProfileId.Should().Be(profileId);
        _ = roundTripped[4].Should().BeOfType<DisableProfileActionBlock>().Which.TargetProfileId.Should().Be(profileId);
        _ = roundTripped[5].Should().BeOfType<PlatformActionBlock>().Which.ActionId.Should().Be("core.kv.set");
        _ = roundTripped[6].Should().BeOfType<RunJsActionBlock>().Which.ScriptId.Should().Be(scriptId);
    }

    #endregion

    #region ExecuteAsync

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncWhenStepDisabledInlineConditionAndDryRunTracksTraceAndPropagatesVariables()
    {
        using ActionBlockTestRig rig = new();
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        WorkspaceActionBlockTask draftTask = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "DryRun Flow",
            InputParameters = [new("user", "User")],
            Blocks =
            [
                new SetVariableActionBlock("tier", "3"),
                new SendMessageActionBlock("Disabled step") { IsEnabled = false },
                new SendMessageActionBlock("Skipped inline")
                {
                    InlineCondition = new ActionBlockConditionGroup([new("{var tier}", ActionBlockComparisonOperator.Equals, "1")]),
                },
                new IfActionBlock(
                    new ActionBlockConditionGroup([new("{var tier}", ActionBlockComparisonOperator.GreaterThanOrEqual, "2")]),
                    ThenBlocks: [new SendMessageActionBlock("Welcome {var user} at tier {var tier}!")],
                    ElseBlocks: [new SendMessageActionBlock("Fallback")]),
            ],
        };

        ActionExecutionTrace trace = await rig.FlowRunner.ExecuteAsync(
            new ActionExecutionRequest
            {
                WorkspaceId = workspaceId,
                TaskOverride = draftTask,
                DryRun = true,
                InitialVariables = new Dictionary<string, string> { ["user"] = "bob" },
            },
            TestContext.Current.CancellationToken);

        _ = trace.Succeeded.Should().BeTrue();
        _ = trace.DryRunOutboundMessages.Should().ContainSingle().Which.Message.Should().Be("Welcome bob at tier 3!");
        _ = rig.SentOutboundMessages.Should().BeEmpty();
        _ = trace.Steps.Should().Contain(s => s.Status == ActionStepExecutionStatus.SkippedDisabled);
        _ = trace.Steps.Should().Contain(s => s.Status == ActionStepExecutionStatus.SkippedCondition);
        _ = trace.FinalVariables["tier"].Should().Be("3");
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncWhenPerformTaskRecursesPastMaxTaskCallDepthThrowsRecursionLimitException()
    {
        using ActionBlockTestRig rig = new();
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;
        Guid taskAId = Guid.CreateVersion7();
        Guid taskBId = Guid.CreateVersion7();

        _ = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = taskAId,
            WorkspaceId = workspaceId,
            Name = "Task A",
            Blocks = [new PerformTaskActionBlock(taskBId, new Dictionary<string, string>())],
        }, TestContext.Current.CancellationToken);

        _ = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = taskBId,
            WorkspaceId = workspaceId,
            Name = "Task B",
            Blocks = [new PerformTaskActionBlock(taskAId, new Dictionary<string, string>())],
        }, TestContext.Current.CancellationToken);

        Func<Task> act = async () => _ = await rig.FlowRunner.ExecuteAsync(
            new ActionExecutionRequest { WorkspaceId = workspaceId, TaskId = taskAId },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = await act.Should().ThrowAsync<ActionFlowRecursionLimitException>();
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncWhenProfileRenamedEnableAndDisableBlocksStillResolveByGuid()
    {
        using ActionBlockTestRig rig = new();
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;
        Guid profileId = Guid.CreateVersion7();
        Guid taskId = Guid.CreateVersion7();

        WorkspaceActionBlockProfile profile = await rig.ActionBlockStore.UpsertProfileAsync(new WorkspaceActionBlockProfile
        {
            Id = profileId,
            WorkspaceId = workspaceId,
            Name = "Original Profile Name",
            IsEnabled = false,
            AttachedTaskId = taskId,
        }, TestContext.Current.CancellationToken);

        profile.Name = "Renamed Profile Name";
        _ = await rig.ActionBlockStore.UpsertProfileAsync(profile, TestContext.Current.CancellationToken);

        WorkspaceActionBlockTask controllerTask = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = taskId,
            WorkspaceId = workspaceId,
            Name = "Controller",
            Blocks = [new EnableProfileActionBlock(profileId)],
        }, TestContext.Current.CancellationToken);

        _ = await rig.FlowRunner.ExecuteAsync(
            new ActionExecutionRequest { WorkspaceId = workspaceId, TaskId = controllerTask.Id },
            TestContext.Current.CancellationToken);

        WorkspaceActionBlockProfile? afterEnable = await rig.ActionBlockStore.GetProfileByIdAsync(workspaceId, profileId, TestContext.Current.CancellationToken);
        _ = afterEnable!.IsEnabled.Should().BeTrue();

        controllerTask.Blocks = [new DisableProfileActionBlock(profileId)];
        _ = await rig.ActionBlockStore.UpsertTaskAsync(controllerTask, TestContext.Current.CancellationToken);

        _ = await rig.FlowRunner.ExecuteAsync(
            new ActionExecutionRequest { WorkspaceId = workspaceId, TaskId = controllerTask.Id },
            TestContext.Current.CancellationToken);

        WorkspaceActionBlockProfile? afterDisable = await rig.ActionBlockStore.GetProfileByIdAsync(workspaceId, profileId, TestContext.Current.CancellationToken);
        _ = afterDisable!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncWhenDelayExceedsMaxRuntimeQuotaCancelsDeterministicallyWithFakeTimeProvider()
    {
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using ActionBlockTestRig rig = new(fakeTime);
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        // Assign two Tier 1 global groups: baseline (15s) and pro (20s) -> MAX resolution gives 20s.
        await rig.AddGlobalGroupForWorkspaceAsync(workspaceId, new PermissionGroup
        {
            Id = Guid.CreateVersion7(),
            Name = "Plan A",
            MaxActionBlockFlowRuntimeSeconds = 10,
            MaxActionBlocksPerFlow = 25,
        });
        await rig.AddGlobalGroupForWorkspaceAsync(workspaceId, new PermissionGroup
        {
            Id = Guid.CreateVersion7(),
            Name = "Plan B",
            MaxActionBlockFlowRuntimeSeconds = 20,
            MaxActionBlocksPerFlow = 50,
        });

        WorkspaceResourceQuotas effectiveQuotas = await rig.PermissionService
            .GetEffectiveResourceQuotasAsync(workspaceId, TestContext.Current.CancellationToken);
        _ = effectiveQuotas.EffectiveMaxActionBlockFlowRuntimeSeconds.Should().Be(20);
        _ = effectiveQuotas.EffectiveMaxActionBlocksPerFlow.Should().Be(50);

        WorkspaceActionBlockTask longSleepTask = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "Sleep Task",
            Blocks = [new DelayActionBlock(30_000)],
        }, TestContext.Current.CancellationToken);

        Task<ActionExecutionTrace> running = rig.FlowRunner.ExecuteAsync(
            new ActionExecutionRequest { WorkspaceId = workspaceId, TaskId = longSleepTask.Id },
            TestContext.Current.CancellationToken).AsTask();

        // Wait until tracker registers the running flow before advancing FakeTimeProvider.
        Guid registeredId = await WaitForRunningFlowAsync(
            rig.ExecutionTracker,
            workspaceId,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = registeredId.Should().NotBe(Guid.Empty);

        fakeTime.Advance(TimeSpan.FromSeconds(21));
        ActionExecutionTrace trace = await running.ConfigureAwait(true);

        _ = trace.Succeeded.Should().BeFalse();
        _ = trace.FailureReason.Should().Contain("MaxActionBlockFlowRuntimeSeconds (20s)");
    }

    [Fact]
    [Trait("Member", "KillFlowAsync")]
    public async Task KillFlowAsyncWhenFlowIsRunningCancelsActiveFlowImmediately()
    {
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using ActionBlockTestRig rig = new(fakeTime);
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;

        WorkspaceActionBlockTask task = await rig.ActionBlockStore.UpsertTaskAsync(new WorkspaceActionBlockTask
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = "Cancellable Flow",
            Blocks = [new DelayActionBlock(10_000)],
        }, TestContext.Current.CancellationToken);

        Task<ActionExecutionTrace> running = rig.FlowRunner.ExecuteAsync(
            new ActionExecutionRequest { WorkspaceId = workspaceId, TaskId = task.Id },
            TestContext.Current.CancellationToken).AsTask();

        Guid executionId = await WaitForRunningFlowAsync(
            rig.ExecutionTracker,
            workspaceId,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        _ = executionId.Should().NotBe(Guid.Empty);
        bool killed = await rig.ExecutionTracker.KillFlowAsync(executionId, TestContext.Current.CancellationToken);
        _ = killed.Should().BeTrue();

        ActionExecutionTrace trace = await running.ConfigureAwait(true);
        _ = trace.Succeeded.Should().BeFalse();
        _ = trace.FailureReason.Should().Contain("Task Manager");
    }

    #endregion

    private static async Task<Guid> WaitForRunningFlowAsync(
        RuntimeExecutionTracker tracker,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < 1000; i++)
        {
            IReadOnlyList<RunningActionFlowInfo> flows = tracker.GetRunningFlows(workspaceId);
            if (flows.Count > 0)
            {
                return flows[0].ExecutionId;
            }

            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        }

        return Guid.Empty;
    }
}

internal sealed class ActionBlockTestRig : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly InMemoryEventBus _eventBus;
    public InMemoryActionBlockStore ActionBlockStore { get; }
    public CommandTestHarness.InMemoryPermissionStore PermissionStore { get; }
    public PermissionService PermissionService { get; }
    public RuntimeExecutionTracker ExecutionTracker { get; } = new();
    public DefaultStreamStatusProvider StreamStatusProvider { get; } = new();
    public ActionBlockCatalog Catalog { get; }
    public ActionFlowRunner FlowRunner { get; }
    public ActionBlockTriggerDispatcher TriggerDispatcher { get; }
    public ActionBlockBindingService BindingService { get; }
    public ConcurrentQueue<OutboundChatMessage> SentOutboundMessages { get; } = new();
    public RecordingOutboundDispatcher OutboundDispatcher { get; }

    public ActionBlockTestRig(TimeProvider? timeProvider = null)
    {
        TimeProvider effectiveTime = timeProvider ?? TimeProvider.System;
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        this._provider = services.BuildServiceProvider();
        HybridCache cache = this._provider.GetRequiredService<HybridCache>();
        this._eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        PermissionRegistry permRegistry = new();

        this.PermissionStore = new CommandTestHarness.InMemoryPermissionStore();
        this.PermissionStore.Workspaces[CommandTestHarness.DefaultWorkspaceId] = new Workspace
        {
            Id = CommandTestHarness.DefaultWorkspaceId,
            OwnerUserId = CommandTestHarness.DefaultOwnerUserId,
            Name = "Test Workspace",
            Slug = "test",
            PrimaryPlatformId = "twitch",
        };

        this.PermissionService = new PermissionService(this.PermissionStore, permRegistry, cache, this._eventBus);
        this.ActionBlockStore = new InMemoryActionBlockStore();
        TagRegistry tagRegistry = new(CoreTagRegistryFactory.CreateDefaultHandlers());
        this.OutboundDispatcher = new RecordingOutboundDispatcher(this.SentOutboundMessages);

        this.Catalog = new ActionBlockCatalog(
            CoreActionBlockCatalogFactory.CreateDefaultActions(),
            CoreActionBlockCatalogFactory.CreateDefaultTriggers());

        this.FlowRunner = new ActionFlowRunner(
            this.ActionBlockStore,
            this.Catalog,
            tagRegistry,
            this.ExecutionTracker,
            this.PermissionService,
            this.OutboundDispatcher,
            this.StreamStatusProvider,
            timeProvider: effectiveTime);

        this.TriggerDispatcher = new ActionBlockTriggerDispatcher(
            this.ActionBlockStore,
            this.FlowRunner,
            tagRegistry,
            this.PermissionService,
            this.StreamStatusProvider,
            timeProvider: effectiveTime);

        this.BindingService = new ActionBlockBindingService(this.ActionBlockStore, this.Catalog);
    }

    public ValueTask AddGlobalGroupForWorkspaceAsync(Guid workspaceId, PermissionGroup group, params PermissionGrant[] grants)
    {
        this.PermissionStore.GlobalGroups[group.Id] = group;
        this.PermissionStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment
        {
            WorkspaceId = workspaceId,
            GlobalGroupId = group.Id,
        });
        foreach (PermissionGrant grant in grants)
        {
            grant.GroupId = group.Id;
            this.PermissionStore.Grants.Add(grant);
        }

        return this.PermissionService.InvalidateWorkspaceAsync(workspaceId, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        this._eventBus.Dispose();
        this._provider.Dispose();
    }

    internal sealed class RecordingOutboundDispatcher(ConcurrentQueue<OutboundChatMessage> queue) : IOutboundChatDispatcher
    {
        public ValueTask EnqueueAsync(OutboundChatMessage message, CancellationToken cancellationToken = default)
        {
            queue.Enqueue(message);
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class InMemoryActionBlockStore : IActionBlockStore
    {
        private readonly ConcurrentDictionary<Guid, WorkspaceActionBlockTask> _tasks = new();
        private readonly ConcurrentDictionary<Guid, WorkspaceActionBlockProfile> _profiles = new();

        public ValueTask<IReadOnlyList<WorkspaceActionBlockTask>> GetTasksAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceActionBlockTask>>([.. this._tasks.Values.Where(t => t.WorkspaceId == workspaceId)]);

        public ValueTask<WorkspaceActionBlockTask?> GetTaskByIdAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._tasks.TryGetValue(taskId, out WorkspaceActionBlockTask? t) && t.WorkspaceId == workspaceId ? t : null);

        public ValueTask<WorkspaceActionBlockTask?> GetTaskByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._tasks.Values.FirstOrDefault(t => t.WorkspaceId == workspaceId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)));

        public ValueTask<WorkspaceActionBlockTask> UpsertTaskAsync(WorkspaceActionBlockTask task, CancellationToken cancellationToken = default)
        {
            if (task.Id == Guid.Empty)
            {
                task.Id = Guid.CreateVersion7();
            }

            this._tasks[task.Id] = task;
            return ValueTask.FromResult(task);
        }

        public ValueTask<bool> DeleteTaskAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._tasks.TryGetValue(taskId, out WorkspaceActionBlockTask? t) && t.WorkspaceId == workspaceId && this._tasks.TryRemove(taskId, out _));

        public ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetProfilesAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceActionBlockProfile>>([.. this._profiles.Values.Where(p => p.WorkspaceId == workspaceId)]);

        public ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceActionBlockProfile>>([.. this._profiles.Values]);

        public ValueTask<WorkspaceActionBlockProfile?> GetProfileByIdAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._profiles.TryGetValue(profileId, out WorkspaceActionBlockProfile? p) && p.WorkspaceId == workspaceId ? p : null);

        public ValueTask<WorkspaceActionBlockProfile?> GetProfileByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._profiles.Values.FirstOrDefault(p => p.WorkspaceId == workspaceId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));

        public ValueTask<WorkspaceActionBlockProfile> UpsertProfileAsync(WorkspaceActionBlockProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile.Id == Guid.Empty)
            {
                profile.Id = Guid.CreateVersion7();
            }

            this._profiles[profile.Id] = profile;
            return ValueTask.FromResult(profile);
        }

        public ValueTask<bool> SetProfileEnabledAsync(Guid workspaceId, Guid profileId, bool isEnabled, CancellationToken cancellationToken = default)
        {
            if (this._profiles.TryGetValue(profileId, out WorkspaceActionBlockProfile? profile) && profile.WorkspaceId == workspaceId)
            {
                profile.IsEnabled = isEnabled;
                return ValueTask.FromResult(true);
            }

            return ValueTask.FromResult(false);
        }

        public ValueTask<bool> DeleteProfileAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._profiles.TryGetValue(profileId, out WorkspaceActionBlockProfile? p) && p.WorkspaceId == workspaceId && this._profiles.TryRemove(profileId, out _));
    }
}

