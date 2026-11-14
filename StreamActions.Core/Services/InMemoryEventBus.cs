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

using Microsoft.Extensions.Logging;
using StreamActions.Core.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace StreamActions.Core.Services;

/// <summary>
/// Bounded channel-backed in-process publish/subscribe implementation of <see cref="IEventBus"/>.
/// </summary>
public sealed partial class InMemoryEventBus : IEventBus, IDisposable
{
    /// <summary>
    /// The default bounded capacity for each event topic channel.
    /// </summary>
    private const int _defaultChannelCapacity = 1024;

    /// <summary>
    /// Active event topics keyed by event payload type.
    /// </summary>
    private readonly ConcurrentDictionary<Type, object> _topics = new();

    /// <summary>
    /// Logger used for event bus diagnostics.
    /// </summary>
    private readonly ILogger<InMemoryEventBus> _logger;

    /// <summary>
    /// Configured bounded capacity for newly created event topic channels.
    /// </summary>
    private readonly int _channelCapacity;

    /// <summary>
    /// Disposal state flag (<c>0</c> when active, <c>1</c> when disposed).
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryEventBus"/> class with the default channel capacity.
    /// </summary>
    /// <param name="logger">The logger used for event bus diagnostics.</param>
    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
        : this(logger, _defaultChannelCapacity)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryEventBus"/> class with a specified channel capacity.
    /// </summary>
    /// <param name="logger">The logger used for event bus diagnostics.</param>
    /// <param name="channelCapacity">The bounded capacity per event type channel.</param>
    public InMemoryEventBus(ILogger<InMemoryEventBus> logger, int channelCapacity)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(channelCapacity, 1);

        this._logger = logger;
        this._channelCapacity = channelCapacity;
    }

    /// <inheritdoc/>
    public async ValueTask PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this._disposed) != 0, this);

        EventTopic<TEvent> topic = this.GetOrCreateTopic<TEvent>();
        LogEventPublished(this._logger, typeof(TEvent).Name);
        await topic.Channel.Writer.WriteAsync(eventData, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this._disposed) != 0, this);

        EventTopic<TEvent> topic = this.GetOrCreateTopic<TEvent>();
        Guid subscriptionId = Guid.NewGuid();
        _ = topic.Subscribers.TryAdd(subscriptionId, handler);
        LogSubscriberAdded(this._logger, typeof(TEvent).Name, subscriptionId);

        return new Subscription(topic, subscriptionId, this._logger);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0)
        {
            return;
        }

        foreach (object entry in this._topics.Values)
        {
            if (entry is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        this._topics.Clear();
    }

    /// <summary>
    /// Gets an existing topic for <typeparamref name="TEvent"/> or creates a new one.
    /// </summary>
    /// <typeparam name="TEvent">The event payload type.</typeparam>
    /// <returns>The <see cref="EventTopic{TEvent}"/> managing subscriptions and delivery for <typeparamref name="TEvent"/>.</returns>
    private EventTopic<TEvent> GetOrCreateTopic<TEvent>()
        where TEvent : class
    {
        object topicObj = this._topics.GetOrAdd(
            typeof(TEvent),
            _ => new EventTopic<TEvent>(this._channelCapacity, this._logger));

        return (EventTopic<TEvent>)topicObj;
    }

    /// <summary>
    /// Logs that an event was published to the bus.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventType">The name of the published event type.</param>
    [LoggerMessage(Level = LogLevel.Trace, Message = "Published event of type {EventType}.")]
    private static partial void LogEventPublished(ILogger logger, string eventType);

    /// <summary>
    /// Logs that the oldest queued event was dropped due to channel capacity limits.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventType">The name of the event type whose item was dropped.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped oldest event of type {EventType} because the channel reached capacity.")]
    private static partial void LogEventDropped(ILogger logger, string eventType);

    /// <summary>
    /// Logs that a subscriber was registered for an event type.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventType">The name of the subscribed event type.</param>
    /// <param name="subscriptionId">The unique identifier of the subscription.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Subscribed handler {SubscriptionId} to event type {EventType}.")]
    private static partial void LogSubscriberAdded(ILogger logger, string eventType, Guid subscriptionId);

    /// <summary>
    /// Logs that a subscriber was removed from an event type.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventType">The name of the unsubscribed event type.</param>
    /// <param name="subscriptionId">The unique identifier of the subscription.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Unsubscribed handler {SubscriptionId} from event type {EventType}.")]
    private static partial void LogSubscriberRemoved(ILogger logger, string eventType, Guid subscriptionId);

    /// <summary>
    /// Logs an unhandled exception thrown by an event subscriber.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventType">The name of the event type being processed.</param>
    /// <param name="subscriptionId">The unique identifier of the failing subscription.</param>
    /// <param name="exception">The exception thrown by the subscriber.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception in subscriber {SubscriptionId} for event type {EventType}.")]
    private static partial void LogSubscriberError(ILogger logger, string eventType, Guid subscriptionId, Exception exception);

    /// <summary>
    /// Non-generic abstraction over a typed event topic for subscription cleanup and disposal.
    /// </summary>
    private interface IEventTopic : IDisposable
    {
        /// <summary>
        /// Removes the subscriber identified by <paramref name="subscriptionId"/> from this topic.
        /// </summary>
        /// <param name="subscriptionId">The unique identifier of the subscription to remove.</param>
        void RemoveSubscriber(Guid subscriptionId);
    }

    /// <summary>
    /// Manages a bounded channel and dispatch loop for events of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The event payload type.</typeparam>
    private sealed class EventTopic<TEvent> : IEventTopic
        where TEvent : class
    {
        /// <summary>
        /// Cancellation source signaled when the topic is disposed.
        /// </summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// Logger used for topic and subscriber diagnostics.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventTopic{TEvent}"/> class and starts its dispatch loop.
        /// </summary>
        /// <param name="capacity">The bounded capacity of the topic channel.</param>
        /// <param name="logger">The logger used for topic diagnostics.</param>
        public EventTopic(int capacity, ILogger logger)
        {
            this._logger = logger;

            BoundedChannelOptions options = new(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
            };

            this.Channel = System.Threading.Channels.Channel.CreateBounded<TEvent>(
                options,
                _ => LogEventDropped(this._logger, typeof(TEvent).Name));

            _ = Task.Run(this.ProcessEventsAsync);
        }

        /// <summary>
        /// Gets the bounded channel storing published events for this topic.
        /// </summary>
        public Channel<TEvent> Channel { get; }

        /// <summary>
        /// Gets the active subscriber handlers keyed by subscription identifier.
        /// </summary>
        public ConcurrentDictionary<Guid, Func<TEvent, CancellationToken, ValueTask>> Subscribers { get; } = new();

        /// <inheritdoc/>
        public void RemoveSubscriber(Guid subscriptionId) => _ = this.Subscribers.TryRemove(subscriptionId, out _);

        /// <inheritdoc/>
        public void Dispose()
        {
            _ = this.Channel.Writer.TryComplete();
            this._cts.Cancel();
            this._cts.Dispose();
        }

        /// <summary>
        /// Continuously reads events from the topic channel and dispatches them to all active subscribers.
        /// </summary>
        /// <returns>A task representing the background dispatch loop.</returns>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Subscriber exceptions must be isolated and logged so one failing subscriber does not crash the event loop or block other subscribers.")]
        private async Task ProcessEventsAsync()
        {
            try
            {
                await foreach (TEvent item in this.Channel.Reader.ReadAllAsync(this._cts.Token).ConfigureAwait(false))
                {
                    foreach (KeyValuePair<Guid, Func<TEvent, CancellationToken, ValueTask>> subscriber in this.Subscribers)
                    {
                        try
                        {
                            await subscriber.Value(item, this._cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (this._cts.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            LogSubscriberError(this._logger, typeof(TEvent).Name, subscriber.Key, ex);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (this._cts.IsCancellationRequested)
            {
                // Expected during bus disposal.
            }
        }
    }

    /// <summary>
    /// Disposable token that unregisters a subscriber from its topic when disposed.
    /// </summary>
    /// <param name="topic">The topic owning the subscription.</param>
    /// <param name="subscriptionId">The unique identifier of the subscription.</param>
    /// <param name="logger">The logger used to record unsubscription.</param>
    private sealed class Subscription(IEventTopic topic, Guid subscriptionId, ILogger logger) : IDisposable
    {
        /// <summary>
        /// Disposal state flag (<c>0</c> when active, <c>1</c> when disposed).
        /// </summary>
        private int _disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this._disposed, 1) != 0)
            {
                return;
            }

            topic.RemoveSubscriber(subscriptionId);
            LogSubscriberRemoved(logger, topic.GetType().GenericTypeArguments[0].Name, subscriptionId);
        }
    }
}
