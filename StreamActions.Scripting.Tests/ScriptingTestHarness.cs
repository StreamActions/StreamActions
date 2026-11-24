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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Actions;
using StreamActions.Core.Configuration;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Permissions;
using StreamActions.Core.Scripting;
using StreamActions.Core.Services;
using StreamActions.Scripting.Runtime;
using StreamActions.Twitch.Api.Common;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;

namespace StreamActions.Scripting.Tests;

internal sealed class ScriptingTestRig : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly InMemoryEventBus _eventBus;

    public Guid WorkspaceId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Guid OwnerUserId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public FakeTimeProvider FakeTime { get; }
    public InMemoryWorkspaceScriptStore ScriptStore { get; } = new();
    public InMemoryKeyValueStore KeyValueStore { get; } = new();
    public InMemoryPermissionStore PermissionStore { get; } = new();
    public PermissionRegistry PermissionRegistry { get; } = new();
    public PermissionService PermissionService { get; }
    public RuntimeExecutionTracker ExecutionTracker { get; } = new();
    public DefaultStreamStatusProvider StreamStatusProvider { get; } = new();
    public TestInstanceSettingsService InstanceSettingsService { get; } = new();
    public BootstrapOptions BootstrapOptions { get; } = new();
    public StubTwitchSessionLeaseProvider? TwitchSessionProvider { get; set; }
    public ConcurrentQueue<OutboundChatMessage> SentMessages { get; } = new();

    public ScriptingTestRig(TimeProvider? timeProvider = null, StubTwitchSessionLeaseProvider? twitchSessionProvider = null)
    {
        this.FakeTime = timeProvider as FakeTimeProvider ?? new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        this.TwitchSessionProvider = twitchSessionProvider;

        ServiceCollection services = new();
        _ = services.AddHybridCache();
        this._provider = services.BuildServiceProvider();
        HybridCache cache = this._provider.GetRequiredService<HybridCache>();
        this._eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        this.PermissionStore.SeedWorkspace(new Workspace
        {
            Id = this.WorkspaceId,
            OwnerUserId = this.OwnerUserId,
            Name = "Script Workspace",
            Slug = "scripts",
            PrimaryPlatformId = "twitch",
        });

        this.PermissionService = new PermissionService(this.PermissionStore, this.PermissionRegistry, cache, this._eventBus);
    }

    public ClearScriptRuntimeService CreateRuntimeService(ISafeExternalHttpClient? safeHttp = null) =>
        new(
            this.ScriptStore,
            this.KeyValueStore,
            this.ExecutionTracker,
            Options.Create(this.BootstrapOptions),
            this.PermissionService,
            this.StreamStatusProvider,
            this.InstanceSettingsService,
            safeHttp,
            new RecordingOutboundDispatcher(this.SentMessages),
            this.TwitchSessionProvider,
            scopeFactory: null,
            this.FakeTime);

    public void GrantClearScriptFeature(Guid workspaceId, bool includePersistent = false, long? ramQuotaBytes = null)
    {
        PermissionGroup group = new()
        {
            Id = Guid.CreateVersion7(),
            Name = $"Scripting Plan {Guid.NewGuid():N}",
            MaxClearScriptTotalRamBytes = ramQuotaBytes,
        };

        List<PermissionGrant> grants =
        [
            new PermissionGrant
            {
                Id = Guid.CreateVersion7(),
                GroupId = group.Id,
                PermissionKey = BuiltInPermissionKeys.FeatureScriptingClearScript,
                State = PermissionState.Allow,
            },
        ];

        if (includePersistent)
        {
            grants.Add(new PermissionGrant
            {
                Id = Guid.CreateVersion7(),
                GroupId = group.Id,
                PermissionKey = BuiltInPermissionKeys.FeatureScriptingClearScriptPersistent,
                State = PermissionState.Allow,
            });
        }

        this.PermissionStore.SeedGlobalGroup(group, [.. grants]);
        this.PermissionStore.AssignGlobalGroup(workspaceId, group.Id);
    }

    public void Dispose()
    {
        this._eventBus.Dispose();
        this._provider.Dispose();
    }

    internal sealed class RecordingOutboundDispatcher(ConcurrentQueue<OutboundChatMessage> queue) : IOutboundChatDispatcher
    {
        public ValueTask EnqueueAsync(OutboundChatMessage message, CancellationToken cancellationToken = default)
        {
            queue.Enqueue(message);
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class StubTwitchSessionLeaseProvider(TwitchSession session) : ITwitchSessionLeaseProvider, IDisposable
    {
        private readonly Lease _lease = new(session);

        public ValueTask<ITwitchSessionLease?> AcquireSessionAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ITwitchSessionLease?>(this._lease);

        public void Dispose() => this._lease.Dispose();

        private sealed class Lease(TwitchSession session) : ITwitchSessionLease
        {
            public TwitchSession Session => session;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    internal sealed class TestInstanceSettingsService : IInstanceSettingsService
    {
        public InstanceSettings Current { get; set; } = new();
        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged;
        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Current);
        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default)
        {
            this.Current = settings;
            this.SettingsChanged?.Invoke(this, new InstanceSettingsChangedEventArgs(settings));
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class InMemoryWorkspaceScriptStore : IWorkspaceScriptStore
    {
        private readonly ConcurrentDictionary<Guid, WorkspaceScript> _scripts = new();

        public ValueTask<IReadOnlyList<WorkspaceScript>> GetScriptsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceScript>>([.. this._scripts.Values.Where(s => s.WorkspaceId == workspaceId)]);

        public ValueTask<IReadOnlyList<WorkspaceScript>> GetPermanentlyLoadedScriptsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceScript>>([.. this._scripts.Values.Where(static s => s.IsEnabled && s.IsPermanentlyLoaded)]);

        public ValueTask<WorkspaceScript?> GetByIdAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._scripts.TryGetValue(scriptId, out WorkspaceScript? s) && s.WorkspaceId == workspaceId ? s : null);

        public ValueTask<WorkspaceScript?> GetByPathAsync(Guid workspaceId, string path, CancellationToken cancellationToken = default)
        {
            string normalized = path.Trim().Replace('\\', '/').TrimStart('/');
            return ValueTask.FromResult(this._scripts.Values.FirstOrDefault(s =>
                s.WorkspaceId == workspaceId
                && string.Equals(s.Path.Trim().Replace('\\', '/').TrimStart('/'), normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public ValueTask<WorkspaceScript> UpsertAsync(WorkspaceScript script, CancellationToken cancellationToken = default)
        {
            if (script.Id == Guid.Empty)
            {
                script.Id = Guid.CreateVersion7();
            }

            script.Path = script.Path.Trim().Replace('\\', '/').TrimStart('/');
            this._scripts[script.Id] = script;
            return ValueTask.FromResult(script);
        }

        public ValueTask<bool> DeleteAsync(Guid workspaceId, Guid scriptId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._scripts.TryGetValue(scriptId, out WorkspaceScript? s) && s.WorkspaceId == workspaceId && this._scripts.TryRemove(scriptId, out _));
    }

    internal sealed class InMemoryKeyValueStore : IWorkspaceKeyValueStore
    {
        private readonly ConcurrentDictionary<(Guid WorkspaceId, string Table, string Section, string Var), string?> _entries = new();

        public ValueTask<string?> GetStringAsync(Guid workspaceId, string table, string section, string variable, string? defaultValue = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? v) ? v : defaultValue);

        public ValueTask<bool> GetBoolAsync(Guid workspaceId, string table, string section, string variable, bool defaultValue = false, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? v) && bool.TryParse(v, out bool b) ? b : defaultValue);

        public ValueTask<int> GetIntAsync(Guid workspaceId, string table, string section, string variable, int defaultValue = 0, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : defaultValue);

        public ValueTask<long> GetLongAsync(Guid workspaceId, string table, string section, string variable, long defaultValue = 0, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : defaultValue);

        public ValueTask<float> GetFloatAsync(Guid workspaceId, string table, string section, string variable, float defaultValue = 0, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : defaultValue);

        public ValueTask<double> GetDoubleAsync(Guid workspaceId, string table, string section, string variable, double defaultValue = 0, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : defaultValue);

        public ValueTask SetAsync<T>(Guid workspaceId, string table, string section, string variable, T value, CancellationToken cancellationToken = default)
        {
            this._entries[(workspaceId, table, section, variable)] = Convert.ToString(value, CultureInfo.InvariantCulture);
            return ValueTask.CompletedTask;
        }

        public ValueTask<long> IncrementLongAsync(Guid workspaceId, string table, string section, string variable, long delta = 1, CancellationToken cancellationToken = default)
        {
            string? next = this._entries.AddOrUpdate(
                (workspaceId, table, section, variable),
                _ => delta.ToString(CultureInfo.InvariantCulture),
                (_, existing) =>
                {
                    long cur = long.TryParse(existing, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0L;
                    return (cur + delta).ToString(CultureInfo.InvariantCulture);
                });
            return ValueTask.FromResult(long.Parse(next!, CultureInfo.InvariantCulture));
        }

        public ValueTask<bool> DeleteAsync(Guid workspaceId, string table, string section, string variable, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._entries.TryRemove((workspaceId, table, section, variable), out _));

        public ValueTask<IReadOnlyList<string>> GetKeysAsync(Guid workspaceId, string table, string section, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>([.. this._entries.Keys.Where(k => k.WorkspaceId == workspaceId && k.Table == table && k.Section == section).Select(static k => k.Var)]);
    }

    internal sealed class InMemoryPermissionStore : IPermissionStore
    {
        private readonly ConcurrentDictionary<Guid, Workspace> _workspaces = new();
        private readonly ConcurrentDictionary<Guid, PermissionGroup> _groups = new();
        private readonly ConcurrentDictionary<Guid, List<PermissionGrant>> _grants = new();
        private readonly ConcurrentDictionary<Guid, List<Guid>> _workspaceGlobalAssignments = new();

        public void SeedWorkspace(Workspace workspace) => this._workspaces[workspace.Id] = workspace;

        public void SeedGlobalGroup(PermissionGroup group, params PermissionGrant[] grants)
        {
            group.WorkspaceId = null;
            this._groups[group.Id] = group;
            this._grants[group.Id] = [.. grants];
        }

        public void AssignGlobalGroup(Guid workspaceId, Guid groupId)
        {
            List<Guid> list = this._workspaceGlobalAssignments.GetOrAdd(workspaceId, static _ => []);
            if (!list.Contains(groupId))
            {
                list.Add(groupId);
            }
        }

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this._groups.Values.Where(g => g.WorkspaceId == workspaceId)]);

        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGrant>>(this._grants.TryGetValue(groupId, out List<PermissionGrant>? g) ? [.. g] : []);

        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PermissionState.Inherit);

        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._workspaces.TryGetValue(workspaceId, out Workspace? ws) ? ws : null);

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => !this._workspaceGlobalAssignments.TryGetValue(workspaceId, out List<Guid>? assignedIds)
                ? ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([])
                : ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. assignedIds
                .Select(id => this._groups.TryGetValue(id, out PermissionGroup? g) ? g : null)
                .Where(static g => g is not null)!]);

        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default)
        {
            this.AssignGlobalGroup(workspaceId, globalGroupId);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<Guid>>([]);
        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<UserPermissionOverride>>([]);
        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<WorkspacePlatformRoleRoster?>(null);
        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default) => ValueTask.FromResult(group);
        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetGrantsBatchAsync(Guid groupId, IReadOnlyDictionary<string, PermissionState> grants, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<PermissionGroup> SeedDefaultGlobalGroupAsync(IReadOnlyCollection<string>? enabledDangerousFeatureKeys = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(new PermissionGroup());
        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGroupMember>>([]);
        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}

internal sealed class EphemeralKestrelServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    public Uri BaseUri { get; }
    public Uri HelixBaseUri { get; }
    public ConcurrentQueue<(string Path, string? AuthHeader)> Requests { get; } = new();

    private EphemeralKestrelServer(WebApplication app, Uri baseUri)
    {
        this._app = app;
        this.BaseUri = baseUri;
        this.HelixBaseUri = new Uri(baseUri, "/helix/");
    }

    public static async Task<EphemeralKestrelServer> StartAsync(Func<HttpContext, Task> handler)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseKestrel(static options => options.Listen(IPAddress.Loopback, 0));
        WebApplication app = builder.Build();
        _ = app.Use(async (HttpContext ctx, RequestDelegate _) => await handler(ctx).ConfigureAwait(false));
        await app.StartAsync();

        IServerAddressesFeature feature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        Uri baseUri = new(feature.Addresses.First().TrimEnd('/'));
        return new EphemeralKestrelServer(app, baseUri);
    }

    public async ValueTask DisposeAsync()
    {
        await this._app.StopAsync();
        await this._app.DisposeAsync();
    }
}
