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
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class PanelUserStoreTests
{
    #region HasAnyUsersAsync
    [Fact]
    [Trait("Member", "HasAnyUsersAsync")]
    public async Task HasAnyUsersAsync_WhenEmptyAndAfterCreate_ReturnsExpectedBoolean()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        _ = (await store.HasAnyUsersAsync(ct)).Should().BeFalse();

        _ = await store.CreateUserAsync(
            new PanelUser { DisplayName = "Moderator" },
            cancellationToken: ct);

        _ = (await store.HasAnyUsersAsync(ct)).Should().BeTrue();
    }
    #endregion

    #region HasRootUserAsync
    [Fact]
    [Trait("Member", "HasRootUserAsync")]
    public async Task HasRootUserAsync_WhenNonRootThenRootCreated_ReturnsTrueOnlyAfterRootExists()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        _ = await store.CreateUserAsync(
            new PanelUser { DisplayName = "AdminUser", IsGlobalAdmin = true, IsRoot = false },
            cancellationToken: ct);

        _ = (await store.HasRootUserAsync(ct)).Should().BeFalse();

        _ = await store.CreateUserAsync(
            new PanelUser { DisplayName = "RootUser", IsRoot = true, IsGlobalAdmin = true },
            cancellationToken: ct);

        _ = (await store.HasRootUserAsync(ct)).Should().BeTrue();
    }
    #endregion

    #region CreateUserAsync
    [Fact]
    [Trait("Member", "CreateUserAsync")]
    public async Task CreateUserAsync_WithInitialIdentity_NormalizesIdentityAndPersistsAtomically()
    {
        using TestSqliteContextFactory factory = new();
        DateTimeOffset now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(now);
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUser created = await store.CreateUserAsync(
            new PanelUser
            {
                DisplayName = "  StreamerOne  ",
                IsRoot = true,
                IsGlobalAdmin = true,
            },
            new UserLoginIdentity
            {
                ProviderId = "  TWITCH ",
                ProviderUserId = "  User_123  ",
                ProviderUsername = "  StreamerOne ",
            },
            ct);

        _ = created.Id.Should().NotBeEmpty();
        _ = created.DisplayName.Should().Be("StreamerOne");
        _ = created.CreatedAtUtc.Should().Be(now);

        PanelUser? fetchedById = await store.GetByIdAsync(created.Id, ct);
        _ = fetchedById.Should().NotBeNull();
        _ = fetchedById!.IsRoot.Should().BeTrue();

        PanelUser? fetchedByIdentity = await store.GetByLoginIdentityAsync("twitch", "USER_123", ct);
        _ = fetchedByIdentity.Should().NotBeNull();
        _ = fetchedByIdentity!.Id.Should().Be(created.Id);

        IReadOnlyList<UserLoginIdentity> identities = await store.GetLoginIdentitiesAsync(created.Id, ct);
        _ = identities.Should().ContainSingle();
        _ = identities[0].ProviderId.Should().Be("twitch");
        _ = identities[0].ProviderUserId.Should().Be("user_123");
        _ = identities[0].ProviderUsername.Should().Be("StreamerOne");
    }

    [Fact]
    [Trait("Member", "CreateUserAsync")]
    public async Task CreateUserAsync_WhenInitialIdentityAlreadyLinked_ThrowsInvalidOperationException()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        _ = await store.CreateUserAsync(
            new PanelUser { DisplayName = "FirstUser", IsRoot = true },
            new UserLoginIdentity
            {
                ProviderId = "twitch",
                ProviderUserId = "42",
                ProviderUsername = "first",
            },
            ct);

        Func<Task> act = async () => _ = await store.CreateUserAsync(
            new PanelUser { DisplayName = "SecondUser" },
            new UserLoginIdentity
            {
                ProviderId = "TWITCH",
                ProviderUserId = " 42 ",
                ProviderUsername = "second",
            },
            ct).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<InvalidOperationException>();
    }
    #endregion

    #region GetAllUsersAsync
    [Fact]
    [Trait("Member", "GetAllUsersAsync")]
    public async Task GetAllUsersAsync_WhenMultipleUsersExist_ReturnsOrderedByCreatedAtUtc()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUser first = await store.CreateUserAsync(new PanelUser { DisplayName = "First" }, cancellationToken: ct);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        PanelUser second = await store.CreateUserAsync(new PanelUser { DisplayName = "Second" }, cancellationToken: ct);

        IReadOnlyList<PanelUser> all = await store.GetAllUsersAsync(ct);
        _ = all.Select(static x => x.Id).Should().Equal(first.Id, second.Id);
    }
    #endregion

    #region UpdateUserAsync
    [Fact]
    [Trait("Member", "UpdateUserAsync")]
    public async Task UpdateUserAsync_WhenUserExists_UpdatesFields()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUser user = await store.CreateUserAsync(
            new PanelUser { DisplayName = "Initial", IsRoot = false, IsGlobalAdmin = false, MfaResetByAdminNoticePending = true },
            cancellationToken: ct);
        _ = user.MfaResetByAdminNoticePending.Should().BeTrue();

        user.DisplayName = "  Promoted  ";
        user.IsGlobalAdmin = true;
        user.MfaResetByAdminNoticePending = false;
        PanelUser updated = await store.UpdateUserAsync(user, ct);

        _ = updated.DisplayName.Should().Be("Promoted");
        _ = updated.IsGlobalAdmin.Should().BeTrue();
        _ = updated.MfaResetByAdminNoticePending.Should().BeFalse();

        PanelUser? persisted = await store.GetByIdAsync(user.Id, ct);
        _ = persisted.Should().NotBeNull();
        _ = persisted!.DisplayName.Should().Be("Promoted");
        _ = persisted.IsGlobalAdmin.Should().BeTrue();
        _ = persisted.MfaResetByAdminNoticePending.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "UpdateUserAsync")]
    public async Task UpdateUserAsync_WhenUserMissing_ThrowsKeyNotFoundException()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Func<Task> act = async () => _ = await store.UpdateUserAsync(
            new PanelUser { Id = Guid.CreateVersion7(), DisplayName = "Missing" },
            ct).ConfigureAwait(false);

        _ = await act.Should().ThrowAsync<KeyNotFoundException>();
    }
    #endregion

    #region LinkLoginIdentityAsync
    [Fact]
    [Trait("Member", "LinkLoginIdentityAsync")]
    public async Task LinkLoginIdentityAsync_WhenRelinkedToSameUser_UpdatesUsernameAndRejectsDifferentUser()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUser userA = await store.CreateUserAsync(new PanelUser { DisplayName = "UserA" }, cancellationToken: ct);
        PanelUser userB = await store.CreateUserAsync(new PanelUser { DisplayName = "UserB" }, cancellationToken: ct);

        UserLoginIdentity linked = await store.LinkLoginIdentityAsync(
            new UserLoginIdentity
            {
                PanelUserId = userA.Id,
                ProviderId = "Discord",
                ProviderUserId = "999",
                ProviderUsername = "OldName",
            },
            ct);

        _ = linked.ProviderId.Should().Be("discord");
        _ = linked.ProviderUserId.Should().Be("999");

        UserLoginIdentity updated = await store.LinkLoginIdentityAsync(
            new UserLoginIdentity
            {
                PanelUserId = userA.Id,
                ProviderId = "discord",
                ProviderUserId = "999",
                ProviderUsername = "NewName",
            },
            ct);

        _ = updated.Id.Should().Be(linked.Id);
        _ = updated.ProviderUsername.Should().Be("NewName");

        Func<Task> stealAttempt = async () => _ = await store.LinkLoginIdentityAsync(
            new UserLoginIdentity
            {
                PanelUserId = userB.Id,
                ProviderId = "discord",
                ProviderUserId = "999",
                ProviderUsername = "Hijack",
            },
            ct).ConfigureAwait(false);

        _ = await stealAttempt.Should().ThrowAsync<InvalidOperationException>();
    }
    #endregion

    #region UnlinkLoginIdentityAsync
    [Fact]
    [Trait("Member", "UnlinkLoginIdentityAsync")]
    public async Task UnlinkLoginIdentityAsync_WhenLinked_RemovesIdentityAndReturnsTrue()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        PanelUserStore store = new(factory, timeProvider);
        CancellationToken ct = TestContext.Current.CancellationToken;

        PanelUser user = await store.CreateUserAsync(
            new PanelUser { DisplayName = "User" },
            new UserLoginIdentity
            {
                ProviderId = "twitch",
                ProviderUserId = "1001",
                ProviderUsername = "user",
            },
            ct);

        bool removed = await store.UnlinkLoginIdentityAsync(user.Id, "TWITCH", "1001", ct);
        bool removedAgain = await store.UnlinkLoginIdentityAsync(user.Id, "twitch", "1001", ct);

        _ = removed.Should().BeTrue();
        _ = removedAgain.Should().BeFalse();
        _ = (await store.GetLoginIdentitiesAsync(user.Id, ct)).Should().BeEmpty();
        _ = (await store.GetByLoginIdentityAsync("twitch", "1001", ct)).Should().BeNull();
    }
    #endregion
}
