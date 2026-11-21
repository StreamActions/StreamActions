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

using Microsoft.Extensions.Caching.Hybrid;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Templates;
using System.Collections.Concurrent;

namespace StreamActions.Core.Commands;

/// <summary>
/// Represents a pre-compiled chat message step within a <see cref="CompiledCommandRoutine"/>.
/// </summary>
/// <param name="RawTemplate">The raw template text from the <see cref="ChatSendActionStep"/>.</param>
/// <param name="ParseResult">The compiled <see cref="TemplateParseResult"/> containing the AST root and diagnostics.</param>
public sealed record CompiledRoutineStep(
    string RawTemplate,
    TemplateParseResult ParseResult);

/// <summary>
/// Represents a compiled <see cref="CommandRoutine"/> cached for repeated command executions.
/// </summary>
/// <param name="WorkspaceId">The owning workspace identifier.</param>
/// <param name="CommandId">The command identifier.</param>
/// <param name="RoutineJson">The exact routine source string used to compile this AST.</param>
/// <param name="Steps">The ordered pre-compiled chat message steps.</param>
/// <param name="WorkspaceEpoch">The workspace invalidation epoch active when compiled.</param>
/// <param name="GlobalEpoch">The global invalidation epoch active when compiled.</param>
public sealed record CompiledCommandRoutine(
    Guid WorkspaceId,
    Guid CommandId,
    string RoutineJson,
    IReadOnlyList<CompiledRoutineStep> Steps,
    long WorkspaceEpoch,
    long GlobalEpoch);

/// <summary>
/// Thread-safe pre-compiled AST cache for custom command routines, using <see cref="HybridCache"/> tag/epoch tracking and immediate <see cref="CommandsInvalidatedEvent"/> invalidation without JSON-serializing abstract AST nodes.
/// </summary>
public sealed class CommandAstCache : IDisposable
{
    /// <summary>
    /// Tag registry used when parsing templates for registry diagnostics.
    /// </summary>
    private readonly ITagRegistry _tagRegistry;

    /// <summary>
    /// Optional <see cref="HybridCache"/> used to track workspace invalidation tags alongside in-memory AST entries.
    /// </summary>
    private readonly HybridCache? _hybridCache;

    /// <summary>
    /// Event bus subscription for <see cref="CommandsInvalidatedEvent"/>.
    /// </summary>
    private readonly IDisposable? _subscription;

    /// <summary>
    /// In-memory compiled command routine AST cache keyed by <c>(WorkspaceId, CommandId, NormalizedTrigger)</c>.
    /// </summary>
    private readonly ConcurrentDictionary<RoutineCacheKey, CompiledCommandRoutine> _routineCache = new();

    /// <summary>
    /// Per-workspace monotonic invalidation epochs.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, long> _workspaceEpochs = new();

    /// <summary>
    /// Lock synchronizing invalidation against concurrent cache inserts so stale compilations cannot overwrite newer epochs.
    /// </summary>
    private readonly Lock _syncLock = new();

    /// <summary>
    /// Global monotonic invalidation epoch incremented when <see cref="CommandsInvalidatedEvent"/> specifies a <see langword="null"/> workspace.
    /// </summary>
    private long _globalEpoch;

    /// <summary>
    /// Total number of routine AST compilations performed (used for observability and cache-hit verification).
    /// </summary>
    private long _compilationCount;

    /// <summary>
    /// Disposal state flag (<c>0</c> when active, <c>1</c> when disposed).
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandAstCache"/> class.
    /// </summary>
    /// <param name="tagRegistry">The tag registry used during template parsing.</param>
    /// <param name="eventBus">Optional event bus for automatic <see cref="CommandsInvalidatedEvent"/> eviction.</param>
    /// <param name="hybridCache">Optional <see cref="HybridCache"/> instance for workspace tag tracking.</param>
    public CommandAstCache(
        ITagRegistry tagRegistry,
        IEventBus? eventBus = null,
        HybridCache? hybridCache = null)
    {
        ArgumentNullException.ThrowIfNull(tagRegistry);

        this._tagRegistry = tagRegistry;
        this._hybridCache = hybridCache;
        this._subscription = eventBus?.Subscribe<CommandsInvalidatedEvent>(this.OnCommandsInvalidatedAsync);
    }

    /// <summary>
    /// Raised after an invalidation event has been processed for a workspace (or <see langword="null"/> for global invalidation).
    /// </summary>
    internal event Action<Guid?>? Invalidated;

    /// <summary>
    /// Gets the total number of AST routine compilations performed across the lifetime of this cache.
    /// </summary>
    public long CompilationCount => Interlocked.Read(ref this._compilationCount);

    /// <summary>
    /// Gets the number of compiled command routines currently stored in memory.
    /// </summary>
    public int CachedCount => this._routineCache.Count;

    /// <summary>
    /// Retrieves a cached compiled AST routine for <paramref name="command"/> or compiles and caches it once for the current workspace epoch and routine text.
    /// </summary>
    /// <param name="command">The custom command entity whose <see cref="CustomCommand.RoutineJson"/> should be compiled.</param>
    /// <returns>The compiled <see cref="CompiledCommandRoutine"/>.</returns>
    public CompiledCommandRoutine GetOrCompileRoutine(CustomCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this._disposed) != 0, this);

        string routineJson = command.RoutineJson ?? string.Empty;
        RoutineCacheKey key = new(
            command.WorkspaceId,
            command.Id,
            BuiltInCommandRegistry.NormalizeTrigger(command.Trigger));

        long currentGlobalEpoch = Volatile.Read(ref this._globalEpoch);
        long currentWorkspaceEpoch = this._workspaceEpochs.GetValueOrDefault(command.WorkspaceId);

        if (this._routineCache.TryGetValue(key, out CompiledCommandRoutine? existing)
            && existing.GlobalEpoch == currentGlobalEpoch
            && existing.WorkspaceEpoch == currentWorkspaceEpoch
            && string.Equals(existing.RoutineJson, routineJson, StringComparison.Ordinal))
        {
            return existing;
        }

        CompiledCommandRoutine compiled = this.CompileRoutine(command.WorkspaceId, command.Id, routineJson, currentWorkspaceEpoch, currentGlobalEpoch);

        lock (this._syncLock)
        {
            long latestGlobal = Volatile.Read(ref this._globalEpoch);
            long latestWorkspace = this._workspaceEpochs.GetValueOrDefault(command.WorkspaceId);
            if (latestGlobal != currentGlobalEpoch || latestWorkspace != currentWorkspaceEpoch)
            {
                compiled = this.CompileRoutine(command.WorkspaceId, command.Id, routineJson, latestWorkspace, latestGlobal);
            }

            this._routineCache[key] = compiled;
        }

        return compiled;
    }

    /// <summary>
    /// Immediately invalidates cached compiled AST entries for <paramref name="workspaceId"/>, or all workspaces when <see langword="null"/>.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier to evict, or <see langword="null"/> for global eviction.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous invalidation operation.</returns>
    public async ValueTask InvalidateAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
    {
        lock (this._syncLock)
        {
            if (workspaceId is Guid targetWorkspaceId)
            {
                _ = this._workspaceEpochs.AddOrUpdate(targetWorkspaceId, 1L, static (_, current) => current + 1L);
                foreach (RoutineCacheKey key in this._routineCache.Keys)
                {
                    if (key.WorkspaceId == targetWorkspaceId)
                    {
                        _ = this._routineCache.TryRemove(key, out _);
                    }
                }
            }
            else
            {
                _ = Interlocked.Increment(ref this._globalEpoch);
                this._routineCache.Clear();
            }
        }

        if (this._hybridCache is not null)
        {
            string tag = workspaceId is Guid wsId
                ? GetWorkspaceCacheTag(wsId)
                : "commands:ast:all";
            await this._hybridCache.RemoveByTagAsync(tag, cancellationToken).ConfigureAwait(false);
        }

        this.Invalidated?.Invoke(workspaceId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0)
        {
            return;
        }

        this._subscription?.Dispose();
        this._routineCache.Clear();
    }

    /// <summary>
    /// Handles <see cref="CommandsInvalidatedEvent"/> notifications from <see cref="IEventBus"/>.
    /// </summary>
    /// <param name="evt">The invalidation event payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the invalidation operation.</returns>
    private ValueTask OnCommandsInvalidatedAsync(CommandsInvalidatedEvent evt, CancellationToken cancellationToken) =>
        this.InvalidateAsync(evt.WorkspaceId, cancellationToken);

    /// <summary>
    /// Parses a <see cref="CommandRoutine"/> and compiles every <see cref="ChatSendActionStep"/> template into an immutable AST.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="routineJson">The serialized routine JSON or plain-text response.</param>
    /// <param name="workspaceEpoch">The active workspace invalidation epoch.</param>
    /// <param name="globalEpoch">The active global invalidation epoch.</param>
    /// <returns>A compiled <see cref="CompiledCommandRoutine"/>.</returns>
    private CompiledCommandRoutine CompileRoutine(
        Guid workspaceId,
        Guid commandId,
        string routineJson,
        long workspaceEpoch,
        long globalEpoch)
    {
        _ = Interlocked.Increment(ref this._compilationCount);
        CommandRoutine routine = CommandRoutine.Parse(routineJson);
        IReadOnlyList<string> chatMessages = routine.GetChatMessages();
        List<CompiledRoutineStep> compiledSteps = new(chatMessages.Count);

        foreach (string messageTemplate in chatMessages)
        {
            TemplateParseResult parseResult = TemplateParser.Parse(messageTemplate, this._tagRegistry);
            compiledSteps.Add(new CompiledRoutineStep(messageTemplate, parseResult));
        }

        return new CompiledCommandRoutine(
            workspaceId,
            commandId,
            routineJson,
            compiledSteps.AsReadOnly(),
            workspaceEpoch,
            globalEpoch);
    }

    /// <summary>
    /// Builds the workspace-scoped <see cref="HybridCache"/> tag for command AST tracking.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <returns>The cache tag string.</returns>
    private static string GetWorkspaceCacheTag(Guid workspaceId) => $"commands:ast:{workspaceId:N}";

    /// <summary>
    /// Composite cache key identifying a compiled custom command routine.
    /// </summary>
    /// <param name="WorkspaceId">The owning workspace identifier.</param>
    /// <param name="CommandId">The command entity identifier.</param>
    /// <param name="NormalizedTrigger">The normalized primary trigger.</param>
    private readonly record struct RoutineCacheKey(Guid WorkspaceId, Guid CommandId, string NormalizedTrigger);
}
