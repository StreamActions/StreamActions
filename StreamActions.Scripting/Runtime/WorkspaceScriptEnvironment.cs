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

using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Actions;
using StreamActions.Core.Scripting;
using StreamActions.Scripting.Proxies;
using StreamActions.Twitch.Api.Common;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;

namespace StreamActions.Scripting.Runtime;

/// <summary>
/// Cross-isolate callable wrapper registered on <c>$</c> by a library or resident script and invoked from any dependent script in the same workspace.
/// </summary>
internal sealed class DollarExportCallable(WorkspaceScriptEnvironment environment, Guid ownerScriptId, ScriptObject function) : DynamicObject
{
    private readonly WorkspaceScriptEnvironment _environment = environment;
    private readonly Guid _ownerScriptId = ownerScriptId;
    private readonly ScriptObject _function = function;

    /// <summary>
    /// Marshals invocation onto the owning script's <see cref="ScriptIsolateInstance"/>.
    /// </summary>
    public object? Invoke(params object?[] args) =>
        this._environment.InvokeCrossIsolateExport(this._ownerScriptId, this._function, args);

    /// <inheritdoc/>
    public override bool TryInvoke(InvokeBinder binder, object?[]? args, out object? result)
    {
        result = this.Invoke(args ?? []);
        return true;
    }
}

/// <summary>
/// Manages all isolated <see cref="ScriptIsolateInstance"/> runtimes, shared <c>$</c> namespace exports,
/// <c>require()</c> VFS library loading, <c>$.bind</c>/<c>$.on</c> event bindings, and concurrency/memory limits for a single workspace.
/// </summary>
public sealed class WorkspaceScriptEnvironment : IAsyncDisposable
{
    /// <summary>
    /// Tracks active <c>require()</c> recursion stack per async flow to detect circular library dependencies.
    /// </summary>
    private static readonly AsyncLocal<HashSet<string>?> _requireStack = new();

    private readonly Guid _workspaceId;
    private readonly IWorkspaceScriptStore _scriptStore;
    private readonly IWorkspaceKeyValueStore _keyValueStore;
    private readonly IRuntimeExecutionTracker _executionTracker;
    private readonly ISafeExternalHttpClient? _safeExternalHttpClient;
    private readonly IOutboundChatDispatcher? _outboundChatDispatcher;
    private readonly ITwitchSessionLeaseProvider? _twitchSessionProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger? _logger;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _executionConcurrencyGate;
    private readonly ConcurrentDictionary<Guid, ScriptIsolateInstance> _isolates = new();
    private readonly ConcurrentDictionary<string, Guid> _scriptIdsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DollarRegistryEntry> _dollarExports = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<ScriptEventBinding>> _eventBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<TwitchSession?> _activeTwitchSession = new();
    private readonly Lock _bindingsLock = new();

    private long _workspaceRamQuotaBytes;
    private TimeSpan _executionTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceScriptEnvironment"/> class.
    /// </summary>
    public WorkspaceScriptEnvironment(
        Guid workspaceId,
        IWorkspaceScriptStore scriptStore,
        IWorkspaceKeyValueStore keyValueStore,
        IRuntimeExecutionTracker executionTracker,
        long workspaceRamQuotaBytes,
        TimeSpan executionTimeout,
        int maxConcurrency,
        ISafeExternalHttpClient? safeExternalHttpClient = null,
        IOutboundChatDispatcher? outboundChatDispatcher = null,
        ITwitchSessionLeaseProvider? twitchSessionProvider = null,
        TimeProvider? timeProvider = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scriptStore);
        ArgumentNullException.ThrowIfNull(keyValueStore);
        ArgumentNullException.ThrowIfNull(executionTracker);

        this._workspaceId = workspaceId;
        this._scriptStore = scriptStore;
        this._keyValueStore = keyValueStore;
        this._executionTracker = executionTracker;
        this._workspaceRamQuotaBytes = Math.Max(1L, workspaceRamQuotaBytes);
        this._executionTimeout = executionTimeout > TimeSpan.Zero ? executionTimeout : TimeSpan.FromSeconds(2);
        this._executionConcurrencyGate = new SemaphoreSlim(Math.Max(1, maxConcurrency), Math.Max(1, maxConcurrency));
        this._safeExternalHttpClient = safeExternalHttpClient;
        this._outboundChatDispatcher = outboundChatDispatcher;
        this._twitchSessionProvider = twitchSessionProvider;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._logger = logger;
    }

    /// <summary>
    /// Updates the workspace RAM quota and execution timeout resolved from Tier 1 groups and instance settings.
    /// </summary>
    public void UpdateLimits(long workspaceRamQuotaBytes, TimeSpan executionTimeout)
    {
        this._workspaceRamQuotaBytes = Math.Max(1L, workspaceRamQuotaBytes);
        if (executionTimeout > TimeSpan.Zero)
        {
            this._executionTimeout = executionTimeout;
        }

        WorkspaceV8MemoryGovernor.EnforceWorkspaceQuota(this._isolates.Values, this._workspaceRamQuotaBytes);
    }

    /// <summary>
    /// Ensures <paramref name="script"/> is loaded into its own Option A <see cref="V8ScriptEngine"/> isolate.
    /// Subsequent calls when already loaded are a fast no-op.
    /// </summary>
    internal async ValueTask<ScriptIsolateInstance> EnsureScriptLoadedAsync(
        WorkspaceScript script,
        ScriptLifecycleMode lifecycleMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        if (this._isolates.TryGetValue(script.Id, out ScriptIsolateInstance? existing) && !existing.IsDisposed)
        {
            existing.LifecycleMode = lifecycleMode;
            return existing;
        }

        await this._loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._isolates.TryGetValue(script.Id, out existing) && !existing.IsDisposed)
            {
                existing.LifecycleMode = lifecycleMode;
                return existing;
            }

            return await this.LoadScriptCoreUnlockedAsync(script, lifecycleMode, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = this._loadLock.Release();
        }
    }

    /// <summary>
    /// Executes an action-block script request under the per-workspace concurrency semaphore and CPU timeout.
    /// </summary>
    public async ValueTask<ScriptExecutionResult> ExecuteScriptAsync(
        WorkspaceScript script,
        ScriptLifecycleMode lifecycleMode,
        ScriptExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(request);

        await this._executionConcurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        ITwitchSessionLease? sessionLease = null;
        TwitchSession? prevSession = this._activeTwitchSession.Value;
        try
        {
            if (this._twitchSessionProvider is not null)
            {
                sessionLease = await this._twitchSessionProvider.AcquireSessionAsync(this._workspaceId, cancellationToken).ConfigureAwait(false);
                this._activeTwitchSession.Value = sessionLease?.Session;
            }

            ScriptIsolateInstance isolate = await this.EnsureScriptLoadedAsync(
                script,
                lifecycleMode,
                cancellationToken).ConfigureAwait(false);

            using IDisposable scope = isolate.ContextObject.PushInvocationScope(
                request.PlatformId,
                request.ChannelId,
                request.GetFlowVariable,
                request.SetFlowVariable,
                request.SendChatAsync);

            object? returnValue = null;
            if (!string.IsNullOrWhiteSpace(request.EntryFunction))
            {
                string fnName = request.EntryFunction.Trim();
                object payload = isolate.ToScriptValue(request.Arguments ?? new Dictionary<string, string>());

                if (this._dollarExports.TryGetValue(fnName, out DollarRegistryEntry? entry)
                    && entry.RawValue is ScriptObject exportedFn
                    && this._isolates.TryGetValue(entry.OwnerScriptId, out ScriptIsolateInstance? ownerIsolate))
                {
                    returnValue = await ownerIsolate
                        .InvokeScriptObjectAsync(exportedFn, [payload], this._executionTimeout, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    (bool found, object? localResult) = await isolate
                        .TryInvokeLocalEntryAsync(fnName, payload, this._executionTimeout, cancellationToken)
                        .ConfigureAwait(false);
                    if (found)
                    {
                        returnValue = localResult;
                    }
                }
            }

            WorkspaceV8MemoryGovernor.EnforceWorkspaceQuota(this._isolates.Values, this._workspaceRamQuotaBytes);
            string? stringResult = returnValue is null or Undefined
                ? null
                : Convert.ToString(returnValue, CultureInfo.InvariantCulture);

            return new ScriptExecutionResult(true, stringResult);
        }
        catch (Exception ex) when (ex is ScriptMemoryQuotaExceededException)
        {
            await this.UnloadScriptAsync(script.Id).ConfigureAwait(false);
            return new ScriptExecutionResult(false, ErrorMessage: ex.Message);
        }
        catch (ScriptEngineException ex) when (IsHeapQuotaV8Exception(ex))
        {
            await this.UnloadScriptAsync(script.Id).ConfigureAwait(false);
            return new ScriptExecutionResult(
                false,
                ErrorMessage: $"Workspace ClearScript RAM usage exceeded Tier 1 MaxClearScriptTotalRamBytes ({this._workspaceRamQuotaBytes} bytes): {ex.Message}");
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or ScriptEngineException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new ScriptExecutionResult(false, ErrorMessage: ex.Message);
        }
        finally
        {
            this._activeTwitchSession.Value = prevSession;
            if (sessionLease is not null)
            {
                await sessionLease.DisposeAsync().ConfigureAwait(false);
            }

            _ = this._executionConcurrencyGate.Release();
        }
    }

    /// <summary>
    /// Dispatches an event to all matching <c>$.bind</c> / <c>$.on</c> handlers registered in this workspace.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Isolate handler failures must not crash event dispatch across remaining handlers.")]
    public async ValueTask<int> DispatchEventAsync(
        ScriptEventDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ScriptEventBinding> matchingBindings;
        lock (this._bindingsLock)
        {
            if (!this._eventBindings.TryGetValue(request.EventName.Trim(), out List<ScriptEventBinding>? bindings) || bindings.Count == 0)
            {
                return 0;
            }

            matchingBindings = [.. bindings];
        }

        int invoked = 0;
        await this._executionConcurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        ITwitchSessionLease? sessionLease = null;
        TwitchSession? prevSession = this._activeTwitchSession.Value;
        try
        {
            if (this._twitchSessionProvider is not null)
            {
                sessionLease = await this._twitchSessionProvider.AcquireSessionAsync(this._workspaceId, cancellationToken).ConfigureAwait(false);
                this._activeTwitchSession.Value = sessionLease?.Session;
            }

            foreach (ScriptEventBinding binding in matchingBindings)
            {
                if (request.TargetScriptId.HasValue && binding.OwnerScriptId != request.TargetScriptId.Value)
                {
                    continue;
                }

                if (!this._isolates.TryGetValue(binding.OwnerScriptId, out ScriptIsolateInstance? isolate) || isolate.IsDisposed)
                {
                    continue;
                }

                await this.InvokeBindingAsync(isolate, binding.Handler, request, cancellationToken).ConfigureAwait(false);

                WorkspaceV8MemoryGovernor.EnforceWorkspaceQuota(this._isolates.Values, this._workspaceRamQuotaBytes);
                invoked++;
            }
        }
        finally
        {
            this._activeTwitchSession.Value = prevSession;
            if (sessionLease is not null)
            {
                await sessionLease.DisposeAsync().ConfigureAwait(false);
            }

            _ = this._executionConcurrencyGate.Release();
        }

        return invoked;
    }

    /// <summary>
    /// Executes a single bound event handler inside its owning isolate scope.
    /// </summary>
    private async ValueTask InvokeBindingAsync(
        ScriptIsolateInstance isolate,
        ScriptObject handler,
        ScriptEventDispatchRequest request,
        CancellationToken cancellationToken)
    {
        using IDisposable scope = isolate.ContextObject.PushInvocationScope(
            request.PlatformId,
            request.ChannelId,
            getVar: null,
            setVar: null,
            sendChat: null);

        object payload = isolate.ToScriptValue(request.Payload);
        _ = await isolate
            .InvokeScriptObjectAsync(handler, [payload], this._executionTimeout, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Registers an export onto <c>$</c> tagged with <paramref name="ownerScriptId"/>.
    /// </summary>
    internal void SetDollarExport(Guid ownerScriptId, string name, object? value)
    {
        object? storedValue = value is ScriptObject scriptObj && IsCallableScriptObject(scriptObj)
            ? new DollarExportCallable(this, ownerScriptId, scriptObj)
            : value;

        this._dollarExports[name] = new DollarRegistryEntry(ownerScriptId, storedValue, value);
    }

    /// <summary>
    /// Attempts to retrieve an exported property or cross-isolate callable from <c>$</c>.
    /// </summary>
    internal bool TryGetDollarExport(string name, out object? value)
    {
        if (this._dollarExports.TryGetValue(name, out DollarRegistryEntry? entry))
        {
            value = entry.ExposedValue;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Registers a <c>$.bind</c> / <c>$.on</c> event subscription tagged with <paramref name="ownerScriptId"/>.
    /// </summary>
    internal void RegisterEventBinding(Guid ownerScriptId, string eventName, object? handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (handler is not ScriptObject scriptHandler)
        {
            throw new ArgumentException("Event handler passed to $.bind/$.on must be a JavaScript function.", nameof(handler));
        }

        string normalizedEvent = eventName.Trim();
        lock (this._bindingsLock)
        {
            if (!this._eventBindings.TryGetValue(normalizedEvent, out List<ScriptEventBinding>? list))
            {
                list = [];
                this._eventBindings[normalizedEvent] = list;
            }

            list.Add(new ScriptEventBinding(ownerScriptId, normalizedEvent, scriptHandler));
        }
    }

    /// <summary>
    /// Marshals a cross-isolate call to an exported <c>$</c> function on the owning script's <see cref="ScriptIsolateInstance"/>.
    /// </summary>
    internal object? InvokeCrossIsolateExport(Guid ownerScriptId, ScriptObject function, object?[] args)
    {
        if (!this._isolates.TryGetValue(ownerScriptId, out ScriptIsolateInstance? ownerIsolate) || ownerIsolate.IsDisposed)
        {
            throw new InvalidOperationException("Target library script isolate is no longer loaded.");
        }

        object?[] marshaledArgs = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            marshaledArgs[i] = MarshalValueForIsolate(args[i], ownerIsolate);
        }

        Task<object?> task = ownerIsolate.InvokeScriptObjectAsync(function, marshaledArgs, this._executionTimeout, CancellationToken.None);
        return task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Unloads a specific script isolate, removing all <c>$</c> exports and <c>$.bind</c>/<c>$.on</c> handlers it owns.
    /// </summary>
    public ValueTask UnloadScriptAsync(Guid scriptId)
    {
        this._executionTracker.UnregisterScriptIsolate(this._workspaceId, scriptId);

        if (this._isolates.TryRemove(scriptId, out ScriptIsolateInstance? isolate))
        {
            _ = this._scriptIdsByPath.TryRemove(isolate.ScriptPath, out _);
            isolate.Dispose();
        }

        foreach (KeyValuePair<string, DollarRegistryEntry> kvp in this._dollarExports)
        {
            if (kvp.Value.OwnerScriptId == scriptId)
            {
                _ = this._dollarExports.TryRemove(kvp.Key, out _);
            }
        }

        lock (this._bindingsLock)
        {
            foreach (List<ScriptEventBinding> list in this._eventBindings.Values)
            {
                _ = list.RemoveAll(b => b.OwnerScriptId == scriptId);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Returns snapshots of all currently loaded isolates in this workspace.
    /// </summary>
    internal IReadOnlyList<ScriptIsolateInstance> GetLoadedIsolates() => [.. this._isolates.Values];

    /// <summary>
    /// Unloads and disposes all loaded script isolates in this workspace.
    /// </summary>
    public async ValueTask UnloadAllAsync()
    {
        List<Guid> ids = [.. this._isolates.Keys];
        foreach (Guid id in ids)
        {
            await this.UnloadScriptAsync(id).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.UnloadAllAsync().ConfigureAwait(false);
        this._loadLock.Dispose();
        this._executionConcurrencyGate.Dispose();
    }

    /// <summary>
    /// Loads a script into its own isolated <see cref="ScriptIsolateInstance"/> while holding <see cref="_loadLock"/> (or re-entrantly from <c>require</c>).
    /// </summary>
    private async ValueTask<ScriptIsolateInstance> LoadScriptCoreUnlockedAsync(
        WorkspaceScript script,
        ScriptLifecycleMode lifecycleMode,
        CancellationToken cancellationToken)
    {
        nuint heapLimit = WorkspaceV8MemoryGovernor.ComputeNextIsolateHeapLimit(this._isolates.Values, this._workspaceRamQuotaBytes);

        ScopedDollarProxy dollarProxy = new(this, script.Id);
        ScriptContextObject contextObject = new(
            this._workspaceId,
            this._keyValueStore,
            this._safeExternalHttpClient,
            this._outboundChatDispatcher,
            this._logger);

        BoundNamespaceProxy? twitchProxy = this._twitchSessionProvider is not null
            ? new BoundNamespaceProxy(() => this.AcquireSessionSync(cancellationToken))
            : null;

        object? RequireSync(string rawPath) => this.RequireLibrarySync(rawPath, lifecycleMode, cancellationToken);
        Task<object?> ImportAsync(string rawPath) => Task.FromResult<object?>(this.RequireLibrarySync(rawPath, lifecycleMode, cancellationToken));

        ScriptIsolateInstance isolate = new(
            this._workspaceId,
            script,
            lifecycleMode,
            this._workspaceRamQuotaBytes,
            heapLimit,
            dollarProxy,
            contextObject,
            twitchProxy,
            RequireSync,
            ImportAsync,
            this._timeProvider);

        this._isolates[script.Id] = isolate;
        this._scriptIdsByPath[script.Path] = script.Id;

        this._executionTracker.RegisterScriptIsolate(
            this._workspaceId,
            script.Id,
            () => isolate.IsDisposed
                ? null
                : isolate.ToSnapshot(this.GetExportsForScript(script.Id), this.GetBoundEventsForScript(script.Id)),
            _ => this.UnloadScriptAsync(script.Id));

        try
        {
            await isolate
                .ExecuteInitialLoadAsync(script.Code, this._executionTimeout, cancellationToken)
                .ConfigureAwait(false);

            WorkspaceV8MemoryGovernor.EnforceWorkspaceQuota(this._isolates.Values, this._workspaceRamQuotaBytes);
            return isolate;
        }
        catch
        {
            await this.UnloadScriptAsync(script.Id).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Implements secure VFS <c>require("lib/myLibrary.js")</c> / <c>importScript(...)</c>, loading each library once into its own isolate.
    /// </summary>
    private ScopedDollarProxy RequireLibrarySync(string rawPath, ScriptLifecycleMode defaultMode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPath);

        string normalized = NormalizePath(rawPath);
        if (this._scriptIdsByPath.TryGetValue(normalized, out Guid loadedId)
            && this._isolates.TryGetValue(loadedId, out ScriptIsolateInstance? alreadyLoaded)
            && !alreadyLoaded.IsDisposed)
        {
            return new ScopedDollarProxy(this, loadedId);
        }

        HashSet<string> activeStack = _requireStack.Value ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!activeStack.Add(normalized))
        {
            throw new InvalidOperationException($"Circular require() dependency detected for '{normalized}'.");
        }

        HashSet<string>? prevStack = _requireStack.Value;
        _requireStack.Value = activeStack;
        try
        {
            WorkspaceScript library = this._scriptStore
                .GetByPathAsync(this._workspaceId, normalized, cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult()
                ?? throw new FileNotFoundException($"Workspace VFS script '{normalized}' was not found.", normalized);

            if (!library.IsEnabled)
            {
                throw new InvalidOperationException($"Workspace VFS script '{normalized}' is disabled.");
            }

            ScriptIsolateInstance loaded = this.LoadScriptCoreUnlockedAsync(
                library,
                library.IsPermanentlyLoaded ? ScriptLifecycleMode.PermanentResident : defaultMode,
                cancellationToken).AsTask().GetAwaiter().GetResult();

            return new ScopedDollarProxy(this, loaded.ScriptId);
        }
        finally
        {
            _ = activeStack.Remove(normalized);
            _requireStack.Value = prevStack;
        }
    }

    /// <summary>
    /// Acquires a <see cref="TwitchSession"/> for <see cref="BoundNamespaceProxy"/> using the active scoped lease when available.
    /// </summary>
    private TwitchSession AcquireSessionSync(CancellationToken cancellationToken)
    {
        if (this._activeTwitchSession.Value is TwitchSession active)
        {
            return active;
        }

        if (this._twitchSessionProvider is null)
        {
            return TwitchSession.Empty;
        }

        ITwitchSessionLease? lease = this._twitchSessionProvider
            .AcquireSessionAsync(this._workspaceId, cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return lease?.Session ?? TwitchSession.Empty;
    }

    private IReadOnlyList<string> GetExportsForScript(Guid scriptId) =>
        [.. this._dollarExports
            .Where(kvp => kvp.Value.OwnerScriptId == scriptId)
            .Select(static kvp => kvp.Key)
            .OrderBy(static k => k, StringComparer.Ordinal)];

    private IReadOnlyList<string> GetBoundEventsForScript(Guid scriptId)
    {
        lock (this._bindingsLock)
        {
            return [.. this._eventBindings
                .Where(kvp => kvp.Value.Any(b => b.OwnerScriptId == scriptId))
                .Select(static kvp => kvp.Key)
                .OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)];
        }
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Workspace VFS paths are normalized to lowercase invariant.")]
    private static string NormalizePath(string rawPath)
    {
        string normalized = rawPath.Trim().Replace('\\', '/').TrimStart('/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new ArgumentException($"Script path '{rawPath}' contains disallowed traversal segments.", nameof(rawPath));
            }
        }

        return string.Join('/', segments).ToLowerInvariant();
    }

    private static bool IsHeapQuotaV8Exception(ScriptEngineException ex) =>
        ex.Message.Contains("heap", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("memory", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("script exception is pending", StringComparison.OrdinalIgnoreCase);

    private static bool IsCallableScriptObject(ScriptObject scriptObj)
    {
        try
        {
            return scriptObj.GetProperty("call") is ScriptObject;
        }
        catch (ScriptEngineException)
        {
            return false;
        }
    }

    private static object? MarshalValueForIsolate(object? value, ScriptIsolateInstance targetIsolate)
    {
        if (value is null or Undefined)
        {
            return null;
        }

        if (value is string or bool or int or long or float or double or decimal)
        {
            return value;
        }

        if (value is ScriptObject scriptObject)
        {
            Dictionary<string, object?> map = new(StringComparer.Ordinal);
            foreach (string prop in scriptObject.PropertyNames)
            {
                map[prop] = scriptObject.GetProperty(prop);
            }

            return targetIsolate.ToScriptValue(map);
        }

        return targetIsolate.ToScriptValue(value);
    }

    private sealed record DollarRegistryEntry(Guid OwnerScriptId, object? ExposedValue, object? RawValue);

    private sealed record ScriptEventBinding(Guid OwnerScriptId, string EventName, ScriptObject Handler);
}
