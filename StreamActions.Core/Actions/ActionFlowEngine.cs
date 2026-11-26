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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Permissions;
using StreamActions.Core.Scripting;
using StreamActions.Core.Templates;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Actions;

/// <summary>
/// Executes <see cref="WorkspaceActionBlockTask"/> Action Block flows with Tier 1 resource quotas, recursion protection, dry-run tracing, and Task Manager cancellation.
/// </summary>
public sealed partial class ActionFlowRunner : IActionFlowRunner
{
    /// <summary>
    /// Optional Action Block persistence store.
    /// </summary>
    private readonly IActionBlockStore? _actionBlockStore;

    /// <summary>
    /// Catalog of platform action handlers and trigger descriptors.
    /// </summary>
    private readonly IActionBlockCatalog _catalog;

    /// <summary>
    /// Tag registry for evaluating <c>{tag}</c> expressions.
    /// </summary>
    private readonly ITagRegistry _tagRegistry;

    /// <summary>
    /// Runtime tracker for Task Manager observability and cancellation.
    /// </summary>
    private readonly IRuntimeExecutionTracker _executionTracker;

    /// <summary>
    /// Outbound chat dispatcher for live message delivery.
    /// </summary>
    private readonly IOutboundChatDispatcher? _outboundChatDispatcher;

    /// <summary>
    /// Optional stream status provider for condition state properties.
    /// </summary>
    private readonly IStreamStatusProvider? _streamStatusProvider;

    /// <summary>
    /// Optional ClearScript V8 action executor.
    /// </summary>
    private readonly IRunJsActionExecutor? _runJsExecutor;

    /// <summary>
    /// Optional DI scope factory for resolving scoped services such as <see cref="IPermissionService"/> and platform session providers.
    /// </summary>
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// Optional root service provider used when no scope factory is supplied.
    /// </summary>
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Optional direct <see cref="IPermissionService"/> override for unit testing.
    /// </summary>
    private readonly IPermissionService? _permissionServiceOverride;

    /// <summary>
    /// Time provider driving runtime timeouts and <see cref="DelayActionBlock"/> delays.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Optional logger.
    /// </summary>
    private readonly ILogger<ActionFlowRunner>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowRunner"/> class.
    /// </summary>
    /// <param name="actionBlockStore">Action Block persistence store.</param>
    /// <param name="catalog">Action Block action and trigger catalog.</param>
    /// <param name="tagRegistry">Tag registry.</param>
    /// <param name="executionTracker">Runtime execution tracker.</param>
    /// <param name="outboundChatDispatcher">Optional outbound chat dispatcher.</param>
    /// <param name="streamStatusProvider">Optional stream status provider.</param>
    /// <param name="runJsExecutor">Optional ClearScript V8 action executor.</param>
    /// <param name="scopeFactory">Optional DI scope factory.</param>
    /// <param name="serviceProvider">Optional root service provider.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    /// <param name="logger">Optional logger.</param>
    [ActivatorUtilitiesConstructor]
    public ActionFlowRunner(
        IActionBlockCatalog catalog,
        ITagRegistry tagRegistry,
        IRuntimeExecutionTracker executionTracker,
        IActionBlockStore? actionBlockStore = null,
        IOutboundChatDispatcher? outboundChatDispatcher = null,
        IStreamStatusProvider? streamStatusProvider = null,
        IRunJsActionExecutor? runJsExecutor = null,
        IServiceScopeFactory? scopeFactory = null,
        IServiceProvider? serviceProvider = null,
        TimeProvider? timeProvider = null,
        ILogger<ActionFlowRunner>? logger = null)
        : this(
            actionBlockStore,
            catalog,
            tagRegistry,
            executionTracker,
            permissionServiceOverride: null,
            outboundChatDispatcher,
            streamStatusProvider,
            runJsExecutor,
            scopeFactory,
            serviceProvider,
            timeProvider,
            logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowRunner"/> class with an explicit <see cref="IPermissionService"/> override for unit tests.
    /// </summary>
    internal ActionFlowRunner(
        IActionBlockStore? actionBlockStore,
        IActionBlockCatalog catalog,
        ITagRegistry tagRegistry,
        IRuntimeExecutionTracker executionTracker,
        IPermissionService? permissionServiceOverride,
        IOutboundChatDispatcher? outboundChatDispatcher = null,
        IStreamStatusProvider? streamStatusProvider = null,
        IRunJsActionExecutor? runJsExecutor = null,
        IServiceScopeFactory? scopeFactory = null,
        IServiceProvider? serviceProvider = null,
        TimeProvider? timeProvider = null,
        ILogger<ActionFlowRunner>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(tagRegistry);
        ArgumentNullException.ThrowIfNull(executionTracker);

        this._actionBlockStore = actionBlockStore;
        this._catalog = catalog;
        this._tagRegistry = tagRegistry;
        this._executionTracker = executionTracker;
        this._permissionServiceOverride = permissionServiceOverride;
        this._outboundChatDispatcher = outboundChatDispatcher;
        this._streamStatusProvider = streamStatusProvider;
        this._runJsExecutor = runJsExecutor;
        this._scopeFactory = scopeFactory;
        this._serviceProvider = serviceProvider;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._logger = logger;
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Flow execution records step failures and returns a structured ActionExecutionTrace.")]
    public async ValueTask<ActionExecutionTrace> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Guid executionId = Guid.CreateVersion7();
        DateTimeOffset startedAtUtc = this._timeProvider.GetUtcNow();
        List<ActionStepTraceEntry> traceSteps = [];
        List<OutboundChatMessage> dryRunMessages = [];

        AsyncServiceScope? ownedScope = this._scopeFactory?.CreateAsyncScope();

        try
        {
            IServiceProvider? scopedServices = ownedScope?.ServiceProvider ?? this._serviceProvider;
            IPermissionService? permissionService = this._permissionServiceOverride
                ?? scopedServices?.GetService<IPermissionService>();

            WorkspaceActionBlockTask? rootTask = request.TaskOverride;
            if (rootTask is null && request.TaskId.HasValue && this._actionBlockStore is not null)
            {
                rootTask = await this._actionBlockStore
                    .GetTaskByIdAsync(request.WorkspaceId, request.TaskId.Value, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (rootTask is null)
            {
                return new ActionExecutionTrace(
                    executionId,
                    Succeeded: false,
                    TotalExecutedSteps: 0,
                    TotalDuration: TimeSpan.Zero,
                    Steps: traceSteps,
                    FinalVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    DryRunOutboundMessages: dryRunMessages,
                    FailureReason: "Target task was not found.");
            }

            if (!rootTask.IsEnabled && request.TaskOverride is null)
            {
                return new ActionExecutionTrace(
                    executionId,
                    Succeeded: true,
                    TotalExecutedSteps: 0,
                    TotalDuration: TimeSpan.Zero,
                    Steps: traceSteps,
                    FinalVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    DryRunOutboundMessages: dryRunMessages,
                    FailureReason: "Task is disabled.");
            }

            WorkspaceResourceQuotas quotas = permissionService is not null
                ? await permissionService.GetEffectiveResourceQuotasAsync(request.WorkspaceId, cancellationToken).ConfigureAwait(false)
                : WorkspaceResourceQuotas.DefaultBaseline;

            int maxBlocksPerFlow = quotas.EffectiveMaxActionBlocksPerFlow;
            int maxRuntimeSeconds = quotas.EffectiveMaxActionBlockFlowRuntimeSeconds;

            int rootBlockCount = ActionBlockJson.CountTotalBlocks(rootTask.Blocks);
            if (rootBlockCount > maxBlocksPerFlow)
            {
                throw new ActionFlowQuotaExceededException(
                    $"Task '{rootTask.Name}' defines {rootBlockCount} blocks, exceeding workspace quota MaxActionBlocksPerFlow ({maxBlocksPerFlow}).");
            }

            using CancellationTokenSource terminateCts = new();
            using CancellationTokenSource runtimeBudgetCts = new(TimeSpan.FromSeconds(maxRuntimeSeconds), this._timeProvider);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                terminateCts.Token,
                runtimeBudgetCts.Token);

            this._executionTracker.RegisterFlow(
                new RunningActionFlowInfo(
                    executionId,
                    request.WorkspaceId,
                    rootTask.Id,
                    rootTask.Name,
                    request.ProfileId,
                    request.ProfileName,
                    CurrentStepIndex: 0,
                    CurrentStepName: rootTask.Name,
                    startedAtUtc,
                    maxRuntimeSeconds),
                () => terminateCts.Cancel());

            PlatformUserIdentity actor = request.ActorIdentity
                ?? request.Message?.Sender.ToIdentity()
                ?? new PlatformUserIdentity(request.PlatformId, "system", "system", PlatformUserDesignations.Broadcaster);

            IMessageEnvelope messageEnvelope = request.Message ?? CreateSyntheticMessage(request, actor, startedAtUtc);

            Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
            foreach (TaskParameterDefinition paramDef in rootTask.InputParameters)
            {
                if (!string.IsNullOrWhiteSpace(paramDef.Name) && paramDef.DefaultValue is not null)
                {
                    variables[paramDef.Name] = paramDef.DefaultValue;
                }
            }

            if (request.InitialVariables is not null)
            {
                foreach (KeyValuePair<string, string> kvp in request.InitialVariables)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        variables[kvp.Key] = kvp.Value;
                    }
                }
            }

            FlowExecutionState sharedState = new(
                this,
                executionId,
                request.WorkspaceId,
                request.PlatformId,
                request.ChannelId,
                request.DryRun,
                actor,
                messageEnvelope,
                scopedServices,
                maxBlocksPerFlow,
                traceSteps,
                dryRunMessages);

            ActionExecutionContextImpl rootContext = new(
                sharedState,
                rootTask.Id,
                callDepth: 0,
                activeTaskStack: [rootTask.Id],
                variables);

            try
            {
                await rootContext.ExecuteBlockListAsync(rootTask.Blocks, linkedCts.Token).ConfigureAwait(false);
                return new ActionExecutionTrace(
                    executionId,
                    Succeeded: true,
                    TotalExecutedSteps: sharedState.ExecutedStepsCount,
                    TotalDuration: this._timeProvider.GetUtcNow() - startedAtUtc,
                    Steps: traceSteps,
                    FinalVariables: new Dictionary<string, string>(rootContext.Variables, StringComparer.OrdinalIgnoreCase),
                    DryRunOutboundMessages: dryRunMessages);
            }
            catch (OperationCanceledException) when (runtimeBudgetCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested && !terminateCts.IsCancellationRequested)
            {
                return new ActionExecutionTrace(
                    executionId,
                    Succeeded: false,
                    TotalExecutedSteps: sharedState.ExecutedStepsCount,
                    TotalDuration: this._timeProvider.GetUtcNow() - startedAtUtc,
                    Steps: traceSteps,
                    FinalVariables: new Dictionary<string, string>(rootContext.Variables, StringComparer.OrdinalIgnoreCase),
                    DryRunOutboundMessages: dryRunMessages,
                    FailureReason: $"Action block flow exceeded MaxActionBlockFlowRuntimeSeconds ({maxRuntimeSeconds}s).");
            }
            catch (OperationCanceledException) when (terminateCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return new ActionExecutionTrace(
                    executionId,
                    Succeeded: false,
                    TotalExecutedSteps: sharedState.ExecutedStepsCount,
                    TotalDuration: this._timeProvider.GetUtcNow() - startedAtUtc,
                    Steps: traceSteps,
                    FinalVariables: new Dictionary<string, string>(rootContext.Variables, StringComparer.OrdinalIgnoreCase),
                    DryRunOutboundMessages: dryRunMessages,
                    FailureReason: "Action block flow was terminated by Task Manager.");
            }
            catch (ActionFlowRecursionLimitException)
            {
                throw;
            }
            catch (ActionFlowQuotaExceededException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (this._logger is not null)
                {
                    LogFlowFailedWarning(this._logger, ex, executionId, rootTask.Id);
                }

                return new ActionExecutionTrace(
                    executionId,
                    Succeeded: false,
                    TotalExecutedSteps: sharedState.ExecutedStepsCount,
                    TotalDuration: this._timeProvider.GetUtcNow() - startedAtUtc,
                    Steps: traceSteps,
                    FinalVariables: new Dictionary<string, string>(rootContext.Variables, StringComparer.OrdinalIgnoreCase),
                    DryRunOutboundMessages: dryRunMessages,
                    FailureReason: ex.Message);
            }
            finally
            {
                this._executionTracker.UnregisterFlow(executionId);
            }
        }
        finally
        {
            if (ownedScope.HasValue)
            {
                await ownedScope.Value.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Logs an unexpected failure during Action Block flow execution.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Action block flow {ExecutionId} failed for task {TaskId}.")]
    private static partial void LogFlowFailedWarning(ILogger logger, Exception exception, Guid executionId, Guid taskId);

    /// <summary>
    /// Creates a synthetic chat message envelope for non-chat triggers (schedules, EventSub events, manual test runs).
    /// </summary>
    private static ChatMessage CreateSyntheticMessage(
        ActionExecutionRequest request,
        PlatformUserIdentity actor,
        DateTimeOffset timestampUtc) =>
        new(
            request.WorkspaceId,
            request.PlatformId,
            MessageId: Guid.NewGuid().ToString("N"),
            Sender: new ChatUser(actor.PlatformId, actor.PlatformUserId, actor.Username, actor.Username, actor.Designations),
            Channel: new ChatChannel(request.WorkspaceId, request.PlatformId, request.ChannelId, request.ChannelName),
            RawText: string.Empty,
            ReceivedAtUtc: timestampUtc);

    /// <summary>
    /// Shared mutable state across root and nested sub-task scopes within a single root flow execution.
    /// </summary>
    private sealed class FlowExecutionState(
        ActionFlowRunner owner,
        Guid executionId,
        Guid workspaceId,
        string platformId,
        string channelId,
        bool dryRun,
        PlatformUserIdentity actorIdentity,
        IMessageEnvelope messageEnvelope,
        IServiceProvider? serviceProvider,
        int maxBlocksPerFlow,
        List<ActionStepTraceEntry> traceSteps,
        List<OutboundChatMessage> dryRunMessages)
    {
        public ActionFlowRunner Owner { get; } = owner;
        public Guid ExecutionId { get; } = executionId;
        public Guid WorkspaceId { get; } = workspaceId;
        public string PlatformId { get; } = platformId;
        public string ChannelId { get; } = channelId;
        public bool DryRun { get; } = dryRun;
        public PlatformUserIdentity ActorIdentity { get; } = actorIdentity;
        public IMessageEnvelope MessageEnvelope { get; } = messageEnvelope;
        public IServiceProvider? ServiceProvider { get; } = serviceProvider;
        public int MaxBlocksPerFlow { get; } = maxBlocksPerFlow;
        public List<ActionStepTraceEntry> TraceSteps { get; } = traceSteps;
        public List<OutboundChatMessage> DryRunMessages { get; } = dryRunMessages;
        public int NextTraceIndex { get; set; }
        public int ExecutedStepsCount { get; set; }
    }

    /// <summary>
    /// Per-task-invocation execution context implementing <see cref="IActionExecutionContext"/>.
    /// </summary>
    private sealed class ActionExecutionContextImpl(
ActionFlowRunner.FlowExecutionState shared,
        Guid taskId,
        int callDepth,
        List<Guid> activeTaskStack,
        Dictionary<string, string> variables) : IActionExecutionContext
    {
        private readonly FlowExecutionState _shared = shared;
        private readonly Guid _taskId = taskId;
        private readonly Dictionary<string, string> _variables = variables;
        private readonly List<Guid> _activeTaskStack = activeTaskStack;

        /// <inheritdoc/>
        public Guid WorkspaceId => this._shared.WorkspaceId;

        /// <inheritdoc/>
        public Guid ExecutionId => this._shared.ExecutionId;

        /// <inheritdoc/>
        public int CallDepth { get; } = callDepth;

        /// <inheritdoc/>
        public IReadOnlyList<Guid> ActiveTaskStack => this._activeTaskStack;

        /// <inheritdoc/>
        public string PlatformId => this._shared.PlatformId;

        /// <inheritdoc/>
        public string ChannelId => this._shared.ChannelId;

        /// <inheritdoc/>
        public bool DryRun => this._shared.DryRun;

        /// <inheritdoc/>
        public PlatformUserIdentity ActorIdentity => this._shared.ActorIdentity;

        /// <inheritdoc/>
        public TimeProvider TimeProvider => this._shared.Owner._timeProvider;

        /// <inheritdoc/>
        public IServiceProvider? ServiceProvider => this._shared.ServiceProvider;

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> Variables => this._variables;

        /// <summary>
        /// Executes an ordered sequence of <see cref="ActionBlock"/> steps within the current task scope.
        /// </summary>
        public async ValueTask ExecuteBlockListAsync(IEnumerable<ActionBlock> blocks, CancellationToken cancellationToken)
        {
            foreach (ActionBlock block in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int stepIndex = this._shared.NextTraceIndex++;
                string blockName = block.GetType().Name;
                this._shared.Owner._executionTracker.UpdateFlowStep(this._shared.ExecutionId, stepIndex, blockName);

                DateTimeOffset stepStart = this.TimeProvider.GetUtcNow();
                if (!block.IsEnabled)
                {
                    this._shared.TraceSteps.Add(new ActionStepTraceEntry(
                        stepIndex,
                        block.Id,
                        blockName,
                        this._taskId,
                        this.CallDepth,
                        ActionStepExecutionStatus.SkippedDisabled,
                        TimeSpan.Zero,
                        new Dictionary<string, string?>()));
                    continue;
                }

                if (block.InlineCondition is not null)
                {
                    bool conditionPassed = await this.EvaluateConditionAsync(block.InlineCondition, cancellationToken).ConfigureAwait(false);
                    if (!conditionPassed)
                    {
                        this._shared.TraceSteps.Add(new ActionStepTraceEntry(
                            stepIndex,
                            block.Id,
                            blockName,
                            this._taskId,
                            this.CallDepth,
                            ActionStepExecutionStatus.SkippedCondition,
                            this.TimeProvider.GetUtcNow() - stepStart,
                            new Dictionary<string, string?>()));
                        continue;
                    }
                }

                if (this._shared.ExecutedStepsCount >= this._shared.MaxBlocksPerFlow)
                {
                    throw new ActionFlowQuotaExceededException(
                        $"Action block flow exceeded MaxActionBlocksPerFlow execution limit ({this._shared.MaxBlocksPerFlow}).");
                }

                this._shared.ExecutedStepsCount++;

                Dictionary<string, string> beforeSnapshot = new(this._variables, StringComparer.OrdinalIgnoreCase);

                try
                {
                    await block.ExecuteAsync(this, cancellationToken).ConfigureAwait(false);
                    Dictionary<string, string?> changes = ComputeVariableChanges(beforeSnapshot, this._variables);
                    this._shared.TraceSteps.Add(new ActionStepTraceEntry(
                        stepIndex,
                        block.Id,
                        blockName,
                        this._taskId,
                        this.CallDepth,
                        ActionStepExecutionStatus.Executed,
                        this.TimeProvider.GetUtcNow() - stepStart,
                        changes));
                }
                catch (Exception ex)
                {
                    Dictionary<string, string?> changes = ComputeVariableChanges(beforeSnapshot, this._variables);
                    this._shared.TraceSteps.Add(new ActionStepTraceEntry(
                        stepIndex,
                        block.Id,
                        blockName,
                        this._taskId,
                        this.CallDepth,
                        ActionStepExecutionStatus.Failed,
                        this.TimeProvider.GetUtcNow() - stepStart,
                        changes,
                        ex.Message));
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask<string> ResolveTemplateAsync(string? templateText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(templateText))
            {
                return string.Empty;
            }

            string trimmed = templateText.Trim();
            if (await this.TryResolveBuiltInStateKeyAsync(trimmed, cancellationToken).ConfigureAwait(false) is string stateValue)
            {
                return stateValue;
            }

            if (!templateText.Contains('{', StringComparison.Ordinal))
            {
                return templateText;
            }

            TagEvaluationDependencies dependencies = new()
            {
                Localizer = this._shared.ServiceProvider?.GetService<IStreamActionsLocalizer>(),
                StreamStatusProvider = this._shared.Owner._streamStatusProvider ?? this._shared.ServiceProvider?.GetService<IStreamStatusProvider>(),
                KeyValueStore = this._shared.ServiceProvider?.GetService<IWorkspaceKeyValueStore>(),
                SafeExternalHttpClient = this._shared.ServiceProvider?.GetService<ISafeExternalHttpClient>(),
                CounterStore = this._shared.ServiceProvider?.GetService<ICounterStore>(),
                CommandDispatcher = this._shared.ServiceProvider?.GetService<ICommandDispatcher>(),
                CommandRepository = this._shared.ServiceProvider?.GetService<ICommandRepository>(),
                ViewerLoyaltyStore = this._shared.ServiceProvider?.GetService<IViewerLoyaltyStore>(),
                TimeProvider = this.TimeProvider,
            };

            TagEvaluationContext tagContext = new(
                this._shared.MessageEnvelope,
                this._shared.Owner._tagRegistry,
                actorIdentity: this._shared.ActorIdentity,
                serviceProvider: this._shared.ServiceProvider,
                dependencies: dependencies,
                timeProvider: this.TimeProvider,
                variables: this._variables);

            TemplateParseResult parseResult = TemplateParser.Parse(templateText, this._shared.Owner._tagRegistry, this.PlatformId);
            TagResult result = await TemplateEvaluator
                .EvaluateAsync(parseResult.Root, tagContext, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (KeyValuePair<string, string> kvp in tagContext.Variables)
            {
                this._variables[kvp.Key] = kvp.Value;
            }

            return result.Value;
        }

        /// <inheritdoc/>
        public void SetVariable(string name, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            this._variables[name.Trim()] = value ?? string.Empty;
        }

        /// <inheritdoc/>
        public bool TryGetVariable(string name, out string? value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                value = null;
                return false;
            }

            return this._variables.TryGetValue(name.Trim(), out value);
        }

        /// <inheritdoc/>
        public ValueTask<bool> EvaluateConditionAsync(ActionBlockConditionGroup? condition, CancellationToken cancellationToken = default) =>
            ActionBlockConditionEvaluator.EvaluateAsync(condition, this.ResolveTemplateAsync, cancellationToken);

        /// <inheritdoc/>
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            if (duration <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(duration, this.TimeProvider, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask SendOutboundMessageAsync(
            string messageTemplate,
            string? platformId,
            string? channelId,
            OutboundTargetScope targetScope,
            CommandDeliveryMode deliveryMode,
            string? replyToMessageId,
            CancellationToken cancellationToken = default)
        {
            string resolvedText = await this.ResolveTemplateAsync(messageTemplate, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(resolvedText))
            {
                return;
            }

            string effectivePlatform = string.IsNullOrWhiteSpace(platformId) ? this.PlatformId : platformId.Trim();
            string effectiveChannel = string.IsNullOrWhiteSpace(channelId) ? this.ChannelId : channelId.Trim();

            OutboundChatMessage outbound = new(
                this.WorkspaceId,
                effectivePlatform,
                effectiveChannel,
                resolvedText,
                OutboundChatPriority.CommandReply,
                targetScope,
                DuplicateMessagePolicy.PlatformDefault,
                deliveryMode == CommandDeliveryMode.ThreadedReply ? replyToMessageId : null,
                IsWhisper: deliveryMode == CommandDeliveryMode.Whisper,
                TargetPlatformUserId: deliveryMode == CommandDeliveryMode.Whisper ? this.ActorIdentity.PlatformUserId : null);

            if (this._shared.DryRun || this._shared.Owner._outboundChatDispatcher is null)
            {
                this._shared.DryRunMessages.Add(outbound);
                return;
            }

            await this._shared.Owner._outboundChatDispatcher.EnqueueAsync(outbound, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public ValueTask ExecuteIfAsync(
            ActionBlockConditionGroup condition,
            IReadOnlyList<ActionBlock> thenBlocks,
            IReadOnlyList<ActionBlock>? elseBlocks,
            CancellationToken cancellationToken = default) =>
            this.ExecuteIfInternalAsync(condition, thenBlocks, elseBlocks, cancellationToken);

        /// <inheritdoc/>
        public async ValueTask PerformSubTaskAsync(
            Guid targetTaskId,
            IReadOnlyDictionary<string, string> arguments,
            string? resultVariablePrefix,
            CancellationToken cancellationToken = default)
        {
            int nextDepth = this.CallDepth + 1;
            if (nextDepth > IActionFlowRunner.MaxTaskCallDepth)
            {
                throw new ActionFlowRecursionLimitException(
                    $"PerformTask recursion depth limit ({IActionFlowRunner.MaxTaskCallDepth}) exceeded while invoking task '{targetTaskId}'.");
            }

            WorkspaceActionBlockTask? targetTask = this._shared.Owner._actionBlockStore is not null
                ? await this._shared.Owner._actionBlockStore
                    .GetTaskByIdAsync(this.WorkspaceId, targetTaskId, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            if (targetTask is null || !targetTask.IsEnabled)
            {
                return;
            }

            Dictionary<string, string> childVariables = new(StringComparer.OrdinalIgnoreCase);
            foreach (TaskParameterDefinition paramDef in targetTask.InputParameters)
            {
                if (!string.IsNullOrWhiteSpace(paramDef.Name) && paramDef.DefaultValue is not null)
                {
                    childVariables[paramDef.Name] = paramDef.DefaultValue;
                }
            }

            if (arguments is not null)
            {
                foreach (KeyValuePair<string, string> arg in arguments)
                {
                    if (!string.IsNullOrWhiteSpace(arg.Key))
                    {
                        string resolvedArg = await this.ResolveTemplateAsync(arg.Value, cancellationToken).ConfigureAwait(false);
                        childVariables[arg.Key.Trim()] = resolvedArg;
                    }
                }
            }

            List<Guid> nextStack = [.. this._activeTaskStack, targetTask.Id];
            ActionExecutionContextImpl childContext = new(
                this._shared,
                targetTask.Id,
                nextDepth,
                nextStack,
                childVariables);

            await childContext.ExecuteBlockListAsync(targetTask.Blocks, cancellationToken).ConfigureAwait(false);

            string prefix = string.IsNullOrWhiteSpace(resultVariablePrefix) ? string.Empty : resultVariablePrefix.Trim() + ".";
            foreach (KeyValuePair<string, string> kvp in childContext._variables)
            {
                this._variables[prefix + kvp.Key] = kvp.Value;
            }
        }

        /// <inheritdoc/>
        public async ValueTask SetProfileEnabledAsync(
            Guid targetProfileId,
            bool isEnabled,
            CancellationToken cancellationToken = default)
        {
            if (this._shared.Owner._actionBlockStore is not null)
            {
                _ = await this._shared.Owner._actionBlockStore
                    .SetProfileEnabledAsync(this.WorkspaceId, targetProfileId, isEnabled, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask InvokePlatformActionAsync(
            string actionId,
            IReadOnlyDictionary<string, string> parameters,
            string? outputVariableName,
            CancellationToken cancellationToken = default)
        {
            IPlatformActionHandler handler = this._shared.Owner._catalog.GetAction(actionId)
                ?? throw new InvalidOperationException($"Platform action handler '{actionId}' is not registered.");

            Dictionary<string, string> resolvedParameters = new(StringComparer.OrdinalIgnoreCase);
            foreach (ActionParameterSchema schema in handler.Parameters)
            {
                if (schema.DefaultValue is not null)
                {
                    resolvedParameters[schema.Key] = await this.ResolveTemplateAsync(schema.DefaultValue, cancellationToken).ConfigureAwait(false);
                }
            }

            if (parameters is not null)
            {
                foreach (KeyValuePair<string, string> kvp in parameters)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        resolvedParameters[kvp.Key.Trim()] = await this.ResolveTemplateAsync(kvp.Value, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            string? output = await handler.ExecuteAsync(this, resolvedParameters, cancellationToken).ConfigureAwait(false);
            string? targetVar = !string.IsNullOrWhiteSpace(outputVariableName)
                ? outputVariableName.Trim()
                : handler.DefaultOutputVariableName;

            if (output is not null && !string.IsNullOrWhiteSpace(targetVar))
            {
                this.SetVariable(targetVar, output);
            }
        }

        /// <inheritdoc/>
        public async ValueTask InvokeScriptAsync(
            Guid scriptId,
            string? entryFunction,
            IReadOnlyDictionary<string, string>? arguments,
            string? outputVariableName,
            CancellationToken cancellationToken = default)
        {
            if (this._shared.Owner._runJsExecutor is null)
            {
                throw new InvalidOperationException("ClearScript runtime executor is not registered.");
            }

            Dictionary<string, string> resolvedArgs = new(StringComparer.OrdinalIgnoreCase);
            if (arguments is not null)
            {
                foreach (KeyValuePair<string, string> kvp in arguments)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        resolvedArgs[kvp.Key.Trim()] = await this.ResolveTemplateAsync(kvp.Value, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            ScriptExecutionRequest scriptRequest = new()
            {
                WorkspaceId = this.WorkspaceId,
                ScriptId = scriptId,
                EntryFunction = entryFunction,
                Arguments = resolvedArgs,
                PlatformId = this.PlatformId,
                ChannelId = this.ChannelId,
                ActorIdentity = this.ActorIdentity,
                GetFlowVariable = name => this.TryGetVariable(name, out string? value) ? value : null,
                SetFlowVariable = this.SetVariable,
                SendChatAsync = (message, ct) => this.SendOutboundMessageAsync(
                    message,
                    platformId: null,
                    channelId: null,
                    OutboundTargetScope.OriginPlatform,
                    CommandDeliveryMode.Chat,
                    replyToMessageId: null,
                    ct),
            };

            ScriptExecutionResult result = await this._shared.Owner._runJsExecutor
                .ExecuteScriptAsync(scriptRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.ErrorMessage ?? "ClearScript execution failed.");
            }

            if (result.ReturnValue is not null && !string.IsNullOrWhiteSpace(outputVariableName))
            {
                this.SetVariable(outputVariableName.Trim(), result.ReturnValue);
            }
        }

        /// <summary>
        /// Evaluates a branch condition and executes the matching block list.
        /// </summary>
        private async ValueTask ExecuteIfInternalAsync(
            ActionBlockConditionGroup condition,
            IReadOnlyList<ActionBlock> thenBlocks,
            IReadOnlyList<ActionBlock>? elseBlocks,
            CancellationToken cancellationToken)
        {
            bool passed = await this.EvaluateConditionAsync(condition, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ActionBlock>? branch = passed ? thenBlocks : elseBlocks;
            if (branch is not null && branch.Count > 0)
            {
                await this.ExecuteBlockListAsync(branch, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resolves built-in Action Block state dot-paths such as <c>Stream.IsOnline</c>, <c>Stream.Game</c>, and <c>User.Designation</c>.
        /// </summary>
        private async ValueTask<string?> TryResolveBuiltInStateKeyAsync(string expression, CancellationToken cancellationToken)
        {
            if (string.Equals(expression, "Stream.IsOnline", StringComparison.OrdinalIgnoreCase))
            {
                IStreamStatusProvider? statusProvider = this._shared.Owner._streamStatusProvider
                    ?? this._shared.ServiceProvider?.GetService<IStreamStatusProvider>();
                bool isLive = statusProvider is not null
                    && await statusProvider.IsStreamLiveAsync(this.WorkspaceId, this.PlatformId, cancellationToken).ConfigureAwait(false);
                return isLive ? "true" : "false";
            }

            if (string.Equals(expression, "Stream.Game", StringComparison.OrdinalIgnoreCase))
            {
                IStreamStatusProvider? statusProvider = this._shared.Owner._streamStatusProvider
                    ?? this._shared.ServiceProvider?.GetService<IStreamStatusProvider>();
                string? game = statusProvider is not null
                    ? await statusProvider.GetCurrentGameCategoryAsync(this.WorkspaceId, this.PlatformId, cancellationToken).ConfigureAwait(false)
                    : null;
                return game ?? string.Empty;
            }

            return string.Equals(expression, "User.Designation", StringComparison.OrdinalIgnoreCase)
                ? this.ActorIdentity.Designations.ToString()
                : string.Equals(expression, "User.Username", StringComparison.OrdinalIgnoreCase)
                ? this.ActorIdentity.Username
                : this._variables.TryGetValue(expression, out string? variableValue) ? variableValue : null;
        }

        /// <summary>
        /// Computes added or changed flow variables between two snapshots.
        /// </summary>
        private static Dictionary<string, string?> ComputeVariableChanges(
            Dictionary<string, string> before,
            Dictionary<string, string> after)
        {
            Dictionary<string, string?> changes = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kvp in after)
            {
                if (!before.TryGetValue(kvp.Key, out string? oldValue)
                    || !string.Equals(oldValue, kvp.Value, StringComparison.Ordinal))
                {
                    changes[kvp.Key] = kvp.Value;
                }
            }

            return changes;
        }
    }
}
