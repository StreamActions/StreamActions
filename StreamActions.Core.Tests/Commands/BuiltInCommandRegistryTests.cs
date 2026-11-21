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
using Microsoft.Extensions.Options;
using StreamActions.Core.Commands;
using StreamActions.Core.Configuration;
using StreamActions.Core.Permissions;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="BuiltInCommandRegistry"/>.
/// </summary>
public sealed class BuiltInCommandRegistryTests
{
    #region RegisterAndResolve

    [Fact]
    [Trait("Member", "Register")]
    public void RegisterDiscoversCommandAttributeMetadataAndResolvesByIdAndSubTrigger()
    {
        AttributedHandler handler = new();
        BuiltInCommandRegistry registry = new([handler]);

        BuiltInCommandRegistration? byId = registry.ResolveByCommandId("BOT.SAMPLE");
        BuiltInCommandRegistration? bySub = registry.ResolveBySubCommandTrigger("sample");
        BuiltInCommandRegistration? byAlias = registry.ResolveBySubCommandTrigger("!smp");

        _ = byId.Should().NotBeNull();
        _ = byId!.Handler.Should().BeSameAs(handler);
        _ = byId.AllowCustomOverride.Should().BeTrue();
        _ = byId.RequiredPermissionKey.Should().Be(BuiltInPermissionKeys.ActionManageQuotes);
        _ = byId.GlobalCooldownSeconds.Should().Be(5);
        _ = byId.UserCooldownSeconds.Should().Be(15);
        _ = bySub.Should().BeSameAs(byId);
        _ = byAlias.Should().BeSameAs(byId);
        _ = registry.GetAll().Should().ContainSingle();
    }

    #endregion

    #region ResolveByTopLevelTriggerAsync

    [Fact]
    [Trait("Member", "ResolveByTopLevelTriggerAsync")]
    public async Task ResolveByTopLevelTriggerAsyncDisablesBotSubCommandTopLevelByDefaultUnlessExplicitlyEnabled()
    {
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;
        CommandTestHarness.InMemoryKeyValueStore kvStore = new();
        BotSubCommandOnlyHandler subOnlyHandler = new();
        BuiltInCommandRegistry registry = new([subOnlyHandler], kvStore);

        bool enabledBefore = await registry.IsTopLevelAliasEnabledAsync(workspaceId, subOnlyHandler.CommandId, TestContext.Current.CancellationToken);
        BuiltInCommandRegistration? resolvedBefore = await registry.ResolveByTopLevelTriggerAsync(workspaceId, "subonly", TestContext.Current.CancellationToken);

        _ = enabledBefore.Should().BeFalse();
        _ = resolvedBefore.Should().BeNull();

        await registry.SetTopLevelAliasEnabledAsync(workspaceId, subOnlyHandler.CommandId, true, TestContext.Current.CancellationToken);

        bool enabledAfter = await registry.IsTopLevelAliasEnabledAsync(workspaceId, subOnlyHandler.CommandId, TestContext.Current.CancellationToken);
        BuiltInCommandRegistration? resolvedAfter = await registry.ResolveByTopLevelTriggerAsync(workspaceId, "subonly", TestContext.Current.CancellationToken);

        _ = enabledAfter.Should().BeTrue();
        _ = resolvedAfter.Should().NotBeNull();
        _ = resolvedAfter!.CommandId.Should().Be(subOnlyHandler.CommandId);
    }

    [Fact]
    [Trait("Member", "ResolveByTopLevelTriggerAsync")]
    public async Task ResolveByTopLevelTriggerAsyncExposesOptionalTopLevelTriggersByDefaultAndCanBeDisabledPerWorkspace()
    {
        Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;
        CommandTestHarness.InMemoryKeyValueStore kvStore = new();
        OptionalTopLevelHandler optionalHandler = new();
        BuiltInCommandRegistry registry = new([optionalHandler], kvStore);

        BuiltInCommandRegistration? byOptional = await registry.ResolveByTopLevelTriggerAsync(workspaceId, "ping", TestContext.Current.CancellationToken);
        _ = byOptional.Should().NotBeNull();

        await registry.SetTopLevelAliasEnabledAsync(workspaceId, optionalHandler.CommandId, false, TestContext.Current.CancellationToken);

        BuiltInCommandRegistration? afterDisable = await registry.ResolveByTopLevelTriggerAsync(workspaceId, "ping", TestContext.Current.CancellationToken);
        _ = afterDisable.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "ResolveByTopLevelTriggerAsync")]
    public async Task ResolveByTopLevelTriggerAsyncSupportsLocalizedTriggersWhenTopLevelAliasesEnabled()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            HybridCache cache = provider.GetRequiredService<HybridCache>();
            Guid workspaceId = CommandTestHarness.DefaultWorkspaceId;
            CommandTestHarness.InMemoryKeyValueStore kvStore = new();
            CommandTestHarness.InMemoryWorkspaceStore wsStore = new();
            wsStore.Workspaces[workspaceId] = new() { Id = workspaceId, PrimaryPlatformId = "twitch", BotCulture = "en-US" };

            StreamActionsLocalizer localizer = new(
                cache,
                Options.Create(new LocalizationOptions { LocalesDirectory = Path.Combine(AppContext.BaseDirectory, "locales") }),
                NullLogger<StreamActionsLocalizer>.Instance,
                wsStore,
                kvStore);

            await localizer.SetWorkspaceCommandTriggersAsync(workspaceId, "bot.command", ["comando"], TestContext.Current.CancellationToken);

            BotCommandSubCommandHandler cmdHandler = new(localizer: localizer);
            BuiltInCommandRegistry registry = new([cmdHandler], kvStore, localizer);

            BuiltInCommandRegistration? disabledResult = await registry.ResolveByTopLevelTriggerAsync(workspaceId, "comando", TestContext.Current.CancellationToken);
            _ = disabledResult.Should().BeNull();

            await registry.SetTopLevelAliasEnabledAsync(workspaceId, "bot.command", true, TestContext.Current.CancellationToken);
            BuiltInCommandRegistration? enabledResult = await registry.ResolveByTopLevelTriggerAsync(workspaceId, "comando", TestContext.Current.CancellationToken);

            _ = enabledResult.Should().NotBeNull();
            _ = enabledResult!.CommandId.Should().Be("bot.command");
        }
    }

    #endregion

    /// <summary>
    /// Built-in handler decorated with <see cref="CommandAttribute"/>.
    /// </summary>
    [Command(
        "sample",
        Aliases = ["smp"],
        DefaultPermissionKey = BuiltInPermissionKeys.ActionManageQuotes,
        GlobalCooldownSeconds = 5,
        UserCooldownSeconds = 15,
        AllowCustomOverride = true)]
    private sealed class AttributedHandler : IBuiltInCommandHandler
    {
        /// <inheritdoc/>
        public string CommandId => "bot.sample";

        /// <inheritdoc/>
        public string DefaultSubCommandTrigger => "sample";

        /// <inheritdoc/>
        public ValueTask ExecuteAsync(BuiltInCommandContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    /// <summary>
    /// Built-in handler without <see cref="IBuiltInCommandHandler.OptionalTopLevelTriggers"/>.
    /// </summary>
    private sealed class BotSubCommandOnlyHandler : IBuiltInCommandHandler
    {
        /// <inheritdoc/>
        public string CommandId => "bot.subonly";

        /// <inheritdoc/>
        public string DefaultSubCommandTrigger => "subonly";

        /// <inheritdoc/>
        public ValueTask ExecuteAsync(BuiltInCommandContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    /// <summary>
    /// Built-in handler declaring <see cref="IBuiltInCommandHandler.OptionalTopLevelTriggers"/>.
    /// </summary>
    private sealed class OptionalTopLevelHandler : IBuiltInCommandHandler
    {
        /// <inheritdoc/>
        public string CommandId => "core.ping";

        /// <inheritdoc/>
        public string DefaultSubCommandTrigger => "ping";

        /// <inheritdoc/>
        public IReadOnlyList<string> OptionalTopLevelTriggers { get; } = ["ping"];

        /// <inheritdoc/>
        public ValueTask ExecuteAsync(BuiltInCommandContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
