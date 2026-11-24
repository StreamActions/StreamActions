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

using Microsoft.Extensions.Localization;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Templates;
using System.Globalization;

namespace StreamActions.Core.Tests.Templates;

/// <summary>
/// Shared in-memory test doubles and context builders for template engine unit tests.
/// </summary>
internal static class TemplateTestHarness
{
    /// <summary>
    /// Creates a <see cref="ChatCommand"/> invocation for testing.
    /// </summary>
    /// <param name="trigger">The command trigger.</param>
    /// <param name="arguments">The parsed command arguments.</param>
    /// <param name="rawArguments">Optional raw arguments string.</param>
    /// <param name="platformId">The platform identifier.</param>
    /// <param name="senderUsername">The sender login username.</param>
    /// <param name="senderDisplayName">The sender display name.</param>
    /// <param name="channelName">The channel name.</param>
    /// <param name="workspaceId">Optional workspace identifier.</param>
    /// <returns>A populated <see cref="ChatCommand"/>.</returns>
    internal static ChatCommand CreateChatCommand(
        string trigger = "test",
        IReadOnlyList<string>? arguments = null,
        string? rawArguments = null,
        string platformId = "twitch",
        string senderUsername = "alice",
        string senderDisplayName = "Alice",
        string channelName = "streamer",
        Guid? workspaceId = null)
    {
        Guid wsId = workspaceId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        IReadOnlyList<string> args = arguments ?? [];
        string rawArgs = rawArguments ?? string.Join(' ', args);
        ChatUser user = new(platformId, "user-42", senderUsername, senderDisplayName);
        ChatChannel channel = new(wsId, platformId, "chan-99", channelName);
        string rawText = string.IsNullOrEmpty(rawArgs) ? $"!{trigger}" : $"!{trigger} {rawArgs}";

        return new ChatCommand(
            wsId,
            platformId,
            "msg-1",
            user,
            channel,
            rawText,
            DateTimeOffset.UnixEpoch,
            "!",
            trigger,
            args,
            rawArgs);
    }

    /// <summary>
    /// Creates a <see cref="TagEvaluationContext"/> using the default Core tag registry.
    /// </summary>
    /// <param name="message">Optional message envelope override.</param>
    /// <param name="registry">Optional tag registry override.</param>
    /// <param name="dependencies">Optional typed dependencies.</param>
    /// <param name="currentGameCategory">Optional current game/category.</param>
    /// <param name="boundCounterName">Optional bound counter name.</param>
    /// <param name="counterUsageBehavior">Bound counter usage behavior.</param>
    /// <param name="boundCounterScopeMode">Optional explicit bound counter scope.</param>
    /// <param name="canMutateBoundCounter">Whether the actor can mutate the bound counter.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    /// <param name="chainDepth">Command chain depth.</param>
    /// <param name="visitedCommandTriggers">Visited command triggers.</param>
    /// <returns>A configured <see cref="TagEvaluationContext"/>.</returns>
    internal static TagEvaluationContext CreateContext(
        IMessageEnvelope? message = null,
        ITagRegistry? registry = null,
        TagEvaluationDependencies? dependencies = null,
        string? currentGameCategory = null,
        string? boundCounterName = null,
        CounterUsageBehavior counterUsageBehavior = CounterUsageBehavior.None,
        CounterScopeMode? boundCounterScopeMode = null,
        bool canMutateBoundCounter = false,
        TimeProvider? timeProvider = null,
        int chainDepth = 0,
        IEnumerable<string>? visitedCommandTriggers = null) => new(
            message ?? CreateChatCommand(),
            registry ?? CoreTagRegistryFactory.CreateDefaultRegistry(),
            currentGameCategory: currentGameCategory,
            boundCounterName: boundCounterName,
            counterUsageBehavior: counterUsageBehavior,
            boundCounterScopeMode: boundCounterScopeMode,
            canMutateBoundCounter: canMutateBoundCounter,
            dependencies: dependencies,
            timeProvider: timeProvider,
            chainDepth: chainDepth,
            visitedCommandTriggers: visitedCommandTriggers);

    /// <summary>
    /// In-memory <see cref="ICounterStore"/> test double supporting Global and PerGame counters.
    /// </summary>
    internal sealed class InMemoryCounterStore : ICounterStore
    {
        /// <summary>
        /// Stored counters keyed by workspace, counter name, scope mode, and game category.
        /// </summary>
        private readonly Dictionary<(Guid WorkspaceId, string Name, CounterScopeMode Scope, string Game), WorkspaceCounter> _counters = [];

        /// <summary>
        /// Gets the total number of mutating calls (<see cref="AdjustAsync"/>, <see cref="SetAsync"/>, <see cref="DeleteAsync"/>).
        /// </summary>
        internal int MutationCount { get; private set; }

        /// <inheritdoc />
        public ValueTask<WorkspaceCounter?> GetAsync(
            Guid workspaceId,
            string counterName,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
            (Guid, string, CounterScopeMode, string) key = (workspaceId, counterName.ToUpperInvariant(), scopeMode, gameCategory.ToUpperInvariant());
            _ = this._counters.TryGetValue(key, out WorkspaceCounter? counter);
            return ValueTask.FromResult(counter);
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<WorkspaceCounter>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WorkspaceCounter> result = [.. this._counters.Values.Where(c => c.WorkspaceId == workspaceId)];
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc />
        public ValueTask<WorkspaceCounter> AdjustAsync(
            Guid workspaceId,
            string counterName,
            long delta,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
            this.MutationCount++;
            (Guid, string, CounterScopeMode, string) key = (workspaceId, counterName.ToUpperInvariant(), scopeMode, gameCategory.ToUpperInvariant());
            if (!this._counters.TryGetValue(key, out WorkspaceCounter? counter))
            {
                counter = new WorkspaceCounter
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    CounterName = counterName,
                    ScopeMode = scopeMode,
                    GameCategory = gameCategory,
                    CurrentValue = 0,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
                this._counters[key] = counter;
            }

            counter.CurrentValue += delta;
            counter.UpdatedAtUtc = DateTimeOffset.UtcNow;
            return ValueTask.FromResult(counter);
        }

        /// <inheritdoc />
        public ValueTask<WorkspaceCounter> SetAsync(
            Guid workspaceId,
            string counterName,
            long value,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
            this.MutationCount++;
            (Guid, string, CounterScopeMode, string) key = (workspaceId, counterName.ToUpperInvariant(), scopeMode, gameCategory.ToUpperInvariant());
            if (!this._counters.TryGetValue(key, out WorkspaceCounter? counter))
            {
                counter = new WorkspaceCounter
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    CounterName = counterName,
                    ScopeMode = scopeMode,
                    GameCategory = gameCategory,
                };
                this._counters[key] = counter;
            }

            counter.CurrentValue = value;
            counter.UpdatedAtUtc = DateTimeOffset.UtcNow;
            return ValueTask.FromResult(counter);
        }

        /// <inheritdoc />
        public ValueTask<bool> DeleteAsync(
            Guid workspaceId,
            string counterName,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
            this.MutationCount++;
            (Guid, string, CounterScopeMode, string) key = (workspaceId, counterName.ToUpperInvariant(), scopeMode, gameCategory.ToUpperInvariant());
            return ValueTask.FromResult(this._counters.Remove(key));
        }
    }

    /// <summary>
    /// In-memory <see cref="IWorkspaceKeyValueStore"/> test double.
    /// </summary>
    internal sealed class InMemoryKeyValueStore : IWorkspaceKeyValueStore
    {
        /// <summary>
        /// Stored values keyed by workspace, table, section, and variable.
        /// </summary>
        private readonly Dictionary<(Guid WorkspaceId, string Table, string Section, string Key), string> _entries = [];

        /// <inheritdoc />
        public ValueTask<string?> GetStringAsync(Guid workspaceId, string table, string section, string variable, string? defaultValue = null, CancellationToken ct = default)
        {
            (Guid, string, string, string) key = (workspaceId, table, section, variable);
            return ValueTask.FromResult(this._entries.TryGetValue(key, out string? value) ? value : defaultValue);
        }

        /// <inheritdoc />
        public ValueTask<bool> GetBoolAsync(Guid workspaceId, string table, string section, string variable, bool defaultValue = false, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<int> GetIntAsync(Guid workspaceId, string table, string section, string variable, int defaultValue = 0, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<long> GetLongAsync(Guid workspaceId, string table, string section, string variable, long defaultValue = 0L, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<float> GetFloatAsync(Guid workspaceId, string table, string section, string variable, float defaultValue = 0f, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<double> GetDoubleAsync(Guid workspaceId, string table, string section, string variable, double defaultValue = 0d, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask SetAsync<T>(Guid workspaceId, string table, string section, string variable, T value, CancellationToken ct = default)
        {
            (Guid, string, string, string) key = (workspaceId, table, section, variable);
            this._entries[key] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask<long> IncrementLongAsync(Guid workspaceId, string table, string section, string variable, long delta = 1L, CancellationToken ct = default) =>
            ValueTask.FromResult(delta);

        /// <inheritdoc />
        public ValueTask<bool> DeleteAsync(Guid workspaceId, string table, string section, string variable, CancellationToken ct = default) =>
            ValueTask.FromResult(this._entries.Remove((workspaceId, table, section, variable)));

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<string>> GetKeysAsync(Guid workspaceId, string table, string section, CancellationToken ct = default)
        {
            IReadOnlyList<string> keys =
            [
                .. this._entries.Keys
                    .Where(k => k.WorkspaceId == workspaceId
                        && string.Equals(k.Table, table, StringComparison.Ordinal)
                        && string.Equals(k.Section, section, StringComparison.Ordinal))
                    .Select(static k => k.Key),
            ];
            return ValueTask.FromResult(keys);
        }
    }

    /// <summary>
    /// Stub <see cref="ISafeExternalHttpClient"/> capturing outgoing requests and returning configurable responses.
    /// </summary>
    /// <param name="handler">Delegate generating responses or throwing <see cref="SafeExternalHttpException"/>.</param>
    internal sealed class StubSafeExternalHttpClient(Func<SafeExternalHttpRequest, SafeExternalHttpResponse> handler) : ISafeExternalHttpClient
    {
        /// <summary>
        /// Captured requests in invocation order.
        /// </summary>
        internal List<SafeExternalHttpRequest> Requests { get; } = [];

        /// <inheritdoc />
        public ValueTask<SafeExternalHttpResponse> SendAsync(SafeExternalHttpRequest request, CancellationToken cancellationToken = default)
        {
            this.Requests.Add(request);
            return ValueTask.FromResult(handler(request));
        }
    }

    /// <summary>
    /// Stub <see cref="ICommandDispatcher"/> recording dispatched invocations.
    /// </summary>
    /// <param name="handler">Optional response delegate.</param>
    internal sealed class StubCommandDispatcher(Func<ICommandInvocation, CommandDispatchResult>? handler = null) : ICommandDispatcher
    {
        /// <summary>
        /// Captured command invocations in dispatch order.
        /// </summary>
        internal List<ICommandInvocation> Invocations { get; } = [];

        /// <inheritdoc />
        public ValueTask<CommandDispatchResult> DispatchAsync(ICommandInvocation invocation, CancellationToken cancellationToken = default)
        {
            this.Invocations.Add(invocation);
            return ValueTask.FromResult(handler is not null
                ? handler(invocation)
                : CommandDispatchResult.Success(invocation.Trigger));
        }
    }

    /// <summary>
    /// Stub <see cref="ICommandRepository"/> returning an in-memory list of <see cref="CustomCommand"/> entities.
    /// </summary>
    /// <param name="commands">The configured custom commands.</param>
    internal sealed class StubCommandRepository(IReadOnlyList<CustomCommand> commands) : ICommandRepository
    {
        /// <inheritdoc />
        public ValueTask<IReadOnlyList<CustomCommand>> GetCommandsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<CustomCommand>>([.. commands.Where(c => c.WorkspaceId == workspaceId)]);

        /// <inheritdoc />
        public ValueTask<CustomCommand?> GetByTriggerAsync(Guid workspaceId, string trigger, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(commands.FirstOrDefault(c => c.WorkspaceId == workspaceId && string.Equals(c.Trigger, trigger, StringComparison.OrdinalIgnoreCase)));

        /// <inheritdoc />
        public ValueTask UpsertAsync(CustomCommand command, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask DeleteAsync(Guid workspaceId, Guid commandId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    /// <summary>
    /// Stub <see cref="IStreamStatusProvider"/> returning a configurable game/category.
    /// </summary>
    /// <param name="currentGameCategory">The current game or category.</param>
    internal sealed class StubStreamStatusProvider(string? currentGameCategory) : IStreamStatusProvider
    {
        /// <inheritdoc />
        public ValueTask<bool> IsStreamLiveAsync(Guid workspaceId, string? platformId = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(!string.IsNullOrWhiteSpace(currentGameCategory));

        /// <inheritdoc />
        public ValueTask<string?> GetCurrentGameCategoryAsync(Guid workspaceId, string? platformId = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(currentGameCategory);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<string>> GetActiveStreamPlatformIdsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>(["twitch"]);
    }

    /// <summary>
    /// Stub <see cref="IStreamActionsLocalizer"/> returning localized entries from an in-memory dictionary.
    /// </summary>
    /// <param name="strings">Dictionary mapping dotted keys to localized values.</param>
    internal sealed class StubLocalizer(IReadOnlyDictionary<string, string> strings) : IStreamActionsLocalizer
    {
        /// <inheritdoc />
        public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

        /// <inheritdoc />
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: true);

        /// <inheritdoc />
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        /// <inheritdoc />
        public ValueTask<string?> GetStringAsync(Guid workspaceId, string module, string key, string? cultureOverride = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(strings.TryGetValue(key, out string? value) ? value : null);

        /// <inheritdoc />
        public ValueTask<string?> GetResponseTemplateAsync(Guid workspaceId, string module, string key, string? cultureOverride = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>(null);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<LocalizedCommandDefinition>> GetCommandDefinitionsAsync(Guid workspaceId, string? cultureOverride = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<LocalizedCommandDefinition>>([]);

        /// <inheritdoc />
        public ValueTask<string?> ResolveCommandIdAsync(Guid workspaceId, string trigger, string? cultureOverride = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>(null);

        /// <inheritdoc />
        public ValueTask SetWorkspaceOverrideAsync(Guid workspaceId, string module, string key, string value, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask<bool> RemoveWorkspaceOverrideAsync(Guid workspaceId, string module, string key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        /// <inheritdoc />
        public ValueTask SetWorkspaceCommandTriggersAsync(Guid workspaceId, string commandId, IReadOnlyCollection<string> triggers, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask InvalidateWorkspaceCacheAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<string>> GetAvailableCulturesAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>(["en-US"]);

        /// <inheritdoc />
        public void ReloadCatalogs()
        {
        }
    }
}
