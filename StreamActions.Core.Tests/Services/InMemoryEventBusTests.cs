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
using Microsoft.Extensions.Logging.Abstractions;
using StreamActions.Core.Services;
using Xunit;

namespace StreamActions.Core.Tests.Services;

public sealed class InMemoryEventBusTests
{
    private sealed record TestEvent(string Message);

    #region PublishAsync

    [Fact]
    [Trait("Member", "PublishAsync")]
    public async Task PublishAsyncWhenEventDataIsNullThrowsArgumentNullException()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);

        Func<Task> action = async () => await bus.PublishAsync<TestEvent>(null!, TestContext.Current.CancellationToken).ConfigureAwait(false);

        _ = await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    [Trait("Member", "PublishAsync")]
    public async Task PublishAsyncWhenBusIsDisposedThrowsObjectDisposedException()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        bus.Dispose();

        Func<Task> action = async () => await bus.PublishAsync(new TestEvent("hello"), TestContext.Current.CancellationToken).ConfigureAwait(false);

        _ = await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Member", "PublishAsync")]
    public async Task PublishAsyncWhenSubscriberIsRegisteredDeliversEvent()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        TaskCompletionSource<TestEvent> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable subscription = bus.Subscribe<TestEvent>((evt, cancellationToken) =>
        {
            _ = received.TrySetResult(evt);
            return ValueTask.CompletedTask;
        });

        await bus.PublishAsync(new TestEvent("payload"), TestContext.Current.CancellationToken);

        TestEvent result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _ = result.Message.Should().Be("payload");
    }

    [Fact]
    [Trait("Member", "PublishAsync")]
    public async Task PublishAsyncWhenMultipleSubscribersRegisteredDeliversToAll()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        TaskCompletionSource<string> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> second = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable sub1 = bus.Subscribe<TestEvent>((evt, cancellationToken) =>
        {
            _ = first.TrySetResult(evt.Message);
            return ValueTask.CompletedTask;
        });
        using IDisposable sub2 = bus.Subscribe<TestEvent>((evt, cancellationToken) =>
        {
            _ = second.TrySetResult(evt.Message);
            return ValueTask.CompletedTask;
        });

        await bus.PublishAsync(new TestEvent("broadcast"), TestContext.Current.CancellationToken);

        string[] results = await Task.WhenAll(first.Task, second.Task)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _ = results.Should().Equal("broadcast", "broadcast");
    }

    [Fact]
    [Trait("Member", "PublishAsync")]
    public async Task PublishAsyncWhenSubscriberThrowsContinuesDeliveringToOtherSubscribers()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        TaskCompletionSource<string> healthyReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable failingSub = bus.Subscribe<TestEvent>((_, _) =>
            throw new InvalidOperationException("Boom"));
        using IDisposable healthySub = bus.Subscribe<TestEvent>((evt, cancellationToken) =>
        {
            _ = healthyReceived.TrySetResult(evt.Message);
            return ValueTask.CompletedTask;
        });

        await bus.PublishAsync(new TestEvent("resilient"), TestContext.Current.CancellationToken);

        string result = await healthyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _ = result.Should().Be("resilient");
    }

    [Fact]
    [Trait("Member", "PublishAsync")]
    public async Task PublishAsyncWhenChannelExceedsCapacityDropsOldestEventsWithoutBlocking()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance, channelCapacity: 2);
        using SemaphoreSlim gate = new(0, 1);
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> received = [];

        using IDisposable sub = bus.Subscribe<TestEvent>(async (evt, ct) =>
        {
            if (evt.Message == "1")
            {
                _ = firstStarted.TrySetResult();
                await gate.WaitAsync(ct).ConfigureAwait(false);
            }

            lock (received)
            {
                received.Add(evt.Message);
            }
        });

        await bus.PublishAsync(new TestEvent("1"), TestContext.Current.CancellationToken);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await bus.PublishAsync(new TestEvent("2"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(new TestEvent("3"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(new TestEvent("4"), TestContext.Current.CancellationToken);

        _ = gate.Release();

        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            lock (received)
            {
                if (received.Count >= 3)
                {
                    break;
                }
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        lock (received)
        {
            _ = received.Should().Contain("1");
            _ = received.Should().Contain("4");
            _ = received.Should().NotContain("2");
        }
    }

    #endregion

    #region Subscribe

    [Fact]
    [Trait("Member", "Subscribe")]
    public void SubscribeWhenHandlerIsNullThrowsArgumentNullException()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);

        Action action = () => bus.Subscribe<TestEvent>(null!);

        _ = action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Member", "Subscribe")]
    public void SubscribeWhenBusIsDisposedThrowsObjectDisposedException()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        bus.Dispose();

        Action action = () => bus.Subscribe<TestEvent>((_, _) => ValueTask.CompletedTask);

        _ = action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Member", "Subscribe")]
    public async Task SubscribeWhenSubscriptionIsDisposedStopsDeliveringEvents()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);
        int callCount = 0;

        IDisposable subscription = bus.Subscribe<TestEvent>((_, _) =>
        {
            _ = Interlocked.Increment(ref callCount);
            return ValueTask.CompletedTask;
        });

        subscription.Dispose();
        subscription.Dispose();

        await bus.PublishAsync(new TestEvent("ignored"), TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        _ = callCount.Should().Be(0);
    }

    #endregion

    #region Dispose

    [Fact]
    [Trait("Member", "Dispose")]
    public void DisposeWhenCalledMultipleTimesIsIdempotent()
    {
        using InMemoryEventBus bus = new(NullLogger<InMemoryEventBus>.Instance);

        Action action = () =>
        {
            bus.Dispose();
            bus.Dispose();
        };

        _ = action.Should().NotThrow();
    }

    #endregion
}
