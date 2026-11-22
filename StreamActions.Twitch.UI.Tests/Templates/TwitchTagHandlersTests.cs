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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using StreamActions.Core.Models.Metadata;
using StreamActions.Core.Templates;
using StreamActions.Twitch.Api;
using StreamActions.Twitch.Api.ChannelPoints;
using StreamActions.Twitch.Api.Channels;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Api.Streams;
using StreamActions.Twitch.Api.Subscriptions;
using StreamActions.Twitch.Api.Teams;
using StreamActions.Twitch.Api.Teams.Objects;
using StreamActions.Twitch.Api.Users;
using StreamActions.Twitch.OAuth;
using StreamActions.Twitch.UI.Abstractions;
using StreamActions.Twitch.UI.Services;
using StreamActions.Twitch.UI.Templates;
using StreamActions.Twitch.UI.Tests.Infrastructure;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Xunit;

namespace StreamActions.Twitch.UI.Tests.Templates;

/// <summary>
/// Offline Kestrel Helix tests for <c>StreamActions.Twitch.UI</c> tag handlers, memoization, session leasing, and stream history.
/// </summary>
public sealed class TwitchTagHandlersTests
{
    #region Private Constants

    /// <summary>
    /// Deterministic workspace identifier used across tests.
    /// </summary>
    private static readonly Guid _workspaceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    #endregion Private Constants

    #region RegistryAndDi

    /// <summary>
    /// Verifies <see cref="ServiceCollectionExtensions.AddStreamActionsTwitchUi"/> and <see cref="TwitchTagRegistryFactory"/> register all 32 Twitch tags and override Core <c>{account_age}</c>.
    /// </summary>
    [Fact]
    [Trait("Member", "AddStreamActionsTwitchUi")]
    public async Task AddStreamActionsTwitchUiWhenRegisteredRegistersAllTwitchHandlersAndOverridesAccountAge()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        _ = services.AddStreamActionsCore(configuration);
        _ = services.AddStreamActionsTwitchUi();

        ServiceProvider provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            ITagRegistry registry = provider.GetRequiredService<ITagRegistry>();

            string[] expectedTags =
            [
                "game", "title", "uptime", "downtime", "playtime", "games_played",
                "viewers", "followers", "subscribers", "followage", "followdate", "hours", "account_age",
                "team_name", "team_url", "team_members", "team_random_member",
                "team_member_game", "team_member_followers", "team_member_url",
                "cp_title", "cp_prompt", "cp_cost", "cp_input", "cp_user", "cp_username",
                "cp_user_id", "cp_redemption_id", "cp_reward_id", "cp_status", "cp_fulfill", "cp_cancel",
            ];

            foreach (string tagName in expectedTags)
            {
                ICommandTagHandler? resolved = registry.Resolve(tagName, "twitch");
                _ = resolved.Should().NotBeNull($"tag '{tagName}' must be registered for twitch");
                _ = resolved.PlatformId.Should().Be("twitch");
            }

            ICommandTagHandler? universalAccountAge = registry.Resolve("account_age", platformId: null);
            _ = universalAccountAge.Should().NotBeNull();
            _ = universalAccountAge.PlatformId.Should().BeNull();
        }
    }

    #endregion RegistryAndDi

    #region WorkspaceSessionProvider

    /// <summary>
    /// Verifies <see cref="TwitchWorkspaceSessionProvider"/> resolves broadcaster credentials, OAuth client config, and channel metadata into a working disposable <see cref="TwitchWorkspaceSessionLease"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "TwitchWorkspaceSessionProvider")]
    public async Task TwitchWorkspaceSessionProviderWhenBroadcasterCredentialActiveResolvesLeaseAndExecutesHelixCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            server.RegisterJsonResponse("GET", "/helix/streams", new ResponseData<TwitchStream>
            {
                Status = System.Net.HttpStatusCode.OK,
                Data =
                [
                    new TwitchStream
                    {
                        UserId = "9001",
                        UserLogin = "mychannel",
                        GameName = "Hollow Knight",
                        Title = "Pantheon 5 Attempts",
                        ViewerCount = 42,
                    },
                ],
            });

            FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 7, 15, 18, 0, 0, TimeSpan.Zero));
            StubPlatformCredentialStore credentialStore = new();
            credentialStore.AddCredential(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                CredentialStatus.Active,
                new PlatformTokenPayload(
                    AccessToken: "broadcaster-access",
                    RefreshToken: "broadcaster-refresh",
                    ExpiresAtUtc: fakeTime.GetUtcNow().AddHours(2),
                    Scopes: ["channel:read:subscriptions", "moderator:read:followers"],
                    PlatformUserId: "9001",
                    PlatformUsername: "mychannel",
                    ClientId: "payload-client"));

            StubWorkspaceStore workspaceStore = new();
            workspaceStore.AddPlatform(new WorkspacePlatform
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _workspaceId,
                PlatformId = "twitch",
                PlatformChannelId = "9001",
                Metadata = new PlatformMetadata
                {
                    ExtraSettings = new Dictionary<string, string> { ["broadcaster_login"] = "mychannel" },
                },
            });

            StubOAuthClientConfigResolver oauthResolver = new(new ResolvedOAuthClientConfig(
                "twitch",
                "resolved-client-id",
                "resolved-client-secret"));

            TwitchWorkspaceSessionProvider sessionProvider = new(
                credentialStore,
                oauthResolver,
                workspaceStore,
                fakeTime,
                server.HelixBaseUri);

            TwitchWorkspaceSessionLease? lease = await sessionProvider.GetSessionAsync(_workspaceId, ct);
            _ = lease.Should().NotBeNull();
            await using (lease!.ConfigureAwait(false))
            {
                _ = lease!.BroadcasterId.Should().Be("9001");
                _ = lease.BroadcasterLogin.Should().Be("mychannel");
                _ = lease.TwitchSession.CustomClientId.Should().Be("resolved-client-id");
                _ = lease.TwitchSession.Token!.HasScope(Scope.ChannelReadSubscriptions).Should().BeTrue();

                ResponseData<TwitchStream>? streams = await TwitchStream.GetStreams(lease.TwitchSession, userLogins: ["mychannel"]);
                _ = streams.Should().NotBeNull();
                _ = streams!.Data.Should().ContainSingle(s => s.GameName == "Hollow Knight");
                _ = server.RecordedRequests.Should().ContainSingle(r =>
                    r.Headers["Client-Id"].Contains("resolved-client-id"));
            }
        }
    }

    /// <summary>
    /// Verifies <see cref="TwitchWorkspaceSessionProvider"/> falls back to bot/app credentials and returns <see langword="null"/> when no active credential exists.
    /// </summary>
    [Fact]
    [Trait("Member", "TwitchWorkspaceSessionProvider")]
    public async Task TwitchWorkspaceSessionProviderWhenBroadcasterMissingFallsBackToAppAndReturnsNullWhenEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StubPlatformCredentialStore credentialStore = new();
        StubWorkspaceStore workspaceStore = new();
        StubOAuthClientConfigResolver oauthResolver = new(null);

        TwitchWorkspaceSessionProvider provider = new(credentialStore, oauthResolver, workspaceStore);
        _ = (await provider.GetSessionAsync(Guid.Empty, ct)).Should().BeNull();
        _ = (await provider.GetSessionAsync(_workspaceId, ct)).Should().BeNull();

        credentialStore.AddCredential(
            null,
            "twitch",
            TokenRole.App,
            CredentialStatus.Active,
            new PlatformTokenPayload("app-token", ClientId: "app-client"));

        TwitchWorkspaceSessionLease? lease = await provider.GetSessionAsync(_workspaceId, ct);
        _ = lease.Should().NotBeNull();
        await using (lease!.ConfigureAwait(false))
        {
            _ = lease!.TwitchSession.Token!.Type.Should().Be(TwitchToken.TokenType.App);
            _ = lease.TwitchSession.CustomClientId.Should().Be("app-client");
        }
    }

    #endregion WorkspaceSessionProvider

    #region StreamAndChannelTags

    /// <summary>
    /// Verifies <c>{game}</c>, <c>{title}</c>, <c>{uptime}</c>, and <c>{viewers}</c> resolve from a single memoized <c>GET /helix/streams</c> request when online.
    /// </summary>
    [Fact]
    [Trait("Member", "StreamOnlineAndMemoization")]
    public async Task StreamTagsWhenOnlineReturnValuesAndMemoizeSingleHelixRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            DateTimeOffset now = new(2026, 7, 15, 18, 30, 45, TimeSpan.Zero);
            FakeTimeProvider fakeTime = new(now);

            server.RegisterHandler("GET", "/helix/streams", async httpContext =>
            {
                string? login = httpContext.Request.Query["user_login"].FirstOrDefault();
                ResponseData<TwitchStream> payload = string.Equals(login, "streamer", StringComparison.OrdinalIgnoreCase)
                    ? new ResponseData<TwitchStream>
                    {
                        Status = System.Net.HttpStatusCode.OK,
                        Data =
                        [
                            new TwitchStream
                            {
                                UserId = "1001",
                                UserLogin = "streamer",
                                GameName = "Celeste",
                                Title = "Any% Practice",
                                ViewerCount = 1234,
                                StartedAt = DateTime.SpecifyKind(new DateTime(2026, 7, 15, 16, 15, 30), DateTimeKind.Unspecified),
                            },
                        ],
                    }
                    : new ResponseData<TwitchStream>
                    {
                        Status = System.Net.HttpStatusCode.OK,
                        Data = [],
                    };

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, TwitchApi.SerializerOptions), Encoding.UTF8).ConfigureAwait(false);
            });

            ServiceProvider sp = BuildServiceProvider(server, "1001", "streamer", timeProvider: fakeTime);
            await using (sp.ConfigureAwait(false))
            {
                TagEvaluationContext evalContext = CreateContext(sp, fakeTime);

                TagResult result = await EvaluateAsync(
                    "{game} | {title} | {uptime} | {viewers} | {game \"streamer\"} | {uptime {channel}}",
                    evalContext,
                    ct);

                _ = result.IsComplete.Should().BeTrue();
                _ = result.Value.Should().Be("Celeste | Any% Practice | 2h 15m 15s | 1234 | Celeste | 2h 15m 15s");
                _ = server.CountRequests("/helix/streams").Should().Be(1);
                _ = server.CountRequests("/helix/channels").Should().Be(0);
            }
        }
    }

    /// <summary>
    /// Verifies <c>{game}</c>, <c>{title}</c>, <c>{uptime}</c>, and <c>{viewers}</c> offline fallback behavior and memoization across <c>GetStreams</c> and <c>GetChannelInformation</c>.
    /// </summary>
    [Fact]
    [Trait("Member", "StreamOfflineFallback")]
    public async Task StreamTagsWhenOfflineFallBackToChannelInfoAndMemoizeRequests()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 7, 15, 18, 0, 0, TimeSpan.Zero));

            server.RegisterJsonResponse("GET", "/helix/streams", new ResponseData<TwitchStream>
            {
                Status = System.Net.HttpStatusCode.OK,
                Data = [],
            });

            server.RegisterHandler("GET", "/helix/channels", async httpContext =>
            {
                string? broadcasterId = httpContext.Request.Query["broadcaster_id"].FirstOrDefault();
                ResponseData<Channel> payload = broadcasterId == "1001"
                    ? new ResponseData<Channel>
                    {
                        Status = System.Net.HttpStatusCode.OK,
                        Data =
                        [
                            new Channel
                            {
                                BroadcasterId = "1001",
                                BroadcasterLogin = "streamer",
                                GameName = "Retro",
                                Title = "Next stream soon!",
                            },
                        ],
                    }
                    : new ResponseData<Channel>
                    {
                        Status = System.Net.HttpStatusCode.OK,
                        Data = [],
                    };

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, TwitchApi.SerializerOptions), Encoding.UTF8).ConfigureAwait(false);
            });

            server.RegisterJsonResponse("GET", "/helix/users", new ResponseData<User>
            {
                Status = System.Net.HttpStatusCode.OK,
                Data = [],
            });

            ServiceProvider sp = BuildServiceProvider(server, "1001", "streamer", timeProvider: fakeTime);
            await using (sp.ConfigureAwait(false))
            {
                TagEvaluationContext evalContext = CreateContext(sp, fakeTime);

                TagResult result = await EvaluateAsync("{game} | {title} | {uptime} | {viewers}", evalContext, ct);
                _ = result.IsComplete.Should().BeTrue();
                _ = result.Value.Should().Be("Retro | Next stream soon! | Offline | 0");
                _ = server.CountRequests("/helix/streams").Should().Be(1);
                _ = server.CountRequests("/helix/channels").Should().Be(1);

                TagResult unknownChannelViewers = await EvaluateAsync("{viewers \"nonexistent_user\"}", CreateContext(sp, fakeTime), ct);
                _ = unknownChannelViewers.IsComplete.Should().BeFalse();
            }
        }
    }

    /// <summary>
    /// Verifies <c>{downtime}</c>, <c>{playtime}</c>, and <c>{games_played}</c> using <see cref="KeyValueTwitchStreamHistoryProvider"/> backed by <see cref="IWorkspaceKeyValueStore"/>.
    /// </summary>
    [Fact]
    [Trait("Member", "StreamHistoryTags")]
    public async Task StreamHistoryTagsWhenHistoryRecordedFormatDowntimePlaytimeAndGamesPlayed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            DateTimeOffset now = new(2026, 7, 15, 18, 0, 0, TimeSpan.Zero);
            FakeTimeProvider fakeTime = new(now);

            bool isOnline = false;
            server.RegisterHandler("GET", "/helix/streams", async httpContext =>
            {
                ResponseData<TwitchStream> payload = isOnline
                    ? new ResponseData<TwitchStream>
                    {
                        Status = System.Net.HttpStatusCode.OK,
                        Data = [new TwitchStream { UserId = "1001", UserLogin = "streamer", StartedAt = now.AddHours(-1).UtcDateTime }],
                    }
                    : new ResponseData<TwitchStream>
                    {
                        Status = System.Net.HttpStatusCode.OK,
                        Data = [],
                    };

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, TwitchApi.SerializerOptions), Encoding.UTF8).ConfigureAwait(false);
            });

            InMemoryKeyValueStore kvStore = new();
            KeyValueTwitchStreamHistoryProvider historyProvider = new(kvStore);
            await historyProvider.RecordStreamOfflineAsync(_workspaceId, now.AddHours(-3).AddMinutes(-15), cancellationToken: ct);
            await historyProvider.RecordStreamOfflineAsync(_workspaceId, now.AddMinutes(-45), channelLogin: "friend", cancellationToken: ct);
            await historyProvider.RecordGameStartedAsync(_workspaceId, "Just Chatting", now.AddHours(-2), ct);
            await historyProvider.RecordGameStartedAsync(_workspaceId, "Balatro", now.AddMinutes(-30), ct);

            ServiceProvider sp = BuildServiceProvider(server, "1001", "streamer", timeProvider: fakeTime, historyProvider: historyProvider);
            await using (sp.ConfigureAwait(false))
            {
                TagResult offlineDowntime = await EvaluateAsync("{downtime}", CreateContext(sp, fakeTime), ct);
                _ = offlineDowntime.IsComplete.Should().BeTrue();
                _ = offlineDowntime.Value.Should().Be("3h 15m 0s");

                TagResult friendDowntime = await EvaluateAsync("{downtime \"@friend\"}", CreateContext(sp, fakeTime), ct);
                _ = friendDowntime.IsComplete.Should().BeTrue();
                _ = friendDowntime.Value.Should().Be("45m 0s");

                TagResult playtime = await EvaluateAsync("{playtime}", CreateContext(sp, fakeTime), ct);
                _ = playtime.IsComplete.Should().BeTrue();
                _ = playtime.Value.Should().Be("30m 0s");

                TagResult gamesPlayed = await EvaluateAsync("{games_played}", CreateContext(sp, fakeTime), ct);
                _ = gamesPlayed.IsComplete.Should().BeTrue();
                _ = gamesPlayed.Value.Should().Be("Just Chatting (2026-07-15 16:00 UTC), Balatro (2026-07-15 17:30 UTC)");

                isOnline = true;
                TagResult onlineDowntime = await EvaluateAsync("{or {downtime} \"Online\"}", CreateContext(sp, fakeTime), ct);
                _ = onlineDowntime.IsComplete.Should().BeTrue();
                _ = onlineDowntime.Value.Should().Be("Online");

                await historyProvider.ResetStreamSessionAsync(_workspaceId, ct);
                _ = (await EvaluateAsync("{playtime}", CreateContext(sp, fakeTime), ct)).IsComplete.Should().BeFalse();
                _ = (await EvaluateAsync("{games_played}", CreateContext(sp, fakeTime), ct)).IsComplete.Should().BeFalse();

                KeyValueTwitchStreamHistoryProvider noStoreProvider = new();
                _ = (await noStoreProvider.GetLastOfflineAtUtcAsync(_workspaceId, cancellationToken: ct)).Should().BeNull();
                _ = (await noStoreProvider.GetCurrentGameStartedAtUtcAsync(_workspaceId, ct)).Should().BeNull();
                _ = (await noStoreProvider.GetGamesPlayedHistoryAsync(_workspaceId, ct)).Should().BeEmpty();
            }
        }
    }

    /// <summary>
    /// Verifies <c>{followers}</c> and <c>{subscribers}</c> return totals from memoized Helix calls and handle missing scopes cleanly.
    /// </summary>
    [Fact]
    [Trait("Member", "FollowersAndSubscribers")]
    public async Task FollowersAndSubscribersWhenAuthorizedReturnTotalsAndHandleMissingScope()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            server.RegisterHandler("GET", "/helix/users", async httpContext =>
            {
                string? login = httpContext.Request.Query["login"].FirstOrDefault();
                ResponseData<User> payload = string.Equals(login, "partner", StringComparison.OrdinalIgnoreCase)
                    ? new ResponseData<User> { Status = System.Net.HttpStatusCode.OK, Data = [new User { Id = "2002", Login = "partner" }] }
                    : new ResponseData<User> { Status = System.Net.HttpStatusCode.OK, Data = [] };

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, TwitchApi.SerializerOptions), Encoding.UTF8).ConfigureAwait(false);
            });

            server.RegisterHandler("GET", "/helix/channels/followers", async httpContext =>
            {
                string? broadcasterId = httpContext.Request.Query["broadcaster_id"].FirstOrDefault();
                int total = broadcasterId == "1001" ? 54321 : 999;
                ResponseData<ChannelFollower> payload = new()
                {
                    Status = System.Net.HttpStatusCode.OK,
                    Total = total,
                    Data = [],
                };

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, TwitchApi.SerializerOptions), Encoding.UTF8).ConfigureAwait(false);
            });

            server.RegisterJsonResponse("GET", "/helix/subscriptions", new SubscriptionResponse
            {
                Status = System.Net.HttpStatusCode.OK,
                Total = 321,
                Points = 400,
                Data = [],
            });

            ServiceProvider sp = BuildServiceProvider(server, "1001", "streamer", scopes: ["channel:read:subscriptions"]);
            await using (sp.ConfigureAwait(false))
            {
                TagEvaluationContext ctx = CreateContext(sp);

                TagResult combined = await EvaluateAsync("{followers} / {followers} / {followers \"@partner\"} / {subscribers} / {subscribers}", ctx, ct);
                _ = combined.IsComplete.Should().BeTrue();
                _ = combined.Value.Should().Be("54321 / 54321 / 999 / 321 / 321");
                _ = server.CountRequests("/helix/channels/followers").Should().Be(2);
                _ = server.CountRequests("/helix/subscriptions").Should().Be(1);
            }

            ServiceProvider unscopedSp = BuildServiceProvider(server, "1001", "streamer", scopes: ["chat:read"]);
            await using (unscopedSp.ConfigureAwait(false))
            {
                TagResult missingSubScope = await EvaluateAsync("{or {subscribers} \"NoScope\"}", CreateContext(unscopedSp), ct);
                _ = missingSubScope.IsComplete.Should().BeTrue();
                _ = missingSubScope.Value.Should().Be("NoScope");
            }
        }
    }

    /// <summary>
    /// Verifies <c>{followage}</c>, <c>{followdate}</c>, and <c>{account_age}</c> convert UTC timestamps deterministically across <see cref="DateTimeKind"/> values and memoize user/follow lookups.
    /// </summary>
    [Fact]
    [Trait("Member", "FollowageFollowdateAndAccountAge")]
    public async Task FollowAndAccountAgeTagsWhenQueriedFormatDeterministicUtcAndMemoize()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            DateTimeOffset now = new(2026, 7, 15, 14, 30, 0, TimeSpan.Zero);
            FakeTimeProvider fakeTime = new(now);

            server.RegisterHandler("GET", "/helix/users", async httpContext =>
            {
                string? login = httpContext.Request.Query["login"].FirstOrDefault();
                string? id = httpContext.Request.Query["id"].FirstOrDefault();

                List<User> users = [];
                if (string.Equals(login, "bob", StringComparison.OrdinalIgnoreCase) || id == "200")
                {
                    users.Add(new User
                    {
                        Id = "200",
                        Login = "bob",
                        CreatedAt = DateTime.SpecifyKind(new DateTime(2026, 7, 10, 12, 0, 0), DateTimeKind.Unspecified),
                    });
                }
                else if (string.Equals(login, "alice", StringComparison.OrdinalIgnoreCase) || id == "user-42")
                {
                    users.Add(new User
                    {
                        Id = "user-42",
                        Login = "alice",
                        CreatedAt = DateTime.SpecifyKind(new DateTime(2026, 7, 14, 14, 0, 0), DateTimeKind.Utc),
                    });
                }

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(new ResponseData<User> { Status = System.Net.HttpStatusCode.OK, Data = users }, TwitchApi.SerializerOptions),
                    Encoding.UTF8).ConfigureAwait(false);
            });

            server.RegisterHandler("GET", "/helix/channels/followers", async httpContext =>
            {
                string? userId = httpContext.Request.Query["user_id"].FirstOrDefault();
                List<ChannelFollower> followers = userId == "200"
                    ?
                    [
                        new ChannelFollower
                        {
                            UserId = "200",
                            UserLogin = "bob",
                            FollowedAt = DateTime.SpecifyKind(new DateTime(2026, 7, 12, 10, 15, 0), DateTimeKind.Unspecified),
                        },
                    ]
                    : [];

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(new ResponseData<ChannelFollower> { Status = System.Net.HttpStatusCode.OK, Total = 10, Data = followers }, TwitchApi.SerializerOptions),
                    Encoding.UTF8).ConfigureAwait(false);
            });

            ServiceProvider sp = BuildServiceProvider(server, "1001", "streamer", timeProvider: fakeTime);
            await using (sp.ConfigureAwait(false))
            {
                TagEvaluationContext ctx = CreateContext(sp, fakeTime);

                TagResult bobFollow = await EvaluateAsync("{followage \"@bob\"} on {followdate \"bob\"} (age: {account_age \"bob\"})", ctx, ct);
                _ = bobFollow.IsComplete.Should().BeTrue();
                _ = bobFollow.Value.Should().Be("3d 4h 15m 0s on 2026-07-12 (age: 2026-07-10 (5d 2h 30m 0s))");
                _ = server.CountRequests("/helix/users").Should().Be(1);
                _ = server.CountRequests("/helix/channels/followers").Should().Be(1);

                TagResult secondMessageFollow = await EvaluateAsync("{followdate \"bob\"}", CreateContext(sp, fakeTime), ct);
                _ = secondMessageFollow.IsComplete.Should().BeTrue();
                _ = secondMessageFollow.Value.Should().Be("2026-07-12");
                _ = server.CountRequests("/helix/users").Should().Be(2);
                _ = server.CountRequests("/helix/channels/followers").Should().Be(2);

                TagResult senderAge = await EvaluateAsync("{account_age}", CreateContext(sp, fakeTime), ct);
                _ = senderAge.IsComplete.Should().BeTrue();
                _ = senderAge.Value.Should().Be("2026-07-14 (1d 0h 30m 0s)");

                TagResult nonFollower = await EvaluateAsync("{followage}", CreateContext(sp, fakeTime), ct);
                _ = nonFollower.IsComplete.Should().BeFalse();
            }
        }
    }

    /// <summary>
    /// Verifies <c>{hours [user] [decimals]}</c> calculates invariant hours from <see cref="IViewerLoyaltyStore"/> and clamps decimals to <c>0..4</c>.
    /// </summary>
    [Theory]
    [InlineData("{hours}", "1.50")]
    [InlineData("{hours \"@bob\"}", "2.25")]
    [InlineData("{hours \"bob\" 0}", "2")]
    [InlineData("{hours \"bob\" 1}", "2.2")]
    [InlineData("{hours \"bob\" 3}", "2.250")]
    [InlineData("{hours \"bob\" 99}", "2.2500")]
    [InlineData("{hours \"bob\" -3}", "2")]
    [InlineData("{hours \"unknown\"}", "0.00")]
    [Trait("Member", "HoursTagHandler")]
    public async Task HoursTagWhenLoyaltyProfileExistsFormatsClampedInvariantDecimals(string template, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        InMemoryLoyaltyStore loyaltyStore = new();
        loyaltyStore.AddProfile(new WorkspaceViewerProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceId,
            PlatformId = "twitch",
            PlatformUserId = "user-42",
            LastKnownLogin = "alice",
            WatchTimeSeconds = 5400,
        });
        loyaltyStore.AddProfile(new WorkspaceViewerProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceId,
            PlatformId = "twitch",
            PlatformUserId = "user-99",
            LastKnownLogin = "bob",
            WatchTimeSeconds = 8100,
        });

        ServiceProvider sp = BuildServiceProvider(server: null, "1001", "streamer", loyaltyStore: loyaltyStore);
        await using (sp.ConfigureAwait(false))
        {
            TagResult result = await EvaluateAsync(template, CreateContext(sp), ct);

            _ = result.IsComplete.Should().BeTrue();
            _ = result.Value.Should().Be(expected);
        }
    }

    #endregion StreamAndChannelTags

    #region TeamTags

    /// <summary>
    /// Verifies all 7 Twitch team tags (<c>{team_name}</c>, <c>{team_url}</c>, <c>{team_members}</c>, <c>{team_random_member}</c>, <c>{team_member_game}</c>, <c>{team_member_followers}</c>, <c>{team_member_url}</c>) and per-message team memoization.
    /// </summary>
    [Fact]
    [Trait("Member", "TeamTags")]
    public async Task TeamTagsWhenTeamResolvedReturnExpectedValuesAndMemoizeTeamCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            server.RegisterHandler("GET", "/helix/teams", async httpContext =>
            {
                string? name = httpContext.Request.Query["name"].FirstOrDefault();
                List<Team> teams = string.Equals(name, "livecoders", StringComparison.OrdinalIgnoreCase)
                    ?
                    [
                        new Team
                        {
                            Id = "team-1",
                            TeamName = "livecoders",
                            TeamDisplayName = "Live Coders",
                            Users =
                            [
                                new TeamMember { UserId = "501", UserLogin = "coder_one", UserName = "CoderOne" },
                                new TeamMember { UserId = "502", UserLogin = "coder_two", UserName = "CoderTwo" },
                            ],
                        },
                    ]
                    : [];

                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(new ResponseData<Team> { Status = System.Net.HttpStatusCode.OK, Data = teams }, TwitchApi.SerializerOptions),
                    Encoding.UTF8).ConfigureAwait(false);
            });

            server.RegisterJsonResponse("GET", "/helix/streams", new ResponseData<TwitchStream>
            {
                Status = System.Net.HttpStatusCode.OK,
                Data =
                [
                    new TwitchStream { UserId = "501", UserLogin = "coder_one", GameName = "Software and Game Development" },
                ],
            });

            server.RegisterJsonResponse("GET", "/helix/channels/followers", new ResponseData<ChannelFollower>
            {
                Status = System.Net.HttpStatusCode.OK,
                Total = 8765,
                Data = [],
            });

            ServiceProvider sp = BuildServiceProvider(server, "1001", "streamer");
            await using (sp.ConfigureAwait(false))
            {
                TagEvaluationContext ctx = CreateContext(sp);

                TagResult combined = await EvaluateAsync(
                    "{team_name \"LiveCoders\"} | {team_url \"livecoders\"} | {team_members \"livecoders\"} | {team_member_game \"livecoders\" \"coder_one\"} | {team_member_followers \"livecoders\" \"@coder_one\"} | {team_member_url \"livecoders\" \"coder_one\"}",
                    ctx,
                    ct);

                _ = combined.IsComplete.Should().BeTrue();
                _ = combined.Value.Should().Be(
                    "Live Coders | https://www.twitch.tv/team/livecoders | CoderOne, CoderTwo | Software and Game Development | 8765 | https://www.twitch.tv/coder_one");
                _ = server.CountRequests("/helix/teams").Should().Be(1);

                TagResult randomMember = await EvaluateAsync("{team_random_member \"livecoders\"}", ctx, ct);
                _ = randomMember.IsComplete.Should().BeTrue();
                _ = randomMember.Value.Should().BeOneOf("CoderOne", "CoderTwo");
                _ = server.CountRequests("/helix/teams").Should().Be(1);

                TagResult nonMember = await EvaluateAsync("{team_member_url \"livecoders\" \"stranger\"}", ctx, ct);
                _ = nonMember.IsComplete.Should().BeFalse();

                TagResult missingTeam = await EvaluateAsync("{team_name \"unknown_team\"}", CreateContext(sp), ct);
                _ = missingTeam.IsComplete.Should().BeFalse();
            }
        }
    }

    #endregion TeamTags

    #region ChannelPointsTags

    /// <summary>
    /// Verifies all read-only <c>{cp_*}</c> tags and guarantees <c>{cp_input}</c> is never re-parsed as template syntax.
    /// </summary>
    [Fact]
    [Trait("Member", "ChannelPointsContextTags")]
    public async Task ChannelPointsTagsWhenContextPresentReturnValuesAndNeverParseCpInput()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string redemptionId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        string rewardId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        string maliciousInput = "{sender} {cp_fulfill} {points_add 9999}";

        ChannelPointRedemptionContext redemption = new(
            RedemptionId: redemptionId,
            RewardId: rewardId,
            RewardTitle: "Hydrate!",
            Cost: 500,
            RedeemerUserId: "3001",
            RedeemerLogin: "kindviewer",
            RedeemerDisplayName: "KindViewer",
            Status: "UNFULFILLED",
            RewardPrompt: "Remind the streamer to drink water",
            UserInput: maliciousInput);

        ServiceProvider sp = BuildServiceProvider(server: null, "1001", "streamer");
        await using (sp.ConfigureAwait(false))
        {
            TagEvaluationContext ctx = CreateContext(sp, channelPointRedemption: redemption);

            TagResult result = await EvaluateAsync(
                "{cp_title}|{cp_prompt}|{cp_cost}|{cp_input}|{cp_user}|{cp_username}|{cp_user_id}|{cp_redemption_id}|{cp_reward_id}|{cp_status}",
                ctx,
                ct);

            _ = result.IsComplete.Should().BeTrue();
            _ = result.Value.Should().Be(
                $"Hydrate!|Remind the streamer to drink water|500|{maliciousInput}|KindViewer|kindviewer|3001|{redemptionId}|{rewardId}|UNFULFILLED");

            TagEvaluationContext noRedemptionCtx = CreateContext(sp);
            _ = (await EvaluateAsync("{cp_title}", noRedemptionCtx, ct)).IsComplete.Should().BeFalse();
            _ = (await EvaluateAsync("{cp_cost}", noRedemptionCtx, ct)).IsComplete.Should().BeFalse();
            _ = (await EvaluateAsync("{cp_input}", noRedemptionCtx, ct)).IsComplete.Should().BeFalse();
        }
    }

    /// <summary>
    /// Verifies <c>{cp_fulfill}</c> and <c>{cp_cancel}</c> invoke <see cref="ChannelPointsRedemption.UpdateRedemptionStatus"/> with <c>FULFILLED</c> and <c>CANCELED</c>.
    /// </summary>
    [Theory]
    [InlineData("{cp_fulfill}", "FULFILLED")]
    [InlineData("{cp_cancel}", "CANCELED")]
    [Trait("Member", "ChannelPointsStatusMutation")]
    public async Task ChannelPointsMutationTagsWhenCalledSendPatchToHelixRedemptionsEndpoint(
        string template,
        string expectedStatus)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TwitchHelixMockServer server = await TwitchHelixMockServer.StartAsync(ct);
        await using (server.ConfigureAwait(false))
        {
            string redemptionId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            string rewardId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

            server.RegisterJsonResponse("PATCH", "/helix/channel_points/custom_rewards/redemptions", new ResponseData<ChannelPointsRedemption>
            {
                Status = System.Net.HttpStatusCode.OK,
                Data =
                [
                    new ChannelPointsRedemption
                    {
                        Id = Guid.Parse(redemptionId),
                        BroadcasterId = "1001",
                        Status = expectedStatus == "FULFILLED"
                            ? ChannelPointsRedemption.RedemptionStatus.Fulfilled
                            : ChannelPointsRedemption.RedemptionStatus.Canceled,
                    },
                ],
            });

            ChannelPointRedemptionContext redemption = new(
                RedemptionId: redemptionId,
                RewardId: rewardId,
                RewardTitle: "Highlight",
                Cost: 250,
                RedeemerUserId: "3001",
                RedeemerLogin: "viewer",
                RedeemerDisplayName: "Viewer");

            ServiceProvider sp = BuildServiceProvider(server, "1001", "streamer", scopes: ["channel:manage:redemptions"]);
            await using (sp.ConfigureAwait(false))
            {
                TagEvaluationContext ctx = CreateContext(sp, channelPointRedemption: redemption);

                TagResult result = await EvaluateAsync(template, ctx, ct);
                _ = result.IsComplete.Should().BeTrue();
                _ = result.Value.Should().BeEmpty();

                RecordedHelixRequest patchRequest = server.RecordedRequests.Should().ContainSingle(r => r.Method == "PATCH").Subject;
                _ = patchRequest.Query["broadcaster_id"].Should().ContainSingle().Which.Should().Be("1001");
                _ = patchRequest.Query["reward_id"].Should().ContainSingle().Which.Should().Be(rewardId);
                _ = patchRequest.Query["id"].Should().ContainSingle().Which.Should().Be(redemptionId);
                _ = patchRequest.Body.Should().Contain($"\"status\":\"{expectedStatus}\"");
            }
        }
    }

    #endregion ChannelPointsTags

    #region Private Helpers

    /// <summary>
    /// Parses and evaluates a template string against <paramref name="context"/>.
    /// </summary>
    /// <param name="template">The curly-brace template string.</param>
    /// <param name="context">The runtime evaluation context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The evaluated <see cref="TagResult"/>.</returns>
    private static ValueTask<TagResult> EvaluateAsync(
        string template,
        TagEvaluationContext context,
        CancellationToken cancellationToken)
    {
        TemplateParseResult parsed = TemplateParser.Parse(template, context.TagRegistry, context.PlatformId);
        return TemplateEvaluator.EvaluateAsync(parsed.Root, context, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Builds a DI <see cref="ServiceProvider"/> configured with Core, Twitch UI handlers, and isolated test doubles.
    /// </summary>
    /// <param name="server">Optional Kestrel Helix mock server used to configure <see cref="TwitchWorkspaceSessionProvider"/>.</param>
    /// <param name="broadcasterId">The workspace broadcaster user ID.</param>
    /// <param name="broadcasterLogin">The workspace broadcaster login name.</param>
    /// <param name="scopes">Optional OAuth scopes attached to the mocked broadcaster token.</param>
    /// <param name="timeProvider">Optional <see cref="TimeProvider"/>.</param>
    /// <param name="historyProvider">Optional <see cref="ITwitchStreamHistoryProvider"/>.</param>
    /// <param name="loyaltyStore">Optional <see cref="IViewerLoyaltyStore"/>.</param>
    /// <returns>The built <see cref="ServiceProvider"/>.</returns>
    private static ServiceProvider BuildServiceProvider(
        TwitchHelixMockServer? server,
        string broadcasterId,
        string broadcasterLogin,
        IReadOnlyList<string>? scopes = null,
        TimeProvider? timeProvider = null,
        ITwitchStreamHistoryProvider? historyProvider = null,
        IViewerLoyaltyStore? loyaltyStore = null)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        _ = services.AddStreamActionsCore(configuration);

        if (server is not null)
        {
            StubPlatformCredentialStore credentialStore = new();
            credentialStore.AddCredential(
                _workspaceId,
                "twitch",
                TokenRole.Broadcaster,
                CredentialStatus.Active,
                new PlatformTokenPayload(
                    AccessToken: "test-access-token",
                    RefreshToken: "test-refresh-token",
                    Scopes: scopes,
                    PlatformUserId: broadcasterId,
                    PlatformUsername: broadcasterLogin,
                    ClientId: "test-client-id"));

            StubWorkspaceStore workspaceStore = new();
            workspaceStore.AddPlatform(new WorkspacePlatform
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _workspaceId,
                PlatformId = "twitch",
                PlatformChannelId = broadcasterId,
                Metadata = new PlatformMetadata
                {
                    ExtraSettings = new Dictionary<string, string> { ["broadcaster_login"] = broadcasterLogin },
                },
            });

            StubOAuthClientConfigResolver oauthResolver = new(new ResolvedOAuthClientConfig(
                "twitch",
                "test-client-id",
                "test-client-secret"));

            _ = services.AddSingleton<ITwitchWorkspaceSessionProvider>(
                new TwitchWorkspaceSessionProvider(
                    credentialStore,
                    oauthResolver,
                    workspaceStore,
                    timeProvider,
                    server.HelixBaseUri));
        }

        if (historyProvider is not null)
        {
            _ = services.AddSingleton(historyProvider);
        }

        if (loyaltyStore is not null)
        {
            _ = services.AddSingleton(loyaltyStore);
        }

        if (timeProvider is not null)
        {
            _ = services.AddSingleton(timeProvider);
        }

        _ = services.AddStreamActionsTwitchUi();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a Twitch <see cref="TagEvaluationContext"/> backed by <paramref name="serviceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The DI service provider.</param>
    /// <param name="timeProvider">Optional time provider.</param>
    /// <param name="channelPointRedemption">Optional channel point redemption metadata.</param>
    /// <returns>A configured <see cref="TagEvaluationContext"/>.</returns>
    private static TagEvaluationContext CreateContext(
        IServiceProvider serviceProvider,
        TimeProvider? timeProvider = null,
        ChannelPointRedemptionContext? channelPointRedemption = null)
    {
        ITagRegistry registry = serviceProvider.GetRequiredService<ITagRegistry>();
        ChatUser sender = new("twitch", "user-42", "alice", "Alice");
        ChatChannel channel = new(_workspaceId, "twitch", "1001", "streamer");
        ChatCommand command = new(
            _workspaceId,
            "twitch",
            "msg-1",
            sender,
            channel,
            "!test",
            DateTimeOffset.UnixEpoch,
            "!",
            "test",
            [],
            string.Empty);

        return new TagEvaluationContext(
            command,
            registry,
            serviceProvider: serviceProvider,
            timeProvider: timeProvider,
            channelPointRedemption: channelPointRedemption);
    }

    #endregion Private Helpers

    #region Test Doubles

    /// <summary>
    /// In-memory <see cref="IWorkspaceKeyValueStore"/> test double.
    /// </summary>
    private sealed class InMemoryKeyValueStore : IWorkspaceKeyValueStore
    {
        /// <summary>
        /// Stored string entries keyed by workspace, table, section, and variable.
        /// </summary>
        private readonly Dictionary<(Guid WorkspaceId, string Table, string Section, string Variable), string> _entries = [];

        /// <inheritdoc />
        public ValueTask<string?> GetStringAsync(Guid workspaceId, string table, string section, string variable, string? defaultValue = null, CancellationToken ct = default) =>
            ValueTask.FromResult(this._entries.TryGetValue((workspaceId, table, section, variable), out string? val) ? val : defaultValue);

        /// <inheritdoc />
        public ValueTask<bool> GetBoolAsync(Guid workspaceId, string table, string section, string variable, bool defaultValue = false, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<int> GetIntAsync(Guid workspaceId, string table, string section, string variable, int defaultValue = 0, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<long> GetLongAsync(Guid workspaceId, string table, string section, string variable, long defaultValue = 0L, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<float> GetFloatAsync(Guid workspaceId, string table, string section, string variable, float defaultValue = 0f, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask<double> GetDoubleAsync(Guid workspaceId, string table, string section, string variable, double defaultValue = 0d, CancellationToken ct = default) =>
            ValueTask.FromResult(defaultValue);

        /// <inheritdoc />
        public ValueTask SetAsync<T>(Guid workspaceId, string table, string section, string variable, T value, CancellationToken ct = default)
        {
            this._entries[(workspaceId, table, section, variable)] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask<long> IncrementLongAsync(Guid workspaceId, string table, string section, string variable, long delta = 1L, CancellationToken ct = default) =>
            ValueTask.FromResult(delta);

        /// <inheritdoc />
        public ValueTask<bool> DeleteAsync(Guid workspaceId, string table, string section, string variable, CancellationToken ct = default) =>
            ValueTask.FromResult(this._entries.Remove((workspaceId, table, section, variable)));

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<string>> GetKeysAsync(Guid workspaceId, string table, string section, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>([.. this._entries.Keys.Where(k => k.WorkspaceId == workspaceId && k.Table == table && k.Section == section).Select(static k => k.Variable)]);
    }

    /// <summary>
    /// In-memory <see cref="IViewerLoyaltyStore"/> test double for <c>{hours}</c>.
    /// </summary>
    private sealed class InMemoryLoyaltyStore : IViewerLoyaltyStore
    {
        /// <summary>
        /// Stored viewer profiles.
        /// </summary>
        private readonly List<WorkspaceViewerProfile> _profiles = [];

        /// <summary>
        /// Adds a viewer profile to the in-memory store.
        /// </summary>
        /// <param name="profile">The profile to add.</param>
        internal void AddProfile(WorkspaceViewerProfile profile) => this._profiles.Add(profile);

        /// <inheritdoc />
        public ValueTask<WorkspaceViewerProfile?> GetProfileAsync(Guid workspaceId, string platformId, string platformUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._profiles.FirstOrDefault(p =>
                p.WorkspaceId == workspaceId
                && string.Equals(p.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase)));

        /// <inheritdoc />
        public ValueTask<WorkspaceViewerProfile?> GetProfileByLoginAsync(Guid workspaceId, string platformId, string login, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._profiles.FirstOrDefault(p =>
                p.WorkspaceId == workspaceId
                && string.Equals(p.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.LastKnownLogin, login, StringComparison.OrdinalIgnoreCase)));

        /// <inheritdoc />
        public ValueTask<WorkspaceViewerProfile> GetOrCreateProfileAsync(Guid workspaceId, string platformId, string platformUserId, string login, string displayName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<WorkspaceViewerProfile>> GetTopByPointsAsync(Guid workspaceId, int count = 10, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspaceViewerProfile>>([]);

        /// <inheritdoc />
        public ValueTask<WorkspaceViewerProfile> AdjustPointsAsync(Guid workspaceId, string platformId, string platformUserId, long delta, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> TrySpendPointsAsync(Guid workspaceId, string platformId, string platformUserId, long cost, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        /// <inheritdoc />
        public ValueTask AddWatchTimeAndXpAsync(Guid workspaceId, string platformId, string platformUserId, long watchTimeSecondsDelta, long chatXpDelta, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    /// <summary>
    /// In-memory <see cref="IPlatformCredentialStore"/> test double.
    /// </summary>
    private sealed class StubPlatformCredentialStore : IPlatformCredentialStore
    {
        /// <summary>
        /// Stored credential tuples keyed by workspace ID, platform ID, and token role.
        /// </summary>
        private readonly Dictionary<(Guid? WorkspaceId, string PlatformId, TokenRole Role), (CredentialStatus Status, PlatformTokenPayload Payload)> _tokens = [];

        /// <summary>
        /// Registers a credential entry in the store.
        /// </summary>
        /// <param name="workspaceId">The optional workspace identifier.</param>
        /// <param name="platformId">The platform identifier.</param>
        /// <param name="role">The token role.</param>
        /// <param name="status">The credential status.</param>
        /// <param name="payload">The token payload.</param>
        internal void AddCredential(Guid? workspaceId, string platformId, TokenRole role, CredentialStatus status, PlatformTokenPayload payload) =>
            this._tokens[(workspaceId, platformId, role)] = (status, payload);

        /// <inheritdoc />
        public ValueTask<PlatformCredential?> GetCredentialRecordAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformCredential?>(null);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PlatformCredential>> GetCredentialsForWorkspaceAsync(Guid? workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([]);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<PlatformCredential>> GetAllCredentialsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<PlatformCredential>>([]);

        /// <inheritdoc />
        public ValueTask<(bool Found, CredentialStatus Status, PlatformTokenPayload? Payload)> TryGetTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._tokens.TryGetValue((workspaceId, platformId, tokenRole), out (CredentialStatus Status, PlatformTokenPayload Payload) entry)
                ? (true, entry.Status, (PlatformTokenPayload?)entry.Payload)
                : (false, CredentialStatus.Active, null));

        /// <inheritdoc />
        public ValueTask<PlatformCredential> SaveTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, PlatformTokenPayload payload, CredentialStatus status = CredentialStatus.Active, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> UpdateStatusAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CredentialStatus status, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        /// <inheritdoc />
        public ValueTask<bool> DeleteTokenAsync(Guid? workspaceId, string platformId, TokenRole tokenRole, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    /// <summary>
    /// Stub <see cref="IOAuthClientConfigResolver"/> returning a fixed configuration.
    /// </summary>
    /// <param name="config">The configuration to return.</param>
    private sealed class StubOAuthClientConfigResolver(ResolvedOAuthClientConfig? config) : IOAuthClientConfigResolver
    {
        /// <inheritdoc />
        public ValueTask<ResolvedOAuthClientConfig?> ResolveAsync(
            string platformId,
            Guid? workspaceId = null,
            PlatformUserIdentity? actor = null,
            string? flow = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(config);
    }

    /// <summary>
    /// Stub <see cref="IWorkspaceStore"/> serving platform attachment records.
    /// </summary>
    private sealed class StubWorkspaceStore : IWorkspaceStore
    {
        /// <summary>
        /// Stored platform attachments.
        /// </summary>
        private readonly List<WorkspacePlatform> _platforms = [];

        /// <summary>
        /// Adds a workspace platform attachment.
        /// </summary>
        /// <param name="platform">The attachment to add.</param>
        internal void AddPlatform(WorkspacePlatform platform) => this._platforms.Add(platform);

        /// <inheritdoc />
        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Workspace?>(null);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([]);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([]);

        /// <inheritdoc />
        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>([.. this._platforms.Where(p => p.WorkspaceId == workspaceId)]);

        /// <inheritdoc />
        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this._platforms.FirstOrDefault(p =>
                p.WorkspaceId == workspaceId && string.Equals(p.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)));

        /// <inheritdoc />
        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);

        /// <inheritdoc />
        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    #endregion Test Doubles
}
