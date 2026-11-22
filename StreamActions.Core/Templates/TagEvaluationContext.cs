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
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Templates;

/// <summary>
/// Default mutable runtime implementation of <see cref="ITagEvaluationContext"/> supporting safeguards, routine variables, counter tracking, and per-message memoization.
/// </summary>
public sealed class TagEvaluationContext : ITagEvaluationContext
{

    /// <summary>
    /// Routine-scoped variable storage keyed case-insensitively.
    /// </summary>
    private readonly Dictionary<string, string> _variables;

    /// <summary>
    /// Ordered list of counter operations recorded during this execution.
    /// </summary>
    private readonly List<CounterMutationResult> _counterMutations = [];

    /// <summary>
    /// Optional strongly-typed dependencies passed directly without a full <see cref="IServiceProvider"/>.
    /// </summary>
    private readonly TagEvaluationDependencies? _dependencies;

    /// <summary>
    /// Initializes a new <see cref="TagEvaluationContext"/> for evaluating a template against <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The inbound message or command invocation.</param>
    /// <param name="tagRegistry">The layered tag registry.</param>
    /// <param name="channelName">Optional channel name override (defaults to <see cref="IChatMessage.Channel"/> name when available).</param>
    /// <param name="currentGameCategory">Optional current stream game/category.</param>
    /// <param name="actorIdentity">Optional actor identity override (defaults to <see cref="IChatUser.ToIdentity"/>).</param>
    /// <param name="boundCounterName">Optional counter name bound to the executing command.</param>
    /// <param name="counterUsageBehavior">Bound counter usage behavior.</param>
    /// <param name="boundCounterScopeMode">Optional explicit scope mode for the bound counter.</param>
    /// <param name="canMutateBoundCounter">Whether the invoking actor is permitted to mutate the bound counter.</param>
    /// <param name="serviceProvider">Optional DI service provider.</param>
    /// <param name="dependencies">Optional strongly-typed service overrides.</param>
    /// <param name="timeProvider">Optional time provider (defaults to <see cref="TimeProvider"/> from dependencies/DI or <see cref="TimeProvider.System"/>).</param>
    /// <param name="chainDepth">Current sub-command chain depth.</param>
    /// <param name="visitedCommandTriggers">Command triggers already visited in the current chain.</param>
    /// <param name="variables">Optional pre-seeded routine variables.</param>
    /// <param name="memoizedCache">Optional shared per-message memoization dictionary.</param>
    /// <param name="maxTagEvaluationsPerExecution">Maximum allowed tag evaluations per execution.</param>
    /// <param name="maxEvaluationBufferChars">Maximum allowed buffer characters per execution.</param>
    /// <param name="maxAstNestingDepth">Maximum allowed AST nesting depth.</param>
    /// <param name="channelPointRedemption">Optional channel point redemption metadata.</param>
    /// <param name="maxDelayedActionsPerExecution">Maximum allowed delayed actions per execution.</param>
    public TagEvaluationContext(
        IMessageEnvelope message,
        ITagRegistry tagRegistry,
        string? channelName = null,
        string? currentGameCategory = null,
        PlatformUserIdentity? actorIdentity = null,
        string? boundCounterName = null,
        CounterUsageBehavior counterUsageBehavior = CounterUsageBehavior.None,
        CounterScopeMode? boundCounterScopeMode = null,
        bool canMutateBoundCounter = false,
        IServiceProvider? serviceProvider = null,
        TagEvaluationDependencies? dependencies = null,
        TimeProvider? timeProvider = null,
        int chainDepth = 0,
        IEnumerable<string>? visitedCommandTriggers = null,
        IEnumerable<KeyValuePair<string, string>>? variables = null,
        ConcurrentDictionary<string, object?>? memoizedCache = null,
        int maxTagEvaluationsPerExecution = TemplateExecutionLimits.MaxTagEvaluationsPerExecution,
        int maxEvaluationBufferChars = TemplateExecutionLimits.MaxEvaluationBufferChars,
        int maxAstNestingDepth = TemplateExecutionLimits.MaxAstNestingDepth,
        ChannelPointRedemptionContext? channelPointRedemption = null,
        int maxDelayedActionsPerExecution = TemplateExecutionLimits.MaxDelayedActionsPerExecution)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(tagRegistry);
        ArgumentOutOfRangeException.ThrowIfNegative(chainDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTagEvaluationsPerExecution);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEvaluationBufferChars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAstNestingDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDelayedActionsPerExecution);

        this.Message = message;
        this.WorkspaceId = message.WorkspaceId;
        this.PlatformId = message.PlatformId;
        this.ChannelName = channelName ?? (message as IChatMessage)?.Channel.ChannelName ?? string.Empty;
        this.CurrentGameCategory = currentGameCategory;
        this.ActorIdentity = actorIdentity ?? message.Sender.ToIdentity();
        this.ChannelPointRedemption = channelPointRedemption;

        if (message is ICommandInvocation invocation)
        {
            this.Arguments = invocation.Arguments;
            this.RawArguments = invocation.RawArguments;
        }
        else
        {
            this.Arguments = [];
            this.RawArguments = string.Empty;
        }

        this.BoundCounterName = boundCounterName;
        this.CounterUsageBehavior = counterUsageBehavior;
        this.BoundCounterScopeMode = boundCounterScopeMode;
        this.CanMutateBoundCounter = canMutateBoundCounter;
        this.TagRegistry = tagRegistry;
        this.ServiceProvider = serviceProvider;
        this._dependencies = dependencies;
        this.TimeProvider = timeProvider
            ?? dependencies?.TimeProvider
            ?? (serviceProvider?.GetService(typeof(TimeProvider)) as TimeProvider)
            ?? TimeProvider.System;

        this.ChainDepth = chainDepth;
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        if (visitedCommandTriggers is not null)
        {
            foreach (string trigger in visitedCommandTriggers)
            {
                if (!string.IsNullOrWhiteSpace(trigger))
                {
                    _ = visited.Add(trigger.Trim());
                }
            }
        }
        else if (message is ICommandInvocation rootInvocation && !string.IsNullOrWhiteSpace(rootInvocation.Trigger))
        {
            _ = visited.Add(rootInvocation.Trigger.Trim());
        }

        this.VisitedCommandTriggers = visited;
        this._variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (variables is not null)
        {
            foreach (KeyValuePair<string, string> kv in variables)
            {
                if (!string.IsNullOrWhiteSpace(kv.Key))
                {
                    this._variables[kv.Key] = kv.Value;
                }
            }
        }

        this.MemoizedCache = memoizedCache ?? new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        this.MaxTagEvaluationsPerExecution = maxTagEvaluationsPerExecution;
        this.MaxEvaluationBufferChars = maxEvaluationBufferChars;
        this.MaxAstNestingDepth = maxAstNestingDepth;
        this.MaxDelayedActionsPerExecution = maxDelayedActionsPerExecution;
    }

    /// <inheritdoc />
    public IMessageEnvelope Message { get; }

    /// <inheritdoc />
    public Guid WorkspaceId { get; }

    /// <inheritdoc />
    public string PlatformId { get; }

    /// <inheritdoc />
    public string ChannelName { get; }

    /// <inheritdoc />
    public string? CurrentGameCategory { get; }

    /// <inheritdoc />
    public PlatformUserIdentity ActorIdentity { get; }

    /// <inheritdoc />
    public ChannelPointRedemptionContext? ChannelPointRedemption { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Arguments { get; }

    /// <inheritdoc />
    public string RawArguments { get; }

    /// <inheritdoc />
    public string? BoundCounterName { get; }

    /// <inheritdoc />
    public CounterUsageBehavior CounterUsageBehavior { get; }

    /// <inheritdoc />
    public CounterScopeMode? BoundCounterScopeMode { get; }

    /// <inheritdoc />
    public bool CanMutateBoundCounter { get; }

    /// <inheritdoc />
    public ITagRegistry TagRegistry { get; }

    /// <inheritdoc />
    public IServiceProvider? ServiceProvider { get; }

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; }

    /// <inheritdoc />
    public int ChainDepth { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> VisitedCommandTriggers { get; }

    /// <inheritdoc />
    public bool IsCanceled { get; private set; }

    /// <inheritdoc />
    public string? CancelReason { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Variables => this._variables;

    /// <inheritdoc />
    public int EvaluationCount { get; private set; }

    /// <inheritdoc />
    public int BufferChars { get; private set; }

    /// <inheritdoc />
    public int CurrentAstDepth { get; private set; }

    /// <inheritdoc />
    public int MaxTagEvaluationsPerExecution { get; }

    /// <inheritdoc />
    public int MaxEvaluationBufferChars { get; }

    /// <inheritdoc />
    public int MaxAstNestingDepth { get; }

    /// <inheritdoc />
    public int DelayedActionCount { get; private set; }

    /// <inheritdoc />
    public int MaxDelayedActionsPerExecution { get; }

    /// <inheritdoc />
    public CounterMutationResult? LastCounterMutation =>
        this._counterMutations.Count > 0 ? this._counterMutations[^1] : null;

    /// <inheritdoc />
    public IReadOnlyList<CounterMutationResult> CounterMutations => this._counterMutations.AsReadOnly();

    /// <summary>
    /// Gets the underlying per-message memoization cache dictionary for sharing across chained sub-command evaluations.
    /// </summary>
    internal ConcurrentDictionary<string, object?> MemoizedCache { get; }

    /// <inheritdoc />
    public T? GetService<T>() where T : class =>
        this._dependencies?.TryResolve<T>()
        ?? this.ServiceProvider?.GetService(typeof(T)) as T;

    /// <inheritdoc />
    public void Cancel(string? reason = null)
    {
        this.IsCanceled = true;
        this.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
    }

    /// <inheritdoc />
    public bool TryGetVariable(string name, [NotNullWhen(true)] out string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            value = null;
            return false;
        }

        return this._variables.TryGetValue(name.Trim(), out value);
    }

    /// <inheritdoc />
    public void SetVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this._variables[name.Trim()] = value ?? string.Empty;
    }

    /// <inheritdoc />
    public async ValueTask<T> GetOrComputeMemoizedAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        cancellationToken.ThrowIfCancellationRequested();

        if (this.MemoizedCache.TryGetValue(key, out object? existingEntry)
            && existingEntry is T cachedValue)
        {
            return cachedValue;
        }

        object? entry = this.MemoizedCache.GetOrAdd(
            key,
            _ => new Lazy<Task<T>>(
                () => this.ExecuteAndCacheAsync(key, factory, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return entry is T directValue
            ? directValue
            : entry is Lazy<Task<T>> lazy
            ? await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false)
            : await this.ExecuteAndCacheAsync(key, factory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes <paramref name="factory"/> once for <paramref name="key"/> and replaces the lazy entry with the resolved value, evicting on failure.
    /// </summary>
    /// <typeparam name="T">The computed value type.</typeparam>
    /// <param name="key">The memoization key.</param>
    /// <param name="factory">The asynchronous factory delegate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The computed value.</returns>
    private async Task<T> ExecuteAndCacheAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken)
    {
        try
        {
            T computed = await factory(cancellationToken).ConfigureAwait(false);
            this.MemoizedCache[key] = computed;
            return computed;
        }
        catch
        {
            _ = this.MemoizedCache.TryRemove(key, out _);
            throw;
        }
    }

    /// <inheritdoc />
    public void IncrementEvaluationCount()
    {
        this.EvaluationCount++;
        if (this.EvaluationCount > this.MaxTagEvaluationsPerExecution)
        {
            throw new TagEvaluationException(
                TagEvaluationFailureReason.MaxTagEvaluationsExceeded,
                $"Template evaluation exceeded the maximum of {this.MaxTagEvaluationsPerExecution} tag evaluations.");
        }
    }

    /// <inheritdoc />
    public void AddBufferChars(int charCount)
    {
        if (charCount <= 0)
        {
            return;
        }

        long nextTotal = (long)this.BufferChars + charCount;
        if (nextTotal > this.MaxEvaluationBufferChars)
        {
            throw new TagEvaluationException(
                TagEvaluationFailureReason.MaxEvaluationBufferExceeded,
                $"Template evaluation buffer exceeded {this.MaxEvaluationBufferChars} characters.");
        }

        this.BufferChars = (int)nextTotal;
    }

    /// <inheritdoc />
    public void EnterTagDepth()
    {
        this.CurrentAstDepth++;
        if (this.CurrentAstDepth > this.MaxAstNestingDepth)
        {
            throw new TagEvaluationException(
                TagEvaluationFailureReason.MaxAstNestingDepthExceeded,
                $"Template evaluation exceeded maximum AST nesting depth of {this.MaxAstNestingDepth}.");
        }
    }

    /// <inheritdoc />
    public void ExitTagDepth()
    {
        if (this.CurrentAstDepth > 0)
        {
            this.CurrentAstDepth--;
        }
    }

    /// <inheritdoc />
    public void RecordCounterMutation(CounterMutationResult mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        this._counterMutations.Add(mutation);
    }

    /// <inheritdoc />
    public bool TryReserveDelayedAction()
    {
        if (this.DelayedActionCount >= this.MaxDelayedActionsPerExecution)
        {
            return false;
        }

        this.DelayedActionCount++;
        return true;
    }
}
