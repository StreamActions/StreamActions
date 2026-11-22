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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class ViewerLoyaltyStoreTests
{
    #region GetProfileAsync
    [Fact]
    [Trait("Member", "GetProfileAsync")]
    public async Task GetProfileAsync_NormalizesIdentityAndIsolatesWorkspaces()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        _ = await store.GetOrCreateProfileAsync(workspaceA, "  TWITCH ", " User123 ", "viewer_login", "Viewer Display", ct);

        WorkspaceViewerProfile? found = await store.GetProfileAsync(workspaceA, "twitch", "user123", ct);
        WorkspaceViewerProfile? otherWorkspace = await store.GetProfileAsync(workspaceB, "twitch", "user123", ct);

        _ = found.Should().NotBeNull();
        _ = found!.PlatformId.Should().Be("twitch");
        _ = found.PlatformUserId.Should().Be("user123");
        _ = otherWorkspace.Should().BeNull();
    }
    #endregion

    #region GetProfileByLoginAsync
    [Fact]
    [Trait("Member", "GetProfileByLoginAsync")]
    public async Task GetProfileByLoginAsync_NormalizesLoginCaseInsensitivelyAndIsolatesWorkspaces()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        _ = await store.GetOrCreateProfileAsync(workspaceA, "TWITCH", "uid-99", "Viewer_Login", "Viewer Display", ct);
        _ = await store.AdjustPointsAsync(workspaceA, "twitch", "uid-99", 420, ct);

        WorkspaceViewerProfile? found = await store.GetProfileByLoginAsync(workspaceA, "  Twitch ", "  viewer_LOGIN ", ct);
        WorkspaceViewerProfile? missingLogin = await store.GetProfileByLoginAsync(workspaceA, "twitch", "unknown_user", ct);
        WorkspaceViewerProfile? otherWorkspace = await store.GetProfileByLoginAsync(workspaceB, "twitch", "viewer_login", ct);

        _ = found.Should().NotBeNull();
        _ = found!.PlatformUserId.Should().Be("uid-99");
        _ = found.Points.Should().Be(420);
        _ = missingLogin.Should().BeNull();
        _ = otherWorkspace.Should().BeNull();
    }

    [Theory]
    [InlineData("", "user")]
    [InlineData("   ", "user")]
    [InlineData("twitch", "")]
    [InlineData("twitch", "   ")]
    [Trait("Member", "GetProfileByLoginAsync")]
    public async Task GetProfileByLoginAsync_WhenPlatformOrLoginBlank_ThrowsArgumentException(string platformId, string login)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        Func<Task> action = async () => _ = await store.GetProfileByLoginAsync(workspaceId, platformId, login, ct).ConfigureAwait(false);
        _ = await action.Should().ThrowAsync<ArgumentException>();
    }
    #endregion

    #region GetOrCreateProfileAsync
    [Fact]
    [Trait("Member", "GetOrCreateProfileAsync")]
    public async Task GetOrCreateProfileAsync_CreatesThenUpdatesIdentityMetadataPreservingBalances()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        DateTimeOffset t0 = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        ViewerLoyaltyStore store = new(factory, timeProvider);

        WorkspaceViewerProfile created = await store.GetOrCreateProfileAsync(
            workspaceId,
            "TWITCH",
            "User42",
            "old_login",
            "Old Display",
            ct);

        _ = created.Points.Should().Be(0);
        _ = created.UpdatedAtUtc.Should().Be(t0);

        _ = await store.AdjustPointsAsync(workspaceId, "twitch", "user42", 250, ct);
        await store.AddWatchTimeAndXpAsync(workspaceId, "twitch", "user42", 180, 40, ct);

        timeProvider.Advance(TimeSpan.FromHours(2));
        WorkspaceViewerProfile updated = await store.GetOrCreateProfileAsync(
            workspaceId,
            "twitch",
            "user42",
            "new_login",
            "New Display",
            ct);

        _ = updated.Id.Should().Be(created.Id);
        _ = updated.LastKnownLogin.Should().Be("new_login");
        _ = updated.LastKnownDisplayName.Should().Be("New Display");
        _ = updated.Points.Should().Be(250);
        _ = updated.WatchTimeSeconds.Should().Be(180);
        _ = updated.ChatXp.Should().Be(40);
        _ = updated.UpdatedAtUtc.Should().Be(t0.AddHours(2));
    }

    [Fact]
    [Trait("Member", "GetOrCreateProfileAsync")]
    public async Task GetOrCreateProfileAsync_ConcurrentCalls_CreatesSingleRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        IEnumerable<Task<WorkspaceViewerProfile>> tasks = Enumerable.Range(0, 10)
            .Select(_ => store.GetOrCreateProfileAsync(workspaceId, "twitch", "concurrent_user", "login", "Display", ct).AsTask());

        WorkspaceViewerProfile[] results = await Task.WhenAll(tasks);
        _ = results.Select(static r => r.Id).Distinct().Should().ContainSingle();

        await using StreamActionsDbContext db = factory.CreateDbContext();
        _ = (await db.WorkspaceViewerProfiles.CountAsync(x => x.WorkspaceId == workspaceId, ct)).Should().Be(1);
    }
    #endregion

    #region AdjustPointsAsync
    [Fact]
    [Trait("Member", "AdjustPointsAsync")]
    public async Task AdjustPointsAsync_CreatesIfMissingAndAppliesSignedDeltas()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        DateTimeOffset t0 = new(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        ViewerLoyaltyStore store = new(factory, timeProvider);

        WorkspaceViewerProfile created = await store.AdjustPointsAsync(workspaceId, "Twitch", "U1", 100, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        WorkspaceViewerProfile adjusted = await store.AdjustPointsAsync(workspaceId, "twitch", "u1", -35, ct);

        _ = created.Points.Should().Be(100);
        _ = adjusted.Points.Should().Be(65);
        _ = adjusted.UpdatedAtUtc.Should().Be(t0.AddMinutes(1));
    }

    [Fact]
    [Trait("Member", "AdjustPointsAsync")]
    public async Task AdjustPointsAsync_ConcurrentCalls_NeverLoseUpdates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        const int concurrentCalls = 25;
        IEnumerable<Task<WorkspaceViewerProfile>> tasks = Enumerable.Range(0, concurrentCalls)
            .Select(_ => store.AdjustPointsAsync(workspaceId, "twitch", "u_conc", 10L, ct).AsTask());

        _ = await Task.WhenAll(tasks);

        WorkspaceViewerProfile? profile = await store.GetProfileAsync(workspaceId, "twitch", "u_conc", ct);
        _ = profile.Should().NotBeNull();
        _ = profile!.Points.Should().Be(concurrentCalls * 10L);
    }
    #endregion

    #region TrySpendPointsAsync
    [Fact]
    [Trait("Member", "TrySpendPointsAsync")]
    public async Task TrySpendPointsAsync_DeductsAtomicallyWhenSufficientAndRejectsWhenInsufficient()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        DateTimeOffset t0 = new(2026, 5, 3, 9, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        ViewerLoyaltyStore store = new(factory, timeProvider);

        _ = await store.AdjustPointsAsync(workspaceId, "twitch", "spender", 100, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        bool firstSpend = await store.TrySpendPointsAsync(workspaceId, " TWITCH ", " SPENDER ", 40, ct);
        bool overSpend = await store.TrySpendPointsAsync(workspaceId, "twitch", "spender", 61, ct);
        bool exactSpend = await store.TrySpendPointsAsync(workspaceId, "twitch", "spender", 60, ct);
        bool missingProfileSpend = await store.TrySpendPointsAsync(workspaceId, "twitch", "missing", 1, ct);

        _ = firstSpend.Should().BeTrue();
        _ = overSpend.Should().BeFalse();
        _ = exactSpend.Should().BeTrue();
        _ = missingProfileSpend.Should().BeFalse();

        WorkspaceViewerProfile? after = await store.GetProfileAsync(workspaceId, "twitch", "spender", ct);
        _ = after.Should().NotBeNull();
        _ = after!.Points.Should().Be(0);
        _ = after.UpdatedAtUtc.Should().Be(t0.AddMinutes(5));
    }

    [Fact]
    [Trait("Member", "TrySpendPointsAsync")]
    public async Task TrySpendPointsAsync_WhenCostIsZero_ReturnsTrueWithoutCreatingProfile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        bool result = await store.TrySpendPointsAsync(workspaceId, "twitch", "no_profile", 0L, ct);
        _ = result.Should().BeTrue();
        _ = (await store.GetProfileAsync(workspaceId, "twitch", "no_profile", ct)).Should().BeNull();
    }

    [Fact]
    [Trait("Member", "TrySpendPointsAsync")]
    public async Task TrySpendPointsAsync_WhenCostIsNegative_ThrowsArgumentOutOfRangeException()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        Func<Task> action = async () => _ = await store.TrySpendPointsAsync(workspaceId, "twitch", "u1", -1L, ct).ConfigureAwait(false);
        _ = await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Member", "TrySpendPointsAsync")]
    public async Task TrySpendPointsAsync_ConcurrentSpends_NeverOverdraws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        ViewerLoyaltyStore store = new(factory, TimeProvider.System);

        _ = await store.AdjustPointsAsync(workspaceId, "twitch", "race_user", 100L, ct);

        IEnumerable<Task<bool>> tasks = Enumerable.Range(0, 10)
            .Select(_ => store.TrySpendPointsAsync(workspaceId, "twitch", "race_user", 30L, ct).AsTask());

        bool[] outcomes = await Task.WhenAll(tasks);

        _ = outcomes.Count(static o => o).Should().Be(3);
        _ = outcomes.Count(static o => !o).Should().Be(7);

        WorkspaceViewerProfile? profile = await store.GetProfileAsync(workspaceId, "twitch", "race_user", ct);
        _ = profile.Should().NotBeNull();
        _ = profile!.Points.Should().Be(10L);
    }
    #endregion

    #region AddWatchTimeAndXpAsync
    [Fact]
    [Trait("Member", "AddWatchTimeAndXpAsync")]
    public async Task AddWatchTimeAndXpAsync_CreatesIfMissingAndIncrementsExisting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);
        DateTimeOffset t0 = new(2026, 5, 4, 18, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        ViewerLoyaltyStore store = new(factory, timeProvider);

        await store.AddWatchTimeAndXpAsync(workspaceId, "Twitch", "Watcher", 60, 15, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        await store.AddWatchTimeAndXpAsync(workspaceId, "twitch", "watcher", 120, 30, ct);

        WorkspaceViewerProfile? profile = await store.GetProfileAsync(workspaceId, "twitch", "watcher", ct);
        _ = profile.Should().NotBeNull();
        _ = profile!.WatchTimeSeconds.Should().Be(180);
        _ = profile.ChatXp.Should().Be(45);
        _ = profile.Points.Should().Be(0);
        _ = profile.UpdatedAtUtc.Should().Be(t0.AddMinutes(2));
    }
    #endregion

    #region GetTopByPointsAsync
    [Fact]
    [Trait("Member", "GetTopByPointsAsync")]
    public async Task GetTopByPointsAsync_OrdersDescendingWithDeterministicTieBreakAndWorkspaceIsolation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(ct);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(ct);
        DateTimeOffset t0 = new(2026, 5, 5, 10, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(t0);
        ViewerLoyaltyStore store = new(factory, timeProvider);

        _ = await store.AdjustPointsAsync(workspaceA, "twitch", "u_low", 50, ct);
        _ = await store.AdjustPointsAsync(workspaceA, "twitch", "u_tie_b", 200, ct);
        _ = await store.AdjustPointsAsync(workspaceA, "twitch", "u_tie_a", 200, ct);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        _ = await store.AdjustPointsAsync(workspaceA, "twitch", "u_tie_later", 200, ct);
        _ = await store.AdjustPointsAsync(workspaceA, "twitch", "u_top", 500, ct);
        _ = await store.AdjustPointsAsync(workspaceB, "twitch", "u_other_ws", 9999, ct);

        IReadOnlyList<WorkspaceViewerProfile> top4 = await store.GetTopByPointsAsync(workspaceA, 4, ct);
        _ = top4.Select(static p => p.PlatformUserId)
            .Should().Equal("u_top", "u_tie_a", "u_tie_b", "u_tie_later");

        IReadOnlyList<WorkspaceViewerProfile> zero = await store.GetTopByPointsAsync(workspaceA, 0, ct);
        _ = zero.Should().BeEmpty();

        Func<Task> negativeCount = async () => _ = await store.GetTopByPointsAsync(workspaceA, -1, ct).ConfigureAwait(false);
        _ = await negativeCount.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
    #endregion

    #region AddStreamActionsDatabase
    [Fact]
    [Trait("Member", "AddStreamActionsDatabase")]
    public void AddStreamActionsDatabase_RegistersIViewerLoyaltyStoreAsSingleton()
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
        IViewerLoyaltyStore first = provider.GetRequiredService<IViewerLoyaltyStore>();
        IViewerLoyaltyStore second = provider.GetRequiredService<IViewerLoyaltyStore>();

        _ = first.Should().BeOfType<ViewerLoyaltyStore>();
        _ = second.Should().BeSameAs(first);
    }
    #endregion
}
