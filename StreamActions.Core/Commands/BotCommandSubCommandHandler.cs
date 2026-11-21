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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Templates;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StreamActions.Core.Commands;

/// <summary>
/// Implements the built-in <c>!bot command &lt;subcommand&gt;</c> management handler (<c>bot.command</c>).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BotCommandSubCommandHandler"/> class for dependency injection.
/// </remarks>
/// <param name="commandRepository">Optional custom command repository.</param>
/// <param name="permissionStore">Optional permission store.</param>
/// <param name="scopeFactory">Optional DI scope factory for resolving <see cref="IPermissionService"/>.</param>
/// <param name="tagRegistry">Optional tag registry for template validation.</param>
/// <param name="localizer">Optional localizer.</param>
[Command(
    "command",
    Aliases = ["cmd"],
    DefaultPermissionKey = BuiltInPermissionKeys.UiCommandsEdit,
    Description = "Manages custom chat commands for the workspace.")]
[method: ActivatorUtilitiesConstructor]
public sealed class BotCommandSubCommandHandler(
    ICommandRepository? commandRepository = null,
    IPermissionStore? permissionStore = null,
    IServiceScopeFactory? scopeFactory = null,
    ITagRegistry? tagRegistry = null,
    IStreamActionsLocalizer? localizer = null) : IBuiltInCommandHandler
{
    /// <summary>
    /// Canonical usage string displayed when sub-command arguments are missing or invalid.
    /// </summary>
    private const string _usageSummary =
        "!bot command <add|edit|delete|permission|cooldown|cost|reward|count|toggle> ...";

    /// <summary>
    /// Fallback English response when <c>responses.invalidArguments</c> is unavailable.
    /// </summary>
    private const string _fallbackInvalidArgs = "{sender}, invalid arguments. Usage: {1}";

    /// <summary>
    /// Fallback English response when <c>responses.commandAdded</c> is unavailable.
    /// </summary>
    private const string _fallbackCommandAdded = "{sender}, command !{1} has been added.";

    /// <summary>
    /// Fallback English response when <c>responses.commandEdited</c> is unavailable.
    /// </summary>
    private const string _fallbackCommandEdited = "{sender}, command !{1} has been updated.";

    /// <summary>
    /// Fallback English response when <c>responses.commandDeleted</c> is unavailable.
    /// </summary>
    private const string _fallbackCommandDeleted = "{sender}, command !{1} has been deleted.";

    /// <summary>
    /// Fallback English response when <c>responses.commandNotFound</c> is unavailable.
    /// </summary>
    private const string _fallbackCommandNotFound = "{sender}, command !{1} was not found.";

    /// <summary>
    /// Fallback English response when <c>responses.commandAlreadyExists</c> is unavailable.
    /// </summary>
    private const string _fallbackCommandAlreadyExists = "{sender}, command !{1} already exists.";

    /// <summary>
    /// Fallback English response when <c>responses.commandInvalidSyntax</c> is unavailable.
    /// </summary>
    private const string _fallbackInvalidSyntax = "{sender}, invalid command template syntax: {1}";

    /// <summary>
    /// Fallback English response when <c>responses.commandPermissionUpdated</c> is unavailable.
    /// </summary>
    private const string _fallbackPermissionUpdated = "{sender}, permission for !{1} on group {2} has been set to {3}.";

    /// <summary>
    /// Fallback English response when <c>responses.commandCooldownUpdated</c> is unavailable.
    /// </summary>
    private const string _fallbackCooldownUpdated = "{sender}, cooldown for !{1} ({2}) has been set to {3}s.";

    /// <summary>
    /// Fallback English response when <c>responses.commandCostUpdated</c> is unavailable.
    /// </summary>
    private const string _fallbackCostUpdated = "{sender}, point cost for !{1} has been set to {2}.";

    /// <summary>
    /// Fallback English response when <c>responses.commandRewardUpdated</c> is unavailable.
    /// </summary>
    private const string _fallbackRewardUpdated = "{sender}, point reward for !{1} has been set to {2}.";

    /// <summary>
    /// Fallback English response when <c>responses.commandCountUpdated</c> is unavailable.
    /// </summary>
    private const string _fallbackCountUpdated = "{sender}, counter settings for !{1} have been updated.";

    /// <summary>
    /// Fallback English response when <c>responses.commandToggled</c> is unavailable.
    /// </summary>
    private const string _fallbackCommandToggled = "{sender}, command !{1} is now {2}.";

    /// <summary>
    /// Fallback English response when <c>responses.permissionGroupNotFound</c> is unavailable.
    /// </summary>
    private const string _fallbackGroupNotFound = "{sender}, permission group '{1}' was not found.";

    /// <summary>
    /// Optional custom command repository.
    /// </summary>
    private readonly ICommandRepository? _commandRepository = commandRepository;

    /// <summary>
    /// Optional permission persistence store.
    /// </summary>
    private readonly IPermissionStore? _permissionStore = permissionStore;

    /// <summary>
    /// Optional DI scope factory used to resolve scoped <see cref="IPermissionService"/> for cache invalidation.
    /// </summary>
    private readonly IServiceScopeFactory? _scopeFactory = scopeFactory;

    /// <summary>
    /// Optional direct <see cref="IPermissionService"/> override (typically supplied in unit tests).
    /// </summary>
    private readonly IPermissionService? _permissionService;

    /// <summary>
    /// Optional tag registry used by <see cref="TemplateParser.Parse"/> during <c>add</c> and <c>edit</c> AST validation.
    /// </summary>
    private readonly ITagRegistry? _tagRegistry = tagRegistry;

    /// <summary>
    /// Optional localizer for localized bot responses.
    /// </summary>
    private readonly IStreamActionsLocalizer? _localizer = localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotCommandSubCommandHandler"/> class with an explicit <see cref="IPermissionService"/> for unit testing.
    /// </summary>
    /// <param name="permissionService">The permission service instance, or <see langword="null"/>.</param>
    /// <param name="commandRepository">Optional custom command repository.</param>
    /// <param name="permissionStore">Optional permission store.</param>
    /// <param name="tagRegistry">Optional tag registry for template validation.</param>
    /// <param name="localizer">Optional localizer.</param>
    internal BotCommandSubCommandHandler(
        IPermissionService? permissionService,
        ICommandRepository? commandRepository = null,
        IPermissionStore? permissionStore = null,
        ITagRegistry? tagRegistry = null,
        IStreamActionsLocalizer? localizer = null)
        : this(commandRepository, permissionStore, scopeFactory: null, tagRegistry, localizer) => this._permissionService = permissionService;

    /// <inheritdoc/>
    public string CommandId => "bot.command";

    /// <inheritdoc/>
    public string DefaultSubCommandTrigger => "command";

    /// <inheritdoc/>
    public IReadOnlyList<string> OptionalTopLevelTriggers => [];

    /// <inheritdoc/>
    public bool AllowCustomOverride => false;

    /// <inheritdoc/>
    public string? RequiredPermissionKey => BuiltInPermissionKeys.UiCommandsEdit;

    /// <inheritdoc/>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes sub-command tokens to lowercase invariant.")]
    public async ValueTask ExecuteAsync(BuiltInCommandContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (this._commandRepository is null)
        {
            await SendResponseAsync(
                context,
                this._localizer,
                "commandFailed",
                "{sender}, the command could not be completed: {1}",
                ["Command repository is not configured."],
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (context.Arguments.Count == 0 || string.IsNullOrWhiteSpace(context.Arguments[0]))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [_usageSummary], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string action = context.Arguments[0].Trim().ToLowerInvariant();
        switch (action)
        {
            case "add":
                await this.HandleAddAsync(context, cancellationToken).ConfigureAwait(false);
                break;

            case "edit":
                await this.HandleEditAsync(context, cancellationToken).ConfigureAwait(false);
                break;

            case "delete":
            case "remove":
                await this.HandleDeleteAsync(context, cancellationToken).ConfigureAwait(false);
                break;

            case "permission":
                await this.HandlePermissionAsync(context, cancellationToken).ConfigureAwait(false);
                break;

            case "cooldown":
                await this.HandleCooldownAsync(context, cancellationToken).ConfigureAwait(false);
                break;

            case "cost":
                await this.HandleCostOrRewardAsync(context, isReward: false, cancellationToken).ConfigureAwait(false);
                break;

            case "reward":
                await this.HandleCostOrRewardAsync(context, isReward: true, cancellationToken).ConfigureAwait(false);
                break;

            case "count":
                await this.HandleCountAsync(context, cancellationToken).ConfigureAwait(false);
                break;

            case "toggle":
                await this.HandleToggleAsync(context, cancellationToken).ConfigureAwait(false);
                break;

            default:
                await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [_usageSummary], cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Handles <c>!bot command add &lt;trigger&gt; &lt;response...&gt;</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    private async ValueTask HandleAddAsync(BuiltInCommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count < 3)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command add <trigger> <response...>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        string responseText = ExtractResponseText(context);

        if (string.IsNullOrEmpty(trigger) || string.IsNullOrWhiteSpace(responseText))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command add <trigger> <response...>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            await SendResponseAsync(context, this._localizer, "commandAlreadyExists", _fallbackCommandAlreadyExists, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        TemplateParseResult parseResult = TemplateParser.Parse(responseText, this._tagRegistry);
        if (!parseResult.IsValid)
        {
            string errorMessage = GetFirstSyntaxError(parseResult);
            await SendResponseAsync(context, this._localizer, "commandInvalidSyntax", _fallbackInvalidSyntax, [errorMessage], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand created = new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = context.Invocation.WorkspaceId,
            Trigger = trigger,
            AliasesJson = "[]",
            RoutineJson = CommandRoutine.FromPlainText(responseText).ToJson(),
            IsEnabled = true,
        };

        await this._commandRepository.UpsertAsync(created, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(context, this._localizer, "commandAdded", _fallbackCommandAdded, [trigger], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>!bot command edit &lt;trigger&gt; &lt;response...&gt;</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    private async ValueTask HandleEditAsync(BuiltInCommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count < 3)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command edit <trigger> <response...>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        string responseText = ExtractResponseText(context);

        if (string.IsNullOrEmpty(trigger) || string.IsNullOrWhiteSpace(responseText))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command edit <trigger> <response...>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await SendResponseAsync(context, this._localizer, "commandNotFound", _fallbackCommandNotFound, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        TemplateParseResult parseResult = TemplateParser.Parse(responseText, this._tagRegistry);
        if (!parseResult.IsValid)
        {
            string errorMessage = GetFirstSyntaxError(parseResult);
            await SendResponseAsync(context, this._localizer, "commandInvalidSyntax", _fallbackInvalidSyntax, [errorMessage], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        existing.RoutineJson = CommandRoutine.FromPlainText(responseText).ToJson();
        await this._commandRepository.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(context, this._localizer, "commandEdited", _fallbackCommandEdited, [existing.Trigger], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>!bot command delete &lt;trigger&gt;</c> and <c>!bot command remove &lt;trigger&gt;</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    private async ValueTask HandleDeleteAsync(BuiltInCommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count < 2)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command delete <trigger>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        if (string.IsNullOrEmpty(trigger))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command delete <trigger>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await SendResponseAsync(context, this._localizer, "commandNotFound", _fallbackCommandNotFound, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await this._commandRepository.DeleteAsync(context.Invocation.WorkspaceId, existing.Id, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(context, this._localizer, "commandDeleted", _fallbackCommandDeleted, [existing.Trigger], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>!bot command permission &lt;trigger&gt; &lt;groupName&gt; &lt;allow|deny|inherit&gt;</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes permission states and triggers to lowercase invariant.")]
    private async ValueTask HandlePermissionAsync(BuiltInCommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count < 4)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command permission <trigger> <groupName> <allow|deny|inherit>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (this._permissionStore is null)
        {
            await SendResponseAsync(context, this._localizer, "commandFailed", "{sender}, the command could not be completed: {1}", ["Permission store is not configured."], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        string groupName = context.Arguments[2].Trim();
        string rawState = context.Arguments[3].Trim().ToLowerInvariant();

        if (!TryParsePermissionState(rawState, out PermissionState state) || string.IsNullOrEmpty(trigger) || string.IsNullOrEmpty(groupName))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command permission <trigger> <groupName> <allow|deny|inherit>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await SendResponseAsync(context, this._localizer, "commandNotFound", _fallbackCommandNotFound, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        IReadOnlyList<PermissionGroup> groups = await this._permissionStore
            .GetGroupsAsync(context.Invocation.WorkspaceId, cancellationToken)
            .ConfigureAwait(false);

        PermissionGroup? targetGroup = groups.FirstOrDefault(
            g => string.Equals(g.Name.Trim(), groupName, StringComparison.OrdinalIgnoreCase));

        if (targetGroup is null)
        {
            await SendResponseAsync(context, this._localizer, "permissionGroupNotFound", _fallbackGroupNotFound, [groupName], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string primaryTrigger = BuiltInCommandRegistry.NormalizeTrigger(existing.Trigger);
        await this._permissionStore
            .SetGrantAsync(targetGroup.Id, $"command.{primaryTrigger}", state, cancellationToken)
            .ConfigureAwait(false);

        await this.InvalidateWorkspacePermissionsAsync(context.Invocation.WorkspaceId, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(
            context,
            this._localizer,
            "commandPermissionUpdated",
            _fallbackPermissionUpdated,
            [primaryTrigger, targetGroup.Name, rawState],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>!bot command cooldown &lt;trigger&gt; [-global|-user] &lt;seconds&gt;</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes cooldown flags to lowercase invariant.")]
    private async ValueTask HandleCooldownAsync(BuiltInCommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count < 3)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command cooldown <trigger> [-global|-user] <seconds>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        bool isUserScope = false;
        string secondsToken;

        if (context.Arguments.Count >= 4)
        {
            string flag = context.Arguments[2].Trim().ToLowerInvariant();
            if (flag is "-user" or "user")
            {
                isUserScope = true;
            }
            else if (flag is not ("-global" or "global"))
            {
                await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command cooldown <trigger> [-global|-user] <seconds>"], cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            secondsToken = context.Arguments[3];
        }
        else
        {
            secondsToken = context.Arguments[2];
        }

        if (string.IsNullOrEmpty(trigger)
            || !int.TryParse(secondsToken.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            || seconds < 0)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command cooldown <trigger> [-global|-user] <seconds>"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await SendResponseAsync(context, this._localizer, "commandNotFound", _fallbackCommandNotFound, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (isUserScope)
        {
            existing.UserCooldownSeconds = seconds;
        }
        else
        {
            existing.GlobalCooldownSeconds = seconds;
        }

        await this._commandRepository.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(
            context,
            this._localizer,
            "commandCooldownUpdated",
            _fallbackCooldownUpdated,
            [existing.Trigger, isUserScope ? "user" : "global", seconds.ToString(CultureInfo.InvariantCulture)],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>!bot command cost &lt;trigger&gt; &lt;points&gt;</c> and <c>!bot command reward &lt;trigger&gt; &lt;points&gt;</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="isReward"><see langword="true"/> for <c>reward</c>; <see langword="false"/> for <c>cost</c>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    private async ValueTask HandleCostOrRewardAsync(BuiltInCommandContext context, bool isReward, CancellationToken cancellationToken)
    {
        string usage = isReward ? "!bot command reward <trigger> <points>" : "!bot command cost <trigger> <points>";
        if (context.Arguments.Count < 3)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [usage], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        if (string.IsNullOrEmpty(trigger)
            || !long.TryParse(context.Arguments[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long points)
            || points < 0)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [usage], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await SendResponseAsync(context, this._localizer, "commandNotFound", _fallbackCommandNotFound, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (isReward)
        {
            existing.RewardPoints = points;
        }
        else
        {
            existing.CostPoints = points;
        }

        await this._commandRepository.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(
            context,
            this._localizer,
            isReward ? "commandRewardUpdated" : "commandCostUpdated",
            isReward ? _fallbackRewardUpdated : _fallbackCostUpdated,
            [existing.Trigger, points.ToString(CultureInfo.InvariantCulture)],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>!bot command count &lt;trigger&gt; &lt;behavior|counterName&gt; [behaviorOrCounterName]</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    private async ValueTask HandleCountAsync(BuiltInCommandContext context, CancellationToken cancellationToken)
    {
        const string usage = "!bot command count <trigger> <behavior|counterName> [behaviorOrCounterName]";
        if (context.Arguments.Count < 3)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [usage], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        if (string.IsNullOrEmpty(trigger))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [usage], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await SendResponseAsync(context, this._localizer, "commandNotFound", _fallbackCommandNotFound, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string firstParam = context.Arguments[2].Trim();
        if (string.IsNullOrWhiteSpace(firstParam))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [usage], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Arguments.Count == 3)
        {
            if (TryParseCounterBehavior(firstParam, out CounterUsageBehavior behavior))
            {
                existing.CounterUsageBehavior = behavior;
            }
            else
            {
                existing.BoundCounterName = firstParam;
            }
        }
        else
        {
            string secondParam = context.Arguments[3].Trim();
            if (string.IsNullOrWhiteSpace(secondParam))
            {
                await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [usage], cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (TryParseCounterBehavior(firstParam, out CounterUsageBehavior firstBehavior)
                && !TryParseCounterBehavior(secondParam, out _))
            {
                existing.CounterUsageBehavior = firstBehavior;
                existing.BoundCounterName = secondParam;
            }
            else if (TryParseCounterBehavior(secondParam, out CounterUsageBehavior secondBehavior))
            {
                existing.BoundCounterName = firstParam;
                existing.CounterUsageBehavior = secondBehavior;
            }
            else
            {
                await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, [usage], cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        await this._commandRepository.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(context, this._localizer, "commandCountUpdated", _fallbackCountUpdated, [existing.Trigger], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>!bot command toggle &lt;trigger&gt; [on|off]</c>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes toggle tokens to lowercase invariant.")]
    private async ValueTask HandleToggleAsync(BuiltInCommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count < 2)
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command toggle <trigger> [on|off]"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        string trigger = BuiltInCommandRegistry.NormalizeTrigger(context.Arguments[1]);
        if (string.IsNullOrEmpty(trigger))
        {
            await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command toggle <trigger> [on|off]"], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        bool? explicitState = null;
        if (context.Arguments.Count >= 3)
        {
            string toggleArg = context.Arguments[2].Trim().ToLowerInvariant();
            explicitState = toggleArg switch
            {
                "on" or "true" or "enable" or "enabled" => true,
                "off" or "false" or "disable" or "disabled" => false,
                _ => null,
            };

            if (!explicitState.HasValue)
            {
                await SendResponseAsync(context, this._localizer, "invalidArguments", _fallbackInvalidArgs, ["!bot command toggle <trigger> [on|off]"], cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        CustomCommand? existing = await this._commandRepository!
            .GetByTriggerAsync(context.Invocation.WorkspaceId, trigger, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await SendResponseAsync(context, this._localizer, "commandNotFound", _fallbackCommandNotFound, [trigger], cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        existing.IsEnabled = explicitState ?? !existing.IsEnabled;
        await this._commandRepository.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        await SendResponseAsync(
            context,
            this._localizer,
            "commandToggled",
            _fallbackCommandToggled,
            [existing.Trigger, existing.IsEnabled ? "enabled" : "disabled"],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invalidates cached permissions for <paramref name="workspaceId"/> via <see cref="IPermissionService"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous invalidation operation.</returns>
    private async ValueTask InvalidateWorkspacePermissionsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (this._permissionService is not null)
        {
            await this._permissionService.InvalidateWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (this._scopeFactory is not null)
        {
            AsyncServiceScope scope = this._scopeFactory.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                IPermissionService? service = scope.ServiceProvider.GetService<IPermissionService>();
                if (service is not null)
                {
                    await service.InvalidateWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Extracts the unparsed command response text after the <c>add|edit</c> verb and <c>&lt;trigger&gt;</c> token.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <returns>The trimmed response template text.</returns>
    private static string ExtractResponseText(BuiltInCommandContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.RawArguments))
        {
            string afterVerb = CommandDispatcher.StripFirstToken(context.RawArguments);
            string afterTrigger = CommandDispatcher.StripFirstToken(afterVerb);
            if (!string.IsNullOrWhiteSpace(afterTrigger))
            {
                return afterTrigger.Trim();
            }
        }

        return string.Join(' ', context.Arguments.Skip(2)).Trim();
    }

    /// <summary>
    /// Returns the first syntax error message from a failed <see cref="TemplateParseResult"/>.
    /// </summary>
    /// <param name="parseResult">The parse result containing error diagnostics.</param>
    /// <returns>The first error message.</returns>
    private static string GetFirstSyntaxError(TemplateParseResult parseResult)
    {
        foreach (TemplateDiagnostic diagnostic in parseResult.Diagnostics)
        {
            if (diagnostic.Severity == TemplateDiagnosticSeverity.Error)
            {
                return diagnostic.Message;
            }
        }

        return "Malformed template expression.";
    }

    /// <summary>
    /// Attempts to parse a <see cref="PermissionState"/> keyword (<c>allow</c>, <c>deny</c>, or <c>inherit</c>).
    /// </summary>
    /// <param name="token">The normalized lowercase token.</param>
    /// <param name="state">When this method returns <see langword="true"/>, contains the parsed state.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    private static bool TryParsePermissionState(string token, out PermissionState state)
    {
        switch (token)
        {
            case "allow":
                state = PermissionState.Allow;
                return true;

            case "deny":
                state = PermissionState.Deny;
                return true;

            case "inherit":
                state = PermissionState.Inherit;
                return true;

            default:
                state = PermissionState.Inherit;
                return false;
        }
    }

    /// <summary>
    /// Attempts to parse a <see cref="CounterUsageBehavior"/> keyword or shorthand alias.
    /// </summary>
    /// <param name="token">The raw argument token.</param>
    /// <param name="behavior">When this method returns <see langword="true"/>, contains the parsed behavior.</param>
    /// <returns><see langword="true"/> if <paramref name="token"/> maps to a recognized <see cref="CounterUsageBehavior"/>; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes behavior keywords to lowercase invariant.")]
    private static bool TryParseCounterBehavior(string token, out CounterUsageBehavior behavior)
    {
        switch (token.Trim().ToLowerInvariant())
        {
            case "none":
            case "off":
                behavior = CounterUsageBehavior.None;
                return true;

            case "increment":
            case "inc":
            case "incrementonuse":
                behavior = CounterUsageBehavior.IncrementOnUse;
                return true;

            case "decrement":
            case "dec":
            case "decrementonuse":
                behavior = CounterUsageBehavior.DecrementOnUse;
                return true;

            case "readonly":
            case "read":
                behavior = CounterUsageBehavior.ReadOnly;
                return true;

            case "setfromarg1":
            case "set":
                behavior = CounterUsageBehavior.SetFromArg1;
                return true;

            case "resettozero":
            case "reset":
                behavior = CounterUsageBehavior.ResetToZero;
                return true;

            case "parameterized":
            case "param":
                behavior = CounterUsageBehavior.Parameterized;
                return true;

            default:
                behavior = CounterUsageBehavior.None;
                return false;
        }
    }

    /// <summary>
    /// Formats a localized core response message and sends it via <see cref="BuiltInCommandContext.SendReplyAsync"/>.
    /// </summary>
    /// <param name="context">The command execution context.</param>
    /// <param name="localizer">Optional localizer instance.</param>
    /// <param name="key">The response key inside the <c>core</c> localization module.</param>
    /// <param name="fallbackTemplate">Fallback template when localization is unavailable.</param>
    /// <param name="positionalArgs">Positional placeholder arguments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous send operation.</returns>
    private static async ValueTask SendResponseAsync(
        BuiltInCommandContext context,
        IStreamActionsLocalizer? localizer,
        string key,
        string fallbackTemplate,
        IReadOnlyList<string> positionalArgs,
        CancellationToken cancellationToken)
    {
        string formatted = await CommandResponseFormatter.FormatCoreResponseAsync(
            localizer,
            context.Invocation.WorkspaceId,
            key,
            fallbackTemplate,
            context.Invocation.Sender.DisplayName,
            positionalArgs,
            cancellationToken).ConfigureAwait(false);

        await context.SendReplyAsync(formatted, cancellationToken).ConfigureAwait(false);
    }
}
