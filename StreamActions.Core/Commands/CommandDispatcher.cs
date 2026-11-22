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
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Permissions;
using StreamActions.Core.Templates;
using StreamActions.Core.Templates.Handlers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace StreamActions.Core.Commands;

/// <summary>
/// Unified command execution middleware pipeline handling built-in <c>!bot</c> / <c>!&lt;botname&gt;</c> sub-commands, top-level built-in commands with <c>AllowCustomOverride</c>, and workspace custom commands across both chat and whispers.
/// </summary>
public sealed partial class CommandDispatcher : ICommandDispatcher
{
    /// <summary>
    /// Tracks active nested command chain depth and active call-stack triggers across async <c>{botcmd}</c> invocations.
    /// </summary>
    private static readonly AsyncLocal<CommandChainFrame?> _activeChainFrame = new();

    /// <summary>
    /// Fallback English message when a viewer lacks sufficient loyalty points to execute a command.
    /// </summary>
    private const string _fallbackInsufficientPoints =
        "{sender}, you do not have enough points ({1} required) to use that command.";

    /// <summary>
    /// Registry of built-in command handlers and top-level alias exposure settings.
    /// </summary>
    private readonly IBuiltInCommandRegistry _builtInRegistry;

    /// <summary>
    /// Pre-compiled AST cache for custom command routines.
    /// </summary>
    private readonly CommandAstCache _astCache;

    /// <summary>
    /// Cooldown tracker enforcing global and per-user cooldowns across chat and whispers.
    /// </summary>
    private readonly CommandCooldownTracker _cooldownTracker;

    /// <summary>
    /// Layered tag registry passed to <see cref="TagEvaluationContext"/>.
    /// </summary>
    private readonly ITagRegistry _tagRegistry;

    /// <summary>
    /// Time provider used for template evaluation timeouts and cooldown tracking.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Logger used for command execution diagnostics.
    /// </summary>
    private readonly ILogger<CommandDispatcher> _logger;

    /// <summary>
    /// Optional DI scope factory used to resolve scoped services such as <see cref="IPermissionService"/>.
    /// </summary>
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// Optional service provider passed to <see cref="TagEvaluationContext"/> for extensible tag resolution.
    /// </summary>
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Optional direct <see cref="IPermissionService"/> override (typically supplied in unit tests).
    /// </summary>
    private readonly IPermissionService? _permissionService;

    /// <summary>
    /// Optional custom command repository.
    /// </summary>
    private readonly ICommandRepository? _commandRepository;

    /// <summary>
    /// Optional counter store for template evaluation.
    /// </summary>
    private readonly ICounterStore? _counterStore;

    /// <summary>
    /// Optional viewer loyalty store for command point costs and rewards.
    /// </summary>
    private readonly IViewerLoyaltyStore? _loyaltyStore;

    /// <summary>
    /// Optional workspace store for bot username resolution, ownership checks, and whisper channel fallback.
    /// </summary>
    private readonly IWorkspaceStore? _workspaceStore;

    /// <summary>
    /// Optional workspace key-value store for template evaluation.
    /// </summary>
    private readonly IWorkspaceKeyValueStore? _keyValueStore;

    /// <summary>
    /// Optional stream status provider for online/offline gating and active game/category lookup.
    /// </summary>
    private readonly IStreamStatusProvider? _streamStatusProvider;

    /// <summary>
    /// Optional outbound chat dispatcher for delivering command responses.
    /// </summary>
    private readonly IOutboundChatDispatcher? _outboundDispatcher;

    /// <summary>
    /// Optional localizer for localized sub-command resolution and response messages.
    /// </summary>
    private readonly IStreamActionsLocalizer? _localizer;

    /// <summary>
    /// Optional SSRF-hardened HTTP client for <c>{api}</c> template evaluation.
    /// </summary>
    private readonly ISafeExternalHttpClient? _safeExternalHttpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandDispatcher"/> class for dependency injection.
    /// </summary>
    /// <param name="builtInRegistry">The built-in command registry.</param>
    /// <param name="astCache">The pre-compiled command AST cache.</param>
    /// <param name="cooldownTracker">The cooldown tracker.</param>
    /// <param name="tagRegistry">The tag registry.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="scopeFactory">Optional service scope factory for resolving scoped <see cref="IPermissionService"/>.</param>
    /// <param name="serviceProvider">Optional service provider.</param>
    /// <param name="commandRepository">Optional custom command repository.</param>
    /// <param name="counterStore">Optional counter store.</param>
    /// <param name="loyaltyStore">Optional viewer loyalty store.</param>
    /// <param name="workspaceStore">Optional workspace store.</param>
    /// <param name="keyValueStore">Optional workspace key-value store.</param>
    /// <param name="streamStatusProvider">Optional stream status provider.</param>
    /// <param name="outboundDispatcher">Optional outbound chat dispatcher.</param>
    /// <param name="localizer">Optional localizer.</param>
    /// <param name="safeExternalHttpClient">Optional safe external HTTP client.</param>
    [ActivatorUtilitiesConstructor]
    public CommandDispatcher(
        IBuiltInCommandRegistry builtInRegistry,
        CommandAstCache astCache,
        CommandCooldownTracker cooldownTracker,
        ITagRegistry tagRegistry,
        TimeProvider? timeProvider = null,
        ILogger<CommandDispatcher>? logger = null,
        IServiceScopeFactory? scopeFactory = null,
        IServiceProvider? serviceProvider = null,
        ICommandRepository? commandRepository = null,
        ICounterStore? counterStore = null,
        IViewerLoyaltyStore? loyaltyStore = null,
        IWorkspaceStore? workspaceStore = null,
        IWorkspaceKeyValueStore? keyValueStore = null,
        IStreamStatusProvider? streamStatusProvider = null,
        IOutboundChatDispatcher? outboundDispatcher = null,
        IStreamActionsLocalizer? localizer = null,
        ISafeExternalHttpClient? safeExternalHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builtInRegistry);
        ArgumentNullException.ThrowIfNull(astCache);
        ArgumentNullException.ThrowIfNull(cooldownTracker);
        ArgumentNullException.ThrowIfNull(tagRegistry);

        this._builtInRegistry = builtInRegistry;
        this._astCache = astCache;
        this._cooldownTracker = cooldownTracker;
        this._tagRegistry = tagRegistry;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._logger = logger ?? NullLogger<CommandDispatcher>.Instance;
        this._scopeFactory = scopeFactory;
        this._serviceProvider = serviceProvider;
        this._commandRepository = commandRepository;
        this._counterStore = counterStore;
        this._loyaltyStore = loyaltyStore;
        this._workspaceStore = workspaceStore;
        this._keyValueStore = keyValueStore;
        this._streamStatusProvider = streamStatusProvider;
        this._outboundDispatcher = outboundDispatcher;
        this._localizer = localizer;
        this._safeExternalHttpClient = safeExternalHttpClient;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandDispatcher"/> class with an explicit <see cref="IPermissionService"/> instance (primarily for unit testing without DI scopes).
    /// </summary>
    /// <param name="builtInRegistry">The built-in command registry.</param>
    /// <param name="astCache">The pre-compiled command AST cache.</param>
    /// <param name="cooldownTracker">The cooldown tracker.</param>
    /// <param name="tagRegistry">The tag registry.</param>
    /// <param name="permissionService">The permission service instance, or <see langword="null"/> to skip permission checks.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="commandRepository">Optional custom command repository.</param>
    /// <param name="counterStore">Optional counter store.</param>
    /// <param name="loyaltyStore">Optional viewer loyalty store.</param>
    /// <param name="workspaceStore">Optional workspace store.</param>
    /// <param name="keyValueStore">Optional workspace key-value store.</param>
    /// <param name="streamStatusProvider">Optional stream status provider.</param>
    /// <param name="outboundDispatcher">Optional outbound chat dispatcher.</param>
    /// <param name="localizer">Optional localizer.</param>
    /// <param name="safeExternalHttpClient">Optional safe external HTTP client.</param>
    /// <param name="serviceProvider">Optional service provider.</param>
    internal CommandDispatcher(
        IBuiltInCommandRegistry builtInRegistry,
        CommandAstCache astCache,
        CommandCooldownTracker cooldownTracker,
        ITagRegistry tagRegistry,
        IPermissionService? permissionService,
        TimeProvider? timeProvider = null,
        ILogger<CommandDispatcher>? logger = null,
        ICommandRepository? commandRepository = null,
        ICounterStore? counterStore = null,
        IViewerLoyaltyStore? loyaltyStore = null,
        IWorkspaceStore? workspaceStore = null,
        IWorkspaceKeyValueStore? keyValueStore = null,
        IStreamStatusProvider? streamStatusProvider = null,
        IOutboundChatDispatcher? outboundDispatcher = null,
        IStreamActionsLocalizer? localizer = null,
        ISafeExternalHttpClient? safeExternalHttpClient = null,
        IServiceProvider? serviceProvider = null)
        : this(
            builtInRegistry,
            astCache,
            cooldownTracker,
            tagRegistry,
            timeProvider,
            logger,
            scopeFactory: null,
            serviceProvider,
            commandRepository,
            counterStore,
            loyaltyStore,
            workspaceStore,
            keyValueStore,
            streamStatusProvider,
            outboundDispatcher,
            localizer,
            safeExternalHttpClient) => this._permissionService = permissionService;

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Command execution exceptions must be isolated, trigger loyalty/cooldown rollback, be logged, and return CommandDispatchStatus.Failed.")]
    public async ValueTask<CommandDispatchResult> DispatchAsync(
        ICommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        string normalizedTrigger = BuiltInCommandRegistry.NormalizeTrigger(invocation.Trigger);
        if (string.IsNullOrEmpty(normalizedTrigger))
        {
            return CommandDispatchResult.NotFound();
        }

        CommandChainFrame? currentFrame = _activeChainFrame.Value;
        (int effectiveDepth, HashSet<string> metadataVisited) = ExtractChainContext(invocation, currentFrame);

        if (effectiveDepth > TemplateExecutionLimits.MaxCommandChainDepth)
        {
            return CommandDispatchResult.Rejected(
                CommandDispatchStatus.Failed,
                normalizedTrigger,
                $"Command chain depth {effectiveDepth} exceeded maximum of {TemplateExecutionLimits.MaxCommandChainDepth}.");
        }

        if (currentFrame is not null && currentFrame.ActiveStack.Contains(normalizedTrigger))
        {
            return CommandDispatchResult.Rejected(
                CommandDispatchStatus.Failed,
                normalizedTrigger,
                $"Detected recursive command cycle on trigger '{normalizedTrigger}'.");
        }

        AsyncServiceScope? ownedScope = null;
        IPermissionService? permissionService = this.ResolvePermissionService(ref ownedScope);
        try
        {
            // 1. Check !bot or !<activeBotUsername> sub-command routing.
            string? activeBotUsername = await this.ResolveActiveBotUsernameAsync(invocation.WorkspaceId, invocation.PlatformId, cancellationToken)
                .ConfigureAwait(false);

            bool isBotAlias = string.Equals(normalizedTrigger, "bot", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(activeBotUsername)
                    && string.Equals(normalizedTrigger, activeBotUsername, StringComparison.OrdinalIgnoreCase));

            if (isBotAlias)
            {
                if (invocation.Arguments.Count == 0 || string.IsNullOrWhiteSpace(invocation.Arguments[0]))
                {
                    return CommandDispatchResult.NotFound(normalizedTrigger);
                }

                string subTrigger = BuiltInCommandRegistry.NormalizeTrigger(invocation.Arguments[0]);
                BuiltInCommandRegistration? subHandler = await this.ResolveSubCommandHandlerAsync(
                    invocation.WorkspaceId,
                    subTrigger,
                    cancellationToken).ConfigureAwait(false);

                if (subHandler is null)
                {
                    return CommandDispatchResult.NotFound(subTrigger);
                }

                IReadOnlyList<string> subArgs = invocation.Arguments.Count > 1
                    ? [.. invocation.Arguments.Skip(1)]
                    : [];
                string rawSubArgs = StripFirstToken(invocation.RawArguments);

                return await this.ExecuteBuiltInAsync(
                    invocation,
                    subHandler,
                    subTrigger,
                    isSubCommandInvocation: true,
                    subArgs,
                    rawSubArgs,
                    permissionService,
                    cancellationToken).ConfigureAwait(false);
            }

            // 2. Top-level built-in vs CustomCommand routing with AllowCustomOverride precedence.
            BuiltInCommandRegistration? topLevelBuiltIn = await this._builtInRegistry
                .ResolveByTopLevelTriggerAsync(invocation.WorkspaceId, normalizedTrigger, cancellationToken)
                .ConfigureAwait(false);

            CustomCommand? customCommand = this._commandRepository is not null
                ? await this._commandRepository.GetByTriggerAsync(invocation.WorkspaceId, normalizedTrigger, cancellationToken).ConfigureAwait(false)
                : null;

            return topLevelBuiltIn is not null
                ? topLevelBuiltIn.AllowCustomOverride && customCommand is not null && customCommand.IsEnabled
                    ? await this.ExecuteCustomCommandAsync(
                        invocation,
                        customCommand,
                        permissionService,
                        effectiveDepth,
                        currentFrame,
                        metadataVisited,
                        cancellationToken).ConfigureAwait(false)
                    : await this.ExecuteBuiltInAsync(
                    invocation,
                    topLevelBuiltIn,
                    normalizedTrigger,
                    isSubCommandInvocation: false,
                    invocation.Arguments,
                    invocation.RawArguments,
                    permissionService,
                    cancellationToken).ConfigureAwait(false)
                : customCommand is null
                ? CommandDispatchResult.NotFound(normalizedTrigger)
                : await this.ExecuteCustomCommandAsync(
                invocation,
                customCommand,
                permissionService,
                effectiveDepth,
                currentFrame,
                metadataVisited,
                cancellationToken).ConfigureAwait(false);
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
    /// Executes a built-in command handler through permission and cooldown gates.
    /// </summary>
    /// <param name="invocation">The inbound command invocation.</param>
    /// <param name="registration">The matched built-in command registration.</param>
    /// <param name="matchedTrigger">The normalized trigger or sub-command trigger.</param>
    /// <param name="isSubCommandInvocation">Whether invoked via <c>!bot</c> or <c>!&lt;botname&gt;</c>.</param>
    /// <param name="arguments">Effective arguments for the handler.</param>
    /// <param name="rawArguments">Effective raw argument string for the handler.</param>
    /// <param name="permissionService">Optional permission service for permission gating.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="CommandDispatchResult"/>.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Built-in command failures must restore cooldowns, log diagnostics, and return CommandDispatchStatus.Failed.")]
    private async ValueTask<CommandDispatchResult> ExecuteBuiltInAsync(
        ICommandInvocation invocation,
        BuiltInCommandRegistration registration,
        string matchedTrigger,
        bool isSubCommandInvocation,
        IReadOnlyList<string> arguments,
        string rawArguments,
        IPermissionService? permissionService,
        CancellationToken cancellationToken)
    {
        PlatformUserIdentity actorIdentity = invocation.Sender.ToIdentity();

        if (!string.IsNullOrWhiteSpace(registration.RequiredPermissionKey) && permissionService is not null)
        {
            PermissionEvaluationResult permissionResult = await permissionService
                .EvaluateAsync(invocation.WorkspaceId, actorIdentity, registration.RequiredPermissionKey, cancellationToken)
                .ConfigureAwait(false);

            if (!permissionResult.IsAllowed)
            {
                return CommandDispatchResult.Rejected(CommandDispatchStatus.PermissionDenied, matchedTrigger, permissionResult.Reason);
            }
        }

        string bucketKey = $"builtin:{registration.CommandId}";
        CooldownCheckResult cooldownCheck = this._cooldownTracker.CheckCooldown(
            invocation.WorkspaceId,
            bucketKey,
            invocation.PlatformId,
            invocation.Sender.PlatformUserId,
            registration.GlobalCooldownSeconds,
            registration.UserCooldownSeconds);

        if (!cooldownCheck.IsAllowed)
        {
            CommandDispatchStatus cooldownStatus = cooldownCheck.IsGlobalCooldown
                ? CommandDispatchStatus.OnGlobalCooldown
                : CommandDispatchStatus.OnUserCooldown;
            return CommandDispatchResult.Rejected(cooldownStatus, matchedTrigger, $"Cooldown active ({Math.Ceiling(cooldownCheck.Remaining.TotalSeconds)}s remaining).");
        }

        CooldownSnapshot cooldownSnapshot = this._cooldownTracker.RecordExecution(
            invocation.WorkspaceId,
            bucketKey,
            invocation.PlatformId,
            invocation.Sender.PlatformUserId,
            registration.GlobalCooldownSeconds,
            registration.UserCooldownSeconds);

        BuiltInCommandContext context = new(
            invocation,
            registration.CommandId,
            matchedTrigger,
            isSubCommandInvocation,
            arguments,
            rawArguments,
            actorIdentity,
            this._outboundDispatcher,
            this._workspaceStore,
            this._localizer,
            this._timeProvider,
            this._serviceProvider);

        bool succeeded = false;
        try
        {
            await registration.Handler.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            succeeded = context.Result is null || context.Result.Executed;
            return context.Result ?? CommandDispatchResult.Success(matchedTrigger);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogBuiltInExecutionFailed(this._logger, registration.CommandId, invocation.WorkspaceId, ex);
            return CommandDispatchResult.Rejected(CommandDispatchStatus.Failed, matchedTrigger, ex.Message);
        }
        finally
        {
            if (!succeeded)
            {
                this._cooldownTracker.RestoreCooldown(cooldownSnapshot);
            }
        }
    }

    /// <summary>
    /// Executes a <see cref="CustomCommand"/> through the full 5-stage middleware pipeline.
    /// </summary>
    /// <param name="invocation">The inbound command invocation.</param>
    /// <param name="command">The matched custom command entity.</param>
    /// <param name="permissionService">Optional permission service.</param>
    /// <param name="chainDepth">Current command chain depth.</param>
    /// <param name="parentFrame">Parent async chain frame, if nested.</param>
    /// <param name="metadataVisited">Visited triggers extracted from invocation metadata.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="CommandDispatchResult"/>.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Custom command failures must refund loyalty points, restore cooldowns, log diagnostics, and return CommandDispatchStatus.Failed.")]
    private async ValueTask<CommandDispatchResult> ExecuteCustomCommandAsync(
        ICommandInvocation invocation,
        CustomCommand command,
        IPermissionService? permissionService,
        int chainDepth,
        CommandChainFrame? parentFrame,
        HashSet<string> metadataVisited,
        CancellationToken cancellationToken)
    {
        string primaryTrigger = BuiltInCommandRegistry.NormalizeTrigger(command.Trigger);
        if (string.IsNullOrEmpty(primaryTrigger))
        {
            primaryTrigger = BuiltInCommandRegistry.NormalizeTrigger(invocation.Trigger);
        }

        if (parentFrame is not null && parentFrame.ActiveStack.Contains(primaryTrigger))
        {
            return CommandDispatchResult.Rejected(
                CommandDispatchStatus.Failed,
                primaryTrigger,
                $"Detected recursive command cycle on trigger '{primaryTrigger}'.");
        }

        PlatformUserIdentity actorIdentity = invocation.Sender.ToIdentity();

        // Stage 1: Feature & Enabled & Platform & Stream Status Gate.
        if (permissionService is not null)
        {
            PlatformUserIdentity featureActor = actorIdentity.IsRoot || actorIdentity.IsGlobalAdmin
                ? actorIdentity
                : new PlatformUserIdentity(
                    invocation.PlatformId,
                    "__workspace__",
                    "workspace",
                    PlatformUserDesignations.Broadcaster);

            bool featureAllowed = await permissionService
                .IsAllowedAsync(invocation.WorkspaceId, featureActor, BuiltInPermissionKeys.FeatureCustomCommands, cancellationToken)
                .ConfigureAwait(false);

            if (!featureAllowed)
            {
                return CommandDispatchResult.Rejected(
                    CommandDispatchStatus.PermissionDenied,
                    primaryTrigger,
                    "Custom commands feature is not enabled for this workspace.");
            }
        }

        if (!command.IsEnabled)
        {
            return CommandDispatchResult.Rejected(CommandDispatchStatus.Disabled, primaryTrigger, "Command is disabled.");
        }

        if (command.Metadata?.AllowedPlatforms is { Count: > 0 } allowedPlatforms
            && !allowedPlatforms.Contains(invocation.PlatformId, StringComparer.OrdinalIgnoreCase))
        {
            return CommandDispatchResult.Rejected(
                CommandDispatchStatus.PlatformNotAllowed,
                primaryTrigger,
                $"Platform '{invocation.PlatformId}' is not permitted for command '{primaryTrigger}'.");
        }

        if (command.StreamStatusRequirement != StreamStatusRequirement.Both)
        {
            bool isLive = this._streamStatusProvider is not null
                && await this._streamStatusProvider
                    .IsStreamLiveAsync(invocation.WorkspaceId, platformId: null, cancellationToken)
                    .ConfigureAwait(false);

            if (command.StreamStatusRequirement == StreamStatusRequirement.OnlineOnly && !isLive)
            {
                return CommandDispatchResult.Rejected(
                    CommandDispatchStatus.StreamStatusMismatch,
                    primaryTrigger,
                    "Command requires the stream to be online.");
            }

            if (command.StreamStatusRequirement == StreamStatusRequirement.OfflineOnly && isLive)
            {
                return CommandDispatchResult.Rejected(
                    CommandDispatchStatus.StreamStatusMismatch,
                    primaryTrigger,
                    "Command requires the stream to be offline.");
            }
        }

        // Stage 2: Permission Check ("command.{trigger}" with Inherit fallback to BuiltInPermissionKeys.ActionRunCommand).
        if (permissionService is not null)
        {
            PermissionEvaluationResult commandPerm = await permissionService
                .EvaluateAsync(invocation.WorkspaceId, actorIdentity, $"command.{primaryTrigger}", cancellationToken)
                .ConfigureAwait(false);

            if (commandPerm.EffectiveState != PermissionState.Inherit)
            {
                if (!commandPerm.IsAllowed)
                {
                    return CommandDispatchResult.Rejected(CommandDispatchStatus.PermissionDenied, primaryTrigger, commandPerm.Reason);
                }
            }
            else
            {
                PermissionEvaluationResult fallbackPerm = await permissionService
                    .EvaluateAsync(invocation.WorkspaceId, actorIdentity, BuiltInPermissionKeys.ActionRunCommand, cancellationToken)
                    .ConfigureAwait(false);

                if (!fallbackPerm.IsAllowed)
                {
                    return CommandDispatchResult.Rejected(CommandDispatchStatus.PermissionDenied, primaryTrigger, fallbackPerm.Reason);
                }
            }
        }

        // Stage 3: Cooldown Check (shared bucket across primary trigger and all aliases, works for both chat and whispers).
        string bucketKey = command.Id != Guid.Empty
            ? $"custom:{command.Id:N}"
            : $"custom:trigger:{primaryTrigger}";

        CooldownCheckResult cooldownCheck = this._cooldownTracker.CheckCooldown(
            invocation.WorkspaceId,
            bucketKey,
            invocation.PlatformId,
            invocation.Sender.PlatformUserId,
            command.GlobalCooldownSeconds,
            command.UserCooldownSeconds);

        if (!cooldownCheck.IsAllowed)
        {
            CommandDispatchStatus status = cooldownCheck.IsGlobalCooldown
                ? CommandDispatchStatus.OnGlobalCooldown
                : CommandDispatchStatus.OnUserCooldown;
            return CommandDispatchResult.Rejected(status, primaryTrigger, $"Cooldown active ({Math.Ceiling(cooldownCheck.Remaining.TotalSeconds)}s remaining).");
        }

        // Stage 4: Cost / Loyalty Check (spend before execution; refund on exception or {cancel}).
        long costPoints = Math.Max(0L, command.CostPoints);
        bool pointsSpent = false;

        if (costPoints > 0)
        {
            if (this._loyaltyStore is null)
            {
                await this.SendInsufficientPointsNoticeAsync(invocation, costPoints, cancellationToken).ConfigureAwait(false);
                return CommandDispatchResult.Rejected(CommandDispatchStatus.InsufficientPoints, primaryTrigger, "Loyalty store is unavailable.");
            }

            pointsSpent = await this._loyaltyStore
                .TrySpendPointsAsync(
                    invocation.WorkspaceId,
                    invocation.PlatformId,
                    invocation.Sender.PlatformUserId,
                    costPoints,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!pointsSpent)
            {
                await this.SendInsufficientPointsNoticeAsync(invocation, costPoints, cancellationToken).ConfigureAwait(false);
                return CommandDispatchResult.Rejected(CommandDispatchStatus.InsufficientPoints, primaryTrigger, "Insufficient loyalty points.");
            }
        }

        // Record cooldown right before execution and roll back on cancel or exception.
        CooldownSnapshot cooldownSnapshot = this._cooldownTracker.RecordExecution(
            invocation.WorkspaceId,
            bucketKey,
            invocation.PlatformId,
            invocation.Sender.PlatformUserId,
            command.GlobalCooldownSeconds,
            command.UserCooldownSeconds);

        bool completedWithoutCancelOrError = false;
        CommandChainFrame? previousChainFrame = _activeChainFrame.Value;

        try
        {
            // Stage 5: Execution, Counter Evaluation & Delivery.
            bool canMutateBoundCounter = await this.DetermineCanMutateBoundCounterAsync(
                invocation.WorkspaceId,
                actorIdentity,
                command,
                permissionService,
                cancellationToken).ConfigureAwait(false);

            string? currentGameCategory = await this.ResolveCurrentGameCategoryAsync(
                invocation.WorkspaceId,
                invocation.PlatformId,
                cancellationToken).ConfigureAwait(false);

            CompiledCommandRoutine compiledRoutine = this._astCache.GetOrCompileRoutine(command);

            HashSet<string> nextActiveStack = parentFrame is not null
                ? new HashSet<string>(parentFrame.ActiveStack, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _ = nextActiveStack.Add(BuiltInCommandRegistry.NormalizeTrigger(invocation.Trigger));
            _ = nextActiveStack.Add(primaryTrigger);

            HashSet<string> contextVisited = new(nextActiveStack, StringComparer.OrdinalIgnoreCase);
            foreach (string visited in metadataVisited)
            {
                _ = contextVisited.Add(visited);
            }

            TagEvaluationDependencies dependencies = new()
            {
                CounterStore = this._counterStore,
                Localizer = this._localizer,
                StreamStatusProvider = this._streamStatusProvider,
                KeyValueStore = this._keyValueStore,
                SafeExternalHttpClient = this._safeExternalHttpClient,
                CommandDispatcher = this,
                CommandRepository = this._commandRepository,
                ViewerLoyaltyStore = this._loyaltyStore,
                TimeProvider = this._timeProvider,
            };

            TagEvaluationContext evalContext = new(
                invocation,
                this._tagRegistry,
                currentGameCategory: currentGameCategory,
                actorIdentity: actorIdentity,
                boundCounterName: command.BoundCounterName,
                counterUsageBehavior: command.CounterUsageBehavior,
                canMutateBoundCounter: canMutateBoundCounter,
                serviceProvider: this._serviceProvider,
                dependencies: dependencies,
                timeProvider: this._timeProvider,
                chainDepth: chainDepth,
                visitedCommandTriggers: contextVisited);

            _activeChainFrame.Value = new CommandChainFrame(chainDepth, nextActiveStack);

            List<string> renderedMessages = new(compiledRoutine.Steps.Count);
            foreach (CompiledRoutineStep step in compiledRoutine.Steps)
            {
                TagResult evalResult = await TemplateEvaluator
                    .EvaluateAsync(step.ParseResult.Root, evalContext, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (evalContext.IsCanceled)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(evalResult.Value))
                {
                    renderedMessages.Add(evalResult.Value.Trim());
                }
            }

            if (evalContext.IsCanceled)
            {
                return CommandDispatchResult.Success(primaryTrigger);
            }

            await this.DeliverRenderedMessagesAsync(invocation, command, renderedMessages, cancellationToken).ConfigureAwait(false);

            if (command.RewardPoints > 0 && this._loyaltyStore is not null)
            {
                _ = await this._loyaltyStore
                    .AdjustPointsAsync(
                        invocation.WorkspaceId,
                        invocation.PlatformId,
                        invocation.Sender.PlatformUserId,
                        command.RewardPoints,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            completedWithoutCancelOrError = true;
            return CommandDispatchResult.Success(primaryTrigger);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCustomCommandExecutionFailed(this._logger, primaryTrigger, invocation.WorkspaceId, ex);
            return CommandDispatchResult.Rejected(CommandDispatchStatus.Failed, primaryTrigger, ex.Message);
        }
        finally
        {
            _activeChainFrame.Value = previousChainFrame;

            if (!completedWithoutCancelOrError)
            {
                this._cooldownTracker.RestoreCooldown(cooldownSnapshot);

                if (pointsSpent && costPoints > 0 && this._loyaltyStore is not null)
                {
                    _ = await this._loyaltyStore
                        .AdjustPointsAsync(
                            invocation.WorkspaceId,
                            invocation.PlatformId,
                            invocation.Sender.PlatformUserId,
                            costPoints,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the active bot username configured for <paramref name="platformId"/> in <paramref name="workspaceId"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The normalized lowercase bot username, or <see langword="null"/> if not configured.</returns>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "StreamActions normalizes bot usernames and triggers to lowercase invariant.")]
    private async ValueTask<string?> ResolveActiveBotUsernameAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken)
    {
        if (this._workspaceStore is null || string.IsNullOrWhiteSpace(platformId))
        {
            return null;
        }

        WorkspacePlatform? platform = await this._workspaceStore
            .GetPlatformAsync(workspaceId, platformId, cancellationToken)
            .ConfigureAwait(false);

        string? botUsername = platform?.Metadata?.BotUsername;
        return string.IsNullOrWhiteSpace(botUsername)
            ? null
            : botUsername.Trim().TrimStart('@').ToLowerInvariant();
    }

    /// <summary>
    /// Resolves a <c>!bot</c> sub-command handler via <see cref="IStreamActionsLocalizer.ResolveCommandIdAsync"/> first, then <see cref="IBuiltInCommandRegistry.ResolveBySubCommandTrigger"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="subTrigger">The normalized sub-command trigger.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="BuiltInCommandRegistration"/>, or <see langword="null"/>.</returns>
    private async ValueTask<BuiltInCommandRegistration?> ResolveSubCommandHandlerAsync(
        Guid workspaceId,
        string subTrigger,
        CancellationToken cancellationToken)
    {
        if (this._localizer is not null)
        {
            string? localizedCommandId = await this._localizer
                .ResolveCommandIdAsync(workspaceId, subTrigger, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(localizedCommandId))
            {
                BuiltInCommandRegistration? byId = this._builtInRegistry.ResolveByCommandId(localizedCommandId);
                if (byId is not null)
                {
                    return byId;
                }
            }
        }

        return this._builtInRegistry.ResolveBySubCommandTrigger(subTrigger);
    }

    /// <summary>
    /// Determines whether the invoking user is permitted to mutate the command's bound counter.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="actorIdentity">The invoking user identity.</param>
    /// <param name="command">The custom command being executed.</param>
    /// <param name="permissionService">Optional permission service.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if counter mutation is allowed; otherwise, <see langword="false"/>.</returns>
    private async ValueTask<bool> DetermineCanMutateBoundCounterAsync(
        Guid workspaceId,
        PlatformUserIdentity actorIdentity,
        CustomCommand command,
        IPermissionService? permissionService,
        CancellationToken cancellationToken)
    {
        if (actorIdentity.IsRoot
            || actorIdentity.IsGlobalAdmin
            || actorIdentity.Designations.HasFlag(PlatformUserDesignations.Broadcaster))
        {
            return true;
        }

        if (actorIdentity.PanelUserId.HasValue && this._workspaceStore is not null)
        {
            Workspace? workspace = await this._workspaceStore.GetByIdAsync(workspaceId, cancellationToken).ConfigureAwait(false);
            if (workspace is not null && workspace.OwnerUserId == actorIdentity.PanelUserId.Value)
            {
                return true;
            }
        }

        string? mutatePermissionKey = command.Metadata?.CounterMutatePermissionKey;
        if (!string.IsNullOrWhiteSpace(mutatePermissionKey))
        {
            return permissionService is not null && await permissionService
                .IsAllowedAsync(workspaceId, actorIdentity, mutatePermissionKey.Trim(), cancellationToken)
                .ConfigureAwait(false);
        }

        PlatformUserDesignations requiredDesignations = command.Metadata?.CounterMutateDesignations ?? PlatformUserDesignations.Moderator;
        return requiredDesignations.HasFlag(PlatformUserDesignations.Everyone) || (actorIdentity.Designations & requiredDesignations) != PlatformUserDesignations.None;
    }

    /// <summary>
    /// Resolves the current stream game or category for <paramref name="workspaceId"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="platformId">The origin platform identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The current game or category name, or <see langword="null"/> if unavailable.</returns>
    private async ValueTask<string?> ResolveCurrentGameCategoryAsync(
        Guid workspaceId,
        string platformId,
        CancellationToken cancellationToken)
    {
        if (this._streamStatusProvider is null)
        {
            return null;
        }

        string? category = await this._streamStatusProvider
            .GetCurrentGameCategoryAsync(workspaceId, platformId, cancellationToken)
            .ConfigureAwait(false);

        return category ?? await this._streamStatusProvider
            .GetCurrentGameCategoryAsync(workspaceId, platformId: null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Delivers rendered command output messages through <see cref="IOutboundChatDispatcher"/> according to <see cref="CustomCommand.DeliveryMode"/> and <see cref="Models.Metadata.CommandMetadata.TargetScope"/>.
    /// </summary>
    /// <param name="invocation">The inbound command invocation.</param>
    /// <param name="command">The executed custom command.</param>
    /// <param name="renderedMessages">The rendered non-empty output messages.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous delivery operation.</returns>
    private async ValueTask DeliverRenderedMessagesAsync(
        ICommandInvocation invocation,
        CustomCommand command,
        List<string> renderedMessages,
        CancellationToken cancellationToken)
    {
        if (this._outboundDispatcher is null || renderedMessages.Count == 0)
        {
            return;
        }

        OutboundTargetScope targetScope = command.Metadata?.TargetScope ?? OutboundTargetScope.OriginPlatform;
        string channelId = await this.ResolveChannelIdAsync(invocation, command, cancellationToken).ConfigureAwait(false);

        foreach (string text in renderedMessages)
        {
            OutboundChatMessage outbound = command.DeliveryMode switch
            {
                CommandDeliveryMode.Whisper => new OutboundChatMessage(
                    invocation.WorkspaceId,
                    invocation.PlatformId,
                    string.Empty,
                    text,
                    OutboundChatPriority.CommandReply,
                    OutboundTargetScope.OriginPlatform,
                    DuplicateMessagePolicy.PlatformDefault,
                    ReplyToMessageId: null,
                    IsWhisper: true,
                    TargetPlatformUserId: invocation.Sender.PlatformUserId),

                CommandDeliveryMode.ThreadedReply => new OutboundChatMessage(
                    invocation.WorkspaceId,
                    invocation.PlatformId,
                    channelId,
                    text,
                    OutboundChatPriority.CommandReply,
                    targetScope,
                    DuplicateMessagePolicy.PlatformDefault,
                    ReplyToMessageId: (invocation as IChatMessage)?.MessageId),
                CommandDeliveryMode.Chat or _ => new OutboundChatMessage(
                    invocation.WorkspaceId,
                    invocation.PlatformId,
                    channelId,
                    text,
                    OutboundChatPriority.CommandReply,
                    targetScope,
                    DuplicateMessagePolicy.PlatformDefault,
                    ReplyToMessageId: null),
            };

            await this._outboundDispatcher.EnqueueAsync(outbound, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves the destination channel identifier for outbound channel messages.
    /// </summary>
    /// <param name="invocation">The inbound command invocation.</param>
    /// <param name="command">The executed custom command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved channel identifier, or <see cref="string.Empty"/> when unavailable.</returns>
    private async ValueTask<string> ResolveChannelIdAsync(
        ICommandInvocation invocation,
        CustomCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.Metadata?.PlatformChannelOverride))
        {
            return command.Metadata.PlatformChannelOverride.Trim();
        }

        if (invocation is IChatMessage chatMessage && !string.IsNullOrWhiteSpace(chatMessage.Channel.ChannelId))
        {
            return chatMessage.Channel.ChannelId;
        }

        if (this._workspaceStore is not null)
        {
            WorkspacePlatform? platform = await this._workspaceStore
                .GetPlatformAsync(invocation.WorkspaceId, invocation.PlatformId, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(platform?.PlatformChannelId))
            {
                return platform.PlatformChannelId;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Sends a localized insufficient-points error notice to the invoking user when <see cref="IOutboundChatDispatcher"/> is available.
    /// </summary>
    /// <param name="invocation">The inbound command invocation.</param>
    /// <param name="requiredPoints">The required loyalty points cost.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    private async ValueTask SendInsufficientPointsNoticeAsync(
        ICommandInvocation invocation,
        long requiredPoints,
        CancellationToken cancellationToken)
    {
        if (this._outboundDispatcher is null)
        {
            return;
        }

        string formatted = await CommandResponseFormatter.FormatCoreResponseAsync(
            this._localizer,
            invocation.WorkspaceId,
            "insufficientPoints",
            _fallbackInsufficientPoints,
            invocation.Sender.DisplayName,
            [requiredPoints.ToString(CultureInfo.InvariantCulture)],
            cancellationToken).ConfigureAwait(false);

        OutboundChatMessage outbound;
        if (invocation is IWhisperCommand)
        {
            outbound = new OutboundChatMessage(
                invocation.WorkspaceId,
                invocation.PlatformId,
                string.Empty,
                formatted,
                OutboundChatPriority.CommandReply,
                OutboundTargetScope.OriginPlatform,
                DuplicateMessagePolicy.PlatformDefault,
                ReplyToMessageId: null,
                IsWhisper: true,
                TargetPlatformUserId: invocation.Sender.PlatformUserId);
        }
        else
        {
            string channelId = (invocation as IChatMessage)?.Channel.ChannelId ?? string.Empty;
            outbound = new OutboundChatMessage(
                invocation.WorkspaceId,
                invocation.PlatformId,
                channelId,
                formatted,
                OutboundChatPriority.CommandReply,
                OutboundTargetScope.OriginPlatform,
                DuplicateMessagePolicy.PlatformDefault,
                ReplyToMessageId: (invocation as IChatMessage)?.MessageId);
        }

        await this._outboundDispatcher.EnqueueAsync(outbound, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves <see cref="IPermissionService"/> either from the direct field or by creating an <see cref="AsyncServiceScope"/>.
    /// </summary>
    /// <param name="ownedScope">Outputs any created <see cref="AsyncServiceScope"/> that the caller must dispose.</param>
    /// <returns>The resolved <see cref="IPermissionService"/>, or <see langword="null"/> if unavailable.</returns>
    private IPermissionService? ResolvePermissionService(ref AsyncServiceScope? ownedScope)
    {
        if (this._permissionService is not null)
        {
            return this._permissionService;
        }

        if (this._scopeFactory is not null)
        {
            ownedScope = this._scopeFactory.CreateAsyncScope();
            return ownedScope.Value.ServiceProvider.GetService<IPermissionService>();
        }

        return this._serviceProvider?.GetService(typeof(IPermissionService)) as IPermissionService;
    }

    /// <summary>
    /// Extracts nested <c>{botcmd}</c> chain depth and visited triggers from <paramref name="invocation"/> metadata and any active <see cref="CommandChainFrame"/>.
    /// </summary>
    /// <param name="invocation">The command invocation.</param>
    /// <param name="activeFrame">The active async chain frame, if any.</param>
    /// <returns>The effective chain depth and visited triggers set.</returns>
    private static (int ChainDepth, HashSet<string> VisitedTriggers) ExtractChainContext(
        ICommandInvocation invocation,
        CommandChainFrame? activeFrame)
    {
        int metadataDepth = 0;
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);

        if (invocation is IChatMessage chatMessage)
        {
            if (chatMessage.Metadata.TryGetValue(BotCmdTagHandler.ChainDepthMetadataKey, out string? depthText)
                && int.TryParse(depthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDepth)
                && parsedDepth >= 0)
            {
                metadataDepth = parsedDepth;
            }

            if (chatMessage.Metadata.TryGetValue(BotCmdTagHandler.VisitedTriggersMetadataKey, out string? visitedText)
                && !string.IsNullOrWhiteSpace(visitedText))
            {
                foreach (string token in visitedText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    string normalized = BuiltInCommandRegistry.NormalizeTrigger(token);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        _ = visited.Add(normalized);
                    }
                }
            }
        }

        int effectiveDepth = activeFrame is not null
            ? Math.Max(activeFrame.Depth + 1, metadataDepth)
            : metadataDepth;

        return (effectiveDepth, visited);
    }

    /// <summary>
    /// Removes the first whitespace-delimited (or quoted) token from <paramref name="rawArguments"/> while preserving the remainder verbatim.
    /// </summary>
    /// <param name="rawArguments">The raw argument string.</param>
    /// <returns>The remaining argument text after the first token.</returns>
    internal static string StripFirstToken(string? rawArguments)
    {
        if (string.IsNullOrWhiteSpace(rawArguments))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = rawArguments.AsSpan().TrimStart();
        if (span.IsEmpty)
        {
            return string.Empty;
        }

        int index = 0;
        if (span[0] == '"')
        {
            index = 1;
            while (index < span.Length)
            {
                if (span[index] == '\\' && (index + 1) < span.Length)
                {
                    index += 2;
                    continue;
                }

                if (span[index] == '"')
                {
                    index++;
                    break;
                }

                index++;
            }
        }
        else
        {
            while (index < span.Length && !char.IsWhiteSpace(span[index]))
            {
                index++;
            }
        }

        return index >= span.Length
            ? string.Empty
            : span[index..].TrimStart().ToString();
    }

    /// <summary>
    /// Logs an unexpected failure during built-in command execution.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="commandId">The canonical command identifier.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="exception">The exception thrown during execution.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Built-in command {CommandId} failed in workspace {WorkspaceId}.")]
    private static partial void LogBuiltInExecutionFailed(ILogger logger, string commandId, Guid workspaceId, Exception exception);

    /// <summary>
    /// Logs an unexpected failure during custom command execution.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="trigger">The normalized primary trigger.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="exception">The exception thrown during execution.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Custom command {Trigger} failed in workspace {WorkspaceId}.")]
    private static partial void LogCustomCommandExecutionFailed(ILogger logger, string trigger, Guid workspaceId, Exception exception);

    /// <summary>
    /// Captures the active <c>{botcmd}</c> chain depth and call-stack triggers in <see cref="_activeChainFrame"/>.
    /// </summary>
    /// <param name="Depth">The current 0-based chain depth.</param>
    /// <param name="ActiveStack">The set of normalized triggers currently executing on the call stack.</param>
    private sealed record CommandChainFrame(int Depth, HashSet<string> ActiveStack);
}
