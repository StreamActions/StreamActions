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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Web.Hubs;
using StreamActions.Web.Security;
using System.Security.Claims;
using Xunit;
using XunitTestContext = Xunit.TestContext;

namespace StreamActions.Web.Tests;

/// <summary>
/// Unit tests for <see cref="CommandHub"/> and <see cref="CommandHubEventBridge"/>.
/// </summary>
public sealed class CommandHubTests
{
    #region JoinWorkspaceAsync

    /// <summary>
    /// Verifies that <see cref="CommandHub.JoinWorkspaceAsync"/> throws <see cref="HubException"/> when the workspace ID is empty.
    /// </summary>
    [Fact]
    [Trait("Member", "JoinWorkspaceAsync")]
    public async Task JoinWorkspaceAsync_WhenWorkspaceIdIsEmpty_ThrowsHubException()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        PanelUser user = new() { Id = userId, DisplayName = "streamer" };
        StubPanelUserStore userStore = new();
        userStore.Users[userId] = user;
        StubWorkspaceAccessService accessService = new([]);
        TrackingGroupManager groups = new();

        using CommandHub hub = new(userStore, accessService)
        {
            Context = new StubHubCallerContext(AuthClaims.BuildClaimsPrincipal(user), "conn-empty", ct),
            Groups = groups,
        };

        Func<Task> act = () => hub.JoinWorkspaceAsync(Guid.Empty);
        _ = await act.Should().ThrowAsync<HubException>();
        _ = groups.AddedConnections.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CommandHub.JoinWorkspaceAsync"/> throws <see cref="HubException"/> when the caller is unauthenticated.
    /// </summary>
    [Fact]
    [Trait("Member", "JoinWorkspaceAsync")]
    public async Task JoinWorkspaceAsync_WhenUserIsUnauthenticated_ThrowsHubException()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        StubPanelUserStore userStore = new();
        StubWorkspaceAccessService accessService = new([]);
        TrackingGroupManager groups = new();

        using CommandHub hub = new(userStore, accessService)
        {
            Context = new StubHubCallerContext(new ClaimsPrincipal(new ClaimsIdentity()), "conn-unauth", ct),
            Groups = groups,
        };

        Func<Task> act = () => hub.JoinWorkspaceAsync(workspaceId);
        _ = await act.Should().ThrowAsync<HubException>();
        _ = groups.AddedConnections.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CommandHub.JoinWorkspaceAsync"/> throws <see cref="HubException"/> when the user lacks workspace access.
    /// </summary>
    [Fact]
    [Trait("Member", "JoinWorkspaceAsync")]
    public async Task JoinWorkspaceAsync_WhenWorkspaceIsUnauthorized_ThrowsHubException()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        Guid allowedWorkspaceId = Guid.NewGuid();
        Guid forbiddenWorkspaceId = Guid.NewGuid();

        PanelUser user = new() { Id = userId, DisplayName = "streamer" };
        StubPanelUserStore userStore = new();
        userStore.Users[userId] = user;
        userStore.Identities[userId] = [];

        StubWorkspaceAccessService accessService = new(new Dictionary<Guid, HashSet<Guid>>
        {
            [userId] = [allowedWorkspaceId],
        });
        TrackingGroupManager groups = new();

        using CommandHub hub = new(userStore, accessService)
        {
            Context = new StubHubCallerContext(AuthClaims.BuildClaimsPrincipal(user), "conn-forbidden", ct),
            Groups = groups,
        };

        Func<Task> act = () => hub.JoinWorkspaceAsync(forbiddenWorkspaceId);
        _ = await act.Should().ThrowAsync<HubException>();
        _ = groups.AddedConnections.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <see cref="CommandHub.JoinWorkspaceAsync"/> adds the connection to the workspace group when authorized.
    /// </summary>
    [Fact]
    [Trait("Member", "JoinWorkspaceAsync")]
    public async Task JoinWorkspaceAsync_WhenAuthorized_AddsConnectionToWorkspaceGroup()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        Guid workspaceId = Guid.NewGuid();

        PanelUser user = new() { Id = userId, DisplayName = "streamer" };
        StubPanelUserStore userStore = new();
        userStore.Users[userId] = user;
        userStore.Identities[userId] = [];

        StubWorkspaceAccessService accessService = new(new Dictionary<Guid, HashSet<Guid>>
        {
            [userId] = [workspaceId],
        });
        TrackingGroupManager groups = new();

        using CommandHub hub = new(userStore, accessService)
        {
            Context = new StubHubCallerContext(AuthClaims.BuildClaimsPrincipal(user), "conn-1", ct),
            Groups = groups,
        };

        await hub.JoinWorkspaceAsync(workspaceId);

        _ = groups.AddedConnections.Should().ContainSingle(x =>
            x.ConnectionId == "conn-1"
            && x.GroupName == CommandHub.WorkspaceGroupName(workspaceId));
    }

    #endregion

    #region LeaveWorkspaceAsync

    /// <summary>
    /// Verifies that <see cref="CommandHub.LeaveWorkspaceAsync"/> removes the connection from the workspace group.
    /// </summary>
    [Fact]
    [Trait("Member", "LeaveWorkspaceAsync")]
    public async Task LeaveWorkspaceAsync_WhenCalled_RemovesConnectionFromWorkspaceGroup()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        Guid workspaceId = Guid.NewGuid();
        PanelUser user = new() { Id = userId, DisplayName = "streamer" };
        StubPanelUserStore userStore = new();
        StubWorkspaceAccessService accessService = new([]);
        TrackingGroupManager groups = new();

        using CommandHub hub = new(userStore, accessService)
        {
            Context = new StubHubCallerContext(AuthClaims.BuildClaimsPrincipal(user), "conn-leave", ct),
            Groups = groups,
        };

        await hub.LeaveWorkspaceAsync(workspaceId);

        _ = groups.RemovedConnections.Should().ContainSingle(x =>
            x.ConnectionId == "conn-leave"
            && x.GroupName == CommandHub.WorkspaceGroupName(workspaceId));
    }

    #endregion

    #region CommandHubEventBridge

    /// <summary>
    /// Verifies that <see cref="CommandHubEventBridge"/> forwards workspace-scoped <see cref="CommandsInvalidatedEvent"/> notifications to the target SignalR group.
    /// </summary>
    [Fact]
    [Trait("Member", "CommandHubEventBridge")]
    public async Task CommandHubEventBridge_WhenWorkspaceCommandsInvalidatedPublished_ForwardsToWorkspaceGroup()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        TrackingCommandHubContext hubContext = new();
        using CommandHubEventBridge bridge = new(eventBus, hubContext);

        TaskCompletionSource<(string? GroupName, Guid WorkspaceId)> receivedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        hubContext.ClientsAdapter.SetGroupNotificationCallback((groupName, notifiedWorkspaceId) => _ = receivedTcs.TrySetResult((groupName, notifiedWorkspaceId)));

        await bridge.StartAsync(ct);
        try
        {
            await eventBus.PublishAsync(new CommandsInvalidatedEvent(workspaceId), ct);

            (string? GroupName, Guid WorkspaceId) = await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = GroupName.Should().Be(CommandHub.WorkspaceGroupName(workspaceId));
            _ = WorkspaceId.Should().Be(workspaceId);
            _ = hubContext.ClientsAdapter.GroupNotifications.Should().ContainSingle(x =>
                x.GroupName == CommandHub.WorkspaceGroupName(workspaceId)
                && x.WorkspaceId == workspaceId);
        }
        finally
        {
            await bridge.StopAsync(ct);
        }
    }

    /// <summary>
    /// Verifies that <see cref="CommandHubEventBridge"/> broadcasts global <see cref="CommandsInvalidatedEvent"/> notifications to all SignalR clients.
    /// </summary>
    [Fact]
    [Trait("Member", "CommandHubEventBridge")]
    public async Task CommandHubEventBridge_WhenGlobalCommandsInvalidatedPublished_ForwardsToAllClients()
    {
        CancellationToken ct = XunitTestContext.Current.CancellationToken;
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        TrackingCommandHubContext hubContext = new();
        using CommandHubEventBridge bridge = new(eventBus, hubContext);

        TaskCompletionSource<Guid> receivedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        hubContext.ClientsAdapter.SetAllNotificationCallback(notifiedWorkspaceId => _ = receivedTcs.TrySetResult(notifiedWorkspaceId));

        await bridge.StartAsync(ct);
        try
        {
            await eventBus.PublishAsync(new CommandsInvalidatedEvent(null), ct);

            Guid result = await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = result.Should().Be(Guid.Empty);
            _ = hubContext.ClientsAdapter.AllNotifications.Should().ContainSingle(static id => id == Guid.Empty);
        }
        finally
        {
            await bridge.StopAsync(ct);
        }
    }

    #endregion

    /// <summary>
    /// In-memory stub implementation of <see cref="IPanelUserStore"/> for <see cref="CommandHub"/> unit tests.
    /// </summary>
    private sealed class StubPanelUserStore : IPanelUserStore
    {
        /// <summary>
        /// Gets the stored panel users keyed by user identifier.
        /// </summary>
        public Dictionary<Guid, PanelUser> Users { get; } = [];

        /// <summary>
        /// Gets the stored login identities keyed by panel user identifier.
        /// </summary>
        public Dictionary<Guid, List<UserLoginIdentity>> Identities { get; } = [];

        /// <inheritdoc/>
        public ValueTask<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Users.Count > 0);

        /// <inheritdoc/>
        public ValueTask<bool> HasRootUserAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Users.Values.Any(static u => u.IsRoot));

        /// <inheritdoc/>
        public ValueTask<PanelUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Users.GetValueOrDefault(userId));

        /// <inheritdoc/>
        public ValueTask<PanelUser?> GetByLoginIdentityAsync(string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PanelUser?>(null);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<PanelUser>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PanelUser>>([.. this.Users.Values]);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<UserLoginIdentity>> GetLoginIdentitiesAsync(Guid panelUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<UserLoginIdentity>>(this.Identities.GetValueOrDefault(panelUserId) ?? []);

        /// <inheritdoc/>
        public ValueTask<PanelUser> CreateUserAsync(PanelUser user, UserLoginIdentity? initialIdentity = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(user);

        /// <inheritdoc/>
        public ValueTask<PanelUser> UpdateUserAsync(PanelUser user, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(user);

        /// <inheritdoc/>
        public ValueTask<UserLoginIdentity> LinkLoginIdentityAsync(UserLoginIdentity identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(identity);

        /// <inheritdoc/>
        public ValueTask<bool> UnlinkLoginIdentityAsync(Guid panelUserId, string providerId, string providerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    /// <summary>
    /// Stub implementation of <see cref="IWorkspaceAccessService"/> that checks an in-memory map of allowed workspaces per user.
    /// </summary>
    /// <param name="allowedWorkspacesByUser">Allowed workspace identifiers keyed by panel user identifier.</param>
    private sealed class StubWorkspaceAccessService(Dictionary<Guid, HashSet<Guid>> allowedWorkspacesByUser) : IWorkspaceAccessService
    {
        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<AccessibleWorkspace>> GetAccessibleWorkspacesAsync(
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            string? authProvider = null,
            bool isMfaVerified = false,
            CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<AccessibleWorkspace>>([]);

        /// <inheritdoc/>
        public ValueTask<bool> CanAccessWorkspaceAsync(
            Guid workspaceId,
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            string? authProvider = null,
            bool isMfaVerified = false,
            CancellationToken ct = default) =>
            ValueTask.FromResult((allowedWorkspacesByUser.GetValueOrDefault(user.Id) ?? []).Contains(workspaceId));

        /// <inheritdoc/>
        public ValueTask<bool> HasPermissionInWorkspaceAsync(
            Guid workspaceId,
            PanelUser user,
            IReadOnlyList<UserLoginIdentity> identities,
            string permissionKey,
            IReadOnlyCollection<string>? oidcGroupClaims = null,
            CancellationToken ct = default) =>
            this.CanAccessWorkspaceAsync(workspaceId, user, identities, oidcGroupClaims, ct: ct);
    }

    /// <summary>
    /// Tracking stub implementation of <see cref="IGroupManager"/> for asserting SignalR group memberships.
    /// </summary>
    private sealed class TrackingGroupManager : IGroupManager
    {
        /// <summary>
        /// Gets the recorded group additions.
        /// </summary>
        public List<(string ConnectionId, string GroupName)> AddedConnections { get; } = [];

        /// <summary>
        /// Gets the recorded group removals.
        /// </summary>
        public List<(string ConnectionId, string GroupName)> RemovedConnections { get; } = [];

        /// <inheritdoc/>
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            this.AddedConnections.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            this.RemovedConnections.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stub implementation of <see cref="HubCallerContext"/> for unit-testing SignalR hubs.
    /// </summary>
    /// <param name="user">The authenticated claims principal.</param>
    /// <param name="connectionId">The SignalR connection identifier.</param>
    /// <param name="connectionAborted">The connection cancellation token.</param>
    private sealed class StubHubCallerContext(
        ClaimsPrincipal user,
        string connectionId,
        CancellationToken connectionAborted) : HubCallerContext
    {
        /// <inheritdoc/>
        public override string ConnectionId { get; } = connectionId;

        /// <inheritdoc/>
        public override string? UserIdentifier => this.User.FindFirstValue(ClaimTypes.NameIdentifier);

        /// <inheritdoc/>
        public override ClaimsPrincipal User { get; } = user;

        /// <inheritdoc/>
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        /// <inheritdoc/>
        public override IFeatureCollection Features => throw new NotSupportedException();

        /// <inheritdoc/>
        public override CancellationToken ConnectionAborted { get; } = connectionAborted;

        /// <inheritdoc/>
        public override void Abort()
        {
        }
    }

    /// <summary>
    /// Tracking stub implementation of <see cref="IHubContext{THub, T}"/> for <see cref="CommandHub"/>.
    /// </summary>
    private sealed class TrackingCommandHubContext : IHubContext<CommandHub, ICommandClient>
    {
        /// <summary>
        /// Gets the tracking client proxy adapter.
        /// </summary>
        public TrackingHubClients ClientsAdapter { get; } = new();

        /// <inheritdoc/>
        public IHubClients<ICommandClient> Clients => this.ClientsAdapter;

        /// <inheritdoc/>
        public IGroupManager Groups { get; } = new TrackingGroupManager();
    }

    /// <summary>
    /// Tracking stub implementation of <see cref="IHubClients{T}"/> for <see cref="ICommandClient"/>.
    /// </summary>
    private sealed class TrackingHubClients : IHubClients<ICommandClient>
    {
        /// <summary>
        /// Optional callback invoked when a group client receives a notification.
        /// </summary>
        private Action<string, Guid>? _onGroupNotified;

        /// <summary>
        /// Optional callback invoked when all clients receive a notification.
        /// </summary>
        private Action<Guid>? _onAllNotified;

        /// <summary>
        /// Gets the recorded workspace group notifications.
        /// </summary>
        public List<(string GroupName, Guid WorkspaceId)> GroupNotifications { get; } = [];

        /// <summary>
        /// Gets the recorded global notifications sent to all clients.
        /// </summary>
        public List<Guid> AllNotifications { get; } = [];

        /// <summary>
        /// Sets the callback invoked when a group client receives a notification.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        public void SetGroupNotificationCallback(Action<string, Guid> callback) => this._onGroupNotified = callback;

        /// <summary>
        /// Sets the callback invoked when all clients receive a notification.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        public void SetAllNotificationCallback(Action<Guid> callback) => this._onAllNotified = callback;

        /// <inheritdoc/>
        public ICommandClient All => new RecordingCommandClient(workspaceId =>
        {
            this.AllNotifications.Add(workspaceId);
            this._onAllNotified?.Invoke(workspaceId);
        });

        /// <inheritdoc/>
        public ICommandClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => this.All;

        /// <inheritdoc/>
        public ICommandClient Client(string connectionId) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ICommandClient Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ICommandClient Group(string groupName) => new RecordingCommandClient(workspaceId =>
        {
            this.GroupNotifications.Add((groupName, workspaceId));
            this._onGroupNotified?.Invoke(groupName, workspaceId);
        });

        /// <inheritdoc/>
        public ICommandClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => this.Group(groupName);

        /// <inheritdoc/>
        public ICommandClient Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ICommandClient User(string userId) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ICommandClient Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    /// <summary>
    /// Recording client proxy implementation of <see cref="ICommandClient"/>.
    /// </summary>
    /// <param name="onNotified">Callback invoked with the notified workspace identifier.</param>
    private sealed class RecordingCommandClient(Action<Guid> onNotified) : ICommandClient
    {
        /// <inheritdoc/>
        public Task OnCommandsInvalidatedAsync(Guid workspaceId)
        {
            onNotified(workspaceId);
            return Task.CompletedTask;
        }
    }
}
