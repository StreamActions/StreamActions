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
using StreamActions.Core.Commands;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Core.Templates;
using Xunit;

namespace StreamActions.Core.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="CommandAstCache"/>.
/// </summary>
public sealed class CommandAstCacheTests
{
    #region GetOrCompileRoutine

    [Fact]
    [Trait("Member", "GetOrCompileRoutine")]
    public void GetOrCompileRoutineCachesAstAcrossRepeatedCallsWithoutReParsing()
    {
        TagRegistry registry = CoreTagRegistryFactory.CreateDefaultRegistry();
        using CommandAstCache cache = new(registry);

        CustomCommand command = new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = CommandTestHarness.DefaultWorkspaceId,
            Trigger = "hello",
            RoutineJson = CommandRoutine.FromPlainText("Hello {sender}!").ToJson(),
            IsEnabled = true,
        };

        CompiledCommandRoutine first = cache.GetOrCompileRoutine(command);
        CompiledCommandRoutine second = cache.GetOrCompileRoutine(command);

        _ = second.Should().BeSameAs(first);
        _ = cache.CompilationCount.Should().Be(1);
        _ = cache.CachedCount.Should().Be(1);
    }

    [Fact]
    [Trait("Member", "GetOrCompileRoutine")]
    public async Task GetOrCompileRoutineInvalidatesImmediatelyOnCommandsInvalidatedEvent()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            HybridCache hybridCache = provider.GetRequiredService<HybridCache>();
            using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
            TagRegistry registry = CoreTagRegistryFactory.CreateDefaultRegistry();
            using CommandAstCache astCache = new(registry, eventBus, hybridCache);

            Guid workspaceA = Guid.NewGuid();
            Guid workspaceB = Guid.NewGuid();

            CustomCommand cmdA = new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceA,
                Trigger = "greet",
                RoutineJson = CommandRoutine.FromPlainText("Hi {sender}!").ToJson(),
                IsEnabled = true,
            };

            CustomCommand cmdB = new()
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceB,
                Trigger = "greet",
                RoutineJson = CommandRoutine.FromPlainText("Hola {sender}!").ToJson(),
                IsEnabled = true,
            };

            CompiledCommandRoutine compiledA1 = astCache.GetOrCompileRoutine(cmdA);
            CompiledCommandRoutine compiledB1 = astCache.GetOrCompileRoutine(cmdB);
            _ = astCache.CompilationCount.Should().Be(2);

            TaskCompletionSource<Guid?> invalidatedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            astCache.Invalidated += wsId =>
            {
                if (wsId == workspaceA)
                {
                    _ = invalidatedTcs.TrySetResult(wsId);
                }
            };

            await eventBus.PublishAsync(new CommandsInvalidatedEvent(workspaceA), TestContext.Current.CancellationToken);
            _ = await invalidatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Workspace B remains cached; workspace A recompiles.
            CompiledCommandRoutine compiledB2 = astCache.GetOrCompileRoutine(cmdB);
            CompiledCommandRoutine compiledA2 = astCache.GetOrCompileRoutine(cmdA);

            _ = compiledB2.Should().BeSameAs(compiledB1);
            _ = compiledA2.Should().NotBeSameAs(compiledA1);
            _ = astCache.CompilationCount.Should().Be(3);

            // Global invalidation (null workspace) evicts all workspaces.
            TaskCompletionSource<Guid?> globalTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            astCache.Invalidated += wsId =>
            {
                if (wsId is null)
                {
                    _ = globalTcs.TrySetResult(null);
                }
            };

            await eventBus.PublishAsync(new CommandsInvalidatedEvent(null), TestContext.Current.CancellationToken);
            _ = await globalTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            CompiledCommandRoutine compiledB3 = astCache.GetOrCompileRoutine(cmdB);
            _ = compiledB3.Should().NotBeSameAs(compiledB2);
            _ = astCache.CompilationCount.Should().Be(4);
        }
    }

    #endregion
}
