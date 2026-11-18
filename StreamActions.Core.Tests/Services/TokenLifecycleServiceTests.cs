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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using System.Collections.Concurrent;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class TokenLifecycleServiceTests
{
    private static readonly Guid _workspaceId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    #region EnsureValidTokenAsync

    [Fact]
    [Trait("Member", "EnsureValidTokenAsync")]
    public async Task EnsureValidTokenAsyncWithTenConcurrentCallersOnExpiringTokenRefreshesExactlyOnce()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        InMemoryCredentialStore credStore = new(timeProvider);
        MockOAuthProvider oauthProvider = new(timeProvider)
        {
            RefreshDelay = TimeSpan.FromMilliseconds(50),
        };
        ServiceProvider sp = BuildScopeProvider();
        await using (sp.ConfigureAwait(false))
        using (TokenLifecycleService service = new(
            credStore,
            [oauthProvider],
            sp.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            NullLogger<TokenLifecycleService>.Instance))
        {
            _ = await credStore.SaveTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                new PlatformTokenPayload(
                    "expired-access",
                    "refresh-1",
                    timeProvider.GetUtcNow().AddMinutes(2),
                    ["chat:read"],
                    "bearer",
                    "1001",
                    "streamer",
                    "cid-1"),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);

            // Advance 1 second so UpdatedAtUtc changes upon save as well as ExpiresAtUtc moving beyond 10m.
            timeProvider.Advance(TimeSpan.FromSeconds(1));

            Task<TokenLifecycleResult>[] tasks =
            [
                .. Enumerable.Range(0, 10).Select(_ => service.EnsureValidTokenAsync(
                    _workspaceId,
                    "twitch",
                    TokenRole.Broadcaster,
                    cancellationToken: TestContext.Current.CancellationToken).AsTask()),
            ];

            TokenLifecycleResult[] results = await Task.WhenAll(tasks);

            _ = oauthProvider.RefreshCallCount.Should().Be(1);
            _ = results.Should().AllSatisfy(r =>
            {
                _ = r.IsUsable.Should().BeTrue();
                _ = r.Payload!.AccessToken.Should().Be("refreshed-access-1");
                _ = r.Payload.PlatformUserId.Should().Be("1001");
            });
            _ = results.Count(static r => r.WasRefreshed).Should().Be(1);
        }
    }

    [Fact]
    [Trait("Member", "EnsureValidTokenAsync")]
    public async Task EnsureValidTokenAsyncWhenTokenIsActiveOutsideRefreshWindowReturnsExistingWithoutRefresh()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        InMemoryCredentialStore credStore = new(timeProvider);
        MockOAuthProvider oauthProvider = new(timeProvider);
        ServiceProvider sp = BuildScopeProvider();
        await using (sp.ConfigureAwait(false))
        using (TokenLifecycleService service = new(
            credStore,
            [oauthProvider],
            sp.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            NullLogger<TokenLifecycleService>.Instance))
        {
            _ = await credStore.SaveTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                new PlatformTokenPayload("valid-access", "refresh-1", timeProvider.GetUtcNow().AddMinutes(30)),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);

            TokenLifecycleResult result = await service.EnsureValidTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                cancellationToken: TestContext.Current.CancellationToken);

            _ = result.Status.Should().Be(TokenLifecycleStatus.Valid);
            _ = result.Payload!.AccessToken.Should().Be("valid-access");
            _ = oauthProvider.RefreshCallCount.Should().Be(0);
        }
    }

    #endregion

    #region RunMaintenancePassAsync

    [Fact]
    [Trait("Member", "RunMaintenancePassAsync")]
    public async Task RunMaintenancePassAsyncWhenTimeAdvancesToTenMinutesBeforeExpiryProactivelyRefreshesToken()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        InMemoryCredentialStore credStore = new(timeProvider);
        MockOAuthProvider oauthProvider = new(timeProvider);
        ServiceProvider sp = BuildScopeProvider();
        await using (sp.ConfigureAwait(false))
        using (TokenLifecycleService service = new(
            credStore,
            [oauthProvider],
            sp.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            NullLogger<TokenLifecycleService>.Instance))
        {
            DateTimeOffset expiresAt = timeProvider.GetUtcNow().AddMinutes(40);
            _ = await credStore.SaveTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                new PlatformTokenPayload("initial-access", "refresh-1", expiresAt),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);

            // At T+29m (11m before expiry): should neither refresh nor validate.
            timeProvider.Advance(TimeSpan.FromMinutes(29));
            await service.RunMaintenancePassAsync(TestContext.Current.CancellationToken);
            _ = oauthProvider.RefreshCallCount.Should().Be(0);
            _ = oauthProvider.ValidateCallCount.Should().Be(0);

            // Advance 1 more minute -> T+30m (10m before expiry): proactive refresh must trigger.
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await service.RunMaintenancePassAsync(TestContext.Current.CancellationToken);

            _ = oauthProvider.RefreshCallCount.Should().Be(1);
            (_, CredentialStatus status, PlatformTokenPayload? updated) = await credStore.TryGetTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                TestContext.Current.CancellationToken);
            _ = status.Should().Be(CredentialStatus.Active);
            _ = updated!.AccessToken.Should().Be("refreshed-access-1");
        }
    }

    [Fact]
    [Trait("Member", "RunMaintenancePassAsync")]
    public async Task RunMaintenancePassAsyncWhenValidationFailsAndRefreshFailsMarksRevokedBeforeExpiryAndExpiredAfterExpiry()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        InMemoryCredentialStore credStore = new(timeProvider);
        MockOAuthProvider oauthProvider = new(timeProvider)
        {
            ValidationResult = new PlatformTokenValidationResult(IsValid: false),
            FailRefresh = true,
        };
        ServiceProvider sp = BuildScopeProvider();
        await using (sp.ConfigureAwait(false))
        using (TokenLifecycleService service = new(
            credStore,
            [oauthProvider],
            sp.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            NullLogger<TokenLifecycleService>.Instance))
        {
            // Token 1: expires in 4 hours, validated at +1h -> invalid & refresh fails -> Revoked.
            _ = await credStore.SaveTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                new PlatformTokenPayload("revoked-access", "refresh-bad", timeProvider.GetUtcNow().AddHours(4)),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);

            // Token 2: expires in 30m, at +1h is past expiry -> refresh fails -> Expired.
            _ = await credStore.SaveTokenAsync(
                null,
                "twitch",
                TokenRole.BotChat,
                new PlatformTokenPayload("expired-access", "refresh-bad-2", timeProvider.GetUtcNow().AddMinutes(30)),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromHours(1));
            await service.RunMaintenancePassAsync(TestContext.Current.CancellationToken);

            PlatformCredential? broadcasterRecord = await credStore.GetCredentialRecordAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                TestContext.Current.CancellationToken);
            PlatformCredential? botRecord = await credStore.GetCredentialRecordAsync(
                null,
                "twitch",
                TokenRole.BotChat,
                TestContext.Current.CancellationToken);

            _ = broadcasterRecord!.Status.Should().Be(CredentialStatus.Revoked);
            _ = botRecord!.Status.Should().Be(CredentialStatus.Expired);
        }
    }

    [Fact]
    [Trait("Member", "EnsureValidTokenAsync")]
    public async Task EnsureValidTokenAsyncPassesExpectedFlowToOAuthClientConfigResolver()
    {
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        InMemoryCredentialStore credStore = new(timeProvider);
        MockOAuthProvider oauthProvider = new(timeProvider);
        FakeOAuthClientConfigResolver resolver = new();
        ServiceCollection services = new();
        _ = services.AddScoped<IOAuthClientConfigResolver>(_ => resolver);
        ServiceProvider sp = services.BuildServiceProvider();

        await using (sp.ConfigureAwait(false))
        using (TokenLifecycleService service = new(
            credStore,
            [oauthProvider],
            sp.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            NullLogger<TokenLifecycleService>.Instance))
        {
            _ = await credStore.SaveTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                new PlatformTokenPayload("a1", "r1", timeProvider.GetUtcNow().AddMinutes(5)),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);
            _ = await credStore.SaveTokenAsync(
                _workspaceId,
                "twitch",
                TokenRole.BotChat,
                new PlatformTokenPayload("a2", "r2", timeProvider.GetUtcNow().AddMinutes(5)),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);
            _ = await credStore.SaveTokenAsync(
                null,
                "twitch",
                TokenRole.BotChat,
                new PlatformTokenPayload("a3", "r3", timeProvider.GetUtcNow().AddMinutes(5)),
                CredentialStatus.Active,
                TestContext.Current.CancellationToken);

            _ = await service.EnsureValidTokenAsync(_workspaceId, "twitch", TokenRole.Broadcaster, cancellationToken: TestContext.Current.CancellationToken);
            _ = await service.EnsureValidTokenAsync(_workspaceId, "twitch", TokenRole.BotChat, cancellationToken: TestContext.Current.CancellationToken);
            _ = await service.EnsureValidTokenAsync(null, "twitch", TokenRole.BotChat, cancellationToken: TestContext.Current.CancellationToken);

            _ = resolver.CapturedCalls.Should().ContainInOrder(
                ("twitch", _workspaceId, "broadcaster"),
                ("twitch", _workspaceId, "bot"),
                ("twitch", null, "instance-bot"));
        }
    }

    #endregion

    private static ServiceProvider BuildScopeProvider()
    {
        ServiceCollection services = new();
        _ = services.AddScoped<IOAuthClientConfigResolver>(static _ => new FakeOAuthClientConfigResolver());
        return services.BuildServiceProvider();
    }

    private sealed class FakeOAuthClientConfigResolver : IOAuthClientConfigResolver
    {
        public List<(string PlatformId, Guid? WorkspaceId, string? Flow)> CapturedCalls { get; } = [];

        public ValueTask<ResolvedOAuthClientConfig?> ResolveAsync(
            string platformId,
            Guid? workspaceId = null,
            PlatformUserIdentity? actor = null,
            string? flow = null,
            CancellationToken cancellationToken = default)
        {
            this.CapturedCalls.Add((platformId, workspaceId, flow));
            return ValueTask.FromResult<ResolvedOAuthClientConfig?>(new ResolvedOAuthClientConfig(
                platformId,
                "test-client-id",
                "test-client-secret",
                WorkspaceId: workspaceId));
        }
    }

    private sealed class MockOAuthProvider(TimeProvider timeProvider) : IPlatformOAuthProvider
    {
        private int _refreshCallCount;
        private int _validateCallCount;

        public string PlatformId => "twitch";
        public IReadOnlyList<string> DefaultLoginScopes => ["openid", "user:read:email"];
        public IReadOnlyList<string> DefaultBroadcasterScopes => ["chat:read", "chat:edit"];
        public IReadOnlyList<string> DefaultBotScopes => ["user:bot", "chat:read"];
        public int RefreshCallCount => Volatile.Read(ref this._refreshCallCount);
        public int ValidateCallCount => Volatile.Read(ref this._validateCallCount);
        public TimeSpan RefreshDelay { get; init; } = TimeSpan.Zero;
        public bool FailRefresh { get; set; }
        public PlatformTokenValidationResult ValidationResult { get; set; } = new(IsValid: true);

        public Uri BuildAuthorizationUri(string clientId, Uri redirectUri, IReadOnlyCollection<string>? scopes, string state, bool forceVerify = false, string? nonce = null) =>
            throw new NotSupportedException();

        public ValueTask<PlatformOAuthTokenResult> ExchangeCodeAsync(string clientId, string clientSecret, string code, Uri redirectUri, string? expectedNonce = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async ValueTask<PlatformOAuthTokenResult> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default)
        {
            if (this.RefreshDelay > TimeSpan.Zero)
            {
                await Task.Delay(this.RefreshDelay, cancellationToken).ConfigureAwait(false);
            }

            int callNumber = Interlocked.Increment(ref this._refreshCallCount);
            return this.FailRefresh
                ? throw new InvalidOperationException("Refresh rejected by mock provider.")
                : new PlatformOAuthTokenResult(
                $"refreshed-access-{callNumber}",
                $"refreshed-refresh-{callNumber}",
                timeProvider.GetUtcNow().AddHours(4),
                ["chat:read"],
                "bearer");
        }

        public ValueTask<PlatformTokenValidationResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref this._validateCallCount);
            return ValueTask.FromResult(this.ValidationResult);
        }

        public ValueTask RevokeTokenAsync(string clientId, string token, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class InMemoryCredentialStore(TimeProvider timeProvider) : IPlatformCredentialStore
    {
        private readonly ConcurrentDictionary<(Guid? WorkspaceId, string PlatformId, TokenRole Role), (PlatformCredential Record, PlatformTokenPayload Payload)> _items = new();
        private readonly Lock _sync = new();

        public ValueTask<PlatformCredential?> GetCredentialRecordAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default)
        {
            lock (this._sync)
            {
                return ValueTask.FromResult(this._items.TryGetValue((workspaceId, platformId.ToUpperInvariant(), tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry)
                    ? CloneRecord(entry.Record)
                    : null);
            }
        }

        public ValueTask<IReadOnlyList<PlatformCredential>> GetCredentialsForWorkspaceAsync(Guid? workspaceId, CancellationToken cancellationToken = default)
        {
            lock (this._sync)
            {
                return ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([.. this._items.Values.Where(v => v.Record.WorkspaceId == workspaceId).Select(static v => CloneRecord(v.Record))]);
            }
        }

        public ValueTask<IReadOnlyList<PlatformCredential>> GetAllCredentialsAsync(CancellationToken cancellationToken = default)
        {
            lock (this._sync)
            {
                return ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([.. this._items.Values.Select(static v => CloneRecord(v.Record))]);
            }
        }

        public ValueTask<(bool Found, CredentialStatus Status, PlatformTokenPayload? Payload)> TryGetTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default)
        {
            lock (this._sync)
            {
                return !this._items.TryGetValue((workspaceId, platformId.ToUpperInvariant(), tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry)
                    ? ValueTask.FromResult<(bool, CredentialStatus, PlatformTokenPayload?)>((false, CredentialStatus.Revoked, null))
                    : ValueTask.FromResult<(bool, CredentialStatus, PlatformTokenPayload?)>((true, entry.Record.Status, entry.Payload));
            }
        }

        public ValueTask<PlatformCredential> SaveTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, PlatformTokenPayload payload, CredentialStatus status = CredentialStatus.Active, CancellationToken cancellationToken = default)
        {
            lock (this._sync)
            {
                string normalized = platformId.ToUpperInvariant();
                Guid id = this._items.TryGetValue((workspaceId, normalized, tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) existing)
                    ? existing.Record.Id
                    : Guid.NewGuid();

                PlatformCredential record = new()
                {
                    Id = id,
                    WorkspaceId = workspaceId,
                    PlatformId = normalized,
                    TokenRole = tokenRole,
                    Status = status,
                    ExpiresAtUtc = payload.ExpiresAtUtc,
                    UpdatedAtUtc = timeProvider.GetUtcNow(),
                };
                this._items[(workspaceId, normalized, tokenRole)] = (record, payload);
                return ValueTask.FromResult(CloneRecord(record));
            }
        }

        public ValueTask<bool> UpdateStatusAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CredentialStatus status, CancellationToken cancellationToken = default)
        {
            lock (this._sync)
            {
                string normalized = platformId.ToUpperInvariant();
                if (!this._items.TryGetValue((workspaceId, normalized, tokenRole), out (PlatformCredential Record, PlatformTokenPayload Payload) entry))
                {
                    return ValueTask.FromResult(false);
                }

                entry.Record.Status = status;
                entry.Record.UpdatedAtUtc = timeProvider.GetUtcNow();
                return ValueTask.FromResult(true);
            }
        }

        public ValueTask<bool> DeleteTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._items.TryRemove((workspaceId, platformId.ToUpperInvariant(), tokenRole), out _));

        private static PlatformCredential CloneRecord(PlatformCredential source) => new()
        {
            Id = source.Id,
            WorkspaceId = source.WorkspaceId,
            PlatformId = source.PlatformId,
            TokenRole = source.TokenRole,
            EncryptedTokenJson = source.EncryptedTokenJson,
            Status = source.Status,
            ExpiresAtUtc = source.ExpiresAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc,
        };
    }
}
