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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Templates;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StreamActions.Core.Actions;

/// <summary>
/// Represents an incoming trigger event evaluated against enabled <see cref="WorkspaceActionBlockProfile"/> definitions.
/// </summary>
/// <param name="WorkspaceId">Target workspace identifier.</param>
/// <param name="TriggerId">Trigger descriptor identifier (for example, <see cref="BuiltInTriggerIds.Subscription"/>).</param>
/// <param name="PlatformId">Originating platform identifier.</param>
/// <param name="ChannelId">Originating channel identifier.</param>
/// <param name="ChannelName">Originating channel display name.</param>
/// <param name="ActorIdentity">Optional actor identity associated with the event.</param>
/// <param name="Message">Optional inbound message or command invocation.</param>
/// <param name="Payload">Output parameter values exposed by the trigger.</param>
/// <param name="CommandTrigger">Normalized command trigger when <paramref name="TriggerId"/> is <see cref="BuiltInTriggerIds.ChatCommand"/>.</param>
/// <param name="MessageText">Message text evaluated against keyword patterns when <paramref name="TriggerId"/> is <see cref="BuiltInTriggerIds.ChatKeyword"/>.</param>
public sealed record ActionBlockTriggerEvent(
    Guid WorkspaceId,
    string TriggerId,
    string PlatformId,
    string ChannelId,
    string ChannelName,
    PlatformUserIdentity? ActorIdentity,
    IMessageEnvelope? Message,
    IReadOnlyDictionary<string, string> Payload,
    string? CommandTrigger = null,
    string? MessageText = null);

/// <summary>
/// Dispatches inbound events and scheduled Timer, Cron, and One-Shot ticks to matching <see cref="WorkspaceActionBlockProfile"/> instances.
/// </summary>
public interface IActionBlockTriggerDispatcher
{
    /// <summary>
    /// Evaluates all matching enabled profiles for <paramref name="triggerEvent"/> and runs their attached tasks.
    /// </summary>
    /// <param name="triggerEvent">The inbound trigger event.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The execution traces produced for each matched profile whose predicates succeeded.</returns>
    ValueTask<IReadOnlyList<ActionExecutionTrace>> DispatchEventTriggerAsync(
        ActionBlockTriggerEvent triggerEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a stream online/offline transition for enforcing the 30-minute post-stream grace period on scheduled profiles.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="isLive">Whether the stream is now live.</param>
    void RecordStreamStatusChange(Guid workspaceId, bool isLive);

    /// <summary>
    /// Evaluates all enabled Timer, Cron, and One-Shot profiles at the current <see cref="TimeProvider"/> instant and executes due schedules.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of scheduled profiles fired.</returns>
    ValueTask<int> EvaluateDueSchedulesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IActionBlockTriggerDispatcher"/> and hosted background timer coordinator for Action Block profiles.
/// </summary>
public sealed partial class ActionBlockTriggerDispatcher : BackgroundService, IActionBlockTriggerDispatcher
{
    /// <summary>
    /// Regex timeout applied when evaluating <see cref="BuiltInTriggerIds.ChatKeyword"/> patterns.
    /// </summary>
    private static readonly TimeSpan _keywordRegexTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// System actor used when evaluating workspace-level Tier 1 feature gates such as <c>feature.action_blocks.offline_schedule</c>.
    /// </summary>
    private static readonly PlatformUserIdentity _workspaceFeatureActor = new(
        "core",
        "workspace-scheduler",
        "workspace-scheduler",
        PlatformUserDesignations.Broadcaster);

    /// <summary>
    /// Optional Action Block persistence store.
    /// </summary>
    private readonly IActionBlockStore? _actionBlockStore;

    /// <summary>
    /// Action flow execution engine.
    /// </summary>
    private readonly IActionFlowRunner _flowRunner;

    /// <summary>
    /// Stream status provider.
    /// </summary>
    private readonly IStreamStatusProvider? _streamStatusProvider;

    /// <summary>
    /// Tag registry for predicate validation/resolution.
    /// </summary>
    private readonly ITagRegistry _tagRegistry;

    /// <summary>
    /// Optional instance settings service for configurable post-stream grace period.
    /// </summary>
    private readonly IInstanceSettingsService? _instanceSettingsService;

    /// <summary>
    /// Optional DI scope factory for resolving <see cref="IPermissionService"/>.
    /// </summary>
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// Optional direct <see cref="IPermissionService"/> override for unit testing.
    /// </summary>
    private readonly IPermissionService? _permissionServiceOverride;

    /// <summary>
    /// Time provider driving grace windows, interval ticks, and cron evaluation.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Optional logger.
    /// </summary>
    private readonly ILogger<ActionBlockTriggerDispatcher>? _logger;

    /// <summary>
    /// Tracks last observed offline timestamp and previous live state per workspace.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, WorkspaceStreamWindowState> _streamWindows = new();

    /// <summary>
    /// Tracks the last fired UTC timestamp per scheduled profile identifier.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastScheduleFiredUtc = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBlockTriggerDispatcher"/> class.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public ActionBlockTriggerDispatcher(
        IActionFlowRunner flowRunner,
        ITagRegistry tagRegistry,
        IActionBlockStore? actionBlockStore = null,
        IStreamStatusProvider? streamStatusProvider = null,
        IInstanceSettingsService? instanceSettingsService = null,
        IServiceScopeFactory? scopeFactory = null,
        TimeProvider? timeProvider = null,
        ILogger<ActionBlockTriggerDispatcher>? logger = null)
        : this(
            actionBlockStore,
            flowRunner,
            tagRegistry,
            permissionServiceOverride: null,
            streamStatusProvider,
            instanceSettingsService,
            scopeFactory,
            timeProvider,
            logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBlockTriggerDispatcher"/> class with an explicit <see cref="IPermissionService"/> override for unit testing.
    /// </summary>
    internal ActionBlockTriggerDispatcher(
        IActionBlockStore? actionBlockStore,
        IActionFlowRunner flowRunner,
        ITagRegistry tagRegistry,
        IPermissionService? permissionServiceOverride,
        IStreamStatusProvider? streamStatusProvider = null,
        IInstanceSettingsService? instanceSettingsService = null,
        IServiceScopeFactory? scopeFactory = null,
        TimeProvider? timeProvider = null,
        ILogger<ActionBlockTriggerDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(flowRunner);
        ArgumentNullException.ThrowIfNull(tagRegistry);

        this._actionBlockStore = actionBlockStore;
        this._flowRunner = flowRunner;
        this._tagRegistry = tagRegistry;
        this._permissionServiceOverride = permissionServiceOverride;
        this._streamStatusProvider = streamStatusProvider;
        this._instanceSettingsService = instanceSettingsService;
        this._scopeFactory = scopeFactory;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ActionExecutionTrace>> DispatchEventTriggerAsync(
        ActionBlockTriggerEvent triggerEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triggerEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerEvent.TriggerId);

        if (string.Equals(triggerEvent.TriggerId, BuiltInTriggerIds.StreamOnline, StringComparison.OrdinalIgnoreCase))
        {
            this.RecordStreamStatusChange(triggerEvent.WorkspaceId, isLive: true);
        }
        else if (string.Equals(triggerEvent.TriggerId, BuiltInTriggerIds.StreamOffline, StringComparison.OrdinalIgnoreCase))
        {
            this.RecordStreamStatusChange(triggerEvent.WorkspaceId, isLive: false);
        }

        if (this._actionBlockStore is null)
        {
            return [];
        }

        IReadOnlyList<WorkspaceActionBlockProfile> profiles = await this._actionBlockStore
            .GetProfilesAsync(triggerEvent.WorkspaceId, cancellationToken)
            .ConfigureAwait(false);

        List<ActionExecutionTrace> traces = [];
        foreach (WorkspaceActionBlockProfile profile in profiles)
        {
            if (!profile.IsEnabled
                || profile.AttachedTaskId == Guid.Empty
                || !string.Equals(profile.Trigger.TriggerId, triggerEvent.TriggerId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(profile.Trigger.PlatformId)
                && !string.Equals(profile.Trigger.PlatformId.Trim(), triggerEvent.PlatformId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(triggerEvent.TriggerId, BuiltInTriggerIds.ChatCommand, StringComparison.OrdinalIgnoreCase))
            {
                string expectedCmd = BuiltInCommandRegistry.NormalizeTrigger(profile.Trigger.CommandTrigger);
                string actualCmd = BuiltInCommandRegistry.NormalizeTrigger(triggerEvent.CommandTrigger);
                if (string.IsNullOrEmpty(expectedCmd) || !string.Equals(expectedCmd, actualCmd, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (string.Equals(triggerEvent.TriggerId, BuiltInTriggerIds.ChatKeyword, StringComparison.OrdinalIgnoreCase)
                && !MatchesKeyword(profile.Trigger.KeywordPattern, triggerEvent.MessageText))
            {
                continue;
            }

            bool predicatesHold = await this.EvaluateProfilePredicatesAsync(
                profile,
                triggerEvent,
                cancellationToken).ConfigureAwait(false);

            if (!predicatesHold)
            {
                continue;
            }

            Dictionary<string, string> initialVars = await this.BuildInitialVariablesAsync(
                profile,
                triggerEvent,
                cancellationToken).ConfigureAwait(false);

            ActionExecutionRequest request = new()
            {
                WorkspaceId = triggerEvent.WorkspaceId,
                TaskId = profile.AttachedTaskId,
                ProfileId = profile.Id,
                ProfileName = profile.Name,
                TriggerId = triggerEvent.TriggerId,
                PlatformId = triggerEvent.PlatformId,
                ChannelId = triggerEvent.ChannelId,
                ChannelName = triggerEvent.ChannelName,
                ActorIdentity = triggerEvent.ActorIdentity,
                Message = triggerEvent.Message,
                InitialVariables = initialVars,
                DryRun = false,
            };

            ActionExecutionTrace trace = await this._flowRunner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            traces.Add(trace);
        }

        return traces;
    }

    /// <inheritdoc/>
    public void RecordStreamStatusChange(Guid workspaceId, bool isLive)
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        _ = this._streamWindows.AddOrUpdate(
            workspaceId,
            _ => new WorkspaceStreamWindowState(isLive, isLive ? null : now),
            (_, existing) => isLive
                ? new WorkspaceStreamWindowState(true, null)
                : existing.LastKnownLive || existing.WentOfflineAtUtc is null
                    ? new WorkspaceStreamWindowState(false, now)
                    : existing);
    }

    /// <inheritdoc/>
    public async ValueTask<int> EvaluateDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        if (this._actionBlockStore is null)
        {
            return 0;
        }

        DateTimeOffset nowUtc = this._timeProvider.GetUtcNow();
        IReadOnlyList<WorkspaceActionBlockProfile> allProfiles = await this._actionBlockStore
            .GetAllProfilesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (allProfiles.Count == 0)
        {
            return 0;
        }

        int graceMinutes = 30;
        if (this._instanceSettingsService is not null)
        {
            InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            if (settings.PostStreamGracePeriodMinutes >= 0)
            {
                graceMinutes = settings.PostStreamGracePeriodMinutes;
            }
        }

        TimeSpan graceWindow = TimeSpan.FromMinutes(graceMinutes);
        int firedCount = 0;

        AsyncServiceScope? ownedScope = this._scopeFactory?.CreateAsyncScope();

        try
        {
            IPermissionService? permissionService = this._permissionServiceOverride
                ?? ownedScope?.ServiceProvider.GetService<IPermissionService>();

            foreach (WorkspaceActionBlockProfile profile in allProfiles)
            {
                bool isScheduledTrigger = string.Equals(profile.Trigger.TriggerId, BuiltInTriggerIds.TimerOrCron, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(profile.Trigger.TriggerId, BuiltInTriggerIds.OneShotSchedule, StringComparison.OrdinalIgnoreCase);

                if (!profile.IsEnabled
                    || profile.AttachedTaskId == Guid.Empty
                    || !isScheduledTrigger)
                {
                    continue;
                }

                bool isDue = this.IsScheduleDue(profile, nowUtc);
                if (!isDue)
                {
                    continue;
                }

                bool isStreamEligible = await this.IsWithinStreamOrAuthorizedOffStreamAsync(
                    profile.WorkspaceId,
                    nowUtc,
                    graceWindow,
                    permissionService,
                    cancellationToken).ConfigureAwait(false);

                if (!isStreamEligible)
                {
                    continue;
                }

                string? gameCategory = this._streamStatusProvider is not null
                    ? await this._streamStatusProvider.GetCurrentGameCategoryAsync(profile.WorkspaceId, profile.Trigger.PlatformId, cancellationToken).ConfigureAwait(false)
                    : null;
                bool isLive = await this.RefreshAndCheckLiveAsync(profile.WorkspaceId, nowUtc, cancellationToken).ConfigureAwait(false);

                Dictionary<string, string> payload = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["firedAtUtc"] = nowUtc.ToString("O", CultureInfo.InvariantCulture),
                    ["isStreamLive"] = isLive ? "true" : "false",
                    ["gameCategory"] = gameCategory ?? string.Empty,
                };

                if (profile.Trigger.ScheduledAtUtc.HasValue)
                {
                    payload["scheduledAtUtc"] = profile.Trigger.ScheduledAtUtc.Value.ToString("O", CultureInfo.InvariantCulture);
                }

                ActionBlockTriggerEvent triggerEvent = new(
                    profile.WorkspaceId,
                    profile.Trigger.TriggerId,
                    profile.Trigger.PlatformId ?? "twitch",
                    ChannelId: string.Empty,
                    ChannelName: string.Empty,
                    ActorIdentity: _workspaceFeatureActor,
                    Message: null,
                    Payload: payload);

                if (!await this.EvaluateProfilePredicatesAsync(profile, triggerEvent, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                this._lastScheduleFiredUtc[profile.Id] = nowUtc;

                bool isOneShot = profile.Trigger.ScheduleMode == ProfileTriggerScheduleMode.OneShot
                    || string.Equals(profile.Trigger.TriggerId, BuiltInTriggerIds.OneShotSchedule, StringComparison.OrdinalIgnoreCase);

                if (isOneShot)
                {
                    profile.Trigger.OneShotFiredAtUtc = nowUtc;
                    profile.IsEnabled = false;
                    _ = await this._actionBlockStore.UpsertProfileAsync(profile, cancellationToken).ConfigureAwait(false);
                }

                Dictionary<string, string> initialVars = await this.BuildInitialVariablesAsync(
                    profile,
                    triggerEvent,
                    cancellationToken).ConfigureAwait(false);

                _ = await this._flowRunner.ExecuteAsync(
                    new ActionExecutionRequest
                    {
                        WorkspaceId = profile.WorkspaceId,
                        TaskId = profile.AttachedTaskId,
                        ProfileId = profile.Id,
                        ProfileName = profile.Name,
                        TriggerId = profile.Trigger.TriggerId,
                        PlatformId = profile.Trigger.PlatformId ?? "twitch",
                        ActorIdentity = _workspaceFeatureActor,
                        InitialVariables = initialVars,
                    },
                    cancellationToken).ConfigureAwait(false);

                firedCount++;
            }
        }
        finally
        {
            if (ownedScope.HasValue)
            {
                await ownedScope.Value.DisposeAsync().ConfigureAwait(false);
            }
        }

        return firedCount;
    }

    /// <summary>
    /// Logs an unexpected failure while evaluating scheduled Action Block profiles.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Error evaluating Action Block scheduled profiles.")]
    private static partial void LogScheduleEvaluationError(ILogger logger, Exception exception);

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background schedule loop must log and survive transient failures across ticks.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(15), this._timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                _ = await this.EvaluateDueSchedulesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (this._logger is not null)
                {
                    LogScheduleEvaluationError(this._logger, ex);
                }
            }
        }
    }

    /// <summary>
    /// Checks whether a scheduled profile is due at <paramref name="nowUtc"/> without mutating fire history.
    /// </summary>
    private bool IsScheduleDue(WorkspaceActionBlockProfile profile, DateTimeOffset nowUtc)
    {
        DateTimeOffset? lastFired = this._lastScheduleFiredUtc.TryGetValue(profile.Id, out DateTimeOffset recorded)
            ? recorded
            : null;

        return profile.Trigger.ScheduleMode == ProfileTriggerScheduleMode.OneShot
            || (string.Equals(profile.Trigger.TriggerId, BuiltInTriggerIds.OneShotSchedule, StringComparison.OrdinalIgnoreCase)
                && profile.Trigger.ScheduledAtUtc.HasValue)
            ? profile.Trigger.ScheduledAtUtc.HasValue
                && !profile.Trigger.OneShotFiredAtUtc.HasValue
                && !lastFired.HasValue
                && nowUtc >= profile.Trigger.ScheduledAtUtc.Value
            : profile.Trigger.ScheduleMode == ProfileTriggerScheduleMode.Interval
            ? profile.Trigger.IntervalSeconds > 0 && (!lastFired.HasValue || (nowUtc - lastFired.Value) >= TimeSpan.FromSeconds(profile.Trigger.IntervalSeconds))
            : profile.Trigger.ScheduleMode == ProfileTriggerScheduleMode.Cron && CronScheduleExpression.TryParse(profile.Trigger.CronExpression, out CronScheduleExpression? cron)
                && cron.Matches(nowUtc)
                && (!lastFired.HasValue
                    || lastFired.Value.UtcDateTime.Year != nowUtc.UtcDateTime.Year
                    || lastFired.Value.UtcDateTime.DayOfYear != nowUtc.UtcDateTime.DayOfYear
                    || lastFired.Value.UtcDateTime.Hour != nowUtc.UtcDateTime.Hour
                    || lastFired.Value.UtcDateTime.Minute != nowUtc.UtcDateTime.Minute);
    }

    /// <summary>
    /// Enforces the live stream + 30-minute post-stream grace period default and <c>feature.action_blocks.offline_schedule</c> gate.
    /// </summary>
    private async ValueTask<bool> IsWithinStreamOrAuthorizedOffStreamAsync(
        Guid workspaceId,
        DateTimeOffset nowUtc,
        TimeSpan graceWindow,
        IPermissionService? permissionService,
        CancellationToken cancellationToken) =>
        await this.RefreshAndCheckLiveAsync(workspaceId, nowUtc, cancellationToken).ConfigureAwait(false)
            || (this._streamWindows.TryGetValue(workspaceId, out WorkspaceStreamWindowState? windowState)
                && windowState.WentOfflineAtUtc.HasValue
                && ((nowUtc - windowState.WentOfflineAtUtc.Value) <= graceWindow))
            || (permissionService is not null
                && await permissionService
                    .IsAllowedAsync(workspaceId, _workspaceFeatureActor, BuiltInPermissionKeys.FeatureActionBlocksOfflineSchedule, cancellationToken)
                    .ConfigureAwait(false));

    /// <summary>
    /// Refreshes live stream state from <see cref="IStreamStatusProvider"/> and updates offline grace tracking.
    /// </summary>
    private async ValueTask<bool> RefreshAndCheckLiveAsync(
        Guid workspaceId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        bool isLive = this._streamStatusProvider is not null
            && await this._streamStatusProvider.IsStreamLiveAsync(workspaceId, platformId: null, cancellationToken).ConfigureAwait(false);

        if (isLive)
        {
            this._streamWindows[workspaceId] = new WorkspaceStreamWindowState(true, null);
            return true;
        }

        _ = this._streamWindows.AddOrUpdate(
            workspaceId,
            _ => new WorkspaceStreamWindowState(false, null),
            (_, existing) => existing.LastKnownLive
                ? new WorkspaceStreamWindowState(false, nowUtc)
                : existing);

        return false;
    }

    /// <summary>
    /// Evaluates <see cref="WorkspaceActionBlockProfile.Predicates"/> against the event payload and live stream status.
    /// </summary>
    private async ValueTask<bool> EvaluateProfilePredicatesAsync(
        WorkspaceActionBlockProfile profile,
        ActionBlockTriggerEvent triggerEvent,
        CancellationToken cancellationToken) => profile.Predicates is null || await ActionBlockConditionEvaluator.EvaluateAsync(
            profile.Predicates,
            (expr, ct) => this.ResolvePredicateExpressionAsync(expr, triggerEvent, ct),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Resolves state dot-paths, trigger payload variables, and <c>{tag}</c> expressions inside profile predicates.
    /// </summary>
    private async ValueTask<string> ResolvePredicateExpressionAsync(
        string expression,
        ActionBlockTriggerEvent triggerEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return string.Empty;
        }

        string trimmed = expression.Trim();
        if (string.Equals(trimmed, "Stream.IsOnline", StringComparison.OrdinalIgnoreCase))
        {
            bool isLive = this._streamStatusProvider is not null
                && await this._streamStatusProvider.IsStreamLiveAsync(triggerEvent.WorkspaceId, triggerEvent.PlatformId, cancellationToken).ConfigureAwait(false);
            return isLive ? "true" : "false";
        }

        if (string.Equals(trimmed, "Stream.Game", StringComparison.OrdinalIgnoreCase))
        {
            string? game = this._streamStatusProvider is not null
                ? await this._streamStatusProvider.GetCurrentGameCategoryAsync(triggerEvent.WorkspaceId, triggerEvent.PlatformId, cancellationToken).ConfigureAwait(false)
                : null;
            return game ?? string.Empty;
        }

        if (string.Equals(trimmed, "User.Designation", StringComparison.OrdinalIgnoreCase))
        {
            return triggerEvent.ActorIdentity?.Designations.ToString() ?? PlatformUserDesignations.None.ToString();
        }

        if (string.Equals(trimmed, "User.Username", StringComparison.OrdinalIgnoreCase))
        {
            return triggerEvent.ActorIdentity?.Username ?? string.Empty;
        }

        if (triggerEvent.Payload.TryGetValue(trimmed, out string? payloadValue))
        {
            return payloadValue;
        }

        if (!expression.Contains('{', StringComparison.Ordinal))
        {
            return expression;
        }

        PlatformUserIdentity actor = triggerEvent.ActorIdentity
            ?? new PlatformUserIdentity(triggerEvent.PlatformId, "system", "system");

        IMessageEnvelope message = triggerEvent.Message ?? new Models.Messaging.ChatMessage(
            triggerEvent.WorkspaceId,
            triggerEvent.PlatformId,
            MessageId: Guid.NewGuid().ToString("N"),
            Sender: new Models.Messaging.ChatUser(actor.PlatformId, actor.PlatformUserId, actor.Username, actor.Username, actor.Designations),
            Channel: new Models.Messaging.ChatChannel(triggerEvent.WorkspaceId, triggerEvent.PlatformId, triggerEvent.ChannelId, triggerEvent.ChannelName),
            RawText: triggerEvent.MessageText ?? string.Empty,
            ReceivedAtUtc: this._timeProvider.GetUtcNow());

        string? currentGame = this._streamStatusProvider is not null
            ? await this._streamStatusProvider.GetCurrentGameCategoryAsync(triggerEvent.WorkspaceId, triggerEvent.PlatformId, cancellationToken).ConfigureAwait(false)
            : null;

        TagEvaluationContext tagContext = new(
            message,
            this._tagRegistry,
            currentGameCategory: currentGame,
            actorIdentity: actor,
            dependencies: new TagEvaluationDependencies
            {
                StreamStatusProvider = this._streamStatusProvider,
                TimeProvider = this._timeProvider,
            },
            timeProvider: this._timeProvider,
            variables: triggerEvent.Payload);

        TemplateParseResult parsed = TemplateParser.Parse(expression, this._tagRegistry, triggerEvent.PlatformId);
        TagResult evaluated = await TemplateEvaluator.EvaluateAsync(parsed.Root, tagContext, cancellationToken: cancellationToken).ConfigureAwait(false);
        return evaluated.Value;
    }

    /// <summary>
    /// Builds initial flow variables combining trigger payload keys and profile <see cref="WorkspaceActionBlockProfile.ParameterMappings"/>.
    /// </summary>
    private async ValueTask<Dictionary<string, string>> BuildInitialVariablesAsync(
        WorkspaceActionBlockProfile profile,
        ActionBlockTriggerEvent triggerEvent,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> kvp in triggerEvent.Payload)
        {
            variables[kvp.Key] = kvp.Value;
        }

        foreach (KeyValuePair<string, string> mapping in profile.ParameterMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.Key))
            {
                continue;
            }

            string resolved = await this.ResolvePredicateExpressionAsync(mapping.Value, triggerEvent, cancellationToken).ConfigureAwait(false);
            variables[mapping.Key.Trim()] = resolved;
        }

        return variables;
    }

    /// <summary>
    /// Checks whether a chat message matches a keyword or <c>regex:</c> pattern.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Invalid keyword regexes must safely fail to match without crashing message dispatch.")]
    private static bool MatchesKeyword(string? pattern, string? messageText)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(messageText))
        {
            return false;
        }

        string trimmed = pattern.Trim();
        if (trimmed.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            string regexPattern = trimmed["regex:".Length..].Trim();
            try
            {
                return Regex.IsMatch(
                    messageText,
                    regexPattern,
                    RegexOptions.NonBacktracking | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    _keywordRegexTimeout);
            }
            catch (Exception)
            {
                return false;
            }
        }

        return messageText.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tracks stream live state and offline timestamp for grace-period enforcement.
    /// </summary>
    /// <param name="LastKnownLive">Whether stream was live on the previous check.</param>
    /// <param name="WentOfflineAtUtc">Timestamp of transition to offline.</param>
    private sealed record WorkspaceStreamWindowState(bool LastKnownLive, DateTimeOffset? WentOfflineAtUtc);
}
