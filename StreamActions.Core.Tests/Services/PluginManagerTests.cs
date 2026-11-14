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
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class PluginManagerTests
{
    private sealed class StubPlugin(
        string id,
        List<string> lifecycleLog,
        bool throwOnShutdown = false) : IPlugin
    {
        public string Id { get; } = id;

        public string Name => this.Id;

        public Version Version { get; } = new(1, 0, 0);

        public IPluginContext? ReceivedContext { get; private set; }

        public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.ReceivedContext = context;
            lifecycleLog.Add($"init:{this.Id}");
            return ValueTask.CompletedTask;
        }

        public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            lifecycleLog.Add($"shutdown:{this.Id}");
            return throwOnShutdown ? throw new InvalidOperationException($"Shutdown failed for {this.Id}") : ValueTask.CompletedTask;
        }
    }

    private static (IServiceProvider Services, InMemoryEventBus EventBus) CreateDependencies()
    {
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        InMemoryEventBus eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        return (services, eventBus);
    }

    #region Constructor

    [Fact]
    [Trait("Member", "PluginManager")]
    public void PluginManagerWhenPluginsIsNullThrowsArgumentNullException()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            Action action = () => _ = new PluginManager(null!, services, eventBus, NullLoggerFactory.Instance);
            _ = action.Should().Throw<ArgumentNullException>();
        }
    }

    [Fact]
    [Trait("Member", "PluginManager")]
    public void PluginManagerWhenConstructedExposesPluginsAndContext()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            List<string> log = [];
            StubPlugin plugin = new("core.test", log);

            PluginManager manager = new([plugin], services, eventBus, NullLoggerFactory.Instance);

            _ = manager.Plugins.Should().ContainSingle().Which.Should().BeSameAs(plugin);
            _ = manager.Services.Should().BeSameAs(services);
            _ = manager.EventBus.Should().BeSameAs(eventBus);
            _ = manager.LoggerFactory.Should().BeSameAs(NullLoggerFactory.Instance);
        }
    }

    #endregion

    #region InitializeAllAsync

    [Fact]
    [Trait("Member", "InitializeAllAsync")]
    public async Task InitializeAllAsyncWhenCalledInitializesPluginsInOrder()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            List<string> log = [];
            StubPlugin first = new("core.first", log);
            StubPlugin second = new("core.second", log);
            PluginManager manager = new([first, second], services, eventBus, NullLoggerFactory.Instance);

            await manager.InitializeAllAsync(TestContext.Current.CancellationToken);

            _ = log.Should().Equal("init:core.first", "init:core.second");
            _ = first.ReceivedContext.Should().BeSameAs(manager);
            _ = second.ReceivedContext.Should().BeSameAs(manager);
        }
    }

    [Fact]
    [Trait("Member", "InitializeAllAsync")]
    public async Task InitializeAllAsyncWhenCancelledThrowsOperationCanceledException()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            List<string> log = [];
            StubPlugin plugin = new("core.first", log);
            PluginManager manager = new([plugin], services, eventBus, NullLoggerFactory.Instance);
            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            Func<Task> action = async () => await manager.InitializeAllAsync(cts.Token).ConfigureAwait(false);

            _ = await action.Should().ThrowAsync<OperationCanceledException>();
            _ = log.Should().BeEmpty();
        }
    }

    #endregion

    #region ShutdownAllAsync

    [Fact]
    [Trait("Member", "ShutdownAllAsync")]
    public async Task ShutdownAllAsyncWhenCalledShutsDownPluginsInReverseOrder()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            List<string> log = [];
            StubPlugin first = new("core.first", log);
            StubPlugin second = new("core.second", log);
            PluginManager manager = new([first, second], services, eventBus, NullLoggerFactory.Instance);

            await manager.ShutdownAllAsync(TestContext.Current.CancellationToken);

            _ = log.Should().Equal("shutdown:core.second", "shutdown:core.first");
        }
    }

    [Fact]
    [Trait("Member", "ShutdownAllAsync")]
    public async Task ShutdownAllAsyncWhenOnePluginThrowsContinuesShuttingDownRemainingPlugins()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            List<string> log = [];
            StubPlugin first = new("core.first", log);
            StubPlugin failing = new("core.failing", log, throwOnShutdown: true);
            StubPlugin third = new("core.third", log);
            PluginManager manager = new([first, failing, third], services, eventBus, NullLoggerFactory.Instance);

            Func<Task> action = async () => await manager.ShutdownAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

            _ = await action.Should().NotThrowAsync();
            _ = log.Should().Equal("shutdown:core.third", "shutdown:core.failing", "shutdown:core.first");
        }
    }

    #endregion

    #region GetPlugin

    [Fact]
    [Trait("Member", "GetPlugin")]
    public void GetPluginWhenPluginIdIsNullThrowsArgumentNullException()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            PluginManager manager = new([], services, eventBus, NullLoggerFactory.Instance);

            Action action = () => manager.GetPlugin(null!);

            _ = action.Should().Throw<ArgumentNullException>();
        }
    }

    [Theory]
    [Trait("Member", "GetPlugin")]
    [InlineData("core.alpha")]
    [InlineData("CORE.ALPHA")]
    public void GetPluginWhenPluginExistsReturnsPlugin(string lookupId)
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            List<string> log = [];
            StubPlugin plugin = new("core.alpha", log);
            PluginManager manager = new([plugin], services, eventBus, NullLoggerFactory.Instance);

            IPlugin? result = manager.GetPlugin(lookupId);

            _ = result.Should().BeSameAs(plugin);
        }
    }

    [Fact]
    [Trait("Member", "GetPlugin")]
    public void GetPluginWhenPluginDoesNotExistReturnsNull()
    {
        (IServiceProvider services, InMemoryEventBus eventBus) = CreateDependencies();
        using (eventBus)
        {
            PluginManager manager = new([], services, eventBus, NullLoggerFactory.Instance);

            IPlugin? result = manager.GetPlugin("core.missing");

            _ = result.Should().BeNull();
        }
    }

    #endregion
}
