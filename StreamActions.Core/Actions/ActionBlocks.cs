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

using StreamActions.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Core.Actions;

/// <summary>
/// Shared JSON serialization options and structural helpers for polymorphic <see cref="ActionBlock"/> trees.
/// </summary>
public static class ActionBlockJson
{
    /// <summary>
    /// Gets the cached <see cref="JsonSerializerOptions"/> instance used for polymorphic <see cref="ActionBlock"/> persistence.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>
    /// Counts the total number of <see cref="ActionBlock"/> steps in a task, including all nested <see cref="IfActionBlock"/> branches.
    /// </summary>
    /// <param name="blocks">The root action blocks.</param>
    /// <returns>The total number of action block steps.</returns>
    public static int CountTotalBlocks(IEnumerable<ActionBlock>? blocks)
    {
        if (blocks is null)
        {
            return 0;
        }

        int count = 0;
        foreach (ActionBlock block in blocks)
        {
            if (block is null)
            {
                continue;
            }

            count++;
            if (block is IfActionBlock ifBlock)
            {
                count += CountTotalBlocks(ifBlock.ThenBlocks);
                count += CountTotalBlocks(ifBlock.ElseBlocks);
            }
        }

        return count;
    }
}

/// <summary>
/// Polymorphic base record for all Action Block steps serialized into <see cref="WorkspaceActionBlockTask.Blocks"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SendMessageActionBlock), "chat.send")]
[JsonDerivedType(typeof(DelayActionBlock), "flow.delay")]
[JsonDerivedType(typeof(SetVariableActionBlock), "flow.set_var")]
[JsonDerivedType(typeof(IfActionBlock), "flow.if")]
[JsonDerivedType(typeof(PerformTaskActionBlock), "flow.perform_task")]
[JsonDerivedType(typeof(EnableProfileActionBlock), "flow.enable_profile")]
[JsonDerivedType(typeof(DisableProfileActionBlock), "flow.disable_profile")]
[JsonDerivedType(typeof(PlatformActionBlock), "platform.invoke")]
[JsonDerivedType(typeof(RunJsActionBlock), "script.clearscript")]
public abstract record ActionBlock
{
    /// <summary>
    /// Gets the unique step identifier within the task.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets a value indicating whether this step is active; disabled steps are skipped without interrupting the flow.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets an optional inline conditional evaluated before this single step executes.
    /// </summary>
    public ActionBlockConditionGroup? InlineCondition { get; init; }

    /// <summary>
    /// Executes this action block against the active <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The active flow execution context.</param>
    /// <param name="cancellationToken">A token linked to the flow's runtime budget and termination controls.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous step execution.</returns>
    public abstract ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Sends a chat or whisper message template through the workspace outbound dispatcher (or captures it during dry runs).
/// </summary>
/// <param name="Message">The message or <c>{tag}</c> template to evaluate and send.</param>
/// <param name="PlatformId">Optional target platform override.</param>
/// <param name="ChannelId">Optional target channel identifier override.</param>
/// <param name="TargetScope">Outbound routing scope across workspace platforms.</param>
/// <param name="DeliveryMode">Delivery mode (chat, threaded reply, or whisper).</param>
/// <param name="ReplyToMessageId">Optional reply-to message identifier.</param>
public sealed record SendMessageActionBlock(
    string Message,
    string? PlatformId = null,
    string? ChannelId = null,
    OutboundTargetScope TargetScope = OutboundTargetScope.OriginPlatform,
    CommandDeliveryMode DeliveryMode = CommandDeliveryMode.Chat,
    string? ReplyToMessageId = null) : ActionBlock
{
    /// <inheritdoc/>
    public override ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.SendOutboundMessageAsync(
            this.Message,
            this.PlatformId,
            this.ChannelId,
            this.TargetScope,
            this.DeliveryMode,
            this.ReplyToMessageId,
            cancellationToken);
    }
}

/// <summary>
/// Pauses flow execution for a bounded duration using the execution context's <see cref="TimeProvider"/>.
/// </summary>
/// <param name="DurationMs">Delay duration in milliseconds when <paramref name="DurationTemplate"/> is not specified.</param>
/// <param name="DurationTemplate">Optional template string resolving to milliseconds.</param>
public sealed record DelayActionBlock(
    int DurationMs,
    string? DurationTemplate = null) : ActionBlock
{
    /// <inheritdoc/>
    public override async ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        int delayMs = this.DurationMs;
        if (!string.IsNullOrWhiteSpace(this.DurationTemplate))
        {
            string resolved = await context.ResolveTemplateAsync(this.DurationTemplate, cancellationToken).ConfigureAwait(false);
            if (int.TryParse(resolved.Trim(), out int parsed))
            {
                delayMs = parsed;
            }
        }

        if (delayMs > 0)
        {
            await context.DelayAsync(TimeSpan.FromMilliseconds(delayMs), cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Evaluates a template expression and assigns the result to a flow-scoped variable accessible via <c>{var Name}</c>.
/// </summary>
/// <param name="VariableName">The target variable name.</param>
/// <param name="ValueTemplate">The template expression whose evaluated value is assigned.</param>
public sealed record SetVariableActionBlock(
    string VariableName,
    string ValueTemplate) : ActionBlock
{
    /// <inheritdoc/>
    public override async ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string resolved = await context.ResolveTemplateAsync(this.ValueTemplate, cancellationToken).ConfigureAwait(false);
        context.SetVariable(this.VariableName, resolved);
    }
}

/// <summary>
/// Evaluates an <see cref="ActionBlockConditionGroup"/> and executes either <see cref="ThenBlocks"/> or <see cref="ElseBlocks"/>.
/// </summary>
/// <param name="Condition">The unified conditional group to evaluate.</param>
/// <param name="ThenBlocks">Blocks executed when <paramref name="Condition"/> evaluates to <see langword="true"/>.</param>
/// <param name="ElseBlocks">Optional blocks executed when <paramref name="Condition"/> evaluates to <see langword="false"/>.</param>
public sealed record IfActionBlock(
    ActionBlockConditionGroup Condition,
    IReadOnlyList<ActionBlock> ThenBlocks,
    IReadOnlyList<ActionBlock>? ElseBlocks = null) : ActionBlock
{
    /// <inheritdoc/>
    public override ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ExecuteIfAsync(this.Condition, this.ThenBlocks, this.ElseBlocks, cancellationToken);
    }
}

/// <summary>
/// Directly invokes another <see cref="WorkspaceActionBlockTask"/> with resolved argument values up to the recursion depth limit.
/// </summary>
/// <param name="TargetTaskId">The immutable <see cref="Guid"/> of the target task.</param>
/// <param name="Arguments">Map of target task parameter names to template expressions.</param>
/// <param name="ResultVariablePrefix">Optional variable prefix under which sub-task output variables are copied back.</param>
public sealed record PerformTaskActionBlock(
    Guid TargetTaskId,
    IReadOnlyDictionary<string, string> Arguments,
    string? ResultVariablePrefix = null) : ActionBlock
{
    /// <inheritdoc/>
    public override ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.PerformSubTaskAsync(this.TargetTaskId, this.Arguments, this.ResultVariablePrefix, cancellationToken);
    }
}

/// <summary>
/// Enables a <see cref="WorkspaceActionBlockProfile"/> referenced by its immutable <see cref="Guid"/>.
/// </summary>
/// <param name="TargetProfileId">The immutable <see cref="Guid"/> of the profile to enable.</param>
public sealed record EnableProfileActionBlock(Guid TargetProfileId) : ActionBlock
{
    /// <inheritdoc/>
    public override ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.SetProfileEnabledAsync(this.TargetProfileId, isEnabled: true, cancellationToken);
    }
}

/// <summary>
/// Disables a <see cref="WorkspaceActionBlockProfile"/> referenced by its immutable <see cref="Guid"/>.
/// </summary>
/// <param name="TargetProfileId">The immutable <see cref="Guid"/> of the profile to disable.</param>
public sealed record DisableProfileActionBlock(Guid TargetProfileId) : ActionBlock
{
    /// <inheritdoc/>
    public override ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.SetProfileEnabledAsync(this.TargetProfileId, isEnabled: false, cancellationToken);
    }
}

/// <summary>
/// Invokes a registered <see cref="IPlatformActionHandler"/> by <paramref name="ActionId"/> with template-resolved parameters.
/// </summary>
/// <param name="ActionId">The unique <see cref="IPlatformActionHandler.ActionId"/>.</param>
/// <param name="Parameters">Parameter map of handler parameter keys to template expressions.</param>
/// <param name="OutputVariableName">Optional variable name overriding the handler's default output variable.</param>
public sealed record PlatformActionBlock(
    string ActionId,
    IReadOnlyDictionary<string, string> Parameters,
    string? OutputVariableName = null) : ActionBlock
{
    /// <inheritdoc/>
    public override ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.InvokePlatformActionAsync(this.ActionId, this.Parameters, this.OutputVariableName, cancellationToken);
    }
}

/// <summary>
/// Loads and executes a ClearScript V8 script stored in the workspace Virtual File System (<c>WorkspaceScripts</c>).
/// </summary>
/// <param name="ScriptId">The immutable <see cref="Guid"/> of the VFS script.</param>
/// <param name="EntryFunction">Optional exported function name on <c>$</c> or module scope to invoke.</param>
/// <param name="Arguments">Optional parameter templates passed to the script execution context.</param>
/// <param name="OutputVariableName">Optional flow variable name receiving the script return value.</param>
public sealed record RunJsActionBlock(
    Guid ScriptId,
    string? EntryFunction = null,
    IReadOnlyDictionary<string, string>? Arguments = null,
    string? OutputVariableName = null) : ActionBlock
{
    /// <inheritdoc/>
    public override ValueTask ExecuteAsync(
        IActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.InvokeScriptAsync(this.ScriptId, this.EntryFunction, this.Arguments, this.OutputVariableName, cancellationToken);
    }
}

