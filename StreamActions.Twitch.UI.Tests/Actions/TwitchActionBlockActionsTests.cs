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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Core.Actions;
using StreamActions.Core.Templates;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.UI.Abstractions;
using StreamActions.Twitch.UI.Actions;
using StreamActions.Twitch.UI.Tests.Infrastructure;
using Xunit;

namespace StreamActions.Twitch.UI.Tests.Actions;

public sealed class TwitchActionBlockActionsTests
{
    #region CreateDefaultActions

    [Fact]
    [Trait("Member", "CreateDefaultActions")]
    public void CreateDefaultActionsRegistersHierarchicalCategoryPaths()
    {
        ActionBlockCatalog catalog = new(
            TwitchActionBlockCatalogFactory.CreateDefaultActions(),
            []);

        IReadOnlyList<ActionCategoryNode> tree = catalog.BuildActionCategoryTree();
        ActionCategoryNode twitchNode = tree.Should().ContainSingle(static n => n.Name == "Twitch").Subject;
        ActionCategoryNode apiNode = twitchNode.Children.Should().ContainSingle(static n => n.Name == "Api").Subject;
        _ = apiNode.Children.Should().Contain(static n => n.Name == "Clips")
            .Which.Actions.Should().ContainSingle(static a => a.ActionId == "twitch.api.clips.create");
        _ = catalog.SearchActions("Clips").Should().ContainSingle(static a => a.ActionId == "twitch.api.clips.create");
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task CreateClipActionHandlerInvokesHelixEndpointAndStoresOutputVariable()
    {
        TwitchHelixMockServer server = await TwitchHelixMockServer
            .StartAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        await using (server.ConfigureAwait(true))
        {
            server.RegisterHandler("POST", "/helix/clips", async context =>
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""
                    {"data":[{"id":"clip-999","edit_url":"https://clips.twitch.tv/clip-999/edit"}]}
                    """).ConfigureAwait(false);
            });

            using TwitchSession session = new()
            {
                CustomBaseUri = server.HelixBaseUri,
                CustomClientId = "twitch-client",
                Token = new TwitchToken
                {
                    OAuth = "user-oauth-token",
                    Type = TwitchToken.TokenType.User,
                    Scopes = [OAuth.Scope.ClipsEdit],
                },
            };

            ServiceCollection services = new();
            using StubSessionProvider sessionProvider = new(session);
            _ = services.AddSingleton<ITwitchWorkspaceSessionProvider>(sessionProvider);
            using ServiceProvider sp = services.BuildServiceProvider();

            ActionBlockCatalog catalog = new(TwitchActionBlockCatalogFactory.CreateDefaultActions(), []);
            ActionFlowRunner runner = new(
                catalog,
                new TagRegistry(CoreTagRegistryFactory.CreateDefaultHandlers()),
                new RuntimeExecutionTracker(),
                actionBlockStore: new EmptyActionBlockStore(),
                scopeFactory: null,
                serviceProvider: sp);

            Guid wsId = Guid.CreateVersion7();
            WorkspaceActionBlockTask task = new()
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = wsId,
                Name = "Clip and Announce",
                Blocks =
                [
                    new PlatformActionBlock(
                        "twitch.api.clips.create",
                        new Dictionary<string, string> { ["title"] = "Epic Play", ["duration"] = "30" },
                        OutputVariableName: "myClipUrl"),
                ],
            };

            ActionExecutionTrace trace = await runner.ExecuteAsync(new ActionExecutionRequest
            {
                WorkspaceId = wsId,
                TaskOverride = task,
                DryRun = true,
            }, TestContext.Current.CancellationToken);

            _ = trace.Succeeded.Should().BeTrue();
            _ = trace.FinalVariables["myClipUrl"].Should().Be("https://clips.twitch.tv/clip-999/edit");
            _ = server.CountRequests("/helix/clips").Should().Be(1);
        }
    }

    #endregion

    private sealed class StubSessionProvider(TwitchSession session) : ITwitchWorkspaceSessionProvider, IDisposable
    {
        private readonly TwitchWorkspaceSessionLease _lease = new(session, "12345", "broadcaster", ownsSession: false);

        public ValueTask<TwitchWorkspaceSessionLease?> GetSessionAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TwitchWorkspaceSessionLease?>(this._lease);

        public void Dispose() => this._lease.Dispose();
    }

    private sealed class EmptyActionBlockStore : Core.Abstractions.IActionBlockStore
    {
        public ValueTask<IReadOnlyList<WorkspaceActionBlockTask>> GetTasksAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<WorkspaceActionBlockTask>>([]);
        public ValueTask<WorkspaceActionBlockTask?> GetTaskByIdAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default) => ValueTask.FromResult<WorkspaceActionBlockTask?>(null);
        public ValueTask<WorkspaceActionBlockTask?> GetTaskByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default) => ValueTask.FromResult<WorkspaceActionBlockTask?>(null);
        public ValueTask<WorkspaceActionBlockTask> UpsertTaskAsync(WorkspaceActionBlockTask task, CancellationToken cancellationToken = default) => ValueTask.FromResult(task);
        public ValueTask<bool> DeleteTaskAsync(Guid workspaceId, Guid taskId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetProfilesAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<WorkspaceActionBlockProfile>>([]);
        public ValueTask<IReadOnlyList<WorkspaceActionBlockProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<WorkspaceActionBlockProfile>>([]);
        public ValueTask<WorkspaceActionBlockProfile?> GetProfileByIdAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default) => ValueTask.FromResult<WorkspaceActionBlockProfile?>(null);
        public ValueTask<WorkspaceActionBlockProfile?> GetProfileByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default) => ValueTask.FromResult<WorkspaceActionBlockProfile?>(null);
        public ValueTask<WorkspaceActionBlockProfile> UpsertProfileAsync(WorkspaceActionBlockProfile profile, CancellationToken cancellationToken = default) => ValueTask.FromResult(profile);
        public ValueTask<bool> SetProfileEnabledAsync(Guid workspaceId, Guid profileId, bool isEnabled, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> DeleteProfileAsync(Guid workspaceId, Guid profileId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }
}

