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

using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Metadata;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class BotCredentialResolverTests
{
    private static readonly Guid _workspaceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid _ownerUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    #region ResolveAsync

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsDefaultOnlyUsesInstanceDefaultBotAndIgnoresWorkspaceOverride()
    {
        (BotCredentialResolver resolver, FakeInstanceSettingsService settings, FakeWorkspaceStore workspaces, FakeCredentialStore creds, _, InMemoryEventBus bus, ServiceProvider sp) = CreateSut();
        await using (sp.ConfigureAwait(false))
        using (bus)
        {
            settings.Settings.InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.DefaultOnly);
            workspaces.Workspaces[_workspaceId] = CreateWorkspace(allowOverride: true);
            workspaces.Platforms[(_workspaceId, "twitch")] = CreatePlatform(useBroadcasterAsBot: true);

            creds.Save(_workspaceId, "twitch", TokenRole.BotChat, new PlatformTokenPayload("ws-bot-token", PlatformUsername: "ws_bot"));
            creds.Save(_workspaceId, "twitch", TokenRole.Broadcaster, new PlatformTokenPayload("ws-broadcaster-token", PlatformUsername: "streamer"));
            creds.Save(null, "twitch", TokenRole.BotChat, new PlatformTokenPayload("root-bot-token", PlatformUsername: "root_bot"));

            BotCredentialResolutionResult result = await resolver.ResolveAsync(_workspaceId, "TWITCH", cancellationToken: TestContext.Current.CancellationToken);

            _ = result.Status.Should().Be(BotCredentialResolutionStatus.Resolved);
            _ = result.Source.Should().Be(BotCredentialSource.InstanceDefaultBot);
            _ = result.Payload!.AccessToken.Should().Be("root-bot-token");
            _ = result.IsCustomOverrideAllowed.Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsDefaultWithOptionalOverrideAndPermittedUsesCustomBotThenBroadcasterFallbackThenRootDefault()
    {
        (BotCredentialResolver resolver, FakeInstanceSettingsService settings, FakeWorkspaceStore workspaces, FakeCredentialStore creds, FakePermissionStore perms, InMemoryEventBus bus, ServiceProvider sp) = CreateSut();
        await using (sp.ConfigureAwait(false))
        using (bus)
        {
            settings.Settings.InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.DefaultWithOptionalOverride);
            workspaces.Workspaces[_workspaceId] = CreateWorkspace(allowOverride: true);
            workspaces.Platforms[(_workspaceId, "twitch")] = CreatePlatform(useBroadcasterAsBot: true);
            GrantGlobalFeature(perms, BuiltInPermissionKeys.FeatureCustomBotAccount, PermissionState.Allow);

            creds.Save(null, "twitch", TokenRole.BotChat, new PlatformTokenPayload("root-bot-token", PlatformUsername: "root_bot"));
            creds.Save(_workspaceId, "twitch", TokenRole.Broadcaster, new PlatformTokenPayload("ws-broadcaster-token", PlatformUsername: "streamer"));
            creds.Save(_workspaceId, "twitch", TokenRole.BotChat, new PlatformTokenPayload("ws-bot-token", PlatformUsername: "custom_bot"));

            // 1. Custom bot present -> WorkspaceCustomBot.
            BotCredentialResolutionResult customResult = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = customResult.Status.Should().Be(BotCredentialResolutionStatus.Resolved);
            _ = customResult.Source.Should().Be(BotCredentialSource.WorkspaceCustomBot);
            _ = customResult.Payload!.AccessToken.Should().Be("ws-bot-token");
            _ = customResult.IsCustomOverrideAllowed.Should().BeTrue();

            // 2. Custom bot removed, UseBroadcasterAsBot=true -> WorkspaceBroadcasterFallback.
            _ = creds.Delete(_workspaceId, "twitch", TokenRole.BotChat);
            BotCredentialResolutionResult broadcasterResult = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = broadcasterResult.Status.Should().Be(BotCredentialResolutionStatus.Resolved);
            _ = broadcasterResult.Source.Should().Be(BotCredentialSource.WorkspaceBroadcasterFallback);
            _ = broadcasterResult.Payload!.AccessToken.Should().Be("ws-broadcaster-token");

            // 3. UseBroadcasterAsBot=false -> falls back to Root Default Bot.
            workspaces.Platforms[(_workspaceId, "twitch")] = CreatePlatform(useBroadcasterAsBot: false);
            BotCredentialResolutionResult defaultFallback = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = defaultFallback.Status.Should().Be(BotCredentialResolutionStatus.Resolved);
            _ = defaultFallback.Source.Should().Be(BotCredentialSource.InstanceDefaultBot);
            _ = defaultFallback.Payload!.AccessToken.Should().Be("root-bot-token");
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsDefaultWithOptionalOverrideAndFeatureDeniedOrWorkspaceDisabledIgnoresWorkspaceBot()
    {
        (BotCredentialResolver resolver, FakeInstanceSettingsService settings, FakeWorkspaceStore workspaces, FakeCredentialStore creds, FakePermissionStore perms, InMemoryEventBus bus, ServiceProvider sp) = CreateSut();
        await using (sp.ConfigureAwait(false))
        using (bus)
        {
            settings.Settings.InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.DefaultWithOptionalOverride);
            workspaces.Workspaces[_workspaceId] = CreateWorkspace(allowOverride: true);
            workspaces.Platforms[(_workspaceId, "twitch")] = CreatePlatform(useBroadcasterAsBot: true);
            GrantGlobalFeature(perms, BuiltInPermissionKeys.FeatureCustomBotAccount, PermissionState.Deny);

            creds.Save(_workspaceId, "twitch", TokenRole.BotChat, new PlatformTokenPayload("ws-bot-token"));
            creds.Save(null, "twitch", TokenRole.BotChat, new PlatformTokenPayload("root-bot-token"));

            BotCredentialResolutionResult deniedFeatureResult = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = deniedFeatureResult.Status.Should().Be(BotCredentialResolutionStatus.Resolved);
            _ = deniedFeatureResult.Source.Should().Be(BotCredentialSource.InstanceDefaultBot);
            _ = deniedFeatureResult.IsCustomOverrideAllowed.Should().BeFalse();

            // Even if feature is allowed, Workspace.AllowCustomBotAccountOverride=false must keep InstanceDefaultBot.
            perms.Grants.Clear();
            GrantGlobalFeature(perms, BuiltInPermissionKeys.FeatureCustomBotAccount, PermissionState.Allow);
            workspaces.Workspaces[_workspaceId] = CreateWorkspace(allowOverride: false);

            BotCredentialResolutionResult workspaceDisabledResult = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = workspaceDisabledResult.Status.Should().Be(BotCredentialResolutionStatus.Resolved);
            _ = workspaceDisabledResult.Source.Should().Be(BotCredentialSource.InstanceDefaultBot);
            _ = workspaceDisabledResult.IsCustomOverrideAllowed.Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsRequireStreamerBotAccountNeverUsesRootBotAndSupportsBroadcasterFallback()
    {
        (BotCredentialResolver resolver, FakeInstanceSettingsService settings, FakeWorkspaceStore workspaces, FakeCredentialStore creds, _, InMemoryEventBus bus, ServiceProvider sp) = CreateSut();
        await using (sp.ConfigureAwait(false))
        using (bus)
        {
            settings.Settings.InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.RequireStreamerBotAccount);
            workspaces.Workspaces[_workspaceId] = CreateWorkspace(allowOverride: false);
            workspaces.Platforms[(_workspaceId, "twitch")] = CreatePlatform(useBroadcasterAsBot: false);
            creds.Save(null, "twitch", TokenRole.BotChat, new PlatformTokenPayload("root-bot-token"));
            creds.Save(_workspaceId, "twitch", TokenRole.Broadcaster, new PlatformTokenPayload("ws-broadcaster-token"));

            BotCredentialResolutionResult awaiting = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = awaiting.Status.Should().Be(BotCredentialResolutionStatus.AwaitingBotAccountAuth);
            _ = awaiting.Source.Should().Be(BotCredentialSource.None);

            workspaces.Platforms[(_workspaceId, "twitch")] = CreatePlatform(useBroadcasterAsBot: true);
            BotCredentialResolutionResult broadcasterFallback = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = broadcasterFallback.Status.Should().Be(BotCredentialResolutionStatus.Resolved);
            _ = broadcasterFallback.Source.Should().Be(BotCredentialSource.WorkspaceBroadcasterFallback);
            _ = broadcasterFallback.Payload!.AccessToken.Should().Be("ws-broadcaster-token");
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenCredentialIsMissingOrKeyRingMismatchedReturnsExpectedStatus()
    {
        (BotCredentialResolver resolver, FakeInstanceSettingsService settings, FakeWorkspaceStore workspaces, FakeCredentialStore creds, _, InMemoryEventBus bus, ServiceProvider sp) = CreateSut();
        await using (sp.ConfigureAwait(false))
        using (bus)
        {
            settings.Settings.InstanceBotAccountPolicy = nameof(InstanceBotAccountPolicy.DefaultOnly);
            workspaces.Workspaces[_workspaceId] = CreateWorkspace(allowOverride: false);

            BotCredentialResolutionResult missingResult = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = missingResult.Status.Should().Be(BotCredentialResolutionStatus.AwaitingBotAccountAuth);

            creds.Save(null, "twitch", TokenRole.BotChat, new PlatformTokenPayload("root-bot-token"), CredentialStatus.KeyRingMismatchRequiresReauth);
            BotCredentialResolutionResult keyRingResult = await resolver.ResolveAsync(_workspaceId, "twitch", cancellationToken: TestContext.Current.CancellationToken);
            _ = keyRingResult.Status.Should().Be(BotCredentialResolutionStatus.KeyRingMismatchRequiresReauth);
        }
    }

    #endregion

    private static Workspace CreateWorkspace(bool allowOverride) => new()
    {
        Id = _workspaceId,
        OwnerUserId = _ownerUserId,
        Name = "Streamer Workspace",
        PrimaryPlatformId = "twitch",
        AllowCustomBotAccountOverride = allowOverride,
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static WorkspacePlatform CreatePlatform(bool useBroadcasterAsBot) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = _workspaceId,
        PlatformId = "twitch",
        Role = PlatformRole.Primary,
        PlatformChannelId = "1001",
        Metadata = new PlatformMetadata
        {
            UseBroadcasterAsBot = useBroadcasterAsBot,
        },
    };

    private static void GrantGlobalFeature(FakePermissionStore permissionStore, string permissionKey, PermissionState state)
    {
        Guid globalGroupId = Guid.NewGuid();
        permissionStore.GlobalGroups[globalGroupId] = new PermissionGroup { Id = globalGroupId, WorkspaceId = null, Name = "Plan" };
        permissionStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = globalGroupId });
        permissionStore.Grants.Add(new PermissionGrant { Id = Guid.NewGuid(), GroupId = globalGroupId, PermissionKey = permissionKey, State = state });
    }

    private static (
        BotCredentialResolver Resolver,
        FakeInstanceSettingsService Settings,
        FakeWorkspaceStore Workspaces,
        FakeCredentialStore Creds,
        FakePermissionStore Perms,
        InMemoryEventBus EventBus,
        ServiceProvider Provider) CreateSut()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        FakeInstanceSettingsService settings = new();
        FakeWorkspaceStore workspaces = new();
        FakeCredentialStore creds = new();
        FakePermissionStore perms = new();
        perms.Workspaces[_workspaceId] = CreateWorkspace(allowOverride: false);

        PermissionRegistry registry = new();
        InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionService permissionService = new(perms, registry, cache, eventBus);
        BotCredentialResolver resolver = new(settings, workspaces, creds, permissionService);

        return (resolver, settings, workspaces, creds, perms, eventBus, provider);
    }

    private sealed class FakeInstanceSettingsService : IInstanceSettingsService
    {
        public InstanceSettings Settings { get; } = new()
        {
            Id = Guid.NewGuid(),
            InstanceOAuthAppPolicy = AuthPolicyParser.InstanceDefaultValue,
            InstanceBotAccountPolicy = AuthPolicyParser.InstanceDefaultValue,
        };

        public event EventHandler<InstanceSettingsChangedEventArgs>? SettingsChanged
        {
            add { }
            remove { }
        }

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Settings);
        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FakeWorkspaceStore : IWorkspaceStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];
        public Dictionary<(Guid WorkspaceId, string PlatformId), WorkspacePlatform> Platforms { get; } = [];

        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values.Where(w => w.OwnerUserId == ownerUserId)]);

        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([.. this.Workspaces.Values]);

        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);

        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);

        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>([.. this.Platforms.Where(kvp => kvp.Key.WorkspaceId == workspaceId).Select(static kvp => kvp.Value)]);

        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default)
        {
            foreach (KeyValuePair<(Guid WorkspaceId, string PlatformId), WorkspacePlatform> kvp in this.Platforms)
            {
                if (kvp.Key.WorkspaceId == workspaceId && string.Equals(kvp.Key.PlatformId, platformId, StringComparison.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult<WorkspacePlatform?>(kvp.Value);
                }
            }

            return ValueTask.FromResult<WorkspacePlatform?>(null);
        }

        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(platform);

        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, bool clearClientSecret = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);

        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class FakeCredentialStore : IPlatformCredentialStore
    {
        private readonly Dictionary<(Guid? WorkspaceId, string PlatformId, TokenRole Role), (PlatformCredential Record, PlatformTokenPayload Payload)> _store = [];

        public void Save(Guid? workspaceId, string platformId, TokenRole role, PlatformTokenPayload payload, CredentialStatus status = CredentialStatus.Active)
        {
            string normalized = platformId.Trim().ToUpperInvariant();
            PlatformCredential record = new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                PlatformId = normalized,
                TokenRole = role,
                Status = status,
                ExpiresAtUtc = payload.ExpiresAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            this._store[(workspaceId, normalized, role)] = (record, payload);
        }

        public bool Delete(Guid? workspaceId, string platformId, TokenRole role) =>
            this._store.Remove((workspaceId, platformId.Trim().ToUpperInvariant(), role));

        public ValueTask<PlatformCredential?> GetCredentialRecordAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._store.TryGetValue((workspaceId, platformId.Trim().ToUpperInvariant(), tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry)
                ? entry.Record
                : null);

        public ValueTask<IReadOnlyList<PlatformCredential>> GetCredentialsForWorkspaceAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([.. this._store.Values.Where(v => v.Record.WorkspaceId == workspaceId).Select(static v => v.Record)]);

        public ValueTask<IReadOnlyList<PlatformCredential>> GetAllCredentialsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([.. this._store.Values.Select(static v => v.Record)]);

        public ValueTask<(bool Found, CredentialStatus Status, PlatformTokenPayload? Payload)> TryGetTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default)
        {
            if (!this._store.TryGetValue((workspaceId, platformId.Trim().ToUpperInvariant(), tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry))
            {
                return ValueTask.FromResult<(bool, CredentialStatus, PlatformTokenPayload?)>((false, CredentialStatus.Revoked, null));
            }

            return entry.Record.Status is CredentialStatus.Revoked or CredentialStatus.KeyRingMismatchRequiresReauth
                ? ValueTask.FromResult<(bool, CredentialStatus, PlatformTokenPayload?)>((true, entry.Record.Status, null))
                : ValueTask.FromResult<(bool, CredentialStatus, PlatformTokenPayload?)>((true, entry.Record.Status, entry.Payload));
        }

        public ValueTask<PlatformCredential> SaveTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, PlatformTokenPayload payload, CredentialStatus status = CredentialStatus.Active, CancellationToken cancellationToken = default)
        {
            this.Save(workspaceId, platformId, tokenRole, payload, status);
            return ValueTask.FromResult(this._store[(workspaceId, platformId.Trim().ToUpperInvariant(), tokenRole)].Record);
        }

        public ValueTask<bool> UpdateStatusAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CredentialStatus status, CancellationToken cancellationToken = default)
        {
            string normalized = platformId.Trim().ToUpperInvariant();
            if (!this._store.TryGetValue((workspaceId, normalized, tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry))
            {
                return ValueTask.FromResult(false);
            }

            entry.Record.Status = status;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> DeleteTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Delete(workspaceId, platformId, tokenRole));
    }

    private sealed class FakePermissionStore : IPermissionStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];
        public Dictionary<Guid, PermissionGroup> GlobalGroups { get; } = [];
        public List<WorkspaceGlobalGroupAssignment> WorkspaceGlobalAssignments { get; } = [];
        public List<PermissionGrant> Grants { get; } = [];

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([]);
        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGrant>>([.. this.Grants.Where(g => g.GroupId == groupId)]);
        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) => ValueTask.FromResult(PermissionState.Inherit);
        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));
        public ValueTask<IReadOnlyList<PermissionGroup>> GetGlobalGroupsForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            HashSet<Guid> assigned = [.. this.WorkspaceGlobalAssignments.Where(a => a.WorkspaceId == workspaceId).Select(a => a.GlobalGroupId)];
            return ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([.. this.GlobalGroups.Values.Where(g => assigned.Contains(g.Id))]);
        }

        public ValueTask AssignWorkspaceToGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RemoveWorkspaceFromGlobalGroupAsync(Guid workspaceId, Guid globalGroupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<Guid>> GetUserGroupIdsAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<Guid>>([]);
        public ValueTask<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<UserPermissionOverride>>([]);
        public ValueTask<WorkspacePlatformRoleRoster?> GetPlatformRoleRosterAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.FromResult<WorkspacePlatformRoleRoster?>(null);
        public ValueTask<PermissionGroup> UpsertGroupAsync(PermissionGroup group, CancellationToken cancellationToken = default) => ValueTask.FromResult(group);
        public ValueTask DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetGrantAsync(Guid groupId, string permissionKey, PermissionState state, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetGrantsBatchAsync(Guid groupId, IReadOnlyDictionary<string, PermissionState> grants, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<PermissionGroup> SeedDefaultGlobalGroupAsync(IReadOnlyCollection<string>? enabledDangerousFeatureKeys = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PermissionGroup { Id = Guid.NewGuid(), Name = "Default Workspace Plan" });
        public ValueTask<IReadOnlyList<PermissionGroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<PermissionGroupMember>>([]);
        public ValueTask AddGroupMemberAsync(PermissionGroupMember member, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask RemoveGroupMemberAsync(Guid groupId, string platformId, string platformUserId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SetUserOverrideAsync(UserPermissionOverride userOverride, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask UpsertRoleRosterAsync(WorkspacePlatformRoleRoster entry, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask SeedBuiltInWorkspaceGroupsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
