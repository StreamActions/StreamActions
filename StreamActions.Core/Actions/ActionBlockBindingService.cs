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
using StreamActions.Core.Commands;
using StreamActions.Core.Models.Entities;
using System.Text.Json;

namespace StreamActions.Core.Actions;

/// <summary>
/// Result of bidirectional Profile &lt;-&gt; Task creation or Tier-1 Custom Command upgrade.
/// </summary>
/// <param name="Profile">The created or updated Action Block profile.</param>
/// <param name="Task">The created or updated Action Block task with auto-populated input parameters.</param>
public sealed record ActionBlockBindingResult(
    WorkspaceActionBlockProfile Profile,
    WorkspaceActionBlockTask Task);

/// <summary>
/// Manages bidirectional Action Block Profile &lt;-&gt; Task creation, automatic <see cref="WorkspaceActionBlockTask.InputParameters"/> population from <see cref="ITriggerDescriptor.OutputParameters"/>, and 1-click Tier-1 Custom Command upgrades.
/// </summary>
public sealed class ActionBlockBindingService
{
    /// <summary>
    /// Action Block persistence store.
    /// </summary>
    private readonly IActionBlockStore _actionBlockStore;

    /// <summary>
    /// Action Block trigger and action catalog.
    /// </summary>
    private readonly IActionBlockCatalog _catalog;

    /// <summary>
    /// Custom command repository for Tier-1 -&gt; Tier-2 upgrades.
    /// </summary>
    private readonly ICommandRepository? _commandRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBlockBindingService"/> class.
    /// </summary>
    /// <param name="actionBlockStore">Action Block persistence store.</param>
    /// <param name="catalog">Action Block action and trigger catalog.</param>
    /// <param name="commandRepository">Optional custom command repository.</param>
    public ActionBlockBindingService(
        IActionBlockStore actionBlockStore,
        IActionBlockCatalog catalog,
        ICommandRepository? commandRepository = null)
    {
        ArgumentNullException.ThrowIfNull(actionBlockStore);
        ArgumentNullException.ThrowIfNull(catalog);

        this._actionBlockStore = actionBlockStore;
        this._catalog = catalog;
        this._commandRepository = commandRepository;
    }

    /// <summary>
    /// Auto-populates <see cref="WorkspaceActionBlockTask.InputParameters"/> and <see cref="WorkspaceActionBlockProfile.ParameterMappings"/>
    /// from the <see cref="ITriggerDescriptor.OutputParameters"/> of <paramref name="triggerId"/> without overwriting existing custom parameters.
    /// </summary>
    /// <param name="task">The target task to update in memory.</param>
    /// <param name="profile">Optional profile receiving default parameter mappings.</param>
    /// <param name="triggerId">The trigger descriptor identifier.</param>
    public void PopulateInputParametersFromTrigger(
        WorkspaceActionBlockTask task,
        WorkspaceActionBlockProfile? profile,
        string triggerId)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerId);

        ITriggerDescriptor? descriptor = this._catalog.GetTrigger(triggerId);
        if (descriptor is null)
        {
            return;
        }

        HashSet<string> existingNames = new(
            task.InputParameters.Select(static p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (TaskParameterDefinition outputParam in descriptor.OutputParameters)
        {
            if (existingNames.Add(outputParam.Name))
            {
                task.InputParameters.Add(outputParam with
                {
                    PopulatedFromTriggerKey = outputParam.PopulatedFromTriggerKey ?? outputParam.Name,
                });
            }

            if (profile is not null && !profile.ParameterMappings.ContainsKey(outputParam.Name))
            {
                string sourceKey = outputParam.PopulatedFromTriggerKey ?? outputParam.Name;
                profile.ParameterMappings[outputParam.Name] = $"{{var {sourceKey}}}";
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="WorkspaceActionBlockTask"/> attached to a new or existing <see cref="WorkspaceActionBlockProfile"/> (Profile -&gt; Task workflow),
    /// auto-populating input parameters from the profile's trigger.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="profileName">The operator-chosen profile name.</param>
    /// <param name="trigger">The trigger definition.</param>
    /// <param name="taskName">The operator-chosen task name.</param>
    /// <param name="predicates">Optional profile predicates.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="ActionBlockBindingResult"/>.</returns>
    public async ValueTask<ActionBlockBindingResult> CreateProfileWithNewTaskAsync(
        Guid workspaceId,
        string profileName,
        ProfileTriggerDefinition trigger,
        string taskName,
        ActionBlockConditionGroup? predicates = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);

        WorkspaceActionBlockTask task = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = taskName.Trim(),
            IsEnabled = true,
        };

        WorkspaceActionBlockProfile profile = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = profileName.Trim(),
            IsEnabled = true,
            Trigger = trigger,
            Predicates = predicates,
            AttachedTaskId = task.Id,
        };

        this.PopulateInputParametersFromTrigger(task, profile, trigger.TriggerId);

        WorkspaceActionBlockTask savedTask = await this._actionBlockStore.UpsertTaskAsync(task, cancellationToken).ConfigureAwait(false);
        profile.AttachedTaskId = savedTask.Id;
        WorkspaceActionBlockProfile savedProfile = await this._actionBlockStore.UpsertProfileAsync(profile, cancellationToken).ConfigureAwait(false);

        return new ActionBlockBindingResult(savedProfile, savedTask);
    }

    /// <summary>
    /// Creates and attaches a new <see cref="WorkspaceActionBlockProfile"/> to an existing <see cref="WorkspaceActionBlockTask"/> (Task -&gt; Profile workflow),
    /// auto-populating missing task input parameters from the new profile's trigger.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="taskId">The existing task identifier.</param>
    /// <param name="profileName">The operator-chosen profile name.</param>
    /// <param name="trigger">The trigger definition.</param>
    /// <param name="predicates">Optional profile predicates.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted <see cref="ActionBlockBindingResult"/>.</returns>
    public async ValueTask<ActionBlockBindingResult> CreateProfileForExistingTaskAsync(
        Guid workspaceId,
        Guid taskId,
        string profileName,
        ProfileTriggerDefinition trigger,
        ActionBlockConditionGroup? predicates = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentNullException.ThrowIfNull(trigger);

        WorkspaceActionBlockTask existingTask = await this._actionBlockStore
            .GetTaskByIdAsync(workspaceId, taskId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Task '{taskId}' was not found in workspace '{workspaceId}'.");

        WorkspaceActionBlockProfile profile = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = profileName.Trim(),
            IsEnabled = true,
            Trigger = trigger,
            Predicates = predicates,
            AttachedTaskId = existingTask.Id,
        };

        this.PopulateInputParametersFromTrigger(existingTask, profile, trigger.TriggerId);

        WorkspaceActionBlockTask savedTask = await this._actionBlockStore.UpsertTaskAsync(existingTask, cancellationToken).ConfigureAwait(false);
        profile.AttachedTaskId = savedTask.Id;
        WorkspaceActionBlockProfile savedProfile = await this._actionBlockStore.UpsertProfileAsync(profile, cancellationToken).ConfigureAwait(false);

        return new ActionBlockBindingResult(savedProfile, savedTask);
    }

    /// <summary>
    /// Performs a 1-click Tier-1 Custom Command to Tier-2 Action Blocks upgrade by converting the command's <see cref="CommandRoutine"/>
    /// into polymorphic <see cref="ActionBlock"/> steps and linking a <see cref="BuiltInTriggerIds.ChatCommand"/> profile.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="command">The custom command to upgrade.</param>
    /// <param name="disableOriginalCommand">Whether to disable the original Tier-1 <see cref="CustomCommand"/> after creating the Action Block profile.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created <see cref="ActionBlockBindingResult"/>.</returns>
    public async ValueTask<ActionBlockBindingResult> UpgradeCustomCommandToActionBlocksAsync(
        Guid workspaceId,
        CustomCommand command,
        bool disableOriginalCommand = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        string normalizedTrigger = BuiltInCommandRegistry.NormalizeTrigger(command.Trigger);
        CommandRoutine routine = CommandRoutine.Parse(command.RoutineJson);

        List<ActionBlock> blocks = new(routine.Steps.Count);
        foreach (CommandRoutineStep step in routine.Steps)
        {
            if (step is ChatSendActionStep chatSend)
            {
                blocks.Add(new SendMessageActionBlock(
                    chatSend.Message,
                    TargetScope: command.Metadata.TargetScope,
                    DeliveryMode: command.DeliveryMode));
            }
            else if (step is UnknownCommandRoutineStep unknown)
            {
                ActionBlock? deserialized = JsonSerializer.Deserialize<ActionBlock>(unknown.RawJson, ActionBlockJson.Default);
                if (deserialized is not null)
                {
                    blocks.Add(deserialized);
                }
            }
        }

        WorkspaceActionBlockTask task = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = $"Command: !{normalizedTrigger}",
            IsEnabled = command.IsEnabled,
            Blocks = blocks,
        };

        ProfileTriggerDefinition trigger = new()
        {
            TriggerId = BuiltInTriggerIds.ChatCommand,
            CommandTrigger = normalizedTrigger,
        };

        WorkspaceActionBlockProfile profile = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            Name = $"On !{normalizedTrigger}",
            IsEnabled = command.IsEnabled,
            Trigger = trigger,
            AttachedTaskId = task.Id,
        };

        this.PopulateInputParametersFromTrigger(task, profile, BuiltInTriggerIds.ChatCommand);

        WorkspaceActionBlockTask savedTask = await this._actionBlockStore.UpsertTaskAsync(task, cancellationToken).ConfigureAwait(false);
        profile.AttachedTaskId = savedTask.Id;
        WorkspaceActionBlockProfile savedProfile = await this._actionBlockStore.UpsertProfileAsync(profile, cancellationToken).ConfigureAwait(false);

        if (disableOriginalCommand && command.IsEnabled)
        {
            command.IsEnabled = false;
            if (this._commandRepository is not null)
            {
                await this._commandRepository.UpsertAsync(command, cancellationToken).ConfigureAwait(false);
            }
        }

        return new ActionBlockBindingResult(savedProfile, savedTask);
    }
}

