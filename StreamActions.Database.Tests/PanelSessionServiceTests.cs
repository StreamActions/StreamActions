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
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

/// <summary>
/// Unit and integration tests for <see cref="PanelSessionService"/>.
/// </summary>
public sealed class PanelSessionServiceTests
{
    #region CreateSessionAsync

    [Fact]
    [Trait("Member", "CreateSessionAsync")]
    public async Task CreateSessionAsync_PersistsSessionAndWarmsHybridCache()
    {
        using TestSqliteContextFactory baseFactory = new();
        (Guid userId, _) = await baseFactory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        CountingContextFactory countingFactory = new(baseFactory);

        ServiceCollection services = new();
        _ = services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        DateTimeOffset now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(now);
        PanelSessionService service = new(countingFactory, cache, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUserSession created = await service.CreateSessionAsync(
            userId,
            "twitch",
            remoteIpAddress: "127.0.0.1",
            userAgent: "StreamActionsTest/1.0",
            cancellationToken: ct);

        _ = created.Id.Should().NotBeEmpty();
        _ = created.PanelUserId.Should().Be(userId);
        _ = created.ProviderId.Should().Be("twitch");
        _ = created.CreatedAtUtc.Should().Be(now);
        _ = created.LastSeenAtUtc.Should().Be(now);
        _ = created.RemoteIpAddress.Should().Be("127.0.0.1");
        _ = created.UserAgent.Should().Be("StreamActionsTest/1.0");
        _ = created.RevokedAtUtc.Should().BeNull();

        PanelUserSession? fetched = await service.GetSessionAsync(created.Id, ct);
        _ = fetched.Should().NotBeNull();
        _ = fetched!.Id.Should().Be(created.Id);

        countingFactory.ResetCount();
        bool valid = await service.IsSessionValidAsync(created.Id, userId, ct);
        _ = valid.Should().BeTrue();
        _ = countingFactory.CreateCount.Should().Be(0);
    }

    #endregion

    #region GetActiveSessionsForUserAsync

    [Fact]
    [Trait("Member", "GetActiveSessionsForUserAsync")]
    public async Task GetActiveSessionsForUserAsync_ReturnsOnlyNonRevokedSessionsOrderedByCreatedAtDescending()
    {
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);

        ServiceCollection services = new();
        _ = services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelSessionService service = new(factory, cache, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUserSession first = await service.CreateSessionAsync(userId, "twitch", cancellationToken: ct);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        PanelUserSession second = await service.CreateSessionAsync(userId, "passkey", cancellationToken: ct);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        PanelUserSession third = await service.CreateSessionAsync(userId, "oidc:corporate", cancellationToken: ct);

        _ = await service.RevokeSessionAsync(second.Id, ct);

        IReadOnlyList<PanelUserSession> active = await service.GetActiveSessionsForUserAsync(userId, ct);
        _ = active.Select(static s => s.Id).Should().Equal(third.Id, first.Id);
    }

    #endregion

    #region IsSessionValidAsync

    [Fact]
    [Trait("Member", "IsSessionValidAsync")]
    public async Task IsSessionValidAsync_CachesLookupWithZeroDbQueriesOnCacheHit_AndEvictsOnRevoke()
    {
        using TestSqliteContextFactory baseFactory = new();
        (Guid userId, _) = await baseFactory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        CountingContextFactory countingFactory = new(baseFactory);

        ServiceCollection buildServices = new();
        _ = buildServices.AddHybridCache();
        await using ServiceProvider warmProvider = buildServices.BuildServiceProvider();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

        PanelSessionService creatorService = new(baseFactory, warmProvider.GetRequiredService<HybridCache>(), timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;
        PanelUserSession session = await creatorService.CreateSessionAsync(userId, "twitch", cancellationToken: ct);

        // Fresh HybridCache instance to verify cold cache -> DB hit -> warm cache -> 0 DB queries
        ServiceCollection freshServices = new();
        _ = freshServices.AddHybridCache();
        await using ServiceProvider freshProvider = freshServices.BuildServiceProvider();
        HybridCache freshCache = freshProvider.GetRequiredService<HybridCache>();
        PanelSessionService service = new(countingFactory, freshCache, timeProvider);

        countingFactory.ResetCount();
        bool firstCheck = await service.IsSessionValidAsync(session.Id, userId, ct);
        _ = firstCheck.Should().BeTrue();
        _ = countingFactory.CreateCount.Should().Be(1);

        // Subsequent checks hit HybridCache with zero DB queries
        bool secondCheck = await service.IsSessionValidAsync(session.Id, userId, ct);
        bool thirdCheck = await service.IsSessionValidAsync(session.Id, userId, ct);
        _ = secondCheck.Should().BeTrue();
        _ = thirdCheck.Should().BeTrue();
        _ = countingFactory.CreateCount.Should().Be(1);

        // Wrong expected user ID returns false even when cached active
        bool wrongUserCheck = await service.IsSessionValidAsync(session.Id, Guid.CreateVersion7(), ct);
        _ = wrongUserCheck.Should().BeFalse();

        // Revoking immediately evicts cache so IsSessionValidAsync returns false
        bool revoked = await service.RevokeSessionAsync(session.Id, ct);
        _ = revoked.Should().BeTrue();

        bool afterRevoke = await service.IsSessionValidAsync(session.Id, userId, ct);
        _ = afterRevoke.Should().BeFalse();
    }

    #endregion

    #region RevokeOtherSessionsAsync

    [Fact]
    [Trait("Member", "RevokeOtherSessionsAsync")]
    public async Task RevokeOtherSessionsAsync_PreservesCurrentSession_RevokesAllOthers_EvictsCacheAndPublishesEvent()
    {
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);

        ServiceCollection services = new();
        _ = services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PanelSessionService service = new(factory, cache, timeProvider, eventBus);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUserSession currentSession = await service.CreateSessionAsync(userId, "twitch", cancellationToken: ct);
        PanelUserSession otherSession1 = await service.CreateSessionAsync(userId, "passkey", cancellationToken: ct);
        PanelUserSession otherSession2 = await service.CreateSessionAsync(userId, "oidc:corp", cancellationToken: ct);

        // Warm cache for all three sessions
        _ = (await service.IsSessionValidAsync(currentSession.Id, userId, ct)).Should().BeTrue();
        _ = (await service.IsSessionValidAsync(otherSession1.Id, userId, ct)).Should().BeTrue();
        _ = (await service.IsSessionValidAsync(otherSession2.Id, userId, ct)).Should().BeTrue();

        TaskCompletionSource<UserSessionsRevokedEvent> eventTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<UserSessionsRevokedEvent>((evt, cancellationToken) =>
        {
            _ = eventTcs.TrySetResult(evt);
            return ValueTask.CompletedTask;
        });

        timeProvider.Advance(TimeSpan.FromMinutes(10));
        int revokedCount = await service.RevokeOtherSessionsAsync(userId, currentSession.Id, ct);

        _ = revokedCount.Should().Be(2);
        _ = (await service.IsSessionValidAsync(currentSession.Id, userId, ct)).Should().BeTrue();
        _ = (await service.IsSessionValidAsync(otherSession1.Id, userId, ct)).Should().BeFalse();
        _ = (await service.IsSessionValidAsync(otherSession2.Id, userId, ct)).Should().BeFalse();

        IReadOnlyList<PanelUserSession> activeSessions = await service.GetActiveSessionsForUserAsync(userId, ct);
        _ = activeSessions.Select(static s => s.Id).Should().Equal(currentSession.Id);

        UserSessionsRevokedEvent published = await eventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        _ = published.PanelUserId.Should().Be(userId);
        _ = published.ExcludedSessionId.Should().Be(currentSession.Id);
    }

    #endregion

    #region RevokeAllSessionsForUserAsync

    [Fact]
    [Trait("Member", "RevokeAllSessionsForUserAsync")]
    public async Task RevokeAllSessionsForUserAsync_RevokesAllUserSessions_EvictsCacheAndPublishesEventWithNullExcludedId()
    {
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);

        ServiceCollection services = new();
        _ = services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();
        HybridCache cache = provider.GetRequiredService<HybridCache>();

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PanelSessionService service = new(factory, cache, timeProvider, eventBus);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUserSession s1 = await service.CreateSessionAsync(userId, "twitch", cancellationToken: ct);
        PanelUserSession s2 = await service.CreateSessionAsync(userId, "passkey", cancellationToken: ct);

        _ = (await service.IsSessionValidAsync(s1.Id, userId, ct)).Should().BeTrue();
        _ = (await service.IsSessionValidAsync(s2.Id, userId, ct)).Should().BeTrue();

        TaskCompletionSource<UserSessionsRevokedEvent> eventTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable sub = eventBus.Subscribe<UserSessionsRevokedEvent>((evt, cancellationToken) =>
        {
            _ = eventTcs.TrySetResult(evt);
            return ValueTask.CompletedTask;
        });

        int revokedCount = await service.RevokeAllSessionsForUserAsync(userId, ct);

        _ = revokedCount.Should().Be(2);
        _ = (await service.IsSessionValidAsync(s1.Id, userId, ct)).Should().BeFalse();
        _ = (await service.IsSessionValidAsync(s2.Id, userId, ct)).Should().BeFalse();
        _ = (await service.GetActiveSessionsForUserAsync(userId, ct)).Should().BeEmpty();

        UserSessionsRevokedEvent published = await eventTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        _ = published.PanelUserId.Should().Be(userId);
        _ = published.ExcludedSessionId.Should().BeNull();
    }

    #endregion

    /// <summary>
    /// Wraps an <see cref="IDbContextFactory{TContext}"/> to count database context creations during cache hit verification.
    /// </summary>
    /// <param name="inner">The underlying context factory.</param>
    private sealed class CountingContextFactory(IDbContextFactory<StreamActionsDbContext> inner)
        : IDbContextFactory<StreamActionsDbContext>
    {
        /// <summary>
        /// Counter tracking the number of created <see cref="StreamActionsDbContext"/> instances.
        /// </summary>
        private int _createCount;

        /// <summary>
        /// Gets the number of <see cref="StreamActionsDbContext"/> instances created since the last reset.
        /// </summary>
        public int CreateCount => Volatile.Read(ref this._createCount);

        /// <summary>
        /// Resets <see cref="CreateCount"/> to zero.
        /// </summary>
        public void ResetCount() => Interlocked.Exchange(ref this._createCount, 0);

        /// <inheritdoc/>
        public StreamActionsDbContext CreateDbContext()
        {
            _ = Interlocked.Increment(ref this._createCount);
            return inner.CreateDbContext();
        }

        /// <inheritdoc/>
        public Task<StreamActionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref this._createCount);
            return inner.CreateDbContextAsync(cancellationToken);
        }
    }
}

