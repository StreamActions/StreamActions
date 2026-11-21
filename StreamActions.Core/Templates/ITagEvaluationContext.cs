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
using System.Diagnostics.CodeAnalysis;

namespace StreamActions.Core.Templates;

/// <summary>
/// Runtime execution context passed through template AST evaluation and tag handlers.
/// </summary>
public interface ITagEvaluationContext
{
    /// <summary>
    /// Gets the inbound message or command invocation being evaluated.
    /// </summary>
    IMessageEnvelope Message { get; }

    /// <summary>
    /// Gets the workspace identifier owning the execution.
    /// </summary>
    Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the originating platform identifier (for example, <c>twitch</c>).
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Gets the channel name associated with the message, or empty when unavailable.
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Gets the current stream game or category when known.
    /// </summary>
    string? CurrentGameCategory { get; }

    /// <summary>
    /// Gets the normalized actor identity for permission checks.
    /// </summary>
    PlatformUserIdentity ActorIdentity { get; }

    /// <summary>
    /// Gets the command's parsed positional arguments.
    /// </summary>
    IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the command's raw unparsed argument string.
    /// </summary>
    string RawArguments { get; }

    /// <summary>
    /// Gets the optional counter name bound to the current command.
    /// </summary>
    string? BoundCounterName { get; }

    /// <summary>
    /// Gets the behavior governing bound counter mutations on command execution.
    /// </summary>
    CounterUsageBehavior CounterUsageBehavior { get; }

    /// <summary>
    /// Gets the optional explicit scope mode configured for the bound counter.
    /// </summary>
    CounterScopeMode? BoundCounterScopeMode { get; }

    /// <summary>
    /// Gets a value indicating whether the invoking actor is permitted to mutate the bound counter.
    /// </summary>
    bool CanMutateBoundCounter { get; }

    /// <summary>
    /// Gets the layered tag registry used to resolve tag handlers.
    /// </summary>
    ITagRegistry TagRegistry { get; }

    /// <summary>
    /// Gets the optional service provider for resolving dependencies such as stores and dispatchers.
    /// </summary>
    IServiceProvider? ServiceProvider { get; }

    /// <summary>
    /// Gets the time provider used for time-based tags and evaluation timeouts.
    /// </summary>
    TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the current sub-command nesting depth (zero for top-level command invocations).
    /// </summary>
    int ChainDepth { get; }

    /// <summary>
    /// Gets the case-insensitive set of command triggers already visited in the active command chain.
    /// </summary>
    IReadOnlySet<string> VisitedCommandTriggers { get; }

    /// <summary>
    /// Gets a value indicating whether template execution was canceled via <c>{cancel}</c>.
    /// </summary>
    bool IsCanceled { get; }

    /// <summary>
    /// Gets the optional cancellation reason supplied to <c>{cancel}</c>.
    /// </summary>
    string? CancelReason { get; }

    /// <summary>
    /// Gets a read-only view of routine-scoped variables set via <c>{var}</c>.
    /// </summary>
    IReadOnlyDictionary<string, string> Variables { get; }

    /// <summary>
    /// Gets the number of tag evaluations performed so far in this execution.
    /// </summary>
    int EvaluationCount { get; }

    /// <summary>
    /// Gets the cumulative number of buffer characters accounted for during this execution.
    /// </summary>
    int BufferChars { get; }

    /// <summary>
    /// Gets the current runtime tag nesting depth.
    /// </summary>
    int CurrentAstDepth { get; }

    /// <summary>
    /// Gets the maximum permitted tag evaluations per execution.
    /// </summary>
    int MaxTagEvaluationsPerExecution { get; }

    /// <summary>
    /// Gets the maximum permitted evaluation buffer character count.
    /// </summary>
    int MaxEvaluationBufferChars { get; }

    /// <summary>
    /// Gets the maximum permitted runtime AST nesting depth.
    /// </summary>
    int MaxAstNestingDepth { get; }

    /// <summary>
    /// Gets the most recent counter read or mutation result recorded during this execution, if any.
    /// </summary>
    CounterMutationResult? LastCounterMutation { get; }

    /// <summary>
    /// Gets all counter read or mutation results recorded during this execution.
    /// </summary>
    IReadOnlyList<CounterMutationResult> CounterMutations { get; }

    /// <summary>
    /// Resolves an optional dependency service of type <typeparamref name="T"/> from explicit dependencies or <see cref="ServiceProvider"/>.
    /// </summary>
    /// <typeparam name="T">The service contract type.</typeparam>
    /// <returns>The resolved service instance, or <see langword="null"/> if unavailable.</returns>
    T? GetService<T>() where T : class;

    /// <summary>
    /// Marks template execution as canceled with an optional <paramref name="reason"/>.
    /// </summary>
    /// <param name="reason">An optional cancellation reason.</param>
    void Cancel(string? reason = null);

    /// <summary>
    /// Attempts to retrieve a routine-scoped variable previously stored via <see cref="SetVariable"/>.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the stored value.</param>
    /// <returns><see langword="true"/> if the variable exists; otherwise, <see langword="false"/>.</returns>
    bool TryGetVariable(string name, [NotNullWhen(true)] out string? value);

    /// <summary>
    /// Stores or overwrites a routine-scoped variable for subsequent <c>{var}</c> reads.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    void SetVariable(string name, string value);

    /// <summary>
    /// Computes or retrieves a per-message memoized value keyed by <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="T">The cached value type.</typeparam>
    /// <param name="key">The memoization key.</param>
    /// <param name="factory">The asynchronous factory invoked on a cache miss.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The memoized or freshly computed value.</returns>
    ValueTask<T> GetOrComputeMemoizedAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments <see cref="EvaluationCount"/> and throws <see cref="TagEvaluationException"/> if <see cref="MaxTagEvaluationsPerExecution"/> is exceeded.
    /// </summary>
    void IncrementEvaluationCount();

    /// <summary>
    /// Adds <paramref name="charCount"/> to <see cref="BufferChars"/> and throws <see cref="TagEvaluationException"/> if <see cref="MaxEvaluationBufferChars"/> is exceeded.
    /// </summary>
    /// <param name="charCount">The number of characters produced.</param>
    void AddBufferChars(int charCount);

    /// <summary>
    /// Increments <see cref="CurrentAstDepth"/> and throws <see cref="TagEvaluationException"/> if <see cref="MaxAstNestingDepth"/> is exceeded.
    /// </summary>
    void EnterTagDepth();

    /// <summary>
    /// Decrements <see cref="CurrentAstDepth"/> when exiting a <see cref="TagNode"/> evaluation.
    /// </summary>
    void ExitTagDepth();

    /// <summary>
    /// Records a counter read or mutation outcome for diagnostics and post-execution inspection.
    /// </summary>
    /// <param name="mutation">The counter mutation result to record.</param>
    void RecordCounterMutation(CounterMutationResult mutation);
}
