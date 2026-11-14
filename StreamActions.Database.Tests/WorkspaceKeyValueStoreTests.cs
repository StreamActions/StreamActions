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
using StreamActions.Database.Services;
using Xunit;

namespace StreamActions.Database.Tests;

public sealed class WorkspaceKeyValueStoreTests
{
    #region GetSet
    [Fact]
    [Trait("Member", "SetAsync")]
    public async Task GetSet_BoundaryValues_RoundTripWithInvariantCulture()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        WorkspaceKeyValueStore store = new(factory);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await store.SetAsync(workspaceId, "Bounds", "Ints", "Min", int.MinValue, ct);
        await store.SetAsync(workspaceId, "Bounds", "Ints", "Max", int.MaxValue, ct);
        await store.SetAsync(workspaceId, "Bounds", "Longs", "Max", long.MaxValue, ct);
        await store.SetAsync(workspaceId, "Bounds", "Doubles", "NaN", double.NaN, ct);
        await store.SetAsync(workspaceId, "Bounds", "Doubles", "Inf", double.PositiveInfinity, ct);

        _ = (await store.GetIntAsync(workspaceId, "bounds", "ints", "min", 0, ct)).Should().Be(int.MinValue);
        _ = (await store.GetIntAsync(workspaceId, "bounds", "ints", "max", 0, ct)).Should().Be(int.MaxValue);
        _ = (await store.GetLongAsync(workspaceId, "bounds", "longs", "max", 0L, ct)).Should().Be(long.MaxValue);
        _ = double.IsNaN(await store.GetDoubleAsync(workspaceId, "bounds", "doubles", "nan", 0d, ct)).Should().BeTrue();
        _ = double.IsPositiveInfinity(await store.GetDoubleAsync(workspaceId, "bounds", "doubles", "inf", 0d, ct)).Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "GetIntAsync")]
    public async Task GetIntAndDouble_MalformedStrings_ReturnsDefaultValue()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        WorkspaceKeyValueStore store = new(factory);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await store.SetAsync(workspaceId, "tbl", "sec", "bad_int", "not_a_number", ct);
        await store.SetAsync(workspaceId, "tbl", "sec", "bad_double", "abc", ct);

        _ = (await store.GetIntAsync(workspaceId, "tbl", "sec", "bad_int", 99, ct)).Should().Be(99);
        _ = (await store.GetDoubleAsync(workspaceId, "tbl", "sec", "bad_double", 3.14, ct)).Should().Be(3.14);
    }

    [Theory]
    [Trait("Member", "GetBoolAsync")]
    [InlineData("true", false, true)]
    [InlineData("false", true, false)]
    [InlineData("1", false, true)]
    [InlineData("0", true, false)]
    [InlineData("invalid", true, true)]
    public async Task GetBoolAsync_ParsesValidAndMalformedValues(string raw, bool defaultValue, bool expected)
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        WorkspaceKeyValueStore store = new(factory);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await store.SetAsync(workspaceId, "flags", "general", "key", raw, ct);
        bool actual = await store.GetBoolAsync(workspaceId, "flags", "general", "key", defaultValue, ct);

        _ = actual.Should().Be(expected);
    }

    [Fact]
    [Trait("Member", "GetStringAsync")]
    public async Task GetStringAsync_DifferentWorkspaces_IsolatesTenants()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceA) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        (_, Guid workspaceB) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        WorkspaceKeyValueStore store = new(factory);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await store.SetAsync(workspaceA, "Config", "Main", "Theme", "dark", ct);

        _ = (await store.GetStringAsync(workspaceA, "config", "main", "theme", null, ct)).Should().Be("dark");
        _ = (await store.GetStringAsync(workspaceB, "config", "main", "theme", "fallback", ct)).Should().Be("fallback");
    }
    #endregion

    #region IncrementLongAsync
    [Fact]
    [Trait("Member", "IncrementLongAsync")]
    public async Task IncrementLongAsync_ConcurrentCalls_NeverLoseUpdates()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        WorkspaceKeyValueStore store = new(factory);
        CancellationToken ct = TestContext.Current.CancellationToken;

        const int concurrentCalls = 25;
        IEnumerable<Task<long>> tasks = Enumerable.Range(0, concurrentCalls)
            .Select(_ => store.IncrementLongAsync(workspaceId, "Counters", "Global", "Deaths", 1L, ct).AsTask());

        _ = await Task.WhenAll(tasks);

        long finalValue = await store.GetLongAsync(workspaceId, "counters", "global", "deaths", 0L, ct);
        _ = finalValue.Should().Be(concurrentCalls);
    }
    #endregion

    #region DeleteAsync
    [Fact]
    [Trait("Member", "DeleteAsync")]
    public async Task DeleteAsync_And_GetKeysAsync_RemoveAndListEntries()
    {
        using TestSqliteContextFactory factory = new();
        (_, Guid workspaceId) = await factory.SeedWorkspaceAsync(TestContext.Current.CancellationToken);
        WorkspaceKeyValueStore store = new(factory);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await store.SetAsync(workspaceId, "Plugin", "Settings", "Alpha", "1", ct);
        await store.SetAsync(workspaceId, "Plugin", "Settings", "Beta", "2", ct);

        IReadOnlyList<string> keysBefore = await store.GetKeysAsync(workspaceId, "PLUGIN", "SETTINGS", ct);
        _ = keysBefore.Should().Equal("alpha", "beta");

        bool deleted = await store.DeleteAsync(workspaceId, "plugin", "settings", "ALPHA", ct);
        _ = deleted.Should().BeTrue();

        IReadOnlyList<string> keysAfter = await store.GetKeysAsync(workspaceId, "plugin", "settings", ct);
        _ = keysAfter.Should().Equal("beta");
    }
    #endregion
}
