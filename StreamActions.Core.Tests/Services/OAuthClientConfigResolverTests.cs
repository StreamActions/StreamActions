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
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class OAuthClientConfigResolverTests
{
    private static readonly Guid _workspaceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _ownerUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    #region ResolveAsync

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsInstanceOnlyIgnoresWorkspaceOverrideAndUsesInstanceCredentials()
    {
        (OAuthClientConfigResolver resolver, FakeInstanceSettingsService settingsService, FakeInstanceOAuthConfigService instanceOAuth, FakeWorkspaceStore workspaceStore, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings.InstanceOAuthAppPolicy = AuthPolicyParser.InstanceDefaultValue;
            instanceOAuth.Credentials["twitch"] = new PlatformOAuthAppCredentials("inst-id", "inst-secret", new Uri("https://panel.example.com/cb"));
            workspaceStore.CustomApps[(_workspaceId, "twitch")] = new PlatformOAuthAppCredentials("ws-id", "ws-secret");

            ResolvedOAuthClientConfig? resolved = await resolver.ResolveAsync("TWITCH", _workspaceId, cancellationToken: TestContext.Current.CancellationToken);

            _ = resolved.Should().NotBeNull();
            _ = resolved!.PlatformId.Should().Be("twitch");
            _ = resolved.ClientId.Should().Be("inst-id");
            _ = resolved.ClientSecret.Should().Be("inst-secret");
            _ = resolved.RedirectUri.Should().Be(new Uri("https://panel.example.com/cb"));
            _ = resolved.IsWorkspaceOverride.Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsOptionalOverrideAndFeatureAllowedReturnsWorkspaceCredentials()
    {
        (OAuthClientConfigResolver resolver, FakeInstanceSettingsService settingsService, FakeInstanceOAuthConfigService instanceOAuth, FakeWorkspaceStore workspaceStore, FakePermissionStore permissionStore, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings.InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceWithOptionalOverride);
            instanceOAuth.Credentials["twitch"] = new PlatformOAuthAppCredentials("inst-id", "inst-secret");
            workspaceStore.CustomApps[(_workspaceId, "twitch")] = new PlatformOAuthAppCredentials("ws-id", "ws-secret");
            GrantGlobalFeature(permissionStore, BuiltInPermissionKeys.FeatureCustomOAuthApp, PermissionState.Allow);

            ResolvedOAuthClientConfig? resolved = await resolver.ResolveAsync("twitch", _workspaceId, cancellationToken: TestContext.Current.CancellationToken);

            _ = resolved.Should().NotBeNull();
            _ = resolved!.ClientId.Should().Be("ws-id");
            _ = resolved.ClientSecret.Should().Be("ws-secret");
            _ = resolved.IsWorkspaceOverride.Should().BeTrue();
            _ = resolved.WorkspaceId.Should().Be(_workspaceId);
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsOptionalOverrideAndFeatureDeniedFallsBackToInstanceCredentials()
    {
        (OAuthClientConfigResolver resolver, FakeInstanceSettingsService settingsService, FakeInstanceOAuthConfigService instanceOAuth, FakeWorkspaceStore workspaceStore, FakePermissionStore permissionStore, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings.InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceWithOptionalOverride);
            instanceOAuth.Credentials["twitch"] = new PlatformOAuthAppCredentials("inst-id", "inst-secret");
            workspaceStore.CustomApps[(_workspaceId, "twitch")] = new PlatformOAuthAppCredentials("ws-id", "ws-secret");
            GrantGlobalFeature(permissionStore, BuiltInPermissionKeys.FeatureCustomOAuthApp, PermissionState.Deny);

            ResolvedOAuthClientConfig? resolved = await resolver.ResolveAsync("twitch", _workspaceId, cancellationToken: TestContext.Current.CancellationToken);

            _ = resolved.Should().NotBeNull();
            _ = resolved!.ClientId.Should().Be("inst-id");
            _ = resolved.ClientSecret.Should().Be("inst-secret");
            _ = resolved.IsWorkspaceOverride.Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsOptionalOverrideAndFeatureNotGrantedFallsBackToInstanceCredentials()
    {
        (OAuthClientConfigResolver resolver, FakeInstanceSettingsService settingsService, FakeInstanceOAuthConfigService instanceOAuth, FakeWorkspaceStore workspaceStore, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings.InstanceOAuthAppPolicy = "instance_with_optional_override";
            instanceOAuth.Credentials["twitch"] = new PlatformOAuthAppCredentials("inst-id", "inst-secret");
            workspaceStore.CustomApps[(_workspaceId, "twitch")] = new PlatformOAuthAppCredentials("ws-id", "ws-secret");

            ResolvedOAuthClientConfig? resolved = await resolver.ResolveAsync("twitch", _workspaceId, cancellationToken: TestContext.Current.CancellationToken);

            _ = resolved.Should().NotBeNull();
            _ = resolved!.ClientId.Should().Be("inst-id");
            _ = resolved.IsWorkspaceOverride.Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsRequireStreamerOAuthAppNeverFallsBackToInstanceCredentials()
    {
        (OAuthClientConfigResolver resolver, FakeInstanceSettingsService settingsService, FakeInstanceOAuthConfigService instanceOAuth, FakeWorkspaceStore workspaceStore, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings.InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.RequireStreamerOAuthApp);
            instanceOAuth.Credentials["twitch"] = new PlatformOAuthAppCredentials("inst-id", "inst-secret");

            ResolvedOAuthClientConfig? withoutWorkspace = await resolver.ResolveAsync("twitch", workspaceId: null, cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? withoutCustomApp = await resolver.ResolveAsync("twitch", _workspaceId, cancellationToken: TestContext.Current.CancellationToken);

            _ = withoutWorkspace.Should().BeNull();
            _ = withoutCustomApp.Should().BeNull();

            workspaceStore.CustomApps[(_workspaceId, "twitch")] = new PlatformOAuthAppCredentials("ws-id", "ws-secret");
            ResolvedOAuthClientConfig? withCustomApp = await resolver.ResolveAsync("twitch", _workspaceId, cancellationToken: TestContext.Current.CancellationToken);

            _ = withCustomApp.Should().NotBeNull();
            _ = withCustomApp!.ClientId.Should().Be("ws-id");
            _ = withCustomApp.ClientSecret.Should().Be("ws-secret");
            _ = withCustomApp.IsWorkspaceOverride.Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncWhenPolicyIsInstanceLoginWithStreamerApiOAuthUsesInstanceForIdentityFlowsAndWorkspaceForApiFlows()
    {
        (OAuthClientConfigResolver resolver, FakeInstanceSettingsService settingsService, FakeInstanceOAuthConfigService instanceOAuth, FakeWorkspaceStore workspaceStore, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings.InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceLoginWithStreamerApiOAuth);
            instanceOAuth.Credentials["twitch"] = new PlatformOAuthAppCredentials("inst-id", "inst-secret");

            ResolvedOAuthClientConfig? loginResult = await resolver.ResolveAsync("twitch", _workspaceId, flow: "login", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? linkResult = await resolver.ResolveAsync("twitch", _workspaceId, flow: "link", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? stepUpResult = await resolver.ResolveAsync("twitch", null, flow: "stepup", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? instanceBotResult = await resolver.ResolveAsync("twitch", null, flow: "instance-bot", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? nullFlowNoWorkspace = await resolver.ResolveAsync("twitch", workspaceId: null, flow: null, cancellationToken: TestContext.Current.CancellationToken);

            _ = loginResult!.ClientId.Should().Be("inst-id");
            _ = loginResult.IsWorkspaceOverride.Should().BeFalse();
            _ = linkResult!.ClientId.Should().Be("inst-id");
            _ = stepUpResult!.ClientId.Should().Be("inst-id");
            _ = instanceBotResult!.ClientId.Should().Be("inst-id");
            _ = nullFlowNoWorkspace!.ClientId.Should().Be("inst-id");

            // Without workspace custom credentials, broadcaster/bot/null-with-workspace never fall back to instance credentials.
            ResolvedOAuthClientConfig? missingBroadcaster = await resolver.ResolveAsync("twitch", _workspaceId, flow: "broadcaster", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? missingBot = await resolver.ResolveAsync("twitch", _workspaceId, flow: "bot", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? missingNullFlowWithWorkspace = await resolver.ResolveAsync("twitch", _workspaceId, flow: null, cancellationToken: TestContext.Current.CancellationToken);

            _ = missingBroadcaster.Should().BeNull();
            _ = missingBot.Should().BeNull();
            _ = missingNullFlowWithWorkspace.Should().BeNull();

            // With workspace custom credentials, broadcaster/bot/null-with-workspace resolve workspace credentials.
            workspaceStore.CustomApps[(_workspaceId, "twitch")] = new PlatformOAuthAppCredentials("ws-id", "ws-secret");
            ResolvedOAuthClientConfig? broadcasterResult = await resolver.ResolveAsync("twitch", _workspaceId, flow: "broadcaster", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? botResult = await resolver.ResolveAsync("twitch", _workspaceId, flow: "bot", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? nullFlowWithWorkspace = await resolver.ResolveAsync("twitch", _workspaceId, flow: null, cancellationToken: TestContext.Current.CancellationToken);

            _ = broadcasterResult!.ClientId.Should().Be("ws-id");
            _ = broadcasterResult.IsWorkspaceOverride.Should().BeTrue();
            _ = botResult!.ClientId.Should().Be("ws-id");
            _ = botResult.IsWorkspaceOverride.Should().BeTrue();
            _ = nullFlowWithWorkspace!.ClientId.Should().Be("ws-id");
            _ = nullFlowWithWorkspace.IsWorkspaceOverride.Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Member", "ResolveAsync")]
    public async Task ResolveAsyncUsesPerPlatformOAuthAppPolicyOverrideWhenConfigured()
    {
        (OAuthClientConfigResolver resolver, FakeInstanceSettingsService settingsService, FakeInstanceOAuthConfigService instanceOAuth, FakeWorkspaceStore workspaceStore, _, InMemoryEventBus eventBus, ServiceProvider provider) = CreateSut();
        await using (provider.ConfigureAwait(false))
        using (eventBus)
        {
            settingsService.Settings.InstanceOAuthAppPolicy = nameof(InstanceOAuthAppPolicy.InstanceOnly);
            AuthPolicyParser.SetPlatformOAuthAppPolicy(settingsService.Settings, "twitch", InstanceOAuthAppPolicy.RequireStreamerOAuthApp);

            instanceOAuth.Credentials["twitch"] = new PlatformOAuthAppCredentials("inst-twitch", "inst-secret");
            instanceOAuth.Credentials["kick"] = new PlatformOAuthAppCredentials("inst-kick", "inst-secret");
            workspaceStore.CustomApps[(_workspaceId, "kick")] = new PlatformOAuthAppCredentials("ws-kick", "ws-secret");

            ResolvedOAuthClientConfig? twitchWithoutCustom = await resolver.ResolveAsync("twitch", _workspaceId, flow: "login", cancellationToken: TestContext.Current.CancellationToken);
            ResolvedOAuthClientConfig? kickUsesInstanceDefault = await resolver.ResolveAsync("kick", _workspaceId, flow: "broadcaster", cancellationToken: TestContext.Current.CancellationToken);

            _ = twitchWithoutCustom.Should().BeNull();
            _ = kickUsesInstanceDefault!.ClientId.Should().Be("inst-kick");
            _ = kickUsesInstanceDefault.IsWorkspaceOverride.Should().BeFalse();
        }
    }

    #endregion

    private static void GrantGlobalFeature(FakePermissionStore permissionStore, string permissionKey, PermissionState state)
    {
        Guid globalGroupId = Guid.NewGuid();
        permissionStore.GlobalGroups[globalGroupId] = new PermissionGroup { Id = globalGroupId, WorkspaceId = null, Name = "Plan" };
        permissionStore.WorkspaceGlobalAssignments.Add(new WorkspaceGlobalGroupAssignment { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, GlobalGroupId = globalGroupId });
        permissionStore.Grants.Add(new PermissionGrant { Id = Guid.NewGuid(), GroupId = globalGroupId, PermissionKey = permissionKey, State = state });
    }

    private static (
        OAuthClientConfigResolver Resolver,
        FakeInstanceSettingsService SettingsService,
        FakeInstanceOAuthConfigService InstanceOAuth,
        FakeWorkspaceStore WorkspaceStore,
        FakePermissionStore PermissionStore,
        InMemoryEventBus EventBus,
        ServiceProvider Provider) CreateSut()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        FakeInstanceSettingsService settingsService = new();
        FakeInstanceOAuthConfigService instanceOAuth = new();
        FakeWorkspaceStore workspaceStore = new();
        FakePermissionStore permissionStore = new();
        permissionStore.Workspaces[_workspaceId] = new Workspace
        {
            Id = _workspaceId,
            OwnerUserId = _ownerUserId,
            Name = "Test Workspace",
            PrimaryPlatformId = "twitch",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        PermissionRegistry registry = new();
        InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionService permissionService = new(permissionStore, registry, cache, eventBus);
        OAuthClientConfigResolver resolver = new(settingsService, instanceOAuth, workspaceStore, permissionService);

        return (resolver, settingsService, instanceOAuth, workspaceStore, permissionStore, eventBus, provider);
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

        public ValueTask<InstanceSettings> GetAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Settings);

        public ValueTask UpdateAsync(InstanceSettings settings, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeInstanceOAuthConfigService : IInstanceOAuthConfigService
    {
        public Dictionary<string, PlatformOAuthAppCredentials> Credentials { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ValueTask<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, PlatformOAuthAppCredentials>>(this.Credentials);

        public ValueTask<PlatformOAuthAppCredentials?> GetAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Credentials.GetValueOrDefault(platformId));

        public ValueTask SetAsync(string platformId, PlatformOAuthAppCredentials credentials, CancellationToken cancellationToken = default)
        {
            this.Credentials[platformId] = credentials;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> RemoveAsync(string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Credentials.Remove(platformId));
    }

    private sealed class FakeWorkspaceStore : IWorkspaceStore
    {
        public Dictionary<(Guid WorkspaceId, string PlatformId), PlatformOAuthAppCredentials> CustomApps { get; } = [];

        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Workspace?>(null);

        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([]);

        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([]);

        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);

        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);

        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>([]);

        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<WorkspacePlatform?>(null);

        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(platform);

        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default)
        {
            foreach (KeyValuePair<(Guid WorkspaceId, string PlatformId), PlatformOAuthAppCredentials> kvp in this.CustomApps)
            {
                if (kvp.Key.WorkspaceId == workspaceId && string.Equals(kvp.Key.PlatformId, platformId, StringComparison.OrdinalIgnoreCase))
                {
                    return ValueTask.FromResult<PlatformOAuthAppCredentials?>(kvp.Value);
                }
            }

            return ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);
        }

        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class FakePermissionStore : IPermissionStore
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];
        public Dictionary<Guid, PermissionGroup> GlobalGroups { get; } = [];
        public List<WorkspaceGlobalGroupAssignment> WorkspaceGlobalAssignments { get; } = [];
        public List<PermissionGrant> Grants { get; } = [];

        public ValueTask<IReadOnlyList<PermissionGroup>> GetGroupsAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGroup>>([]);

        public ValueTask<IReadOnlyList<PermissionGrant>> GetGrantsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PermissionGrant>>([.. this.Grants.Where(g => g.GroupId == groupId)]);

        public ValueTask<PermissionState> EvaluatePermissionAsync(Guid workspaceId, IReadOnlyCollection<Guid> groupIds, string permissionKey, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PermissionState.Inherit);

        public ValueTask<Workspace?> GetWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Workspaces.GetValueOrDefault(workspaceId));

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
