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

public sealed class InstanceOAuthConfigServiceTests
{
    #region GetAllAsync
    [Fact]
    [Trait("Member", "GetAllAsync")]
    public async Task GetAllAsync_WhenUnconfigured_ReturnsEmptyDictionary()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using InstanceSettingsService settingsService = new(factory, timeProvider);
        SecretProtectionService secretProtection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        using InstanceOAuthConfigService service = new(settingsService, secretProtection);
        CancellationToken ct = TestContext.Current.CancellationToken;

        IReadOnlyDictionary<string, PlatformOAuthAppCredentials> all = await service.GetAllAsync(ct);

        _ = all.Should().BeEmpty();
        _ = (await service.GetAsync("twitch", ct)).Should().BeNull();
    }

    [Fact]
    [Trait("Member", "GetAllAsync")]
    public async Task GetAllAsync_WhenKeyRingReplaced_ReturnsEmptyWithoutThrowing()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using InstanceSettingsService settingsService = new(factory, timeProvider);
        SecretProtectionService originalProtection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        using InstanceOAuthConfigService writerService = new(settingsService, originalProtection);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await writerService.SetAsync("twitch", new PlatformOAuthAppCredentials("cid", "csecret"), ct);

        SecretProtectionService replacedProtection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        using InstanceOAuthConfigService readerService = new(settingsService, replacedProtection);

        IReadOnlyDictionary<string, PlatformOAuthAppCredentials> all = await readerService.GetAllAsync(ct);
        _ = all.Should().BeEmpty();
        _ = (await readerService.GetAsync("twitch", ct)).Should().BeNull();
    }
    #endregion

    #region SetAsync
    [Fact]
    [Trait("Member", "SetAsync")]
    public async Task SetAsync_WhenMultiplePlatformsConfigured_EncryptsPayloadAndNormalizesKeys()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using InstanceSettingsService settingsService = new(factory, timeProvider);
        SecretProtectionService secretProtection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        using InstanceOAuthConfigService service = new(settingsService, secretProtection);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Uri twitchRedirect = new("https://panel.example.com/signin-twitch");
        await service.SetAsync("  TWITCH ", new PlatformOAuthAppCredentials(" twitch-id ", " twitch-secret ", twitchRedirect), ct);
        await service.SetAsync("Discord", new PlatformOAuthAppCredentials("discord-id", "discord-secret"), ct);

        InstanceSettings persistedSettings = await settingsService.GetAsync(ct);
        _ = persistedSettings.EncryptedPlatformOAuthConfigJson.Should().NotBeNullOrWhiteSpace();
        _ = persistedSettings.EncryptedPlatformOAuthConfigJson.Should().NotContain("twitch-secret");
        _ = persistedSettings.EncryptedPlatformOAuthConfigJson.Should().NotContain("discord-secret");

        PlatformOAuthAppCredentials? twitch = await service.GetAsync("twitch", ct);
        _ = twitch.Should().NotBeNull();
        _ = twitch!.ClientId.Should().Be("twitch-id");
        _ = twitch.ClientSecret.Should().Be("twitch-secret");
        _ = twitch.RedirectUri.Should().Be(twitchRedirect);

        IReadOnlyDictionary<string, PlatformOAuthAppCredentials> all = await service.GetAllAsync(ct);
        _ = all.Keys.Should().BeEquivalentTo("twitch", "discord");
    }
    #endregion

    #region RemoveAsync
    [Fact]
    [Trait("Member", "RemoveAsync")]
    public async Task RemoveAsync_WhenRemovingPlatforms_DeletesEntriesAndClearsPayloadWhenEmpty()
    {
        using TestSqliteContextFactory factory = new();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        using InstanceSettingsService settingsService = new(factory, timeProvider);
        SecretProtectionService secretProtection = new(new EphemeralDataProtectionProvider(), NullLogger<SecretProtectionService>.Instance);
        using InstanceOAuthConfigService service = new(settingsService, secretProtection);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await service.SetAsync("twitch", new PlatformOAuthAppCredentials("tid", "tsecret"), ct);
        await service.SetAsync("discord", new PlatformOAuthAppCredentials("did", "dsecret"), ct);

        bool removedTwitch = await service.RemoveAsync("TWITCH", ct);
        bool removedMissing = await service.RemoveAsync("kick", ct);

        _ = removedTwitch.Should().BeTrue();
        _ = removedMissing.Should().BeFalse();
        _ = (await service.GetAsync("twitch", ct)).Should().BeNull();
        _ = (await service.GetAsync("discord", ct)).Should().NotBeNull();

        bool removedDiscord = await service.RemoveAsync("discord", ct);
        _ = removedDiscord.Should().BeTrue();
        _ = (await service.GetAllAsync(ct)).Should().BeEmpty();

        InstanceSettings persistedSettings = await settingsService.GetAsync(ct);
        _ = persistedSettings.EncryptedPlatformOAuthConfigJson.Should().BeNull();
    }
    #endregion
}
