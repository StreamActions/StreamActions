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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Database.Security;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class PlatformCredentialStoreTests
{
    #region SaveTokenAsync

    [Fact]
    [Trait("Member", "SaveTokenAsync")]
    public async Task SaveAndTryGetTokenAsync_SupportsWorkspaceAndInstanceCredentials()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretProtectionService protection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        PlatformCredentialStore store = new(factory, protection, timeProvider);

        PlatformTokenPayload wsPayload = new(
            "ws-access",
            "ws-refresh",
            timeProvider.GetUtcNow().AddHours(4),
            ["chat:read", "chat:edit"],
            "bearer",
            "1001",
            "streamer",
            "cid-1");

        PlatformTokenPayload instanceBotPayload = new(
            "bot-access",
            "bot-refresh",
            timeProvider.GetUtcNow().AddHours(4),
            ["user:bot"],
            "bearer",
            "2002",
            "globalbot",
            "cid-inst");

        _ = await store.SaveTokenAsync(workspaceId, "TWITCH", TokenRole.Broadcaster, wsPayload, cancellationToken: ct);
        _ = await store.SaveTokenAsync(null, "twitch", TokenRole.BotChat, instanceBotPayload, cancellationToken: ct);

        (bool foundWs, CredentialStatus statusWs, PlatformTokenPayload? gotWs) = await store.TryGetTokenAsync(workspaceId, "twitch", TokenRole.Broadcaster, ct);
        (bool foundBot, CredentialStatus statusBot, PlatformTokenPayload? gotBot) = await store.TryGetTokenAsync(null, "twitch", TokenRole.BotChat, ct);

        _ = foundWs.Should().BeTrue();
        _ = statusWs.Should().Be(CredentialStatus.Active);
        _ = gotWs.Should().BeEquivalentTo(wsPayload);

        _ = foundBot.Should().BeTrue();
        _ = statusBot.Should().Be(CredentialStatus.Active);
        _ = gotBot.Should().BeEquivalentTo(instanceBotPayload);
    }

    #endregion

    #region TryGetTokenAsync

    [Fact]
    [Trait("Member", "TryGetTokenAsync")]
    public async Task TryGetTokenAsync_WhenKeyRingMismatches_MarksStatusKeyRingMismatchWithoutThrowing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretProtectionService protectionA = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        PlatformCredentialStore storeA = new(factory, protectionA, timeProvider);

        _ = await storeA.SaveTokenAsync(
            workspaceId,
            "twitch",
            TokenRole.Broadcaster,
            new PlatformTokenPayload("secret-access", "secret-refresh"),
            cancellationToken: ct);

        SecretProtectionService protectionB = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        PlatformCredentialStore storeB = new(factory, protectionB, timeProvider);

        (bool found, CredentialStatus status, PlatformTokenPayload? payload) = await storeB.TryGetTokenAsync(
            workspaceId,
            "twitch",
            TokenRole.Broadcaster,
            ct);

        _ = found.Should().BeTrue();
        _ = status.Should().Be(CredentialStatus.KeyRingMismatchRequiresReauth);
        _ = payload.Should().BeNull();

        PlatformCredential? record = await storeB.GetCredentialRecordAsync(workspaceId, "twitch", TokenRole.Broadcaster, ct);
        _ = record.Should().NotBeNull();
        _ = record!.Status.Should().Be(CredentialStatus.KeyRingMismatchRequiresReauth);
    }

    #endregion

    #region GetAllCredentialsAsync

    [Fact]
    [Trait("Member", "GetAllCredentialsAsync")]
    public async Task GetAllCredentialsAsync_ReturnsInstanceAndWorkspaceCredentials()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        SecretProtectionService protection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        PlatformCredentialStore store = new(factory, protection, timeProvider);

        _ = await store.SaveTokenAsync(null, "twitch", TokenRole.BotChat, new PlatformTokenPayload("bot-token"), cancellationToken: ct);
        _ = await store.SaveTokenAsync(workspaceId, "twitch", TokenRole.Broadcaster, new PlatformTokenPayload("broadcaster-token"), cancellationToken: ct);

        IReadOnlyList<PlatformCredential> all = await store.GetAllCredentialsAsync(ct);

        _ = all.Should().HaveCount(2);
        _ = all.Should().Contain(c => c.WorkspaceId == null && c.TokenRole == TokenRole.BotChat);
        _ = all.Should().Contain(c => c.WorkspaceId == workspaceId && c.TokenRole == TokenRole.Broadcaster);
    }

    #endregion
}
