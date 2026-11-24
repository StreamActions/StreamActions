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
using StreamActions.Core.Scripting;
using StreamActions.Twitch.Api.Common;
using System.Collections.Concurrent;
using Xunit;

namespace StreamActions.Scripting.Tests;

public sealed class BoundApiTypeProxyTests
{
    #region TryInvokeMember

    [Fact]
    [Trait("Member", "TryInvokeMember")]
    public async Task BoundNamespaceProxy_PrependsTwitchSessionHidesTokenAndBlocksReflection()
    {
        const string secretOAuth = "super-secret-oauth-token-value";
        ConcurrentQueue<string?> observedAuthHeaders = new();
        ConcurrentQueue<string> observedPaths = new();

        await using EphemeralKestrelServer mockServer = await EphemeralKestrelServer.StartAsync(async ctx =>
        {
            observedAuthHeaders.Enqueue(ctx.Request.Headers.Authorization.ToString());
            observedPaths.Enqueue(ctx.Request.Path + ctx.Request.QueryString);
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"data":[{"id":"27471","name":"Minecraft","box_art_url":"https://example.com/box.jpg"}]}""");
        });

        using TwitchSession session = new()
        {
            CustomBaseUri = mockServer.HelixBaseUri,
            CustomClientId = "test-client-id",
            Token = new TwitchToken
            {
                OAuth = secretOAuth,
                Type = TwitchToken.TokenType.App,
            },
        };

        using ScriptingTestRig rig = new(twitchSessionProvider: new ScriptingTestRig.StubTwitchSessionLeaseProvider(session));
        rig.GrantClearScriptFeature(rig.WorkspaceId);

        WorkspaceScript libScript = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "lib/gameHelpers.js",
            Code = """
                $.lookupGameId = async function(gameName) {
                    const response = await twitch.Games.Game.GetGames(null, [gameName]);
                    return (response && response.Data && response.Data.Count > 0) ? response.Data[0].Id : null;
                };
                """,
        }, TestContext.Current.CancellationToken);

        WorkspaceScript cmdScript = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "commands/checkGame.js",
            Code = """
                require("lib/gameHelpers.js");
                async function run(args) {
                    const id = await $.lookupGameId(args.game);
                    context.setVar("gameId", id);
                    return id;
                }
                """,
        }, TestContext.Current.CancellationToken);

        using Runtime.ClearScriptRuntimeService runtime = rig.CreateRuntimeService();
        Dictionary<string, string> flowVars = [];

        ScriptExecutionResult result = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest
        {
            WorkspaceId = rig.WorkspaceId,
            ScriptId = cmdScript.Id,
            EntryFunction = "run",
            Arguments = new Dictionary<string, string> { ["game"] = "Minecraft" },
            GetFlowVariable = name => flowVars.GetValueOrDefault(name),
            SetFlowVariable = (name, val) => flowVars[name] = val,
        }, TestContext.Current.CancellationToken);

        _ = result.Succeeded.Should().BeTrue(result.ErrorMessage);
        _ = result.ReturnValue.Should().Be("27471");
        _ = flowVars["gameId"].Should().Be("27471");
        _ = observedAuthHeaders.Should().ContainSingle().Which.Should().Be($"Bearer {secretOAuth}");
        _ = observedPaths.Should().ContainSingle().Which.Should().Contain("name=Minecraft");

        // Verify .GetType() reflection escape and token access are blocked.
        WorkspaceScript probeScript = await rig.ScriptStore.UpsertAsync(new WorkspaceScript
        {
            WorkspaceId = rig.WorkspaceId,
            Path = "probes/reflect.js",
            Code = """
                function tryReflect() {
                    return twitch.Games.Game.GetType().Assembly.FullName;
                }
                """,
        }, TestContext.Current.CancellationToken);

        ScriptExecutionResult probeResult = await runtime.ExecuteScriptAsync(new ScriptExecutionRequest
        {
            WorkspaceId = rig.WorkspaceId,
            ScriptId = probeScript.Id,
            EntryFunction = "tryReflect",
        }, TestContext.Current.CancellationToken);

        _ = probeResult.Succeeded.Should().BeFalse();
        _ = probeResult.ErrorMessage.Should().NotContain(secretOAuth);
        _ = libScript.Id.Should().NotBe(Guid.Empty);
    }

    #endregion
}

