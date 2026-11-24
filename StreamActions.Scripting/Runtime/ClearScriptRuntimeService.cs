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
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Actions;
using StreamActions.Core.Commands;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Permissions;
using StreamActions.Core.Scripting;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Scripting.Runtime;

/// <summary>
/// Manages per-workspace ClearScript V8 environments, host kill-switch and Tier 1 permission enforcement,
/// and automatic stream-session / idle / persistent resident script unloading lifecycles.
/// </summary>
public sealed partial class ClearScriptRuntimeService : BackgroundService, IRunJsActionExecutor
{
    /// <summary>
    /// Feature-check actor identity for workspace-level ClearScript permissions.
    /// </summary>
    private static readonly PlatformUserIdentity _workspaceFeatureActor = new(
        "core",
        "workspace-scripting",
        "workspace-scripting",
        PlatformUserDesignations.Broadcaster);

    private readonly IWorkspaceScriptStore _scriptStore;
    private readonly IWorkspaceKeyValueStore _keyValueStore;
    private readonly IRuntimeExecutionTracker _executionTracker;
    private readonly IOptions<BootstrapOptions> _bootstrapOptions;
    private readonly IStreamStatusProvider? _streamStatusProvider;
    private readonly IInstanceSettingsService? _instanceSettingsService;
    private readonly ISafeExternalHttpClient? _safeExternalHttpClient;
    private readonly IOutboundChatDispatcher? _outboundChatDispatcher;
    private readonly ITwitchSessionLeaseProvider? _twitchSessionProvider;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IPermissionService? _permissionServiceOverride;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ClearScriptRuntimeService>? _logger;

    private readonly ConcurrentDictionary<Guid, WorkspaceScriptEnvironment> _environments = new();
    private readonly ConcurrentDictionary<Guid, WorkspaceStreamTracker> _streamTrackers = new();
    private readonly SemaphoreSlim _processConcurrencyCap = new(16, 16);

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearScriptRuntimeService"/> class for dependency injection.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public ClearScriptRuntimeService(
        IWorkspaceScriptStore scriptStore,
        IWorkspaceKeyValueStore keyValueStore,
        IRuntimeExecutionTracker executionTracker,
        IOptions<BootstrapOptions> bootstrapOptions,
        IStreamStatusProvider? streamStatusProvider = null,
        IInstanceSettingsService? instanceSettingsService = null,
        ISafeExternalHttpClient? safeExternalHttpClient = null,
        IOutboundChatDispatcher? outboundChatDispatcher = null,
        ITwitchSessionLeaseProvider? twitchSessionProvider = null,
        IServiceScopeFactory? scopeFactory = null,
        TimeProvider? timeProvider = null,
        ILogger<ClearScriptRuntimeService>? logger = null)
        : this(
            scriptStore,
            keyValueStore,
            executionTracker,
            bootstrapOptions,
            permissionServiceOverride: null,
            streamStatusProvider,
            instanceSettingsService,
            safeExternalHttpClient,
            outboundChatDispatcher,
            twitchSessionProvider,
            scopeFactory,
            timeProvider,
            logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearScriptRuntimeService"/> class with an explicit <see cref="IPermissionService"/> override for unit testing.
    /// </summary>
    internal ClearScriptRuntimeService(
        IWorkspaceScriptStore scriptStore,
        IWorkspaceKeyValueStore keyValueStore,
        IRuntimeExecutionTracker executionTracker,
        IOptions<BootstrapOptions> bootstrapOptions,
        IPermissionService? permissionServiceOverride,
        IStreamStatusProvider? streamStatusProvider = null,
        IInstanceSettingsService? instanceSettingsService = null,
        ISafeExternalHttpClient? safeExternalHttpClient = null,
        IOutboundChatDispatcher? outboundChatDispatcher = null,
        ITwitchSessionLeaseProvider? twitchSessionProvider = null,
        IServiceScopeFactory? scopeFactory = null,
        TimeProvider? timeProvider = null,
        ILogger<ClearScriptRuntimeService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scriptStore);
        ArgumentNullException.ThrowIfNull(keyValueStore);
        ArgumentNullException.ThrowIfNull(executionTracker);
        ArgumentNullException.ThrowIfNull(bootstrapOptions);

        this._scriptStore = scriptStore;
        this._keyValueStore = keyValueStore;
        this._executionTracker = executionTracker;
        this._bootstrapOptions = bootstrapOptions;
        this._permissionServiceOverride = permissionServiceOverride;
        this._streamStatusProvider = streamStatusProvider;
        this._instanceSettingsService = instanceSettingsService;
        this._safeExternalHttpClient = safeExternalHttpClient;
        this._outboundChatDispatcher = outboundChatDispatcher;
        this._twitchSessionProvider = twitchSessionProvider;
        this._scopeFactory = scopeFactory;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<ScriptExecutionResult> ExecuteScriptAsync(
        ScriptExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (this.IsClearScriptHostDisabled())
        {
            return new ScriptExecutionResult(false, ErrorMessage: "ClearScript execution is disabled by host security configuration (Security:DisableClearScript).");
        }

        AsyncServiceScope? ownedScope = this._scopeFactory?.CreateAsyncScope();
        try
        {
            IPermissionService? permissionService = this._permissionServiceOverride
                ?? ownedScope?.ServiceProvider.GetService<IPermissionService>();

            if (!await IsWorkspaceScriptingAllowedAsync(request.WorkspaceId, permissionService, cancellationToken).ConfigureAwait(false))
            {
                return new ScriptExecutionResult(false, ErrorMessage: "Workspace does not have permission 'feature.scripting.clearscript'.");
            }

            WorkspaceScript? script = await this._scriptStore
                .GetByIdAsync(request.WorkspaceId, request.ScriptId, cancellationToken)
                .ConfigureAwait(false);

            if (script is null || !script.IsEnabled)
            {
                return new ScriptExecutionResult(false, ErrorMessage: "Requested script was not found or is disabled.");
            }

            ScriptLifecycleMode lifecycleMode = await this.ResolveLifecycleModeAsync(
                request.WorkspaceId,
                script,
                permissionService,
                cancellationToken).ConfigureAwait(false);

            WorkspaceScriptEnvironment environment = await this.GetOrCreateEnvironmentAsync(
                request.WorkspaceId,
                permissionService,
                cancellationToken).ConfigureAwait(false);

            await this._processConcurrencyCap.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await environment
                    .ExecuteScriptAsync(script, lifecycleMode, request, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _ = this._processConcurrencyCap.Release();
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

    /// <inheritdoc/>
    public async ValueTask<int> DispatchEventAsync(
        ScriptEventDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (this.IsClearScriptHostDisabled())
        {
            return 0;
        }

        if (string.Equals(request.EventName, "streamOffline", StringComparison.OrdinalIgnoreCase))
        {
            this.RecordStreamStatusChange(request.WorkspaceId, isLive: false);
        }
        else if (string.Equals(request.EventName, "streamOnline", StringComparison.OrdinalIgnoreCase))
        {
            this.RecordStreamStatusChange(request.WorkspaceId, isLive: true);
        }

        AsyncServiceScope? ownedScope = this._scopeFactory?.CreateAsyncScope();
        try
        {
            IPermissionService? permissionService = this._permissionServiceOverride
                ?? ownedScope?.ServiceProvider.GetService<IPermissionService>();

            if (!await IsWorkspaceScriptingAllowedAsync(request.WorkspaceId, permissionService, cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            WorkspaceScriptEnvironment environment = await this.GetOrCreateEnvironmentAsync(
                request.WorkspaceId,
                permissionService,
                cancellationToken).ConfigureAwait(false);

            if (request.TargetScriptId.HasValue)
            {
                WorkspaceScript? targetScript = await this._scriptStore
                    .GetByIdAsync(request.WorkspaceId, request.TargetScriptId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (targetScript is not null && targetScript.IsEnabled)
                {
                    ScriptLifecycleMode mode = await this.ResolveLifecycleModeAsync(
                        request.WorkspaceId,
                        targetScript,
                        permissionService,
                        cancellationToken).ConfigureAwait(false);

                    _ = await environment
                        .EnsureScriptLoadedAsync(targetScript, mode, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                IReadOnlyList<WorkspaceScript> scripts = await this._scriptStore
                    .GetScriptsAsync(request.WorkspaceId, cancellationToken)
                    .ConfigureAwait(false);

                foreach (WorkspaceScript script in scripts)
                {
                    if (!script.IsEnabled || !script.IsPermanentlyLoaded)
                    {
                        continue;
                    }

                    ScriptLifecycleMode mode = await this.ResolveLifecycleModeAsync(
                        request.WorkspaceId,
                        script,
                        permissionService,
                        cancellationToken).ConfigureAwait(false);

                    if (mode == ScriptLifecycleMode.PermanentResident)
                    {
                        _ = await environment
                            .EnsureScriptLoadedAsync(script, mode, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }

            await this._processConcurrencyCap.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await environment.DispatchEventAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = this._processConcurrencyCap.Release();
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

    /// <inheritdoc/>
    public async ValueTask<ScriptExecutionResult> DispatchCommandAsync(
        ICommandInvocation invocation,
        WorkspaceScript script,
        ScriptCommandRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(registration);

        if (this.IsClearScriptHostDisabled())
        {
            return new ScriptExecutionResult(false, ErrorMessage: "ClearScript execution is disabled by host security configuration.");
        }

        AsyncServiceScope? ownedScope = this._scopeFactory?.CreateAsyncScope();
        try
        {
            IPermissionService? permissionService = this._permissionServiceOverride
                ?? ownedScope?.ServiceProvider.GetService<IPermissionService>();

            if (!await IsWorkspaceScriptingAllowedAsync(invocation.WorkspaceId, permissionService, cancellationToken).ConfigureAwait(false))
            {
                return new ScriptExecutionResult(false, ErrorMessage: "Workspace does not have permission 'feature.scripting.clearscript'.");
            }

            ScriptLifecycleMode lifecycleMode = await this.ResolveLifecycleModeAsync(
                invocation.WorkspaceId,
                script,
                permissionService,
                cancellationToken).ConfigureAwait(false);

            WorkspaceScriptEnvironment environment = await this.GetOrCreateEnvironmentAsync(
                invocation.WorkspaceId,
                permissionService,
                cancellationToken).ConfigureAwait(false);

            _ = await environment
                .EnsureScriptLoadedAsync(script, lifecycleMode, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            string normalizedTrigger = BuiltInCommandRegistry.NormalizeTrigger(registration.Trigger);
            Dictionary<string, object?> payload = new(StringComparer.Ordinal)
            {
                ["command"] = normalizedTrigger,
                ["args"] = invocation.Arguments,
                ["rawArgs"] = invocation.RawArguments,
                ["sender"] = invocation.Sender.Username,
                ["senderDisplayName"] = invocation.Sender.DisplayName,
                ["senderId"] = invocation.Sender.PlatformUserId,
                ["platformId"] = invocation.PlatformId,
            };

            string channelId = (invocation as IChatMessage)?.Channel.ChannelId ?? string.Empty;
            int invoked = await environment.DispatchEventAsync(
                new ScriptEventDispatchRequest(
                    invocation.WorkspaceId,
                    "command",
                    payload,
                    invocation.PlatformId,
                    channelId,
                    TargetScriptId: script.Id,
                    ActorIdentity: invocation.Sender.ToIdentity()),
                cancellationToken).ConfigureAwait(false);

            return invoked > 0
                ? new ScriptExecutionResult(true)
                : new ScriptExecutionResult(false, ErrorMessage: $"Script '{script.Path}' did not bind a handler for command '!{normalizedTrigger}'.");
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
    /// Records a stream online/offline transition for automatic Rule 1 post-stream script unloading.
    /// </summary>
    public void RecordStreamStatusChange(Guid workspaceId, bool isLive)
    {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        _ = this._streamTrackers.AddOrUpdate(
            workspaceId,
            _ => new WorkspaceStreamTracker(isLive, isLive ? null : now),
            (_, existing) => isLive
                ? new WorkspaceStreamTracker(true, null)
                : existing.LastKnownLive || existing.WentOfflineAtUtc is null
                    ? new WorkspaceStreamTracker(false, now)
                    : existing);
    }

    /// <summary>
    /// Preloads all permanently resident scripts (<see cref="WorkspaceScript.IsPermanentlyLoaded"/>) whose workspaces hold <c>feature.scripting.clearscript</c> and <c>feature.scripting.clearscript.persistent</c>.
    /// </summary>
    public async ValueTask PreloadPermanentScriptsAsync(CancellationToken cancellationToken = default)
    {
        if (this.IsClearScriptHostDisabled())
        {
            return;
        }

        IReadOnlyList<WorkspaceScript> permanentScripts = await this._scriptStore
            .GetPermanentlyLoadedScriptsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (permanentScripts.Count == 0)
        {
            return;
        }

        AsyncServiceScope? ownedScope = this._scopeFactory?.CreateAsyncScope();
        try
        {
            IPermissionService? permissionService = this._permissionServiceOverride
                ?? ownedScope?.ServiceProvider.GetService<IPermissionService>();

            foreach (WorkspaceScript script in permanentScripts)
            {
                if (!script.IsEnabled || !script.IsPermanentlyLoaded)
                {
                    continue;
                }

                if (!await IsWorkspaceScriptingAllowedAsync(script.WorkspaceId, permissionService, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                ScriptLifecycleMode mode = await this.ResolveLifecycleModeAsync(
                    script.WorkspaceId,
                    script,
                    permissionService,
                    cancellationToken).ConfigureAwait(false);

                if (mode != ScriptLifecycleMode.PermanentResident)
                {
                    continue;
                }

                WorkspaceScriptEnvironment env = await this.GetOrCreateEnvironmentAsync(
                    script.WorkspaceId,
                    permissionService,
                    cancellationToken).ConfigureAwait(false);

                _ = await env
                    .EnsureScriptLoadedAsync(script, mode, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
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
    /// Evaluates Rule 1 (post-stream grace period unload) and Rule 2 (off-stream idle TTL unload) across all loaded workspace environments.
    /// </summary>
    public async ValueTask<int> EvaluateLifecycleSweepAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset nowUtc = this._timeProvider.GetUtcNow();
        int graceMinutes = 30;
        int idleMinutes = 3;

        if (this._instanceSettingsService is not null)
        {
            InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            if (settings.PostStreamGracePeriodMinutes >= 0)
            {
                graceMinutes = settings.PostStreamGracePeriodMinutes;
            }

            if (settings.OffStreamScriptIdleTimeoutMinutes > 0)
            {
                idleMinutes = settings.OffStreamScriptIdleTimeoutMinutes;
            }
        }

        TimeSpan postStreamGrace = TimeSpan.FromMinutes(graceMinutes);
        TimeSpan offStreamIdle = TimeSpan.FromMinutes(idleMinutes);
        int unloadedCount = 0;

        foreach (KeyValuePair<Guid, WorkspaceScriptEnvironment> entry in this._environments)
        {
            Guid workspaceId = entry.Key;
            WorkspaceScriptEnvironment env = entry.Value;
            IReadOnlyList<ScriptIsolateInstance> isolates = env.GetLoadedIsolates();
            if (isolates.Count == 0)
            {
                continue;
            }

            bool isLive = await this.RefreshStreamStatusAsync(workspaceId, nowUtc, cancellationToken).ConfigureAwait(false);
            if (isLive)
            {
                continue;
            }

            bool withinGrace = this._streamTrackers.TryGetValue(workspaceId, out WorkspaceStreamTracker? tracker)
                && tracker.WentOfflineAtUtc.HasValue
                && (nowUtc - tracker.WentOfflineAtUtc.Value) <= postStreamGrace;

            foreach (ScriptIsolateInstance isolate in isolates)
            {
                if (isolate.LifecycleMode == ScriptLifecycleMode.PermanentResident)
                {
                    continue;
                }

                if (isolate.LifecycleMode == ScriptLifecycleMode.StreamSession && !withinGrace)
                {
                    await env.UnloadScriptAsync(isolate.ScriptId).ConfigureAwait(false);
                    unloadedCount++;
                    continue;
                }

                if (isolate.LifecycleMode == ScriptLifecycleMode.EventOnDemand
                    && !withinGrace
                    && (nowUtc - isolate.LastActivityAtUtc) >= offStreamIdle)
                {
                    await env.UnloadScriptAsync(isolate.ScriptId).ConfigureAwait(false);
                    unloadedCount++;
                }
            }
        }

        return unloadedCount;
    }

    /// <summary>
    /// Logs a warning when permanently resident ClearScript preload fails during host startup.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to preload permanently resident ClearScript scripts on startup.")]
    private static partial void LogPreloadWarning(ILogger logger, Exception exception);

    /// <summary>
    /// Logs an unexpected failure during the ClearScript lifecycle unload sweep.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Error during ClearScript lifecycle unload sweep.")]
    private static partial void LogSweepError(ILogger logger, Exception exception);

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background script lifecycle sweep must survive transient exceptions.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await this.PreloadPermanentScriptsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            if (this._logger is not null)
            {
                LogPreloadWarning(this._logger, ex);
            }
        }

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30), this._timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                _ = await this.EvaluateLifecycleSweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (this._logger is not null)
                {
                    LogSweepError(this._logger, ex);
                }
            }
        }
    }

    /// <inheritdoc/>
    public override async void Dispose()
    {
        foreach (WorkspaceScriptEnvironment env in this._environments.Values)
        {
            await env.DisposeAsync().ConfigureAwait(false);
        }

        this._processConcurrencyCap.Dispose();
        base.Dispose();
    }

    private bool IsClearScriptHostDisabled() => this._bootstrapOptions.Value.Security.DisableClearScript;

    private static async ValueTask<bool> IsWorkspaceScriptingAllowedAsync(
        Guid workspaceId,
        IPermissionService? permissionService,
        CancellationToken cancellationToken) =>
        permissionService is null
        || await permissionService
            .IsAllowedAsync(workspaceId, _workspaceFeatureActor, BuiltInPermissionKeys.FeatureScriptingClearScript, cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<ScriptLifecycleMode> ResolveLifecycleModeAsync(
        Guid workspaceId,
        WorkspaceScript script,
        IPermissionService? permissionService,
        CancellationToken cancellationToken)
    {
        if (script.IsPermanentlyLoaded && permissionService is not null)
        {
            bool persistentAllowed = await permissionService
                .IsAllowedAsync(workspaceId, _workspaceFeatureActor, BuiltInPermissionKeys.FeatureScriptingClearScriptPersistent, cancellationToken)
                .ConfigureAwait(false);

            if (persistentAllowed)
            {
                return ScriptLifecycleMode.PermanentResident;
            }
        }
        else if (script.IsPermanentlyLoaded && permissionService is null)
        {
            return ScriptLifecycleMode.PermanentResident;
        }

        bool isLive = await this.RefreshStreamStatusAsync(workspaceId, this._timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return isLive ? ScriptLifecycleMode.StreamSession : ScriptLifecycleMode.EventOnDemand;
    }

    private async ValueTask<bool> RefreshStreamStatusAsync(Guid workspaceId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        bool isLive = this._streamStatusProvider is not null
            && await this._streamStatusProvider.IsStreamLiveAsync(workspaceId, platformId: null, cancellationToken).ConfigureAwait(false);

        if (isLive)
        {
            this._streamTrackers[workspaceId] = new WorkspaceStreamTracker(true, null);
            return true;
        }

        _ = this._streamTrackers.AddOrUpdate(
            workspaceId,
            _ => new WorkspaceStreamTracker(false, null),
            (_, existing) => existing.LastKnownLive
                ? new WorkspaceStreamTracker(false, nowUtc)
                : existing);

        return false;
    }

    private async ValueTask<WorkspaceScriptEnvironment> GetOrCreateEnvironmentAsync(
        Guid workspaceId,
        IPermissionService? permissionService,
        CancellationToken cancellationToken)
    {
        WorkspaceResourceQuotas quotas = permissionService is not null
            ? await permissionService.GetEffectiveResourceQuotasAsync(workspaceId, cancellationToken).ConfigureAwait(false)
            : WorkspaceResourceQuotas.DefaultBaseline;

        int timeoutSeconds = 2;
        int maxConcurrency = 4;
        if (this._instanceSettingsService is not null)
        {
            InstanceSettings settings = await this._instanceSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
            if (settings.ClearScriptExecutionTimeoutSeconds > 0)
            {
                timeoutSeconds = settings.ClearScriptExecutionTimeoutSeconds;
            }

            if (settings.ClearScriptMaxConcurrencyPerWorkspace > 0)
            {
                maxConcurrency = settings.ClearScriptMaxConcurrencyPerWorkspace;
            }
        }

        long ramQuotaBytes = quotas.EffectiveMaxClearScriptTotalRamBytes;
        TimeSpan execTimeout = TimeSpan.FromSeconds(timeoutSeconds);

        if (this._environments.TryGetValue(workspaceId, out WorkspaceScriptEnvironment? existing))
        {
            existing.UpdateLimits(ramQuotaBytes, execTimeout);
            return existing;
        }

        WorkspaceScriptEnvironment created = new(
            workspaceId,
            this._scriptStore,
            this._keyValueStore,
            this._executionTracker,
            ramQuotaBytes,
            execTimeout,
            maxConcurrency,
            this._safeExternalHttpClient,
            this._outboundChatDispatcher,
            this._twitchSessionProvider,
            this._timeProvider,
            this._logger);

        if (this._environments.TryAdd(workspaceId, created))
        {
            return created;
        }

        await created.DisposeAsync().ConfigureAwait(false);
        WorkspaceScriptEnvironment winner = this._environments[workspaceId];
        winner.UpdateLimits(ramQuotaBytes, execTimeout);
        return winner;
    }

    private sealed record WorkspaceStreamTracker(bool LastKnownLive, DateTimeOffset? WentOfflineAtUtc);
}

