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
using StreamActions.Core.Models;
using StreamActions.Core.Models.Messaging;

namespace StreamActions.Core.Actions;

/// <summary>
/// Exception thrown when nested <see cref="PerformTaskActionBlock"/> calls exceed the maximum recursion depth.
/// </summary>
public sealed class ActionFlowRecursionLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowRecursionLimitException"/> class.
    /// </summary>
    public ActionFlowRecursionLimitException()
        : base("Action block sub-task recursion depth limit exceeded.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowRecursionLimitException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ActionFlowRecursionLimitException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowRecursionLimitException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ActionFlowRecursionLimitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an Action Block flow or task definition exceeds its Tier 1 <c>MaxActionBlocksPerFlow</c> quota.
/// </summary>
public sealed class ActionFlowQuotaExceededException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowQuotaExceededException"/> class.
    /// </summary>
    public ActionFlowQuotaExceededException()
        : base("Action block flow quota exceeded.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowQuotaExceededException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ActionFlowQuotaExceededException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFlowQuotaExceededException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ActionFlowQuotaExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Trace entry describing the execution of a single <see cref="ActionBlock"/> step.
/// </summary>
/// <param name="StepIndex">Zero-based sequential step index observed during execution.</param>
/// <param name="BlockId">The <see cref="ActionBlock.Id"/>.</param>
/// <param name="BlockType">Human-readable block type name.</param>
/// <param name="TaskId">The owning <see cref="WorkspaceActionBlockTask.Id"/>.</param>
/// <param name="CallDepth">The sub-task nesting depth at which this step ran.</param>
/// <param name="Status">The step outcome.</param>
/// <param name="Duration">Wall-clock/TimeProvider duration of this step.</param>
/// <param name="VariableChanges">Snapshot of variable mutations performed by this step.</param>
/// <param name="Detail">Optional diagnostic or failure detail.</param>
public sealed record ActionStepTraceEntry(
    int StepIndex,
    Guid BlockId,
    string BlockType,
    Guid TaskId,
    int CallDepth,
    ActionStepExecutionStatus Status,
    TimeSpan Duration,
    IReadOnlyDictionary<string, string?> VariableChanges,
    string? Detail = null);

/// <summary>
/// Complete execution result and diagnostic trace returned by <see cref="IActionFlowRunner.ExecuteAsync"/>.
/// </summary>
/// <param name="ExecutionId">The unique execution identifier tracked by <see cref="IRuntimeExecutionTracker"/>.</param>
/// <param name="Succeeded">Whether the flow completed without an unhandled error, timeout, or quota failure.</param>
/// <param name="TotalExecutedSteps">Total number of executed blocks across the root task and sub-tasks.</param>
/// <param name="TotalDuration">Total elapsed execution duration.</param>
/// <param name="Steps">Ordered trace entries for every visited block.</param>
/// <param name="FinalVariables">Final flow variable dictionary.</param>
/// <param name="DryRunOutboundMessages">Outbound chat messages captured without dispatching when <c>DryRun</c> is <see langword="true"/>.</param>
/// <param name="FailureReason">Optional error message when <paramref name="Succeeded"/> is <see langword="false"/>.</param>
public sealed record ActionExecutionTrace(
    Guid ExecutionId,
    bool Succeeded,
    int TotalExecutedSteps,
    TimeSpan TotalDuration,
    IReadOnlyList<ActionStepTraceEntry> Steps,
    IReadOnlyDictionary<string, string> FinalVariables,
    IReadOnlyList<OutboundChatMessage> DryRunOutboundMessages,
    string? FailureReason = null);

/// <summary>
/// Input request for executing a <see cref="WorkspaceActionBlockTask"/> or unsaved editor task draft.
/// </summary>
public sealed class ActionExecutionRequest
{
    /// <summary>
    /// Gets or sets the owning workspace identifier.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the stored task identifier to load when <see cref="TaskOverride"/> is <see langword="null"/>.
    /// </summary>
    public Guid? TaskId { get; set; }

    /// <summary>
    /// Gets or sets an optional in-memory task instance (for example, an unsaved draft in the editor dry-run runner).
    /// </summary>
    public WorkspaceActionBlockTask? TaskOverride { get; set; }

    /// <summary>
    /// Gets or sets the optional triggering profile identifier.
    /// </summary>
    public Guid? ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the optional triggering profile display name.
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// Gets or sets the optional trigger key that initiated the flow.
    /// </summary>
    public string? TriggerId { get; set; }

    /// <summary>
    /// Gets or sets the originating platform identifier.
    /// </summary>
    public string PlatformId { get; set; } = "twitch";

    /// <summary>
    /// Gets or sets the originating channel identifier.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the originating channel name.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional actor identity associated with the trigger.
    /// </summary>
    public PlatformUserIdentity? ActorIdentity { get; set; }

    /// <summary>
    /// Gets or sets the optional inbound message or command invocation.
    /// </summary>
    public IMessageEnvelope? Message { get; set; }

    /// <summary>
    /// Gets or sets initial flow variables populated from trigger outputs or manual test inputs.
    /// </summary>
    public IReadOnlyDictionary<string, string>? InitialVariables { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether outbound chat messages should be captured instead of dispatched.
    /// </summary>
    public bool DryRun { get; set; }
}

/// <summary>
/// Runtime execution context passed to <see cref="ActionBlock.ExecuteAsync"/>.
/// </summary>
public interface IActionExecutionContext
{
    /// <summary>
    /// Gets the workspace identifier for this execution.
    /// </summary>
    Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the unique execution identifier.
    /// </summary>
    Guid ExecutionId { get; }

    /// <summary>
    /// Gets the current sub-task nesting depth (<c>0</c> for root tasks).
    /// </summary>
    int CallDepth { get; }

    /// <summary>
    /// Gets the active task call stack for cycle/depth inspection.
    /// </summary>
    IReadOnlyList<Guid> ActiveTaskStack { get; }

    /// <summary>
    /// Gets the active platform identifier.
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the active channel identifier.
    /// </summary>
    string ChannelId { get; }

    /// <summary>
    /// Gets a value indicating whether this execution is running in dry-run mode.
    /// </summary>
    bool DryRun { get; }

    /// <summary>
    /// Gets the actor identity associated with the triggering event.
    /// </summary>
    PlatformUserIdentity ActorIdentity { get; }

    /// <summary>
    /// Gets the <see cref="System.TimeProvider"/> used for deterministic delays and timestamps.
    /// </summary>
    TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the scoped service provider for this execution.
    /// </summary>
    IServiceProvider? ServiceProvider { get; }

    /// <summary>
    /// Gets a snapshot of current flow variables.
    /// </summary>
    IReadOnlyDictionary<string, string> Variables { get; }

    /// <summary>
    /// Evaluates a template or state expression in the current flow context.
    /// </summary>
    /// <param name="templateText">The template expression.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved string value.</returns>
    ValueTask<string> ResolveTemplateAsync(string? templateText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or updates a flow variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    void SetVariable(string name, string value);

    /// <summary>
    /// Attempts to read a flow variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">Outputs the variable value when found.</param>
    /// <returns><see langword="true"/> if present; otherwise, <see langword="false"/>.</returns>
    bool TryGetVariable(string name, out string? value);

    /// <summary>
    /// Evaluates an <see cref="ActionBlockConditionGroup"/> in the current flow context.
    /// </summary>
    /// <param name="condition">The condition group.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the condition holds; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> EvaluateConditionAsync(ActionBlockConditionGroup? condition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delays execution using <see cref="TimeProvider"/> while observing the flow runtime cancellation token.
    /// </summary>
    /// <param name="duration">The requested delay duration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the delay.</returns>
    ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends or dry-run captures an outbound chat or whisper message.
    /// </summary>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="platformId">Optional platform override.</param>
    /// <param name="channelId">Optional channel override.</param>
    /// <param name="targetScope">Outbound routing scope.</param>
    /// <param name="deliveryMode">Delivery mode.</param>
    /// <param name="replyToMessageId">Optional reply message identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the send operation.</returns>
    ValueTask SendOutboundMessageAsync(
        string messageTemplate,
        string? platformId,
        string? channelId,
        OutboundTargetScope targetScope,
        CommandDeliveryMode deliveryMode,
        string? replyToMessageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an <see cref="IfActionBlock"/> branch against this context.
    /// </summary>
    /// <param name="condition">The condition group.</param>
    /// <param name="thenBlocks">Blocks to execute when true.</param>
    /// <param name="elseBlocks">Optional blocks to execute when false.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing branch execution.</returns>
    ValueTask ExecuteIfAsync(
        ActionBlockConditionGroup condition,
        IReadOnlyList<ActionBlock> thenBlocks,
        IReadOnlyList<ActionBlock>? elseBlocks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes another <see cref="WorkspaceActionBlockTask"/> as a sub-task within the current flow budget and call-depth limit.
    /// </summary>
    /// <param name="targetTaskId">The target task identifier.</param>
    /// <param name="arguments">Argument templates mapped to target input parameter names.</param>
    /// <param name="resultVariablePrefix">Optional variable prefix for copying sub-task variables back to the caller.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing sub-task execution.</returns>
    ValueTask PerformSubTaskAsync(
        Guid targetTaskId,
        IReadOnlyDictionary<string, string> arguments,
        string? resultVariablePrefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a <see cref="WorkspaceActionBlockProfile"/> by its immutable identifier.
    /// </summary>
    /// <param name="targetProfileId">The profile identifier.</param>
    /// <param name="isEnabled">Desired enabled state.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the state update.</returns>
    ValueTask SetProfileEnabledAsync(
        Guid targetProfileId,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a registered <see cref="IPlatformActionHandler"/> and stores any output value into the target variable.
    /// </summary>
    /// <param name="actionId">The registered action handler identifier.</param>
    /// <param name="parameters">Parameter templates.</param>
    /// <param name="outputVariableName">Optional variable name override.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing action invocation.</returns>
    ValueTask InvokePlatformActionAsync(
        string actionId,
        IReadOnlyDictionary<string, string> parameters,
        string? outputVariableName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a workspace VFS ClearScript V8 script via <c>IRunJsActionExecutor</c>.
    /// </summary>
    /// <param name="scriptId">The VFS script identifier.</param>
    /// <param name="entryFunction">Optional exported entry function name.</param>
    /// <param name="arguments">Optional parameter templates.</param>
    /// <param name="outputVariableName">Optional output variable name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing script execution.</returns>
    ValueTask InvokeScriptAsync(
        Guid scriptId,
        string? entryFunction,
        IReadOnlyDictionary<string, string>? arguments,
        string? outputVariableName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes Action Block tasks with Tier 1 resource quotas, recursion depth protection, dry-run tracing, and Task Manager cancellation.
/// </summary>
public interface IActionFlowRunner
{
    /// <summary>
    /// Maximum allowed nested <see cref="PerformTaskActionBlock"/> call depth.
    /// </summary>
    const int MaxTaskCallDepth = 5;

    /// <summary>
    /// Executes an Action Block flow request and returns the detailed execution trace.
    /// </summary>
    /// <param name="request">The execution request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="ActionExecutionTrace"/>.</returns>
    ValueTask<ActionExecutionTrace> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken = default);
}
