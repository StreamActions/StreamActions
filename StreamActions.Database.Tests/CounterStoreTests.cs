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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Counters;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class CounterStoreTests
{
    #region GetAsync
    [Fact]
    [Trait("Member", "GetAsync")]
    public async Task GetAsync_NormalizesNameAndGameCategoryByScopeMode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        CounterStore store = new(factory, timeProvider, eventBus);

        _ = await store.SetAsync(workspaceId, "  Deaths ", 10, CounterScopeMode.Global, "Ignored Category", ct);
        _ = await store.SetAsync(workspaceId, "DEATHS", 4, CounterScopeMode.PerGame, "  Elden Ring ", ct);

        WorkspaceCounter? global = await store.GetAsync(workspaceId, "deaths", CounterScopeMode.Global, "Anything", ct);
        WorkspaceCounter? perGame = await store.GetAsync(workspaceId, "  deaths ", CounterScopeMode.PerGame, "Elden Ring", ct);
        WorkspaceCounter? otherGame = await store.GetAsync(workspaceId, "deaths", CounterScopeMode.PerGame, "elden ring", ct);

        _ = global.Should().NotBeNull();
        _ = global!.CounterName.Should().Be("deaths");
        _ = global.GameCategory.Should().BeEmpty();
        _ = global.CurrentValue.Should().Be(10);

        _ = perGame.Should().NotBeNull();
        _ = perGame!.CounterName.Should().Be("deaths");
        _ = perGame.GameCategory.Should().Be("Elden Ring");
        _ = perGame.CurrentValue.Should().Be(4);

        _ = otherGame.Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetAsync")]
    public async Task GetAsync_WhenNotFound_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        CounterStore store = new(factory, TimeProvider.System, eventBus);

        WorkspaceCounter? missing = await store.GetAsync(workspaceId, "missing", CounterScopeMode.Global, string.Empty, ct);
        _ = missing.Should().BeNull();
    }
    #endregion

    #region GetAllAsync
    [Fact]
    [Trait("Member", "GetAllAsync")]
    public async Task GetAllAsync_ReturnsWorkspaceScopedCountersOrderedDeterministically()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        CounterStore store = new(factory, TimeProvider.System, eventBus);

        _ = await store.SetAsync(workspaceA, "wins", 3, CounterScopeMode.Global, string.Empty, ct);
        _ = await store.SetAsync(workspaceA, "deaths", 2, CounterScopeMode.PerGame, "Game B", ct);
        _ = await store.SetAsync(workspaceA, "deaths", 1, CounterScopeMode.PerGame, "Game A", ct);
        _ = await store.SetAsync(workspaceA, "deaths", 5, CounterScopeMode.Global, string.Empty, ct);
        _ = await store.SetAsync(workspaceB, "other", 99, CounterScopeMode.Global, string.Empty, ct);

        IReadOnlyList<WorkspaceCounter> allA = await store.GetAllAsync(workspaceA, ct);
        _ = allA.Should().HaveCount(4);
        _ = allA.Select(static c => (c.CounterName, c.ScopeMode, c.GameCategory, c.CurrentValue))
            .Should().Equal(
                ("deaths", CounterScopeMode.Global, string.Empty, 5L),
                ("deaths", CounterScopeMode.PerGame, "Game A", 1L),
                ("deaths", CounterScopeMode.PerGame, "Game B", 2L),
                ("wins", CounterScopeMode.Global, string.Empty, 3L));
    }
    #endregion

    #region AdjustAsync
    [Fact]
    [Trait("Member", "AdjustAsync")]
    public async Task AdjustAsync_CreatesAndUpdatesCounter_UpdatingTimestampAndPublishingEvents()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        DateTimeOffset t0 = new(2026, 7, 10, 8, 30, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        CounterStore store = new(factory, timeProvider, eventBus);

        List<CounterUpdatedEvent> events = [];
        TaskCompletionSource threeEventsTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<CounterUpdatedEvent>((evt, cancellationToken) =>
        {
            events.Add(evt);
            if (events.Count == 3)
            {
                _ = threeEventsTcs.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        WorkspaceCounter created = await store.AdjustAsync(workspaceId, "  Deaths ", 5, CounterScopeMode.Global, string.Empty, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        WorkspaceCounter incremented = await store.AdjustAsync(workspaceId, "deaths", 3, CounterScopeMode.Global, string.Empty, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        WorkspaceCounter decremented = await store.AdjustAsync(workspaceId, "deaths", -2, CounterScopeMode.Global, string.Empty, ct);

        await threeEventsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        _ = created.CurrentValue.Should().Be(5);
        _ = created.UpdatedAtUtc.Should().Be(t0);
        _ = incremented.CurrentValue.Should().Be(8);
        _ = incremented.UpdatedAtUtc.Should().Be(t0.AddMinutes(5));
        _ = decremented.CurrentValue.Should().Be(6);
        _ = decremented.UpdatedAtUtc.Should().Be(t0.AddMinutes(10));

        _ = events.Should().HaveCount(3);
        _ = events[0].Should().Be(new CounterUpdatedEvent(workspaceId, "deaths", CounterScopeMode.Global, string.Empty, null, 5, t0));
        _ = events[1].Should().Be(new CounterUpdatedEvent(workspaceId, "deaths", CounterScopeMode.Global, string.Empty, 5, 8, t0.AddMinutes(5)));
        _ = events[2].Should().Be(new CounterUpdatedEvent(workspaceId, "deaths", CounterScopeMode.Global, string.Empty, 8, 6, t0.AddMinutes(10)));
    }

    [Fact]
    [Trait("Member", "AdjustAsync")]
    public async Task AdjustAsync_ConcurrentCalls_NeverLoseUpdates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        CounterStore store = new(factory, TimeProvider.System, eventBus);

        const int concurrentCalls = 25;
        IEnumerable<Task<WorkspaceCounter>> tasks = Enumerable.Range(0, concurrentCalls)
            .Select(_ => store.AdjustAsync(workspaceId, "Deaths", 1L, CounterScopeMode.Global, string.Empty, ct).AsTask());

        _ = await Task.WhenAll(tasks);

        WorkspaceCounter? final = await store.GetAsync(workspaceId, "deaths", CounterScopeMode.Global, string.Empty, ct);
        _ = final.Should().NotBeNull();
        _ = final!.CurrentValue.Should().Be(concurrentCalls);
    }
    #endregion

    #region SetAsync
    [Fact]
    [Trait("Member", "SetAsync")]
    public async Task SetAsync_CreatesAndOverwritesCounterAndPublishesEvents()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        DateTimeOffset t0 = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        CounterStore store = new(factory, timeProvider, eventBus);

        List<CounterUpdatedEvent> events = [];
        TaskCompletionSource twoEventsTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<CounterUpdatedEvent>((evt, cancellationToken) =>
        {
            events.Add(evt);
            if (events.Count == 2)
            {
                _ = twoEventsTcs.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        WorkspaceCounter first = await store.SetAsync(workspaceId, "Wins", 10, CounterScopeMode.PerGame, " Halo ", ct);
        timeProvider.Advance(TimeSpan.FromHours(1));
        WorkspaceCounter second = await store.SetAsync(workspaceId, "wins", 42, CounterScopeMode.PerGame, "Halo", ct);

        await twoEventsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        _ = first.CurrentValue.Should().Be(10);
        _ = second.CurrentValue.Should().Be(42);
        _ = second.UpdatedAtUtc.Should().Be(t0.AddHours(1));
        _ = events[0].Should().Be(new CounterUpdatedEvent(workspaceId, "wins", CounterScopeMode.PerGame, "Halo", null, 10, t0));
        _ = events[1].Should().Be(new CounterUpdatedEvent(workspaceId, "wins", CounterScopeMode.PerGame, "Halo", 10, 42, t0.AddHours(1)));
    }
    #endregion

    #region DeleteAsync
    [Fact]
    [Trait("Member", "DeleteAsync")]
    public async Task DeleteAsync_WhenCounterExists_RemovesRowAndPublishesDeletionEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        DateTimeOffset t0 = new(2026, 9, 15, 14, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        CounterStore store = new(factory, timeProvider, eventBus);

        List<CounterUpdatedEvent> events = [];
        TaskCompletionSource deleteEventTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<CounterUpdatedEvent>((evt, cancellationToken) =>
        {
            events.Add(evt);
            if (events.Count == 2)
            {
                _ = deleteEventTcs.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        _ = await store.SetAsync(workspaceId, "deaths", 7, CounterScopeMode.Global, string.Empty, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(10));

        bool missingDeleted = await store.DeleteAsync(workspaceId, "nonexistent", CounterScopeMode.Global, string.Empty, ct);
        bool deleted = await store.DeleteAsync(workspaceId, " DEATHS ", CounterScopeMode.Global, string.Empty, ct);

        await deleteEventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        _ = missingDeleted.Should().BeFalse();
        _ = deleted.Should().BeTrue();
        _ = (await store.GetAsync(workspaceId, "deaths", CounterScopeMode.Global, string.Empty, ct)).Should().BeNull();
        _ = events.Should().HaveCount(2);
        _ = events[1].Should().Be(new CounterUpdatedEvent(workspaceId, "deaths", CounterScopeMode.Global, string.Empty, 7, null, t0.AddMinutes(10)));
    }
    #endregion

    #region AddStreamActionsDatabase
    [Fact]
    [Trait("Member", "AddStreamActionsDatabase")]
    public void AddStreamActionsDatabase_RegistersICounterStoreAsSingleton()
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
        ICounterStore first = provider.GetRequiredService<ICounterStore>();
        ICounterStore second = provider.GetRequiredService<ICounterStore>();

        _ = first.Should().BeOfType<CounterStore>();
        _ = second.Should().BeSameAs(first);
    }
    #endregion
}
