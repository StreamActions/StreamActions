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
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class ScopeDriftDetectorTests
{
    #region DetectDrift

    [Fact]
    [Trait("Member", "DetectDrift")]
    public void DetectDriftAggregatesPluginAndPlatformRequirementsAndComputesUpgradeUnion()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);

        StubPlugin twitchPlugin = new(
            id: "twitch.polls",
            requiredPlatformId: "twitch",
            requiredPlatformScopes: new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["twitch"] = ["channel:manage:polls", "chat:read"],
            });

        StubPlugin universalPlugin = new(
            id: "core.universal",
            requiredPlatformId: null,
            requiredPlatformScopes: new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["TWITCH"] = ["channel:read:redemptions"],
                ["youtube"] = ["youtube.readonly"],
            });

        StubPlugin otherPlatformPlugin = new(
            id: "youtube.only",
            requiredPlatformId: "youtube",
            requiredPlatformScopes: new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["twitch"] = ["should:not:apply"],
            });

        PluginManager pluginManager = new(
            [twitchPlugin, universalPlugin, otherPlatformPlugin],
            sp,
            bus,
            NullLoggerFactory.Instance);

        IPlatformScopeRequirement[] requirements =
        [
            new StubScopeRequirement(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["twitch"] = ["channel:bot", "moderator:manage:banned_users"],
            }),
        ];

        ScopeDriftDetector detector = new(pluginManager, requirements);
        PlatformTokenPayload payload = new(
            "access-token",
            Scopes: ["CHAT:READ", "user:read:email"]);

        ScopeDriftReport report = detector.DetectDrift("TWITCH", payload, ["clips:edit"]);

        _ = report.PlatformId.Should().Be("twitch");
        _ = report.HasDrift.Should().BeTrue();
        _ = report.MissingScopes.Should().BeEquivalentTo(
            ["channel:manage:polls", "channel:read:redemptions", "channel:bot", "moderator:manage:banned_users", "clips:edit"]);
        _ = report.UpgradeScopesUnion.Should().BeEquivalentTo(
            ["CHAT:READ", "user:read:email", "channel:manage:polls", "channel:read:redemptions", "channel:bot", "moderator:manage:banned_users", "clips:edit"]);
    }

    [Fact]
    [Trait("Member", "DetectDrift")]
    public void DetectDriftWhenAllRequiredScopesAreGrantedCaseInsensitivelyReturnsNoDrift()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        PluginManager pluginManager = new(
            [
                new StubPlugin(
                    "twitch.chat",
                    "twitch",
                    new Dictionary<string, IReadOnlyCollection<string>>
                    {
                        ["twitch"] = ["chat:read", "chat:edit"],
                    }),
            ],
            sp,
            bus,
            NullLoggerFactory.Instance);

        ScopeDriftDetector detector = new(pluginManager, []);
        ScopeDriftReport report = detector.DetectDrift(
            "twitch",
            new PlatformTokenPayload("access", Scopes: ["CHAT:READ", "chat:edit", "openid"]));

        _ = report.HasDrift.Should().BeFalse();
        _ = report.MissingScopes.Should().BeEmpty();
        _ = report.UpgradeScopesUnion.Should().BeEquivalentTo(["CHAT:READ", "chat:edit", "openid"]);
    }

    #endregion

    private sealed class StubPlugin(
        string id,
        string? requiredPlatformId,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> requiredPlatformScopes) : IPlugin
    {
        public string Id { get; } = id;
        public string Name { get; } = id;
        public Version Version { get; } = new(1, 0, 0);
        public string? RequiredPlatformId { get; } = requiredPlatformId;
        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> RequiredPlatformScopes { get; } = requiredPlatformScopes;

        public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ShutdownAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubScopeRequirement(IReadOnlyDictionary<string, IReadOnlyCollection<string>> map) : IPlatformScopeRequirement
    {
        public IReadOnlyCollection<string> GetRequiredScopes(string platformId) =>
            map.TryGetValue(platformId, out IReadOnlyCollection<string>? scopes) ? scopes : [];
    }
}
