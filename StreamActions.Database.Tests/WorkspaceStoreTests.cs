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
using StreamActions.Core.Services;
using StreamActions.Database.Security;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class WorkspaceStoreTests
{
    #region CreateWorkspaceAsync

    [Fact]
    [Trait("Member", "CreateWorkspaceAsync")]
    public async Task CreateWorkspaceAsync_NormalizesPlatformIdAndSeedsBuiltInWorkspaceGroups()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (Guid userId, _) = await factory.SeedWorkspaceAsync(ct);

        SecretProtectionService protection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionStore permissionStore = new(factory, timeProvider, eventBus);
        WorkspaceStore store = new(factory, protection, permissionStore, timeProvider);

        Workspace created = await store.CreateWorkspaceAsync(
            new Workspace
            {
                OwnerUserId = userId,
                Name = "  Main Channel  ",
                PrimaryPlatformId = "  TWITCH ",
            },
            new WorkspacePlatform
            {
                PlatformId = " TWITCH ",
                Role = PlatformRole.Primary,
                PlatformChannelId = "12345",
            },
            ct);

        _ = created.PrimaryPlatformId.Should().Be("twitch");
        _ = created.Name.Should().Be("Main Channel");
        _ = created.CreatedAtUtc.Should().Be(timeProvider.GetUtcNow());

        IReadOnlyList<WorkspacePlatform> platforms = await store.GetPlatformsAsync(created.Id, ct);
        _ = platforms.Should().ContainSingle();
        _ = platforms[0].PlatformId.Should().Be("twitch");

        IReadOnlyList<PermissionGroup> groups = await permissionStore.GetGroupsAsync(created.Id, ct);
        _ = groups.Select(static g => g.Name).Should().Contain(["Broadcaster", "Moderator", "VIP", "Subscriber", "Regular", "Follower", "Everyone"]);
    }

    #endregion

    #region CustomOAuthApp

    [Fact]
    [Trait("Member", "SetCustomOAuthAppAsync")]
    public async Task SetAndGetCustomOAuthAppAsync_EncryptsSecretAndHandlesKeyRingMismatchSafely()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(ct);

        FakeTimeProvider timeProvider = new();
        using InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        PermissionStore permissionStore = new(factory, timeProvider, eventBus);
        SecretProtectionService protectionA = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        WorkspaceStore storeA = new(factory, protectionA, permissionStore, timeProvider);

        _ = await storeA.UpsertPlatformAsync(
            new WorkspacePlatform
            {
                WorkspaceId = workspaceId,
                PlatformId = "TWITCH",
                Role = PlatformRole.Primary,
                PlatformChannelId = "999",
            },
            cancellationToken: ct);

        WorkspacePlatform updated = await storeA.SetCustomOAuthAppAsync(workspaceId, "TWITCH", "custom-cid", "custom-secret", ct);
        _ = updated.PlatformId.Should().Be("twitch");
        _ = updated.CustomClientId.Should().Be("custom-cid");
        _ = updated.EncryptedCustomClientSecret.Should().NotBeNullOrWhiteSpace().And.NotContain("custom-secret");

        PlatformOAuthAppCredentials? decrypted = await storeA.GetCustomOAuthAppAsync(workspaceId, "twitch", ct);
        _ = decrypted.Should().NotBeNull();
        _ = decrypted!.ClientId.Should().Be("custom-cid");
        _ = decrypted.ClientSecret.Should().Be("custom-secret");

        SecretProtectionService protectionB = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        WorkspaceStore storeB = new(factory, protectionB, permissionStore, timeProvider);

        PlatformOAuthAppCredentials? mismatch = await storeB.GetCustomOAuthAppAsync(workspaceId, "twitch", ct);
        _ = mismatch.Should().BeNull();
    }

    #endregion
}
