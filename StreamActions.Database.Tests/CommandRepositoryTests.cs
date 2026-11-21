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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Commands;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Metadata;
using StreamActions.Core.Services;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class CommandRepositoryTests
{
    #region GetCommandsAsync
    [Fact]
    [Trait("Member", "GetCommandsAsync")]
    public async Task GetCommandsAsync_WhenCalledTwice_UsesWorkspaceCache()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            await repository.UpsertAsync(
                new CustomCommand
                {
                    WorkspaceId = workspaceId,
                    Trigger = "!ping",
                    AliasesJson = "[]",
                    RoutineJson = "{}",
                    IsEnabled = true,
                },
                ct);

            IReadOnlyList<CustomCommand> first = await repository.GetCommandsAsync(workspaceId, ct);
            _ = first.Should().ContainSingle(static c => c.Trigger == "!ping");

            await using (StreamActionsDbContext db = factory.CreateDbContext())
            {
                _ = db.CustomCommands.Add(new CustomCommand
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    Trigger = "!bypass",
                    AliasesJson = "[]",
                    RoutineJson = "{}",
                    IsEnabled = true,
                });
                _ = await db.SaveChangesAsync(ct);
            }

            IReadOnlyList<CustomCommand> second = await repository.GetCommandsAsync(workspaceId, ct);
            _ = second.Should().ContainSingle(static c => c.Trigger == "!ping");
        }
    }

    [Fact]
    [Trait("Member", "GetCommandsAsync")]
    public async Task GetCommandsAsync_DifferentWorkspaces_IsolatesTenants()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            await repository.UpsertAsync(
                new CustomCommand
                {
                    WorkspaceId = workspaceA,
                    Trigger = "!alpha",
                    AliasesJson = "[]",
                    RoutineJson = "{}",
                    IsEnabled = true,
                },
                ct);
            await repository.UpsertAsync(
                new CustomCommand
                {
                    WorkspaceId = workspaceB,
                    Trigger = "!beta",
                    AliasesJson = "[]",
                    RoutineJson = "{}",
                    IsEnabled = true,
                },
                ct);

            IReadOnlyList<CustomCommand> commandsA = await repository.GetCommandsAsync(workspaceA, ct);
            IReadOnlyList<CustomCommand> commandsB = await repository.GetCommandsAsync(workspaceB, ct);

            _ = commandsA.Should().ContainSingle(static c => c.Trigger == "!alpha");
            _ = commandsB.Should().ContainSingle(static c => c.Trigger == "!beta");
        }
    }
    #endregion

    #region GetByTriggerAsync
    [Theory]
    [Trait("Member", "GetByTriggerAsync")]
    [InlineData("!hello", true)]
    [InlineData("  !HELLO  ", true)]
    [InlineData("!hi", true)]
    [InlineData(" !HEY ", true)]
    [InlineData("hello", false)]
    [InlineData("!missing", false)]
    public async Task GetByTriggerAsync_MatchesNormalizedPrimaryTriggerAndAliases(string lookup, bool shouldFind)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            await repository.UpsertAsync(
                new CustomCommand
                {
                    WorkspaceId = workspaceId,
                    Trigger = "  !Hello ",
                    AliasesJson = "[\" !Hi \", \"!HEY\"]",
                    RoutineJson = "{}",
                    IsEnabled = true,
                },
                ct);

            CustomCommand? found = await repository.GetByTriggerAsync(workspaceId, lookup, ct);
            if (shouldFind)
            {
                _ = found.Should().NotBeNull();
                _ = found!.Trigger.Should().Be("!hello");
            }
            else
            {
                _ = found.Should().BeNull();
            }
        }
    }

    [Fact]
    [Trait("Member", "GetByTriggerAsync")]
    public async Task GetByTriggerAsync_WhenAliasesJsonIsMalformed_FallsBackToPrimaryTriggerGracefully()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            await using (StreamActionsDbContext db = factory.CreateDbContext())
            {
                _ = db.CustomCommands.Add(new CustomCommand
                {
                    Id = Guid.CreateVersion7(),
                    WorkspaceId = workspaceId,
                    Trigger = "!legacy",
                    AliasesJson = "not-valid-json",
                    RoutineJson = "{}",
                    IsEnabled = true,
                });
                _ = await db.SaveChangesAsync(ct);
            }

            CustomCommand? found = await repository.GetByTriggerAsync(workspaceId, "!legacy", ct);
            CustomCommand? missing = await repository.GetByTriggerAsync(workspaceId, "!other", ct);

            _ = found.Should().NotBeNull();
            _ = missing.Should().BeNull();
        }
    }
    #endregion

    #region UpsertAsync
    [Fact]
    [Trait("Member", "UpsertAsync")]
    public async Task UpsertAsync_WhenNewWithEmptyId_AssignsIdNormalizesAndPublishesInvalidationEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        TaskCompletionSource<CommandsInvalidatedEvent> eventTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<CommandsInvalidatedEvent>((evt, cancellationToken) =>
        {
            _ = eventTcs.TrySetResult(evt);
            return ValueTask.CompletedTask;
        });

        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            CustomCommand command = new()
            {
                Id = Guid.Empty,
                WorkspaceId = workspaceId,
                Trigger = "  !Ping ",
                AliasesJson = "[\" !P \", \"!p\", \"\", \" !pong \"]",
                RoutineJson = "{\"steps\":[]}",
                IsEnabled = true,
            };

            await repository.UpsertAsync(command, ct);

            _ = command.Id.Should().NotBe(Guid.Empty);
            _ = command.Trigger.Should().Be("!ping");
            _ = command.AliasesJson.Should().Be("[\"!p\",\"!pong\"]");

            CommandsInvalidatedEvent received = await eventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = received.WorkspaceId.Should().Be(workspaceId);
        }
    }

    [Fact]
    [Trait("Member", "UpsertAsync")]
    public async Task UpsertAsync_WhenMatchingById_UpdatesRowMetadataAndInvalidatesCache()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        int eventCount = 0;
        TaskCompletionSource secondEventTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<CommandsInvalidatedEvent>((evt, cancellationToken) =>
        {
            if (evt.WorkspaceId == workspaceId && Interlocked.Increment(ref eventCount) == 2)
            {
                _ = secondEventTcs.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            CustomCommand initial = new()
            {
                WorkspaceId = workspaceId,
                Trigger = "!lurk",
                AliasesJson = "[]",
                GlobalCooldownSeconds = 5,
                RoutineJson = "{}",
                IsEnabled = true,
            };

            await repository.UpsertAsync(initial, ct);
            _ = await repository.GetCommandsAsync(workspaceId, ct);

            CustomCommand updated = new()
            {
                Id = initial.Id,
                WorkspaceId = workspaceId,
                Trigger = "!brb",
                AliasesJson = "[\"!afk\"]",
                GlobalCooldownSeconds = 30,
                UserCooldownSeconds = 10,
                CostPoints = 25,
                RewardPoints = 5,
                BoundCounterName = "lurks",
                CounterUsageBehavior = CounterUsageBehavior.IncrementOnUse,
                DeliveryMode = CommandDeliveryMode.ThreadedReply,
                StreamStatusRequirement = StreamStatusRequirement.OnlineOnly,
                IsHiddenFromPublicPortal = true,
                ExposeAsDiscordSlashCommand = true,
                RoutineJson = "{\"steps\":[\"ok\"]}",
                IsEnabled = false,
                Metadata = new CommandMetadata
                {
                    TargetScope = OutboundTargetScope.AllActiveStreamPlatforms,
                    AllowedPlatforms = ["twitch", "discord"],
                    ExtraSettings = new Dictionary<string, string>(StringComparer.Ordinal) { ["tone"] = "friendly" },
                },
            };

            await repository.UpsertAsync(updated, ct);
            await secondEventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            IReadOnlyList<CustomCommand> commands = await repository.GetCommandsAsync(workspaceId, ct);
            _ = commands.Should().ContainSingle();
            CustomCommand stored = commands[0];
            _ = stored.Id.Should().Be(initial.Id);
            _ = stored.Trigger.Should().Be("!brb");
            _ = stored.AliasesJson.Should().Be("[\"!afk\"]");
            _ = stored.GlobalCooldownSeconds.Should().Be(30);
            _ = stored.UserCooldownSeconds.Should().Be(10);
            _ = stored.CostPoints.Should().Be(25);
            _ = stored.RewardPoints.Should().Be(5);
            _ = stored.BoundCounterName.Should().Be("lurks");
            _ = stored.CounterUsageBehavior.Should().Be(CounterUsageBehavior.IncrementOnUse);
            _ = stored.DeliveryMode.Should().Be(CommandDeliveryMode.ThreadedReply);
            _ = stored.StreamStatusRequirement.Should().Be(StreamStatusRequirement.OnlineOnly);
            _ = stored.IsHiddenFromPublicPortal.Should().BeTrue();
            _ = stored.ExposeAsDiscordSlashCommand.Should().BeTrue();
            _ = stored.RoutineJson.Should().Be("{\"steps\":[\"ok\"]}");
            _ = stored.IsEnabled.Should().BeFalse();
            _ = stored.Metadata.TargetScope.Should().Be(OutboundTargetScope.AllActiveStreamPlatforms);
            _ = stored.Metadata.AllowedPlatforms.Should().Equal("twitch", "discord");
            _ = stored.Metadata.ExtraSettings.Should().ContainKey("tone").WhoseValue.Should().Be("friendly");
        }
    }

    [Fact]
    [Trait("Member", "UpsertAsync")]
    public async Task UpsertAsync_WhenMatchingByWorkspaceAndTriggerWithEmptyId_UpdatesExistingRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            CustomCommand first = new()
            {
                WorkspaceId = workspaceId,
                Trigger = "!shoutout",
                AliasesJson = "[]",
                GlobalCooldownSeconds = 10,
                RoutineJson = "{}",
                IsEnabled = true,
            };
            await repository.UpsertAsync(first, ct);

            CustomCommand second = new()
            {
                Id = Guid.Empty,
                WorkspaceId = workspaceId,
                Trigger = " !SHOUTOUT ",
                AliasesJson = "[\"!so\"]",
                GlobalCooldownSeconds = 45,
                RoutineJson = "{\"updated\":true}",
                IsEnabled = false,
            };
            await repository.UpsertAsync(second, ct);

            _ = second.Id.Should().Be(first.Id);
            IReadOnlyList<CustomCommand> commands = await repository.GetCommandsAsync(workspaceId, ct);
            _ = commands.Should().ContainSingle();
            _ = commands[0].GlobalCooldownSeconds.Should().Be(45);
            _ = commands[0].AliasesJson.Should().Be("[\"!so\"]");
            _ = commands[0].IsEnabled.Should().BeFalse();
        }
    }
    #endregion

    #region DeleteAsync
    [Fact]
    [Trait("Member", "DeleteAsync")]
    public async Task DeleteAsync_WhenCommandExists_DeletesRowInvalidatesCacheAndPublishesEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        List<CommandsInvalidatedEvent> events = [];
        TaskCompletionSource secondEventTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<CommandsInvalidatedEvent>((evt, cancellationToken) =>
        {
            events.Add(evt);
            if (events.Count == 2)
            {
                _ = secondEventTcs.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            CustomCommand command = new()
            {
                WorkspaceId = workspaceId,
                Trigger = "!todelete",
                AliasesJson = "[]",
                RoutineJson = "{}",
                IsEnabled = true,
            };
            await repository.UpsertAsync(command, ct);
            _ = await repository.GetCommandsAsync(workspaceId, ct);

            await repository.DeleteAsync(workspaceId, command.Id, ct);
            await secondEventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            IReadOnlyList<CustomCommand> remaining = await repository.GetCommandsAsync(workspaceId, ct);
            _ = remaining.Should().BeEmpty();
            _ = events.Should().HaveCount(2).And.AllSatisfy(e => e.WorkspaceId.Should().Be(workspaceId));
        }
    }

    [Fact]
    [Trait("Member", "DeleteAsync")]
    public async Task DeleteAsync_WhenMissingOrWrongWorkspace_DoesNotDeleteOrPublishEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        List<CommandsInvalidatedEvent> events = [];
        TaskCompletionSource deleteEventTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<CommandsInvalidatedEvent>((evt, cancellationToken) =>
        {
            events.Add(evt);
            if (events.Count == 2)
            {
                _ = deleteEventTcs.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        (CommandRepository repository, ServiceProvider provider) = CreateSut(factory, eventBus);
        await using (provider.ConfigureAwait(false))
        {
            CustomCommand command = new()
            {
                WorkspaceId = workspaceA,
                Trigger = "!keep",
                AliasesJson = "[]",
                RoutineJson = "{}",
                IsEnabled = true,
            };
            await repository.UpsertAsync(command, ct);

            await repository.DeleteAsync(workspaceB, command.Id, ct);
            await repository.DeleteAsync(workspaceA, Guid.CreateVersion7(), ct);

            IReadOnlyList<CustomCommand> stillPresent = await repository.GetCommandsAsync(workspaceA, ct);
            _ = stillPresent.Should().ContainSingle(c => c.Id == command.Id);

            await repository.DeleteAsync(workspaceA, command.Id, ct);
            await deleteEventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = events.Should().HaveCount(2);
        }
    }
    #endregion

    #region AddStreamActionsDatabase
    [Fact]
    [Trait("Member", "AddStreamActionsDatabase")]
    public void AddStreamActionsDatabase_RegistersICommandRepositoryAsSingleton()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:Database:Provider"] = "Sqlite",
                ["Bootstrap:Database:ConnectionString"] = "Data Source=:memory:",
            })
            .Build();

        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddDataProtection();
        _ = services.AddStreamActionsCore(configuration);
        _ = services.AddStreamActionsDatabase(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        ICommandRepository first = provider.GetRequiredService<ICommandRepository>();
        ICommandRepository second = provider.GetRequiredService<ICommandRepository>();

        _ = first.Should().BeOfType<CommandRepository>();
        _ = second.Should().BeSameAs(first);
    }
    #endregion

    /// <summary>
    /// Creates a <see cref="CommandRepository"/> backed by a fresh <see cref="HybridCache"/> service provider.
    /// </summary>
    /// <param name="factory">The test SQLite context factory.</param>
    /// <param name="eventBus">The event bus instance.</param>
    /// <returns>The repository and owning <see cref="ServiceProvider"/>.</returns>
    private static (CommandRepository Repository, ServiceProvider Provider) CreateSut(
        TestSqliteContextFactory factory,
        IEventBus eventBus)
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        ServiceProvider provider = services.BuildServiceProvider();
        WorkspaceCacheService cache = new(provider.GetRequiredService<HybridCache>());
        CommandRepository repository = new(factory, cache, eventBus);
        return (repository, provider);
    }
}
