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
using StreamActions.Core.Actions;
using StreamActions.Core.Scripting;
using StreamActions.Scripting.Proxies;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StreamActions.Scripting.Runtime;

/// <summary>
/// Owns a single isolated <see cref="V8ScriptEngine"/> instance for one loaded <see cref="WorkspaceScript"/>.
/// Enforces <c>AllowReflection = false</c>, heap limits, CPU interrupt timeouts, and surgical disposal.
/// </summary>
internal sealed partial class ScriptIsolateInstance : IDisposable
{
    /// <summary>
    /// Strict JavaScript identifier pattern for validating optional local entry function names.
    /// </summary>
    [GeneratedRegex("^[A-Za-z_$][A-Za-z0-9_$]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidIdentifierRegex();

    /// <summary>
    /// Isolated V8 script engine instance for this script only.
    /// </summary>
    private readonly V8ScriptEngine _engine;

    /// <summary>
    /// Lifetime cancellation source triggered when this script is unloaded or terminated by Task Manager.
    /// </summary>
    private readonly CancellationTokenSource _lifetimeCts = new();

    /// <summary>
    /// Time provider for lifecycle idle tracking.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Workspace RAM quota snapshot in bytes.
    /// </summary>
    private long _workspaceRamQuotaBytes;

    /// <summary>
    /// Cumulative execution ticks measured via <see cref="Stopwatch"/>.
    /// </summary>
    private long _cumulativeStopwatchTicks;

    /// <summary>
    /// Disposal guard (0 = active, 1 = disposed).
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Optional local resolver inside the script's strict IIFE closure capable of resolving any local function by validated identifier.
    /// </summary>
    private ScriptObject? _localEntryResolver;

    /// <summary>
    /// Initializes a new isolated <see cref="ScriptIsolateInstance"/> and sets mandatory V8 security boundaries.
    /// </summary>
    public ScriptIsolateInstance(
        Guid workspaceId,
        WorkspaceScript script,
        ScriptLifecycleMode lifecycleMode,
        long workspaceRamQuotaBytes,
        nuint initialMaxHeapSize,
        ScopedDollarProxy dollarProxy,
        ScriptContextObject contextObject,
        BoundNamespaceProxy? twitchProxy,
        Func<string, object?> requireDelegate,
        Func<string, Task<object?>> importScriptDelegate,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(dollarProxy);
        ArgumentNullException.ThrowIfNull(contextObject);
        ArgumentNullException.ThrowIfNull(requireDelegate);
        ArgumentNullException.ThrowIfNull(importScriptDelegate);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.InstanceId = Guid.CreateVersion7();
        this.WorkspaceId = workspaceId;
        this.ScriptId = script.Id;
        this.ScriptPath = script.Path;
        this.LifecycleMode = lifecycleMode;
        this._workspaceRamQuotaBytes = workspaceRamQuotaBytes;
        this._timeProvider = timeProvider;
        this.LoadedAtUtc = timeProvider.GetUtcNow();
        this.LastActivityAtUtc = this.LoadedAtUtc;
        this.ContextObject = contextObject;

        this._engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion)
        {
            AllowReflection = false,
            MaxRuntimeHeapSize = initialMaxHeapSize,
        };

        this._engine.Script.__sa_dollar = dollarProxy;
        this._engine.Script.__sa_context = contextObject;
        this._engine.Script.__sa_twitch = twitchProxy ?? (object)Undefined.Value;
        this._engine.Script.__sa_require = requireDelegate;
        this._engine.Script.__sa_importScript = importScriptDelegate;
        this._engine.Script.__sa_registerLocalLookup = new Action<object?>(fn =>
        {
            if (fn is ScriptObject so)
            {
                this._localEntryResolver = so;
            }
        });
    }

    /// <summary>Gets the unique isolate instance identifier.</summary>
    public Guid InstanceId { get; }

    /// <summary>Gets the owning workspace identifier.</summary>
    public Guid WorkspaceId { get; }

    /// <summary>Gets the VFS script identifier.</summary>
    public Guid ScriptId { get; }

    /// <summary>Gets the normalized VFS script path.</summary>
    public string ScriptPath { get; }

    /// <summary>Gets or sets the active lifecycle unloading mode.</summary>
    public ScriptLifecycleMode LifecycleMode { get; set; }

    /// <summary>Gets the UTC timestamp when this isolate was loaded.</summary>
    public DateTimeOffset LoadedAtUtc { get; }

    /// <summary>Gets the UTC timestamp of the most recent activity on this isolate.</summary>
    public DateTimeOffset LastActivityAtUtc { get; private set; }

    /// <summary>Gets the <see cref="ScriptContextObject"/> bound to this isolate.</summary>
    public ScriptContextObject ContextObject { get; }

    /// <summary>Gets a value indicating whether this isolate has been disposed or terminated.</summary>
    public bool IsDisposed => Volatile.Read(ref this._disposed) != 0;

    /// <summary>
    /// Updates the cached workspace RAM quota and isolate heap ceiling.
    /// </summary>
    public void UpdateMaxRuntimeHeapSize(nuint maxHeapBytes, long? workspaceQuotaBytes = null)
    {
        if (this.IsDisposed)
        {
            return;
        }

        if (workspaceQuotaBytes.HasValue)
        {
            this._workspaceRamQuotaBytes = workspaceQuotaBytes.Value;
        }

        try
        {
            this._engine.MaxRuntimeHeapSize = maxHeapBytes;
        }
        catch (ObjectDisposedException)
        {
            // Isolate disposed concurrently.
        }
    }

    /// <summary>
    /// Returns the current V8 used heap size in bytes.
    /// </summary>
    public long GetUsedHeapBytes()
    {
        if (this.IsDisposed)
        {
            return 0;
        }

        try
        {
            V8RuntimeHeapInfo heap = this._engine.GetRuntimeHeapInfo();
            return unchecked((long)heap.UsedHeapSize);
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Builds a Task Manager metrics snapshot for this isolate.
    /// </summary>
    public LoadedScriptInstanceInfo ToSnapshot(IReadOnlyList<string> exports, IReadOnlyList<string> boundEvents) =>
        new(
            this.InstanceId,
            this.WorkspaceId,
            this.ScriptId,
            this.ScriptPath,
            this.LifecycleMode,
            this.GetUsedHeapBytes(),
            this._workspaceRamQuotaBytes,
            Stopwatch.GetElapsedTime(0, Volatile.Read(ref this._cumulativeStopwatchTicks)),
            exports,
            boundEvents,
            this.LoadedAtUtc,
            this.LastActivityAtUtc);

    /// <summary>
    /// Executes the script's top-level body inside an isolated strict IIFE closure.
    /// </summary>
    public async Task ExecuteInitialLoadAsync(
        string code,
        TimeSpan cpuTimeout,
        CancellationToken cancellationToken)
    {
        const string localLookupFooter =
            "\n__sa_registerLocalLookup(function(name) {\n"
            + "  if (typeof name !== 'string' || !/^[A-Za-z_$][A-Za-z0-9_$]*$/.test(name)) { return null; }\n"
            + "  try { const fn = eval(name); return typeof fn === 'function' ? fn : null; }\n"
            + "  catch { return null; }\n"
            + "});";

        string wrappedCode =
            "(function(__sa_dollarHost, context, __sa_twitch, __sa_require, __sa_importScript) {\n\"use strict\";\n"
            + "const makeDollarProxy = (host) => new Proxy(function() {}, {\n"
            + "  get(_target, prop) {\n"
            + "    if (typeof prop !== 'string' || prop === 'then') { return undefined; }\n"
            + "    if (prop === 'bind' || prop === 'on') { return function(eventName, handler) { host.BindEvent(eventName, handler); }; }\n"
            + "    const desc = host.GetExport(prop);\n"
            + "    if (!desc) { return undefined; }\n"
            + "    if (desc.IsCallable) { return function(...args) { return desc.InvokeArray(args); }; }\n"
            + "    return desc.Value;\n"
            + "  },\n"
            + "  set(_target, prop, value) {\n"
            + "    if (typeof prop === 'string') { host.SetExport(prop, value); }\n"
            + "    return true;\n"
            + "  }\n"
            + "});\n"
            + "const makeTwitchProxy = (node) => new Proxy(function() {}, {\n"
            + "  get(_target, prop) {\n"
            + "    if (typeof prop !== 'string' || prop === 'then') { return undefined; }\n"
            + "    if (typeof node.InvokeMember === 'function') { return function(...args) { return node.InvokeMember(prop, args); }; }\n"
            + "    if (typeof node.ResolveMember === 'function') { const child = node.ResolveMember(prop); return child ? makeTwitchProxy(child) : undefined; }\n"
            + "    return undefined;\n"
            + "  }\n"
            + "});\n"
            + "const $ = makeDollarProxy(__sa_dollarHost);\n"
            + "const twitch = __sa_twitch ? makeTwitchProxy(__sa_twitch) : undefined;\n"
            + "const require = (path) => { const lib = __sa_require(path); return lib ? makeDollarProxy(lib) : undefined; };\n"
            + "const importScript = async (path) => { const lib = await __sa_importScript(path); return lib ? makeDollarProxy(lib) : undefined; };\n"
            + (code ?? string.Empty)
            + localLookupFooter
            + "\n})(__sa_dollar, __sa_context, __sa_twitch, __sa_require, __sa_importScript);";

        _ = await this.RunWithInterruptAsync(
            () => this._engine.Evaluate(this.ScriptPath, wrappedCode),
            cpuTimeout,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes a local entry function captured from the IIFE scope if present.
    /// </summary>
    public async Task<(bool Found, object? Result)> TryInvokeLocalEntryAsync(
        string entryFunctionName,
        object? argumentPayload,
        TimeSpan cpuTimeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entryFunctionName) || this._localEntryResolver is null)
        {
            return (false, null);
        }

        string trimmedName = entryFunctionName.Trim();
        if (!ValidIdentifierRegex().IsMatch(trimmedName))
        {
            return (false, null);
        }

        object? resolved = null;
        object? result = await this.RunWithInterruptAsync(
            () =>
            {
                resolved = this._localEntryResolver.InvokeAsFunction(trimmedName);
                return resolved is ScriptObject localFn
                    ? localFn.InvokeAsFunction(argumentPayload)
                    : Undefined.Value;
            },
            cpuTimeout,
            cancellationToken).ConfigureAwait(false);

        return resolved is ScriptObject ? (true, result) : (false, null);
    }

    /// <summary>
    /// Invokes an exported <see cref="ScriptObject"/> function or event handler owned by this isolate with CPU timeout and Promise awaiting.
    /// </summary>
    public Task<object?> InvokeScriptObjectAsync(
        ScriptObject function,
        object?[] args,
        TimeSpan cpuTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);
        return this.RunWithInterruptAsync(
            () => function.InvokeAsFunction(args),
            cpuTimeout,
            cancellationToken);
    }

    /// <summary>
    /// Converts a .NET dictionary/payload into a native JS object inside this isolate's V8 engine.
    /// </summary>
    public object ToScriptValue(object? value)
    {
        if (value is null)
        {
            return Undefined.Value;
        }

        if (value is string or bool or int or long or float or double or decimal)
        {
            return value;
        }

        string json = JsonSerializer.Serialize(value);
        return this._engine.Script.JSON.parse(json);
    }

    /// <summary>
    /// Interrupts any active V8 execution and disposes this isolate cleanly.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Surgical termination/disposal must swallow V8 interrupt exceptions cleanly.")]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0)
        {
            return;
        }

        try
        {
            this._lifetimeCts.Cancel();
        }
        catch (Exception)
        {
            // Ignore concurrent disposal.
        }

        try
        {
            this._engine.Interrupt();
        }
        catch (Exception)
        {
            // Ignore interrupt races during shutdown.
        }

        try
        {
            this._engine.Dispose();
        }
        catch (Exception)
        {
            // Ignore disposal races after interrupt.
        }

        this._lifetimeCts.Dispose();
    }

    /// <summary>
    /// Executes <paramref name="work"/> against the V8 engine under a CPU timeout + lifetime cancellation guard,
    /// converting returned JS Promises into awaitable .NET tasks and recording cumulative runtime.
    /// </summary>
    private async Task<object?> RunWithInterruptAsync(
        Func<object?> work,
        TimeSpan cpuTimeout,
        CancellationToken cancellationToken)
    {
        if (this.IsDisposed)
        {
            throw new OperationCanceledException("Script isolate has been terminated.");
        }

        this.LastActivityAtUtc = this._timeProvider.GetUtcNow();
        long startTimestamp = Stopwatch.GetTimestamp();

        using CancellationTokenSource timeoutCts = new(cpuTimeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this._lifetimeCts.Token,
            timeoutCts.Token);

        using CancellationTokenRegistration interruptRegistration = linkedCts.Token.Register(() =>
        {
            if (!this.IsDisposed)
            {
                try
                {
                    this._engine.Interrupt();
                }
                catch (ObjectDisposedException)
                {
                    // Isolate disposed concurrently.
                }
            }
        });

        try
        {
            object? rawResult = work();
            return await UnwrapPotentialPromiseOrTaskAsync(rawResult, linkedCts.Token).ConfigureAwait(false);
        }
        catch (ScriptInterruptedException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested && !this._lifetimeCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Script '{this.ScriptPath}' exceeded the {cpuTimeout.TotalSeconds:0.##}s CPU execution timeout.", ex);
        }
        catch (ScriptInterruptedException ex)
        {
            throw new OperationCanceledException($"Script '{this.ScriptPath}' execution was interrupted.", ex, linkedCts.Token);
        }
        finally
        {
            long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            _ = Interlocked.Add(ref this._cumulativeStopwatchTicks, elapsed);
            this.LastActivityAtUtc = this._timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// Unwraps a .NET <see cref="Task"/> or ClearScript JS <c>Promise</c> (<see cref="ScriptObject"/> with <c>then</c>) into an awaitable value.
    /// </summary>
    private static async Task<object?> UnwrapPotentialPromiseOrTaskAsync(object? rawResult, CancellationToken cancellationToken)
    {
        if (rawResult is Task<object?> typedTask)
        {
            return await typedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (rawResult is Task task)
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            Type taskType = task.GetType();
            return taskType.IsGenericType ? (taskType.GetProperty("Result")?.GetValue(task)) : null;
        }

        if (rawResult is ScriptObject scriptObject && scriptObject.GetProperty("then") is ScriptObject thenFn)
        {
            TaskCompletionSource<object?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            Action<object?> onFulfilled = value => tcs.TrySetResult(value is Undefined ? null : value);
            Action<object?> onRejected = reason => tcs.TrySetException(
                new ScriptEngineException(Convert.ToString(reason, System.Globalization.CultureInfo.InvariantCulture) ?? "JavaScript Promise rejected."));

            _ = scriptObject.InvokeMethod("then", onFulfilled, onRejected);
            _ = thenFn;
            return await tcs.Task.ConfigureAwait(false);
        }

        return rawResult is Undefined ? null : rawResult;
    }
}

