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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="BotCommandSubCommandHandler"/> and <see cref="CommandsPlugin"/>.
/// </summary>
public sealed class BotCommandSubCommandHandlerTests
{
    #region CommandsPlugin

    [Fact]
    [Trait("Member", "CommandsPlugin")]
    public async Task CommandsPluginExposesExpectedMetadataAndRegistersHandler()
    {
        BotCommandSubCommandHandler handler = new(permissionService: null);
        CommandsPlugin plugin = new(handler);
        BuiltInCommandRegistry registry = new();

        ServiceCollection services = new();
        _ = services.AddSingleton<IBuiltInCommandRegistry>(registry);
        _ = services.AddSingleton(handler);
        ServiceProvider provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        using (InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance))
        {
            PluginManager pluginManager = new([plugin], provider, eventBus, NullLoggerFactory.Instance);
            await pluginManager.InitializeAllAsync(TestContext.Current.CancellationToken);

            _ = plugin.Id.Should().Be("core.commands");
            _ = plugin.Name.Should().Be("Commands");
            _ = plugin.Version.Should().Be(new Version(1, 0, 0));
            _ = registry.ResolveByCommandId("bot.command").Should().NotBeNull();

            await pluginManager.ShutdownAllAsync(TestContext.Current.CancellationToken);
        }
    }

    #endregion

    #region ExecuteAsync

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncAddAndEditValidateAstAndPersistCustomCommands()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            ChatCommand addCmd = CommandTestHarness.CreateChatCommand(
                "bot",
                ["command", "add", "!Greet", "Hello", "{sender}!"],
                "command add !Greet Hello {sender}!",
                designations: PlatformUserDesignations.Moderator);

            CommandDispatchResult addResult = await rig.Dispatcher.DispatchAsync(addCmd, TestContext.Current.CancellationToken);
            _ = addResult.Executed.Should().BeTrue();

            CustomCommand? saved = await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "greet", TestContext.Current.CancellationToken);
            _ = saved.Should().NotBeNull();
            _ = saved!.Trigger.Should().Be("greet");
            _ = saved.IsEnabled.Should().BeTrue();
            _ = CommandRoutine.Parse(saved.RoutineJson).GetChatMessages().Should().Equal("Hello {sender}!");
            _ = rig.OutboundDispatcher.Messages.Should().ContainSingle(m => m.Message.Contains("!greet", StringComparison.OrdinalIgnoreCase));

            // Edit with valid template updates RoutineJson.
            ChatCommand editCmd = CommandTestHarness.CreateChatCommand(
                "bot",
                ["command", "edit", "!greet", "Welcome", "{sender}!"],
                "command edit !greet Welcome {sender}!",
                designations: PlatformUserDesignations.Moderator);

            CommandDispatchResult editResult = await rig.Dispatcher.DispatchAsync(editCmd, TestContext.Current.CancellationToken);
            _ = editResult.Executed.Should().BeTrue();

            CustomCommand? edited = await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "greet", TestContext.Current.CancellationToken);
            _ = CommandRoutine.Parse(edited!.RoutineJson).GetChatMessages().Should().Equal("Welcome {sender}!");
        }
    }

    [Theory]
    [InlineData("add", "!bad", "Hello {unclosed")]
    [InlineData("edit", "!existing", "Bad } brace")]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncRejectsInvalidTemplateSyntaxOnAddAndEdit(string verb, string trigger, string badTemplate)
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = "existing",
                    RoutineJson = CommandRoutine.FromPlainText("Original").ToJson(),
                    IsEnabled = true,
                },
                TestContext.Current.CancellationToken);

            ChatCommand invocation = CommandTestHarness.CreateChatCommand(
                "bot",
                ["command", verb, trigger, badTemplate],
                $"command {verb} {trigger} {badTemplate}",
                designations: PlatformUserDesignations.Moderator);

            CommandDispatchResult result = await rig.Dispatcher.DispatchAsync(invocation, TestContext.Current.CancellationToken);

            _ = result.Executed.Should().BeTrue();
            _ = rig.OutboundDispatcher.Messages.Should().ContainSingle(m => m.Message.Contains("syntax", StringComparison.OrdinalIgnoreCase));

            if (verb == "add")
            {
                _ = (await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "bad", TestContext.Current.CancellationToken)).Should().BeNull();
            }
            else
            {
                CustomCommand? unchanged = await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "existing", TestContext.Current.CancellationToken);
                _ = CommandRoutine.Parse(unchanged!.RoutineJson).GetChatMessages().Should().Equal("Original");
            }
        }
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncDeleteAndRemoveDeleteCommand()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "one", RoutineJson = "1", IsEnabled = true },
                TestContext.Current.CancellationToken);
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand { Id = Guid.NewGuid(), WorkspaceId = CommandTestHarness.DefaultWorkspaceId, Trigger = "two", RoutineJson = "2", IsEnabled = true },
                TestContext.Current.CancellationToken);

            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "delete", "!one"], "command delete !one", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "remove", "two"], "command remove two", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            _ = (await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "one", TestContext.Current.CancellationToken)).Should().BeNull();
            _ = (await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "two", TestContext.Current.CancellationToken)).Should().BeNull();
        }
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncPermissionUpdatesGroupGrantAndInvalidatesPermissionCache()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = "vipcmd",
                    RoutineJson = CommandRoutine.FromPlainText("Secret!").ToJson(),
                    IsEnabled = true,
                },
                TestContext.Current.CancellationToken);

            PlatformUserIdentity viewer = new("twitch", "v1", "viewer");
            // Warm permission cache before update (currently allowed via ActionRunCommand fallback).
            CommandDispatchResult beforeResult = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("vipcmd", userId: "v1", username: "viewer"),
                TestContext.Current.CancellationToken);
            _ = beforeResult.Executed.Should().BeTrue();

            // Deny Everyone via !bot command permission.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand(
                    "bot",
                    ["command", "permission", "!vipcmd", "Everyone", "deny"],
                    "command permission !vipcmd Everyone deny",
                    designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            PermissionEvaluationResult evalDenied = await rig.PermissionService.EvaluateAsync(
                CommandTestHarness.DefaultWorkspaceId,
                viewer,
                "command.vipcmd",
                TestContext.Current.CancellationToken);
            _ = evalDenied.IsAllowed.Should().BeFalse();
            _ = evalDenied.EffectiveState.Should().Be(PermissionState.Deny);

            CommandDispatchResult afterDeny = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("vipcmd", userId: "v1", username: "viewer"),
                TestContext.Current.CancellationToken);
            _ = afterDeny.Status.Should().Be(CommandDispatchStatus.PermissionDenied);

            // Reset to inherit -> falls back to ActionRunCommand (allowed).
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand(
                    "bot",
                    ["command", "permission", "!vipcmd", "Everyone", "inherit"],
                    "command permission !vipcmd Everyone inherit",
                    designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            CommandDispatchResult afterInherit = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("vipcmd", userId: "v1", username: "viewer"),
                TestContext.Current.CancellationToken);
            _ = afterInherit.Executed.Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncCooldownCostRewardCountAndToggleUpdateCommandProperties()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            await rig.CommandRepository.UpsertAsync(
                new CustomCommand
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
                    Trigger = "death",
                    RoutineJson = CommandRoutine.FromPlainText("Deaths: {count}").ToJson(),
                    IsEnabled = true,
                },
                TestContext.Current.CancellationToken);

            // Global cooldown (default) and -user cooldown.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "cooldown", "!death", "15"], "command cooldown !death 15", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "cooldown", "!death", "-user", "45"], "command cooldown !death -user 45", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // Cost and reward.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "cost", "!death", "100"], "command cost !death 100", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "reward", "!death", "25"], "command reward !death 25", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // Count (counterName + behavior).
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "count", "!death", "deaths", "parameterized"], "command count !death deaths parameterized", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            // Toggle explicit off, then implicit toggle back on.
            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "toggle", "!death", "off"], "command toggle !death off", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            CustomCommand? offCmd = await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "death", TestContext.Current.CancellationToken);
            _ = offCmd!.GlobalCooldownSeconds.Should().Be(15);
            _ = offCmd.UserCooldownSeconds.Should().Be(45);
            _ = offCmd.CostPoints.Should().Be(100);
            _ = offCmd.RewardPoints.Should().Be(25);
            _ = offCmd.BoundCounterName.Should().Be("deaths");
            _ = offCmd.CounterUsageBehavior.Should().Be(CounterUsageBehavior.Parameterized);
            _ = offCmd.IsEnabled.Should().BeFalse();

            _ = await rig.Dispatcher.DispatchAsync(
                CommandTestHarness.CreateChatCommand("bot", ["command", "toggle", "!death"], "command toggle !death", designations: PlatformUserDesignations.Moderator),
                TestContext.Current.CancellationToken);

            CustomCommand? onCmd = await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "death", TestContext.Current.CancellationToken);
            _ = onCmd!.IsEnabled.Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Member", "ExecuteAsync")]
    public async Task ExecuteAsyncRequiresUiCommandsEditPermission()
    {
        CommandTestHarness.DispatcherTestRig rig = CommandTestHarness.CreateRig();
        await using (rig.ConfigureAwait(false))
        {
            ChatCommand viewerAttempt = CommandTestHarness.CreateChatCommand(
                "bot",
                ["command", "add", "!hi", "Hello!"],
                "command add !hi Hello!",
                designations: PlatformUserDesignations.None);

            CommandDispatchResult result = await rig.Dispatcher.DispatchAsync(viewerAttempt, TestContext.Current.CancellationToken);

            _ = result.Status.Should().Be(CommandDispatchStatus.PermissionDenied);
            _ = result.Executed.Should().BeFalse();
            _ = (await rig.CommandRepository.GetByTriggerAsync(CommandTestHarness.DefaultWorkspaceId, "hi", TestContext.Current.CancellationToken)).Should().BeNull();
        }
    }

    #endregion
}
