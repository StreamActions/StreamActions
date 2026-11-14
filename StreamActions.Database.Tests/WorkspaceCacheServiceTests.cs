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
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class WorkspaceCacheServiceTests
{
    #region GetOrCreateAsync
    [Fact]
    [Trait("Member", "GetOrCreateAsync")]
    public async Task GetOrCreateAsync_SecondCall_ReturnsCachedValueWithoutInvokingFactory()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();

        WorkspaceCacheService cacheService = new(provider.GetRequiredService<HybridCache>());
        Guid workspaceId = Guid.CreateVersion7();
        int factoryCalls = 0;

        string first = await cacheService.GetOrCreateAsync(
            workspaceId,
            "commands",
            "!hello",
            _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult("world");
            },
            cancellationToken: TestContext.Current.CancellationToken);

        string second = await cacheService.GetOrCreateAsync(
            workspaceId,
            "commands",
            "!hello",
            _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult("updated");
            },
            cancellationToken: TestContext.Current.CancellationToken);

        _ = first.Should().Be("world");
        _ = second.Should().Be("world");
        _ = factoryCalls.Should().Be(1);
    }

    [Fact]
    [Trait("Member", "InvalidateAsync")]
    public async Task InvalidateAsync_EvictsTaggedEntries()
    {
        ServiceCollection services = new();
        _ = services.AddHybridCache();
        await using ServiceProvider provider = services.BuildServiceProvider();

        WorkspaceCacheService cacheService = new(provider.GetRequiredService<HybridCache>());
        Guid workspaceId = Guid.CreateVersion7();
        int factoryCalls = 0;

        _ = await cacheService.GetOrCreateAsync(
            workspaceId,
            "commands",
            "!hello",
            _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult($"v{factoryCalls}");
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await cacheService.InvalidateAsync(workspaceId, "commands", TestContext.Current.CancellationToken);

        string afterInvalidation = await cacheService.GetOrCreateAsync(
            workspaceId,
            "commands",
            "!hello",
            _ =>
            {
                factoryCalls++;
                return ValueTask.FromResult($"v{factoryCalls}");
            },
            cancellationToken: TestContext.Current.CancellationToken);

        _ = afterInvalidation.Should().Be("v2");
        _ = factoryCalls.Should().Be(2);
    }
    #endregion
}
