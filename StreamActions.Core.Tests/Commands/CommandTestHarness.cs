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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Models.Metadata;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using StreamActions.Core.Templates;
using System.Globalization;
using System.Text.Json;

namespace StreamActions.Core.Tests.Commands;

/// <summary>
/// Shared in-memory test doubles and builders for <c>StreamActions.Core.Tests.Commands</c>.
/// </summary>
internal static class CommandTestHarness
{
    /// <summary>
    /// Gets the default workspace identifier used across command pipeline tests.
    /// </summary>
    internal static Guid DefaultWorkspaceId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Gets the default workspace owner panel user identifier.
    /// </summary>
    internal static Guid DefaultOwnerUserId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Creates a <see cref="ChatCommand"/> invocation for testing.
    /// </summary>
    /// <param name="trigger">The command trigger without prefix.</param>
    /// <param name="arguments">Optional parsed arguments.</param>
    /// <param name="rawArguments">Optional raw arguments string.</param>
    /// <param name="platformId">Platform identifier.</param>
    /// <param name="userId">Platform user identifier.</param>
    /// <param name="username">Login username.</param>
    /// <param name="displayName">Display name.</param>
    /// <param name="designations">Platform designations.</param>
    /// <param name="messageId">Inbound message identifier.</param>
    /// <param name="channelId">Channel identifier.</param>
    /// <param name="channelName">Channel name.</param>
    /// <param name="workspaceId">Optional workspace identifier.</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    /// <returns>A populated <see cref="ChatCommand"/>.</returns>
    internal static ChatCommand CreateChatCommand(
        string trigger,
        IReadOnlyList<string>? arguments = null,
        string? rawArguments = null,
        string platformId = "twitch",
        string userId = "viewer-1",
        string username = "alice",
        string displayName = "Alice",
        PlatformUserDesignations designations = PlatformUserDesignations.None,
        string messageId = "msg-1",
        string channelId = "chan-1",
        string channelName = "streamer",
        Guid? workspaceId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Guid wsId = workspaceId ?? DefaultWorkspaceId;
        IReadOnlyList<string> args = arguments ?? [];
        string rawArgs = rawArguments ?? string.Join(' ', args);
        ChatUser sender = new(platformId, userId, username, displayName, designations);
        ChatChannel channel = new(wsId, platformId, channelId, channelName);
        string rawText = string.IsNullOrEmpty(rawArgs) ? $"!{trigger}" : $"!{trigger} {rawArgs}";

        return new ChatCommand(
            wsId,
            platformId,
            messageId,
            sender,
            channel,
            rawText,
            DateTimeOffset.UnixEpoch,
            "!",
            trigger,
            args,
            rawArgs,
            ReplyToMessageId: null,
            IsAction: false,
            Metadata: metadata);
    }

    /// <summary>
    /// Creates a <see cref="WhisperCommand"/> invocation for testing.
    /// </summary>
    /// <param name="trigger">The command trigger without prefix.</param>
    /// <param name="arguments">Optional parsed arguments.</param>
    /// <param name="rawArguments">Optional raw arguments string.</param>
    /// <param name="platformId">Platform identifier.</param>
    /// <param name="userId">Platform user identifier.</param>
    /// <param name="username">Login username.</param>
    /// <param name="displayName">Display name.</param>
    /// <param name="designations">Platform designations.</param>
    /// <param name="workspaceId">Optional workspace identifier.</param>
    /// <returns>A populated <see cref="WhisperCommand"/>.</returns>
    internal static WhisperCommand CreateWhisperCommand(
        string trigger,
        IReadOnlyList<string>? arguments = null,
        string? rawArguments = null,
        string platformId = "twitch",
        string userId = "viewer-1",
        string username = "alice",
        string displayName = "Alice",
        PlatformUserDesignations designations = PlatformUserDesignations.None,
        Guid? workspaceId = null)
    {
        Guid wsId = workspaceId ?? DefaultWorkspaceId;
        IReadOnlyList<string> args = arguments ?? [];
        string rawArgs = rawArguments ?? string.Join(' ', args);
        ChatUser sender = new(platformId, userId, username, displayName, designations);
        string rawText = string.IsNullOrEmpty(rawArgs) ? $"!{trigger}" : $"!{trigger} {rawArgs}";

        return new WhisperCommand(
            wsId,
            platformId,
            "whisper-1",
            sender,
            rawText,
            DateTimeOffset.UnixEpoch,
            "!",
            trigger,
            args,
            rawArgs);
    }

    /// <summary>
    /// Creates a wired test environment containing a <see cref="CommandDispatcher"/> and all in-memory stores.
    /// </summary>
    /// <param name="extraHandlers">Optional extra built-in command handlers to register.</param>
    /// <param name="seedDefaultPermissions">Whether to seed default global/workspace permission groups allowing <c>feature.custom_commands</c> and <c>action.run_command</c>.</param>
    /// <param name="counterStoreOverride">Optional <see cref="ICounterStore"/> override (for example, to simulate exceptions).</param>
    /// <returns>A disposable <see cref="DispatcherTestRig"/>.</returns>
    internal static DispatcherTestRig CreateRig(
        IEnumerable<IBuiltInCommandHandler>? extraHandlers = null,
        bool seedDefaultPermissions = true,
        ICounterStore? counterStoreOverride = null) =>
        new(extraHandlers, seedDefaultPermissions, counterStoreOverride);

    /// <summary>
    /// Self-contained test rig bundling a <see cref="CommandDispatcher"/> and in-memory test doubles.
    /// </summary>
    internal sealed class DispatcherTestRig : IAsyncDisposable
    {
        /// <summary>
        /// DI service provider backing <see cref="HybridCache"/>.
        /// </summary>
        private readonly ServiceProvider _cacheProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DispatcherTestRig"/> class.
        /// </summary>
        /// <param name="extraHandlers">Optional additional built-in handlers.</param>
        /// <param name="seedDefaultPermissions">Whether to seed baseline workspace and global permission grants.</param>
        /// <param name="counterStoreOverride">Optional custom counter store.</param>
        public DispatcherTestRig(
            IEnumerable<IBuiltInCommandHandler>? extraHandlers = null,
            bool seedDefaultPermissions = true,
            ICounterStore? counterStoreOverride = null)
        {
            ServiceCollection services = new();
            _ = services.AddHybridCache();
            this._cacheProvider = services.BuildServiceProvider();
            this.HybridCache = this._cacheProvider.GetRequiredService<HybridCache>();

            this.TimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
            this.EventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
            this.TagRegistry = CoreTagRegistryFactory.CreateDefaultRegistry();
            this.CommandRepository = new InMemoryCommandRepository(this.EventBus);
            this.WorkspaceStore = new InMemoryWorkspaceStore();
            this.KeyValueStore = new InMemoryKeyValueStore();
            this.CounterStore = counterStoreOverride ?? new InMemoryCounterStore();
            this.LoyaltyStore = new InMemoryViewerLoyaltyStore();
            this.PermissionStore = new InMemoryPermissionStore();
            this.StreamStatusProvider = new DefaultStreamStatusProvider();
            this.OutboundDispatcher = new RecordingOutboundChatDispatcher();

            string localesDir = Path.Combine(AppContext.BaseDirectory, "locales");
            this.Localizer = new StreamActionsLocalizer(
                this.HybridCache,
                Options.Create(new LocalizationOptions { LocalesDirectory = localesDir }),
                NullLogger<StreamActionsLocalizer>.Instance,
                this.WorkspaceStore,
                this.KeyValueStore);

            this.PermissionRegistry = new PermissionRegistry();
            this.PermissionService = new PermissionService(
                this.PermissionStore,
                this.PermissionRegistry,
                this.HybridCache,
                this.EventBus);

            this.SeedWorkspace(DefaultWorkspaceId, seedDefaultPermissions);

            this.BotCommandHandler = new BotCommandSubCommandHandler(
                this.PermissionService,
                this.CommandRepository,
                this.PermissionStore,
                this.TagRegistry,
                this.Localizer);

            List<IBuiltInCommandHandler> handlers = [this.BotCommandHandler];
            if (extraHandlers is not null)
            {
                handlers.AddRange(extraHandlers);
            }

            this.BuiltInRegistry = new BuiltInCommandRegistry(handlers, this.KeyValueStore, this.Localizer);
            this.AstCache = new CommandAstCache(this.TagRegistry, this.EventBus, this.HybridCache);
            this.CooldownTracker = new CommandCooldownTracker(this.TimeProvider);

            this.Dispatcher = new CommandDispatcher(
                this.BuiltInRegistry,
                this.AstCache,
                this.CooldownTracker,
                this.TagRegistry,
                this.PermissionService,
                this.TimeProvider,
                NullLogger<CommandDispatcher>.Instance,
                this.CommandRepository,
                this.CounterStore,
                this.LoyaltyStore,
                this.WorkspaceStore,
                this.KeyValueStore,
                this.StreamStatusProvider,
                this.OutboundDispatcher,
                this.Localizer);
        }

        /// <summary>Gets the fake time provider.</summary>
        public FakeTimeProvider TimeProvider { get; }

        /// <summary>Gets the in-memory event bus.</summary>
        public InMemoryEventBus EventBus { get; }

        /// <summary>Gets the hybrid cache instance.</summary>
        public HybridCache HybridCache { get; }

        /// <summary>Gets the tag registry.</summary>
        public TagRegistry TagRegistry { get; }

        /// <summary>Gets the in-memory command repository.</summary>
        public InMemoryCommandRepository CommandRepository { get; }

        /// <summary>Gets the in-memory workspace store.</summary>
        public InMemoryWorkspaceStore WorkspaceStore { get; }

        /// <summary>Gets the in-memory key-value store.</summary>
        public InMemoryKeyValueStore KeyValueStore { get; }

        /// <summary>Gets the counter store.</summary>
        public ICounterStore CounterStore { get; }

        /// <summary>Gets the in-memory loyalty store.</summary>
        public InMemoryViewerLoyaltyStore LoyaltyStore { get; }

        /// <summary>Gets the in-memory permission store.</summary>
        public InMemoryPermissionStore PermissionStore { get; }

        /// <summary>Gets the permission registry.</summary>
        public PermissionRegistry PermissionRegistry { get; }

        /// <summary>Gets the permission service.</summary>
        public PermissionService PermissionService { get; }

        /// <summary>Gets the stream status provider.</summary>
        public DefaultStreamStatusProvider StreamStatusProvider { get; }

        /// <summary>Gets the recording outbound chat dispatcher.</summary>
        public RecordingOutboundChatDispatcher OutboundDispatcher { get; }

        /// <summary>Gets the localizer.</summary>
        public StreamActionsLocalizer Localizer { get; }

        /// <summary>Gets the built-in <c>bot.command</c> handler.</summary>
        public BotCommandSubCommandHandler BotCommandHandler { get; }

        /// <summary>Gets the built-in command registry.</summary>
        public BuiltInCommandRegistry BuiltInRegistry { get; }

        /// <summary>Gets the AST cache.</summary>
        public CommandAstCache AstCache { get; }

        /// <summary>Gets the cooldown tracker.</summary>
        public CommandCooldownTracker CooldownTracker { get; }

        /// <summary>Gets the command dispatcher under test.</summary>
        public CommandDispatcher Dispatcher { get; }

        /// <summary>Gets the seeded "Everyone" workspace permission group identifier.</summary>
        public Guid EveryoneGroupId { get; private set; }

        /// <summary>Gets the seeded "Moderators" workspace permission group identifier.</summary>
        public Guid ModeratorGroupId { get; private set; }

        /// <summary>Gets the seeded global plan permission group identifier.</summary>
        public Guid GlobalGroupId { get; private set; }

        /// <summary>
        /// Seeds a workspace with a primary Twitch platform attachment and optional baseline permission groups.
        /// </summary>
        /// <param name="workspaceId">The workspace identifier.</param>
        /// <param name="seedPermissions">Whether to seed global and workspace permission grants.</param>
        private void SeedWorkspace(Guid workspaceId, bool seedPermissions)
        {
            Workspace workspace = new()
            {
                Id = workspaceId,
                OwnerUserId = DefaultOwnerUserId,
                Name = "Test Workspace",
                Slug = "test-workspace",
                PrimaryPlatformId = "twitch",
                BotCulture = "en-US",
                CreatedAtUtc = this.TimeProvider.GetUtcNow(),
            };

            this.WorkspaceStore.Workspaces[workspaceId] = workspace;
            this.WorkspaceStore.Platforms[(workspaceId, "twitch")] = new WorkspacePlatform
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                PlatformId = "twitch",
                Role = PlatformRole.Primary,
                PlatformChannelId = "chan-1",
                Metadata = new PlatformMetadata(),
            };

            this.PermissionStore.Workspaces[workspaceId] = workspace;

            this.GlobalGroupId = Guid.NewGuid();
            this.EveryoneGroupId = Guid.NewGuid();
            this.ModeratorGroupId = Guid.NewGuid();

            this.PermissionStore.GlobalGroups[this.GlobalGroupId] = new PermissionGroup
            {
                Id = this.GlobalGroupId,
                WorkspaceId = null,
                Name = BuiltInPermissionKeys.DefaultWorkspacePlanGroupName,
            };
            this.PermissionStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                GlobalGroupId = this.GlobalGroupId,
            });

            this.PermissionStore.WorkspaceGroups[this.EveryoneGroupId] = new PermissionGroup
            {
                Id = this.EveryoneGroupId,
                WorkspaceId = workspaceId,
                Name = "Everyone",
                MappedPlatformDesignation = "Everyone",
            };

            this.PermissionStore.WorkspaceGroups[this.ModeratorGroupId] = new PermissionGroup
            {
                Id = this.ModeratorGroupId,
                WorkspaceId = workspaceId,
                Name = "Moderators",
                MappedPlatformDesignation = "Moderator",
            };

            if (seedPermissions)
            {
                this.PermissionStore.Grants.Add(new PermissionGrant
                {
                    Id = Guid.NewGuid(),
                    GroupId = this.GlobalGroupId,
                    PermissionKey = BuiltInPermissionKeys.FeatureCustomCommands,
                    State = PermissionState.Allow,
                });
                this.PermissionStore.Grants.Add(new PermissionGrant
                {
                    Id = Guid.NewGuid(),
                    GroupId = this.EveryoneGroupId,
                    PermissionKey = BuiltInPermissionKeys.ActionRunCommand,
                    State = PermissionState.Allow,
                });
                this.PermissionStore.Grants.Add(new PermissionGrant
                {
                    Id = Guid.NewGuid(),
                    GroupId = this.ModeratorGroupId,
                    PermissionKey = BuiltInPermissionKeys.UiCommandsEdit,
                    State = PermissionState.Allow,
                });
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            this.AstCache.Dispose();
            this.EventBus.Dispose();
            await this._cacheProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// In-memory <see cref="ICommandRepository"/> test double.
    /// </summary>
    /// <param name="eventBus">Optional event bus to publish <see cref="CommandsInvalidatedEvent"/> on mutations.</param>
    internal sealed class InMemoryCommandRepository(IEventBus? eventBus = null) : ICommandRepository
    {
        /// <summary>
        /// Stored commands keyed by identifier.
        /// </summary>
        private readonly Dictionary<Guid, CustomCommand> _commands = [];

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<CustomCommand>> GetCommandsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CustomCommand> result = [.. this._commands.Values
                .Where(c => c.WorkspaceId == workspaceId)
                .OrderBy(static c => c.Trigger, StringComparer.OrdinalIgnoreCase)];
            return ValueTask.FromResult(result);
        }

        /// <inheritdoc/>
        public ValueTask<CustomCommand?> GetByTriggerAsync(Guid workspaceId, string trigger, CancellationToken cancellationToken = default)
        {
            string normalized = BuiltInCommandRegistry.NormalizeTrigger(trigger);
            foreach (CustomCommand cmd in this._commands.Values)
            {
                if (cmd.WorkspaceId != workspaceId)
                {
                    continue;
                }

                if (string.Equals(BuiltInCommandRegistry.NormalizeTrigger(cmd.Trigger), normalized, StringComparison.Ordinal))
                {
                    return ValueTask.FromResult<CustomCommand?>(cmd);
                }

                if (!string.IsNullOrWhiteSpace(cmd.AliasesJson))
                {
                    try
                    {
                        string[]? aliases = JsonSerializer.Deserialize<string[]>(cmd.AliasesJson);
                        if (aliases is not null && aliases.Any(a => string.Equals(BuiltInCommandRegistry.NormalizeTrigger(a), normalized, StringComparison.Ordinal)))
                        {
                            return ValueTask.FromResult<CustomCommand?>(cmd);
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore malformed alias JSON in tests.
                    }
                }
            }

            return ValueTask.FromResult<CustomCommand?>(null);
        }

        /// <inheritdoc/>
        public async ValueTask UpsertAsync(CustomCommand command, CancellationToken cancellationToken = default)
        {
            if (command.Id == Guid.Empty)
            {
                command.Id = Guid.NewGuid();
            }

            command.Trigger = BuiltInCommandRegistry.NormalizeTrigger(command.Trigger);
            this._commands[command.Id] = command;

            if (eventBus is not null)
            {
                await eventBus.PublishAsync(new CommandsInvalidatedEvent(command.WorkspaceId), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DeleteAsync(Guid workspaceId, Guid commandId, CancellationToken cancellationToken = default)
        {
            if (this._commands.TryGetValue(commandId, out CustomCommand? existing) && existing.WorkspaceId == workspaceId)
            {
                _ = this._commands.Remove(commandId);
                if (eventBus is not null)
                {
                    await eventBus.PublishAsync(new CommandsInvalidatedEvent(workspaceId), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// In-memory <see cref="IWorkspaceStore"/> test double.
    /// </summary>
    internal sealed class InMemoryWorkspaceStore : IWorkspaceStore
    {
        /// <summary>Gets workspaces keyed by identifier.</summary>
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];

        /// <summary>Gets workspace platform attachments keyed by <c>(WorkspaceId, PlatformId)</c>.</summary>
        public Dictionary<(Guid WorkspaceId, string PlatformId), WorkspacePlatform> Platforms { get; } = [];

        /// <inheritdoc/>
        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values.Where(w => w.OwnerUserId == ownerUserId)]);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values]);

        /// <inheritdoc/>
        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default)
        {
            this.Workspaces[workspace.Id] = workspace;
            if (primaryPlatform is not null)
            {
                this.Platforms[(workspace.Id, primaryPlatform.PlatformId)] = primaryPlatform;
            }

            return ValueTask.FromResult(workspace);
        }

        /// <inheritdoc/>
        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            this.Workspaces[workspace.Id] = workspace;
            return ValueTask.FromResult(workspace);
        }

        /// <inheritdoc/>
        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.Remove(workspaceId));

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>([.. this.Platforms.Values.Where(p => p.WorkspaceId == workspaceId)]);

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default)
        {
            WorkspacePlatform? match = this.Platforms.Values.FirstOrDefault(p =>
                p.WorkspaceId == workspaceId
                && string.Equals(p.PlatformId, platformId, StringComparison.OrdinalIgnoreCase));
            return ValueTask.FromResult(match);
        }

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default)
        {
            this.Platforms[(platform.WorkspaceId, platform.PlatformId)] = platform;
            return ValueTask.FromResult(platform);
        }

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);

        /// <inheritdoc/>
        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Platforms.Remove((workspaceId, platformId)));
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

        /// <inheritdoc/>
        public ValueTask<string?> GetStringAsync(Guid workspaceId, string table, string section, string variable, string? defaultValue = null, CancellationToken ct = default)
        {
            (Guid, string, string, string) key = (workspaceId, table, section, variable);
            return ValueTask.FromResult(this._entries.TryGetValue(key, out string? value) ? value : defaultValue);
        }

        /// <inheritdoc/>
        public ValueTask<bool> GetBoolAsync(Guid workspaceId, string table, string section, string variable, bool defaultValue = false, CancellationToken ct = default) =>
            this._entries.TryGetValue((workspaceId, table, section, variable), out string? val) && bool.TryParse(val, out bool parsed)
                ? ValueTask.FromResult(parsed)
                : ValueTask.FromResult(defaultValue);

        /// <inheritdoc/>
        public ValueTask<int> GetIntAsync(Guid workspaceId, string table, string section, string variable, int defaultValue = 0, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc/>
        public ValueTask<long> GetLongAsync(Guid workspaceId, string table, string section, string variable, long defaultValue = 0L, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc/>
        public ValueTask<float> GetFloatAsync(Guid workspaceId, string table, string section, string variable, float defaultValue = 0f, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc/>
        public ValueTask<double> GetDoubleAsync(Guid workspaceId, string table, string section, string variable, double defaultValue = 0d, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc/>
        public ValueTask SetAsync<T>(Guid workspaceId, string table, string section, string variable, T value, CancellationToken ct = default)
        {
            this._entries[(workspaceId, table, section, variable)] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask<long> IncrementLongAsync(Guid workspaceId, string table, string section, string variable, long delta = 1L, CancellationToken ct = default) =>
            ValueTask.FromResult(delta);

        /// <inheritdoc/>
        public ValueTask<bool> DeleteAsync(Guid workspaceId, string table, string section, string variable, CancellationToken ct = default) =>
            ValueTask.FromResult(this._entries.Remove((workspaceId, table, section, variable)));

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<string>> GetKeysAsync(Guid workspaceId, string table, string section, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>([.. this._entries.Keys
                .Where(k => k.WorkspaceId == workspaceId && k.Table == table && k.Section == section)
                .Select(static k => k.Key)]);
    }

    /// <summary>
    /// In-memory <see cref="ICounterStore"/> test double.
    /// </summary>
    internal sealed class InMemoryCounterStore : ICounterStore
    {
        /// <summary>
        /// Counters keyed by workspace, uppercase counter name, scope mode, and uppercase game category.
        /// </summary>
        private readonly Dictionary<(Guid WorkspaceId, string Name, CounterScopeMode Scope, string Game), WorkspaceCounter> _counters = [];

        /// <inheritdoc/>
        public ValueTask<WorkspaceCounter?> GetAsync(
            Guid workspaceId,
            string counterName,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
            (Guid, string, CounterScopeMode, string) key = (workspaceId, counterName.ToUpperInvariant(), scopeMode, gameCategory.ToUpperInvariant());
            return ValueTask.FromResult(this._counters.GetValueOrDefault(key));
        }

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<WorkspaceCounter>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceCounter>>([.. this._counters.Values.Where(c => c.WorkspaceId == workspaceId)]);

        /// <inheritdoc/>
        public ValueTask<WorkspaceCounter> AdjustAsync(
            Guid workspaceId,
            string counterName,
            long delta,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
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

            counter.CurrentValue += delta;
            counter.UpdatedAtUtc = DateTimeOffset.UtcNow;
            return ValueTask.FromResult(counter);
        }

        /// <inheritdoc/>
        public ValueTask<WorkspaceCounter> SetAsync(
            Guid workspaceId,
            string counterName,
            long value,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
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

        /// <inheritdoc/>
        public ValueTask<bool> DeleteAsync(
            Guid workspaceId,
            string counterName,
            CounterScopeMode scopeMode = CounterScopeMode.Global,
            string gameCategory = "",
            CancellationToken cancellationToken = default)
        {
            (Guid, string, CounterScopeMode, string) key = (workspaceId, counterName.ToUpperInvariant(), scopeMode, gameCategory.ToUpperInvariant());
            return ValueTask.FromResult(this._counters.Remove(key));
        }
    }

    /// <summary>
    /// In-memory <see cref="IViewerLoyaltyStore"/> test double.
    /// </summary>
    internal sealed class InMemoryViewerLoyaltyStore : IViewerLoyaltyStore
    {
        /// <summary>
        /// Viewer profiles keyed by <c>(WorkspaceId, PlatformId, PlatformUserId)</c>.
        /// </summary>
        private readonly Dictionary<(Guid WorkspaceId, string PlatformId, string UserId), WorkspaceViewerProfile> _profiles = [];

        /// <summary>
        /// Seeds or overwrites a viewer's loyalty points balance.
        /// </summary>
        /// <param name="workspaceId">The workspace identifier.</param>
        /// <param name="platformId">The platform identifier.</param>
        /// <param name="platformUserId">The platform user identifier.</param>
        /// <param name="points">The points balance.</param>
        public void SetPoints(Guid workspaceId, string platformId, string platformUserId, long points)
        {
            WorkspaceViewerProfile profile = this.GetOrCreateInternal(workspaceId, platformId, platformUserId, platformUserId, platformUserId);
            profile.Points = points;
        }

        /// <summary>
        /// Reads the current points balance for a viewer.
        /// </summary>
        /// <param name="workspaceId">The workspace identifier.</param>
        /// <param name="platformId">The platform identifier.</param>
        /// <param name="platformUserId">The platform user identifier.</param>
        /// <returns>The current points balance (0 if no profile exists).</returns>
        public long GetPoints(Guid workspaceId, string platformId, string platformUserId) =>
            this._profiles.TryGetValue((workspaceId, platformId.ToUpperInvariant(), platformUserId), out WorkspaceViewerProfile? profile)
                ? profile.Points
                : 0L;

        /// <inheritdoc/>
        public ValueTask<WorkspaceViewerProfile?> GetProfileAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._profiles.GetValueOrDefault((workspaceId, platformId.ToUpperInvariant(), platformUserId)));

        /// <inheritdoc/>
        public ValueTask<WorkspaceViewerProfile?> GetProfileByLoginAsync(Guid workspaceId, string platformId, string login, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
            ArgumentException.ThrowIfNullOrWhiteSpace(login);

            string normalizedPlatform = platformId.Trim();
            string normalizedLogin = login.Trim();

            WorkspaceViewerProfile? match = this._profiles.Values
                .Where(p => p.WorkspaceId == workspaceId
                    && string.Equals(p.PlatformId, normalizedPlatform, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.LastKnownLogin.Trim(), normalizedLogin, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static p => p.UpdatedAtUtc)
                .FirstOrDefault();

            return ValueTask.FromResult(match);
        }

        /// <inheritdoc/>
        public ValueTask<WorkspaceViewerProfile> GetOrCreateProfileAsync(Guid workspaceId, string platformId, string platformUserId, string login, string displayName, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.GetOrCreateInternal(workspaceId, platformId, platformUserId, login, displayName));

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<WorkspaceViewerProfile>> GetTopByPointsAsync(Guid workspaceId, int count = 10, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceViewerProfile>>([.. this._profiles.Values
                .Where(p => p.WorkspaceId == workspaceId)
                .OrderByDescending(static p => p.Points)
                .Take(count)]);

        /// <inheritdoc/>
        public ValueTask<WorkspaceViewerProfile> AdjustPointsAsync(Guid workspaceId, string platformId, string platformUserId, long delta, CancellationToken cancellationToken = default)
        {
            WorkspaceViewerProfile profile = this.GetOrCreateInternal(workspaceId, platformId, platformUserId, platformUserId, platformUserId);
            profile.Points = Math.Max(0L, profile.Points + delta);
            return ValueTask.FromResult(profile);
        }

        /// <inheritdoc/>
        public ValueTask<bool> TrySpendPointsAsync(Guid workspaceId, string platformId, string platformUserId, long cost, CancellationToken cancellationToken = default)
        {
            WorkspaceViewerProfile profile = this.GetOrCreateInternal(workspaceId, platformId, platformUserId, platformUserId, platformUserId);
            if (profile.Points < cost)
            {
                return ValueTask.FromResult(false);
            }

            profile.Points -= cost;
            return ValueTask.FromResult(true);
        }

        /// <inheritdoc/>
        public ValueTask AddWatchTimeAndXpAsync(Guid workspaceId, string platformId, string platformUserId, long watchTimeSecondsDelta, long chatXpDelta, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <summary>
        /// Retrieves an existing profile or initializes a new one.
        /// </summary>
        /// <param name="workspaceId">Workspace identifier.</param>
        /// <param name="platformId">Platform identifier.</param>
        /// <param name="platformUserId">Platform user identifier.</param>
        /// <param name="login">Login username.</param>
        /// <param name="displayName">Display name.</param>
        /// <returns>The profile entity.</returns>
        private WorkspaceViewerProfile GetOrCreateInternal(Guid workspaceId, string platformId, string platformUserId, string login, string displayName)
        {
            (Guid, string, string) key = (workspaceId, platformId.ToUpperInvariant(), platformUserId);
            if (!this._profiles.TryGetValue(key, out WorkspaceViewerProfile? profile))
            {
                profile = new WorkspaceViewerProfile
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    PlatformId = platformId,
                    PlatformUserId = platformUserId,
                    LastKnownLogin = login,
                    LastKnownDisplayName = displayName,
                };
                this._profiles[key] = profile;
            }

            return profile;
        }
    }

    /// <summary>
    /// In-memory <see cref="IPermissionStore"/> test double supporting grant updates via <see cref="SetGrantAsync"/>.
    /// </summary>
    internal sealed class InMemoryPermissionStore : IPermissionStore
    {
        /// <summary>Gets workspaces keyed by identifier.</summary>
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];

        /// <summary>Gets global permission groups keyed by identifier.</summary>
        public Dictionary<Guid, PermissionGroup> GlobalGroups { get; } = [];

        /// <summary>Gets workspace permission groups keyed by identifier.</summary>
        public Dictionary<Guid, PermissionGroup> WorkspaceGroups { get; } = [];

        /// <summary>Gets workspace-to-global-group assignments.</summary>
        public List<WorkspaceGlobalGroupAssignment> WorkspaceGlobalAssignments { get; } = [];

        /// <summary>Gets permission grants.</summary>
        public List<PermissionGrant> Grants { get; } = [];

        /// <summary>Gets permission group memberships.</summary>
        public List<PermissionGroupMember> GroupMembers { get; } = [];

        /// <summary>Gets per-user permission overrides.</summary>
        public List<UserPermissionOverride> UserOverrides { get; } = [];

        /// <summary>Gets platform role roster entries.</summary>
        public List<WorkspacePlatformRoleRoster> RoleRosters { get; } = [];

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroup>>(workspaceId.HasValue
                ? [.. this.WorkspaceGroups.Values.Where(g => g.WorkspaceId == workspaceId.Value)]
                : [.. this.GlobalGroups.Values]);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGrant>>([.. this.Grants.Where(g => g.GroupId == groupId)]);

        /// <inheritdoc/>
        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PermissionState.Inherit);

        /// <inheritdoc/>
        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            HashSet<Guid> assigned = [.. this.WorkspaceGlobalAssignments.Where(a => a.WorkspaceId == workspaceId).Select(a => a.GlobalGroupId)];
            return ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this.GlobalGroups.Values.Where(g => assigned.Contains(g.Id))]);
        }

        /// <inheritdoc/>
        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default)
        {
            HashSet<Guid> wsGroupIds = [.. this.WorkspaceGroups.Values.Where(g => g.WorkspaceId == workspaceId).Select(g => g.Id)];
            return ValueTask.FromResult<IReadOnlyList<Guid>>([.. this.GroupMembers
                .Where(m => wsGroupIds.Contains(m.GroupId)
                    && string.Equals(m.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.GroupId)]);
        }

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserPermissionOverride>>([.. this.UserOverrides.Where(o =>
                o.WorkspaceId == workspaceId
                && string.Equals(o.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(o.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase))]);

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.RoleRosters.FirstOrDefault(r =>
                r.WorkspaceId == workspaceId
                && string.Equals(r.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase)));

        /// <inheritdoc/>
        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default)
        {
            if (group.WorkspaceId.HasValue)
            {
                this.WorkspaceGroups[group.Id] = group;
            }
            else
            {
                this.GlobalGroups[group.Id] = group;
            }

            return ValueTask.FromResult(group);
        }

        /// <inheritdoc/>
        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default)
        {
            _ = this.Grants.RemoveAll(g => g.GroupId == groupId && string.Equals(g.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase));
            if (state != PermissionState.Inherit)
            {
                this.Grants.Add(new PermissionGrant
                {
                    Id = Guid.NewGuid(),
                    GroupId = groupId,
                    PermissionKey = permissionKey,
                    State = state,
                });
            }

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask SetGrantsBatchAsync(Guid groupId, IReadOnlyDictionary<string, PermissionState> grants, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask<PermissionGroup> SeedDefaultGlobalGroupAsync(IReadOnlyCollection<string>? enabledDangerousFeatureKeys = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PermissionGroup { Id = Guid.NewGuid(), Name = BuiltInPermissionKeys.DefaultWorkspacePlanGroupName });

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroupMember>>([.. this.GroupMembers.Where(m => m.GroupId == groupId)]);

        /// <inheritdoc/>
        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    /// <summary>
    /// Recording <see cref="IOutboundChatDispatcher"/> test double capturing all enqueued <see cref="OutboundChatMessage"/> records.
    /// </summary>
    internal sealed class RecordingOutboundChatDispatcher : IOutboundChatDispatcher
    {
        /// <summary>Gets all captured outbound messages in enqueue order.</summary>
        public List<OutboundChatMessage> Messages { get; } = [];

        /// <inheritdoc/>
        public ValueTask EnqueueAsync(OutboundChatMessage message, CancellationToken cancellationToken = default)
        {
            this.Messages.Add(message);
            return ValueTask.CompletedTask;
        }
    }
}
