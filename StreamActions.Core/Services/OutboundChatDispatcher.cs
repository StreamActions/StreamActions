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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Models.Messaging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace StreamActions.Core.Services;

/// <summary>
/// Four-lane priority bounded outbound chat dispatcher with per-channel token-bucket pacing, word-boundary chunking, 30-second deduplication, and simulcast routing.
/// </summary>
public sealed partial class OutboundChatDispatcher : BackgroundService, IOutboundChatDispatcher
{
    /// <summary>
    /// Maximum cached deduplication entries before opportunistic pruning runs.
    /// </summary>
    private const int _dedupPruneThreshold = 1024;

    /// <summary>
    /// Bounded channel for <see cref="OutboundChatPriority.ModerationImmediate"/> messages.
    /// </summary>
    private readonly Channel<OutboundChatMessage> _moderationChannel;

    /// <summary>
    /// Bounded channel for <see cref="OutboundChatPriority.CommandReply"/> messages.
    /// </summary>
    private readonly Channel<OutboundChatMessage> _commandReplyChannel;

    /// <summary>
    /// Bounded channel for <see cref="OutboundChatPriority.EventAnnouncement"/> messages.
    /// </summary>
    private readonly Channel<OutboundChatMessage> _eventAnnouncementChannel;

    /// <summary>
    /// Bounded channel for <see cref="OutboundChatPriority.Timer"/> messages.
    /// </summary>
    private readonly Channel<OutboundChatMessage> _timerChannel;

    /// <summary>
    /// Registered chat platform adapters keyed by normalized platform identifier.
    /// </summary>
    private readonly Dictionary<string, IChatPlatform> _platforms;

    /// <summary>
    /// Provider used to query live stream platform identifiers for simulcast routing.
    /// </summary>
    private readonly IStreamStatusProvider _streamStatusProvider;

    /// <summary>
    /// Optional workspace store used to resolve primary and simulcast peer platform attachments.
    /// </summary>
    private readonly IWorkspaceStore? _workspaceStore;

    /// <summary>
    /// Time provider driving token bucket refill timing and duplicate-window checks.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Logger used for dispatcher diagnostics.
    /// </summary>
    private readonly ILogger<OutboundChatDispatcher> _logger;

    /// <summary>
    /// Effective dispatcher configuration options.
    /// </summary>
    private readonly OutboundChatDispatcherOptions _options;

    /// <summary>
    /// Per-(platform, channel) token bucket rate limiters.
    /// </summary>
    private readonly ConcurrentDictionary<ChannelKey, ChannelTokenBucket> _rateLimiters = new();

    /// <summary>
    /// Tracks the last sent UTC timestamp per (platform, channel, message chunk) for duplicate-message suppression.
    /// </summary>
    private readonly ConcurrentDictionary<DuplicateMessageKey, DateTimeOffset> _recentMessages = new();

    /// <summary>
    /// Lock synchronizing duplicate-window checks and updates.
    /// </summary>
    private readonly Lock _dedupLock = new();

    /// <summary>
    /// Total count of messages dropped due to saturation in <see cref="OutboundChatPriority.EventAnnouncement"/> or <see cref="OutboundChatPriority.Timer"/> lanes.
    /// </summary>
    private long _droppedMessageCount;

    /// <summary>
    /// Disposal state flag (<c>0</c> when active, <c>1</c> when disposed).
    /// </summary>
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboundChatDispatcher"/> class.
    /// </summary>
    /// <param name="platforms">The registered chat platform adapters.</param>
    /// <param name="streamStatusProvider">The live stream status provider.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> used for rate limiting and deduplication.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="workspaceStore">Optional workspace store for resolving primary and simulcast peer channels.</param>
    /// <param name="options">Optional dispatcher configuration options.</param>
    public OutboundChatDispatcher(
        IEnumerable<IChatPlatform> platforms,
        IStreamStatusProvider streamStatusProvider,
        TimeProvider timeProvider,
        ILogger<OutboundChatDispatcher> logger,
        IWorkspaceStore? workspaceStore = null,
        IOptions<OutboundChatDispatcherOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(platforms);
        ArgumentNullException.ThrowIfNull(streamStatusProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        this._streamStatusProvider = streamStatusProvider;
        this._timeProvider = timeProvider;
        this._logger = logger;
        this._workspaceStore = workspaceStore;
        this._options = options?.Value ?? new OutboundChatDispatcherOptions();

        ArgumentOutOfRangeException.ThrowIfLessThan(this._options.ModerationLaneCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(this._options.CommandReplyLaneCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(this._options.EventAnnouncementLaneCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(this._options.TimerLaneCapacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(this._options.MaxMessageChunks, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(this._options.ModeratorOrVipMessagesPerWindow, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(this._options.RegularMessagesPerWindow, 1);

        if (this._options.RateLimitWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), this._options.RateLimitWindow, "RateLimitWindow must be positive.");
        }

        if (this._options.DuplicateWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), this._options.DuplicateWindow, "DuplicateWindow cannot be negative.");
        }

        this._platforms = new Dictionary<string, IChatPlatform>(StringComparer.OrdinalIgnoreCase);
        foreach (IChatPlatform platform in platforms)
        {
            this._platforms[platform.PlatformId] = platform;
        }

        this._moderationChannel = Channel.CreateBounded<OutboundChatMessage>(new BoundedChannelOptions(this._options.ModerationLaneCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

        this._commandReplyChannel = Channel.CreateBounded<OutboundChatMessage>(new BoundedChannelOptions(this._options.CommandReplyLaneCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

        this._eventAnnouncementChannel = Channel.CreateBounded<OutboundChatMessage>(
            new BoundedChannelOptions(this._options.EventAnnouncementLaneCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            },
            dropped => this.OnChannelItemDropped(OutboundChatPriority.EventAnnouncement, dropped));

        this._timerChannel = Channel.CreateBounded<OutboundChatMessage>(
            new BoundedChannelOptions(this._options.TimerLaneCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            },
            dropped => this.OnChannelItemDropped(OutboundChatPriority.Timer, dropped));
    }

    /// <summary>
    /// Gets the total number of messages dropped due to channel saturation in droppable priority lanes.
    /// </summary>
    public long DroppedMessageCount => Interlocked.Read(ref this._droppedMessageCount);

    /// <inheritdoc/>
    public ValueTask EnqueueAsync(OutboundChatMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this._disposed) != 0, this);

        Channel<OutboundChatMessage> targetChannel = this.GetChannelForPriority(message.Priority);
        return targetChannel.Writer.WriteAsync(message, cancellationToken);
    }

    /// <summary>
    /// Splits <paramref name="message"/> into word-boundary chunks that do not exceed <paramref name="maxLength"/> characters, capped at <paramref name="maxChunks"/>.
    /// </summary>
    /// <param name="message">The outbound message text.</param>
    /// <param name="maxLength">The maximum character length per chunk.</param>
    /// <param name="maxChunks">The maximum number of chunks to produce.</param>
    /// <returns>An ordered read-only list of message chunks.</returns>
    internal static IReadOnlyList<string> SplitIntoChunks(string message, int maxLength, int maxChunks)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return [];
        }

        int effectiveMaxLength = Math.Max(maxLength, 1);
        int effectiveMaxChunks = Math.Max(maxChunks, 1);

        ReadOnlySpan<char> remaining = message.AsSpan().Trim();
        if (remaining.Length <= effectiveMaxLength)
        {
            return [remaining.ToString()];
        }

        List<string> chunks = new(effectiveMaxChunks);
        while (!remaining.IsEmpty && chunks.Count < effectiveMaxChunks)
        {
            if (remaining.Length <= effectiveMaxLength)
            {
                chunks.Add(remaining.ToString());
                break;
            }

            int splitIndex = -1;
            for (int i = effectiveMaxLength; i >= 1; i--)
            {
                if (char.IsWhiteSpace(remaining[i]))
                {
                    splitIndex = i;
                    break;
                }
            }

            if (splitIndex > 0)
            {
                string chunk = remaining[..splitIndex].TrimEnd().ToString();
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(chunk);
                }

                remaining = remaining[splitIndex..].TrimStart();
            }
            else
            {
                chunks.Add(remaining[..effectiveMaxLength].ToString());
                remaining = remaining[effectiveMaxLength..].TrimStart();
            }
        }

        return chunks.AsReadOnly();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) == 0)
        {
            _ = this._moderationChannel.Writer.TryComplete();
            _ = this._commandReplyChannel.Writer.TryComplete();
            _ = this._eventAnnouncementChannel.Writer.TryComplete();
            _ = this._timerChannel.Writer.TryComplete();
        }

        base.Dispose();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (this.TryReadHighestPriority(out OutboundChatMessage? nextMessage))
                {
                    await this.DispatchMessageAsync(nextMessage, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                bool hasData = await this.WaitForAnyLaneAsync(stoppingToken).ConfigureAwait(false);
                if (!hasData)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful host shutdown.
        }
    }

    /// <summary>
    /// Selects the bounded channel corresponding to <paramref name="priority"/>.
    /// </summary>
    /// <param name="priority">The outbound message priority lane.</param>
    /// <returns>The matching bounded <see cref="Channel{T}"/>.</returns>
    private Channel<OutboundChatMessage> GetChannelForPriority(OutboundChatPriority priority) =>
        priority switch
        {
            OutboundChatPriority.ModerationImmediate => this._moderationChannel,
            OutboundChatPriority.CommandReply => this._commandReplyChannel,
            OutboundChatPriority.EventAnnouncement => this._eventAnnouncementChannel,
            OutboundChatPriority.Timer => this._timerChannel,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown outbound chat priority."),
        };

    /// <summary>
    /// Attempts to dequeue the next message from the four priority lanes in strict priority order.
    /// </summary>
    /// <param name="message">When this method returns <see langword="true"/>, contains the dequeued <see cref="OutboundChatMessage"/>.</param>
    /// <returns><see langword="true"/> if a message was available in any lane; otherwise, <see langword="false"/>.</returns>
    private bool TryReadHighestPriority([NotNullWhen(true)] out OutboundChatMessage? message) =>
        this._moderationChannel.Reader.TryRead(out message)
        || this._commandReplyChannel.Reader.TryRead(out message)
        || this._eventAnnouncementChannel.Reader.TryRead(out message)
        || this._timerChannel.Reader.TryRead(out message);

    /// <summary>
    /// Suspends until at least one priority lane becomes readable, canceling unused lane wait registrations immediately.
    /// </summary>
    /// <param name="stoppingToken">The host cancellation token.</param>
    /// <returns><see langword="true"/> if at least one channel may contain data; <see langword="false"/> if all lanes are completed.</returns>
    private async Task<bool> WaitForAnyLaneAsync(CancellationToken stoppingToken)
    {
        using CancellationTokenSource waitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        CancellationToken waitToken = waitCts.Token;

        Task<bool> moderationWait = this._moderationChannel.Reader.WaitToReadAsync(waitToken).AsTask();
        Task<bool> commandWait = this._commandReplyChannel.Reader.WaitToReadAsync(waitToken).AsTask();
        Task<bool> eventWait = this._eventAnnouncementChannel.Reader.WaitToReadAsync(waitToken).AsTask();
        Task<bool> timerWait = this._timerChannel.Reader.WaitToReadAsync(waitToken).AsTask();

        Task<bool> completed = await Task.WhenAny(moderationWait, commandWait, eventWait, timerWait).ConfigureAwait(false);
        await waitCts.CancelAsync().ConfigureAwait(false);

        if (await completed.ConfigureAwait(false))
        {
            return true;
        }

        // If the completing lane was closed, check whether any remaining lanes are still open.
        return !this._moderationChannel.Reader.Completion.IsCompleted
            || !this._commandReplyChannel.Reader.Completion.IsCompleted
            || !this._eventAnnouncementChannel.Reader.Completion.IsCompleted
            || !this._timerChannel.Reader.Completion.IsCompleted;
    }

    /// <summary>
    /// Resolves target routes and delivers chunks for a single dequeued outbound message.
    /// </summary>
    /// <param name="message">The dequeued outbound message.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous dispatch operation.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Platform adapter send failures must be isolated and logged so the background dispatch loop continues serving other channels and lanes.")]
    private async Task DispatchMessageAsync(OutboundChatMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Message))
        {
            return;
        }

        if (message.IsWhisper)
        {
            await this.DispatchWhisperAsync(message, cancellationToken).ConfigureAwait(false);
            return;
        }

        IReadOnlyList<RouteTarget> targets = await this.ResolveTargetsAsync(message, cancellationToken).ConfigureAwait(false);
        foreach (RouteTarget target in targets)
        {
            if (!this._platforms.TryGetValue(target.PlatformId, out IChatPlatform? platform))
            {
                LogUnknownPlatform(this._logger, target.PlatformId, message.WorkspaceId);
                continue;
            }

            try
            {
                bool isModOrVip = await platform
                    .IsBotModeratorOrVipAsync(message.WorkspaceId, target.ChannelId, cancellationToken)
                    .ConfigureAwait(false);

                int maxChunks = message.MaxChunks ?? this._options.MaxMessageChunks;
                IReadOnlyList<string> chunks = SplitIntoChunks(message.Message, platform.MaxMessageLength, maxChunks);

                foreach (string chunk in chunks)
                {
                    if (this.ShouldSuppressDuplicate(
                        target.PlatformId,
                        target.ChannelId,
                        chunk,
                        message.DuplicatePolicy,
                        platform.SupportsDuplicateMessagesAsNonModerator,
                        isModOrVip))
                    {
                        LogDuplicateSuppressed(this._logger, target.PlatformId, target.ChannelId);
                        continue;
                    }

                    int rateLimit = isModOrVip
                        ? this._options.ModeratorOrVipMessagesPerWindow
                        : this._options.RegularMessagesPerWindow;

                    ChannelTokenBucket bucket = this._rateLimiters.GetOrAdd(
                        new ChannelKey(target.PlatformId, target.ChannelId),
                        _ => new ChannelTokenBucket(rateLimit, this._options.RateLimitWindow, this._timeProvider));

                    await bucket.AcquireAsync(rateLimit, cancellationToken).ConfigureAwait(false);

                    string? replyId = string.Equals(target.PlatformId, message.PlatformId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(target.ChannelId, message.ChannelId, StringComparison.OrdinalIgnoreCase)
                        ? message.ReplyToMessageId
                        : null;

                    await platform
                        .SendMessageAsync(message.WorkspaceId, target.ChannelId, chunk, replyId, cancellationToken)
                        .ConfigureAwait(false);

                    this.RecordSentMessage(target.PlatformId, target.ChannelId, chunk);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogDispatchError(this._logger, target.PlatformId, target.ChannelId, message.WorkspaceId, ex);
            }
        }
    }

    /// <summary>
    /// Delivers a private whisper message in word-boundary chunks via the origin platform adapter.
    /// </summary>
    /// <param name="message">The outbound whisper message.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous whisper send operation.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Platform adapter send failures must be isolated and logged so the background dispatch loop continues serving other messages.")]
    private async Task DispatchWhisperAsync(OutboundChatMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.TargetPlatformUserId))
        {
            return;
        }

        if (!this._platforms.TryGetValue(message.PlatformId, out IChatPlatform? platform))
        {
            LogUnknownPlatform(this._logger, message.PlatformId, message.WorkspaceId);
            return;
        }

        try
        {
            int maxChunks = message.MaxChunks ?? this._options.MaxMessageChunks;
            IReadOnlyList<string> chunks = SplitIntoChunks(message.Message, platform.MaxMessageLength, maxChunks);

            foreach (string chunk in chunks)
            {
                await platform
                    .SendWhisperAsync(message.WorkspaceId, message.TargetPlatformUserId, chunk, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogDispatchError(this._logger, message.PlatformId, message.TargetPlatformUserId, message.WorkspaceId, ex);
        }
    }

    /// <summary>
    /// Resolves the destination <see cref="RouteTarget"/> entries based on <see cref="OutboundChatMessage.TargetScope"/>.
    /// </summary>
    /// <param name="message">The outbound message.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An ordered read-only list of unique destination platform channels.</returns>
    private async ValueTask<IReadOnlyList<RouteTarget>> ResolveTargetsAsync(
        OutboundChatMessage message,
        CancellationToken cancellationToken)
    {
        switch (message.TargetScope)
        {
            case OutboundTargetScope.OriginPlatform:
                return string.IsNullOrWhiteSpace(message.PlatformId) || string.IsNullOrWhiteSpace(message.ChannelId)
                    ? []
                    : [new RouteTarget(message.PlatformId, message.ChannelId)];

            case OutboundTargetScope.PrimaryPlatformOnly:
                if (this._workspaceStore is not null)
                {
                    IReadOnlyList<WorkspacePlatform> platforms = await this._workspaceStore
                        .GetPlatformsAsync(message.WorkspaceId, cancellationToken)
                        .ConfigureAwait(false);

                    WorkspacePlatform? primary = platforms.FirstOrDefault(static p => p.Role == PlatformRole.Primary);
                    if (primary is not null && !string.IsNullOrWhiteSpace(primary.PlatformId) && !string.IsNullOrWhiteSpace(primary.PlatformChannelId))
                    {
                        return [new RouteTarget(primary.PlatformId, primary.PlatformChannelId)];
                    }
                }

                return string.IsNullOrWhiteSpace(message.PlatformId) || string.IsNullOrWhiteSpace(message.ChannelId)
                    ? []
                    : [new RouteTarget(message.PlatformId, message.ChannelId)];

            case OutboundTargetScope.AllActiveStreamPlatforms:
                IReadOnlyList<string> activePlatformIds = await this._streamStatusProvider
                    .GetActiveStreamPlatformIdsAsync(message.WorkspaceId, cancellationToken)
                    .ConfigureAwait(false);

                if (activePlatformIds.Count == 0)
                {
                    return [];
                }

                HashSet<string> activeSet = new(activePlatformIds, StringComparer.OrdinalIgnoreCase);
                List<RouteTarget> targets = [];
                HashSet<RouteTarget> seen = [];

                if (this._workspaceStore is not null)
                {
                    IReadOnlyList<WorkspacePlatform> attachedPlatforms = await this._workspaceStore
                        .GetPlatformsAsync(message.WorkspaceId, cancellationToken)
                        .ConfigureAwait(false);

                    foreach (WorkspacePlatform attached in attachedPlatforms)
                    {
                        if (attached.Role is not (PlatformRole.Primary or PlatformRole.SimulcastPeer))
                        {
                            continue;
                        }

                        if (!activeSet.Contains(attached.PlatformId) || string.IsNullOrWhiteSpace(attached.PlatformChannelId))
                        {
                            continue;
                        }

                        RouteTarget target = new(attached.PlatformId, attached.PlatformChannelId);
                        if (seen.Add(target))
                        {
                            targets.Add(target);
                        }
                    }
                }
                else if (activeSet.Contains(message.PlatformId) && !string.IsNullOrWhiteSpace(message.ChannelId))
                {
                    targets.Add(new RouteTarget(message.PlatformId, message.ChannelId));
                }

                return targets.AsReadOnly();

            default:
                throw new ArgumentOutOfRangeException(nameof(message), message.TargetScope, "Unknown outbound target scope.");
        }
    }

    /// <summary>
    /// Determines whether an outbound chunk should be suppressed due to being identical to a message recently sent on the same channel.
    /// </summary>
    /// <param name="platformId">The target platform identifier.</param>
    /// <param name="channelId">The target channel identifier.</param>
    /// <param name="chunk">The outbound chunk text.</param>
    /// <param name="policy">The message's duplicate handling policy.</param>
    /// <param name="platformSupportsDuplicatesAsNonMod">Whether the target platform permits non-moderators to send duplicates.</param>
    /// <param name="isModOrVip">Whether the bot is currently a moderator or VIP in the target channel.</param>
    /// <returns><see langword="true"/> if the chunk should be suppressed; otherwise, <see langword="false"/>.</returns>
    private bool ShouldSuppressDuplicate(
        string platformId,
        string channelId,
        string chunk,
        DuplicateMessagePolicy policy,
        bool platformSupportsDuplicatesAsNonMod,
        bool isModOrVip)
    {
        if (this._options.DuplicateWindow <= TimeSpan.Zero)
        {
            return false;
        }

        if (policy == DuplicateMessagePolicy.Allow)
        {
            return false;
        }

        if (policy == DuplicateMessagePolicy.PlatformDefault && (platformSupportsDuplicatesAsNonMod || isModOrVip))
        {
            return false;
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DuplicateMessageKey key = new(platformId, channelId, chunk);

        lock (this._dedupLock)
        {
            return this._recentMessages.TryGetValue(key, out DateTimeOffset lastSentAt)
                && (now - lastSentAt) < this._options.DuplicateWindow;
        }
    }

    /// <summary>
    /// Records the send timestamp of a chunk in the duplicate-guard cache and prunes expired entries when above threshold.
    /// </summary>
    /// <param name="platformId">The target platform identifier.</param>
    /// <param name="channelId">The target channel identifier.</param>
    /// <param name="chunk">The sent chunk text.</param>
    private void RecordSentMessage(string platformId, string channelId, string chunk)
    {
        if (this._options.DuplicateWindow <= TimeSpan.Zero)
        {
            return;
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DuplicateMessageKey key = new(platformId, channelId, chunk);

        lock (this._dedupLock)
        {
            this._recentMessages[key] = now;

            if (this._recentMessages.Count > _dedupPruneThreshold)
            {
                DateTimeOffset cutoff = now - this._options.DuplicateWindow;
                foreach (KeyValuePair<DuplicateMessageKey, DateTimeOffset> entry in this._recentMessages)
                {
                    if (entry.Value <= cutoff)
                    {
                        _ = this._recentMessages.TryRemove(entry.Key, out _);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Callback invoked by saturated droppable channels when the oldest message is discarded.
    /// </summary>
    /// <param name="priority">The priority lane that dropped the item.</param>
    /// <param name="dropped">The dropped outbound message.</param>
    private void OnChannelItemDropped(OutboundChatPriority priority, OutboundChatMessage dropped)
    {
        _ = Interlocked.Increment(ref this._droppedMessageCount);
        LogMessageDropped(this._logger, priority, dropped.PlatformId, dropped.ChannelId);
    }

    /// <summary>
    /// Logs a warning when a droppable lane drops an item due to saturation.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="priority">The priority lane.</param>
    /// <param name="platformId">The target platform identifier.</param>
    /// <param name="channelId">The target channel identifier.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped oldest outbound message in priority lane {Priority} for {PlatformId}/{ChannelId} because the channel reached capacity.")]
    private static partial void LogMessageDropped(ILogger logger, OutboundChatPriority priority, string platformId, string channelId);

    /// <summary>
    /// Logs a warning when a target platform identifier has no registered <see cref="IChatPlatform"/> adapter.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="platformId">The missing platform identifier.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "No registered IChatPlatform adapter found for platform '{PlatformId}' (workspace {WorkspaceId}).")]
    private static partial void LogUnknownPlatform(ILogger logger, string platformId, Guid workspaceId);

    /// <summary>
    /// Logs a debug entry when an identical message is suppressed by the 30-second duplicate guard.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="platformId">The target platform identifier.</param>
    /// <param name="channelId">The target channel identifier.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Suppressed duplicate outbound message within dedup window for {PlatformId}/{ChannelId}.")]
    private static partial void LogDuplicateSuppressed(ILogger logger, string platformId, string channelId);

    /// <summary>
    /// Logs an error when dispatching an outbound message to a platform fails.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="platformId">The target platform identifier.</param>
    /// <param name="channelId">The target channel or user identifier.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="exception">The exception thrown by the platform adapter.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch outbound message to {PlatformId}/{ChannelId} for workspace {WorkspaceId}.")]
    private static partial void LogDispatchError(ILogger logger, string platformId, string channelId, Guid workspaceId, Exception exception);

    /// <summary>
    /// Represents a resolved destination platform and channel pair.
    /// </summary>
    /// <param name="PlatformId">The target platform identifier.</param>
    /// <param name="ChannelId">The target channel identifier.</param>
    private readonly record struct RouteTarget(string PlatformId, string ChannelId) : IEquatable<RouteTarget>
    {
        /// <inheritdoc/>
        public bool Equals(RouteTarget other) =>
            string.Equals(this.PlatformId, other.PlatformId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.ChannelId, other.ChannelId, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.PlatformId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.ChannelId));
    }

    /// <summary>
    /// Case-insensitive key identifying a rate-limited platform channel.
    /// </summary>
    /// <param name="PlatformId">The platform identifier.</param>
    /// <param name="ChannelId">The channel identifier.</param>
    private readonly record struct ChannelKey(string PlatformId, string ChannelId) : IEquatable<ChannelKey>
    {
        /// <inheritdoc/>
        public bool Equals(ChannelKey other) =>
            string.Equals(this.PlatformId, other.PlatformId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.ChannelId, other.ChannelId, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.PlatformId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.ChannelId));
    }

    /// <summary>
    /// Composite key for duplicate message detection per channel and exact message text.
    /// </summary>
    /// <param name="PlatformId">The platform identifier.</param>
    /// <param name="ChannelId">The channel identifier.</param>
    /// <param name="Message">The sent chunk text.</param>
    private readonly record struct DuplicateMessageKey(string PlatformId, string ChannelId, string Message) : IEquatable<DuplicateMessageKey>
    {
        /// <inheritdoc/>
        public bool Equals(DuplicateMessageKey other) =>
            string.Equals(this.PlatformId, other.PlatformId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.ChannelId, other.ChannelId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.Message, other.Message, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.PlatformId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(this.ChannelId),
                StringComparer.Ordinal.GetHashCode(this.Message));
    }

    /// <summary>
    /// <see cref="TimeProvider"/>-driven token bucket enforcing per-channel outbound message pacing.
    /// </summary>
    private sealed class ChannelTokenBucket
    {
        /// <summary>
        /// Floating-point tolerance used when checking whether a full token is available after refill.
        /// </summary>
        private const double _tokenEpsilon = 1e-9;

        /// <summary>
        /// Synchronization lock protecting bucket state.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        /// Time provider supplying monotonic timestamps and async delays.
        /// </summary>
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Refill window length in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        private readonly long _windowTicks;

        /// <summary>
        /// Current maximum bucket capacity (for example, 100 for moderator/VIP or 20 for regular).
        /// </summary>
        private int _capacity;

        /// <summary>
        /// Current available token count.
        /// </summary>
        private double _tokens;

        /// <summary>
        /// Monotonic timestamp from <see cref="TimeProvider.GetTimestamp"/> when tokens were last refilled.
        /// </summary>
        private long _lastRefillTimestamp;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelTokenBucket"/> class.
        /// </summary>
        /// <param name="initialCapacity">The initial token bucket capacity.</param>
        /// <param name="window">The refill window duration.</param>
        /// <param name="timeProvider">The <see cref="TimeProvider"/> instance.</param>
        public ChannelTokenBucket(int initialCapacity, TimeSpan window, TimeProvider timeProvider)
        {
            this._capacity = Math.Max(initialCapacity, 1);
            this._tokens = this._capacity;
            this._windowTicks = Math.Max(window.Ticks, 1L);
            this._timeProvider = timeProvider;
            this._lastRefillTimestamp = timeProvider.GetTimestamp();
        }

        /// <summary>
        /// Waits until a token is available under <paramref name="capacity"/> and consumes one token.
        /// </summary>
        /// <param name="capacity">The effective channel capacity for the current send (100 for mod/VIP, 20 for regular).</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> that completes once a token has been acquired.</returns>
        public async Task AcquireAsync(int capacity, CancellationToken cancellationToken)
        {
            int effectiveCapacity = Math.Max(capacity, 1);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TimeSpan waitDuration;
                lock (this._lock)
                {
                    this.RefillLocked(effectiveCapacity);

                    if (this._tokens >= (1.0 - _tokenEpsilon))
                    {
                        this._tokens = Math.Max(0.0, this._tokens - 1.0);
                        return;
                    }

                    double deficit = 1.0 - this._tokens;
                    double ticksPerToken = (double)this._windowTicks / this._capacity;
                    long waitTicks = (long)Math.Ceiling(deficit * ticksPerToken);
                    waitDuration = TimeSpan.FromTicks(Math.Max(waitTicks, 1L));
                }

                await Task.Delay(waitDuration, this._timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Refills tokens based on elapsed time since the previous refill and updates capacity if changed.
        /// </summary>
        /// <param name="capacity">The effective token capacity.</param>
        private void RefillLocked(int capacity)
        {
            long now = this._timeProvider.GetTimestamp();
            TimeSpan elapsed = this._timeProvider.GetElapsedTime(this._lastRefillTimestamp, now);

            if (capacity != this._capacity)
            {
                this._capacity = capacity;
                if (this._tokens > this._capacity)
                {
                    this._tokens = this._capacity;
                }
            }

            if (elapsed > TimeSpan.Zero)
            {
                double added = (double)elapsed.Ticks * this._capacity / this._windowTicks;
                this._tokens = Math.Min(this._capacity, this._tokens + added);
                this._lastRefillTimestamp = now;
            }
        }
    }
}
