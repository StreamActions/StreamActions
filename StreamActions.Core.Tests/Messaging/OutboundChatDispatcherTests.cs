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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StreamActions.Core.Abstractions;
using StreamActions.Core.Models;
using StreamActions.Core.Models.Entities;
using StreamActions.Core.Services;
using System.Collections.Concurrent;
using Xunit;

namespace StreamActions.Core.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="OutboundChatDispatcher"/>.
/// </summary>
public sealed class OutboundChatDispatcherTests
{
    #region PriorityLanes

    [Fact]
    [Trait("Member", "PriorityLanes")]
    public async Task DispatcherDrainsLanesInStrictPriorityOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));
        RecordingChatPlatform twitch = new("twitch");
        DefaultStreamStatusProvider statusProvider = new();

        using OutboundChatDispatcher dispatcher = new(
            [twitch],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance);

        TaskCompletionSource allFourSent = twitch.ExpectMessageCount(4);

        // Enqueue lowest to highest before starting the background loop so all 4 lanes are populated.
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "timer-msg", OutboundChatPriority.Timer), ct);
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "event-msg", OutboundChatPriority.EventAnnouncement), ct);
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "reply-msg", OutboundChatPriority.CommandReply), ct);
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "mod-msg", OutboundChatPriority.ModerationImmediate), ct);

        await dispatcher.StartAsync(ct);
        try
        {
            await allFourSent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            _ = twitch.SentMessages.Select(static m => m.Message).Should().Equal(
                "mod-msg",
                "reply-msg",
                "event-msg",
                "timer-msg");
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    #endregion

    #region SaturationDropOldest

    [Theory]
    [InlineData(OutboundChatPriority.EventAnnouncement)]
    [InlineData(OutboundChatPriority.Timer)]
    [Trait("Member", "SaturationDropOldest")]
    public async Task DroppableLanesDropOldestMessagesWhenSaturated(OutboundChatPriority priority)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));
        RecordingChatPlatform twitch = new("twitch");
        DefaultStreamStatusProvider statusProvider = new();
        OutboundChatDispatcherOptions options = new()
        {
            EventAnnouncementLaneCapacity = 2,
            TimerLaneCapacity = 2,
        };

        using OutboundChatDispatcher dispatcher = new(
            [twitch],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance,
            options: Options.Create(options));

        // Capacity is 2; enqueue 4 messages before starting so messages 1 and 2 are dropped.
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "msg-1", priority), ct);
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "msg-2", priority), ct);
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "msg-3", priority), ct);
        await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "msg-4", priority), ct);

        _ = dispatcher.DroppedMessageCount.Should().Be(2);

        TaskCompletionSource twoSent = twitch.ExpectMessageCount(2);
        await dispatcher.StartAsync(ct);
        try
        {
            await twoSent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = twitch.SentMessages.Select(static m => m.Message).Should().Equal("msg-3", "msg-4");
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    #endregion

    #region WordBoundaryChunking

    [Fact]
    [Trait("Member", "SplitIntoChunks")]
    public void SplitIntoChunksSplitsOnWordBoundariesUpToMaxChunks()
    {
        const string text = "alpha beta gamma delta epsilon";

        IReadOnlyList<string> twoChunks = OutboundChatDispatcher.SplitIntoChunks(text, maxLength: 11, maxChunks: 2);
        IReadOnlyList<string> hardSplit = OutboundChatDispatcher.SplitIntoChunks("abcdefghijklmno", maxLength: 5, maxChunks: 2);

        _ = twoChunks.Should().Equal("alpha beta", "gamma delta");
        _ = hardSplit.Should().Equal("abcde", "fghij");
    }

    [Fact]
    [Trait("Member", "WordBoundaryChunking")]
    public async Task DispatcherSplitsLongMessageIntoAtMostDefaultTwoChunks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));
        RecordingChatPlatform twitch = new("twitch") { MaxMessageLength = 15 };
        DefaultStreamStatusProvider statusProvider = new();

        using OutboundChatDispatcher dispatcher = new(
            [twitch],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance);

        TaskCompletionSource twoSent = twitch.ExpectMessageCount(2);
        await dispatcher.StartAsync(ct);
        try
        {
            await dispatcher.EnqueueAsync(
                new(workspaceId, "twitch", "chan1", "hello world again friends forever", OutboundChatPriority.CommandReply, ReplyToMessageId: "parent-1"),
                ct);

            await twoSent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = twitch.SentMessages.Select(static m => m.Message).Should().Equal("hello world", "again friends");
            _ = twitch.SentMessages.Should().OnlyContain(static m => m.ReplyToMessageId == "parent-1");
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    #endregion

    #region RateLimitPacing

    [Fact]
    [Trait("Member", "RateLimitPacing")]
    public async Task RegularChannelPacesAtTwentyPerThirtySecondsWithFakeTimeProvider()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));
        TrackingTimeProvider timeProvider = new(fakeTime);
        RecordingChatPlatform twitch = new("twitch") { IsModOrVip = false };
        DefaultStreamStatusProvider statusProvider = new();

        using OutboundChatDispatcher dispatcher = new(
            [twitch],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance);

        TaskCompletionSource burstDone = twitch.ExpectMessageCount(20);
        TaskCompletionSource delayRegistered = timeProvider.ExpectNextTimer();
        TaskCompletionSource message21Done = twitch.ExpectMessageCount(21);

        await dispatcher.StartAsync(ct);
        try
        {
            for (int i = 1; i <= 21; i++)
            {
                await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", $"msg-{i}"), ct);
            }

            await burstDone.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            await delayRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = message21Done.Task.IsCompleted.Should().BeFalse();

            // 20 tokens / 30s = 1.5s per token. Advancing 1.5s releases message #21.
            fakeTime.Advance(TimeSpan.FromMilliseconds(1500));
            await message21Done.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            _ = twitch.SentMessages.Should().HaveCount(21);
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    [Fact]
    [Trait("Member", "RateLimitPacing")]
    public async Task ModeratorOrVipChannelPacesAtHundredPerThirtySecondsWithFakeTimeProvider()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider fakeTime = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));
        TrackingTimeProvider timeProvider = new(fakeTime);
        RecordingChatPlatform twitch = new("twitch") { IsModOrVip = true };
        DefaultStreamStatusProvider statusProvider = new();

        using OutboundChatDispatcher dispatcher = new(
            [twitch],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance);

        TaskCompletionSource burstDone = twitch.ExpectMessageCount(100);
        TaskCompletionSource delayRegistered = timeProvider.ExpectNextTimer();
        TaskCompletionSource message101Done = twitch.ExpectMessageCount(101);

        await dispatcher.StartAsync(ct);
        try
        {
            for (int i = 1; i <= 101; i++)
            {
                await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", $"mod-{i}"), ct);
            }

            await burstDone.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            await delayRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = message101Done.Task.IsCompleted.Should().BeFalse();

            // 100 tokens / 30s = 300ms per token. Advancing 300ms releases message #101.
            fakeTime.Advance(TimeSpan.FromMilliseconds(300));
            await message101Done.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            _ = twitch.SentMessages.Should().HaveCount(101);
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    #endregion

    #region DuplicateMessageGuard

    [Fact]
    [Trait("Member", "DuplicateMessageGuard")]
    public async Task IdenticalMessagesAreSuppressedWithinThirtySecondsForNonModeratorBotAndAllowedAfterWindow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));
        RecordingChatPlatform twitch = new("twitch")
        {
            IsModOrVip = false,
            SupportsDuplicatesAsNonMod = false,
        };
        DefaultStreamStatusProvider statusProvider = new();

        using OutboundChatDispatcher dispatcher = new(
            [twitch],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance);

        TaskCompletionSource firstSent = twitch.ExpectMessageCount(1);
        TaskCompletionSource sentinelAfterSuppressed = twitch.ExpectMessageCount(2);
        TaskCompletionSource afterWindowSent = twitch.ExpectMessageCount(3);

        await dispatcher.StartAsync(ct);
        try
        {
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Hello chat!"), ct);
            await firstSent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            // Within 30s -> suppressed, so enqueueing a distinct sentinel after it lets us deterministically wait for both to be processed.
            timeProvider.Advance(TimeSpan.FromSeconds(15));
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Hello chat!"), ct);
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Sentinel message"), ct);
            await sentinelAfterSuppressed.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            _ = twitch.SentMessages.Select(static m => m.Message).Should().Equal("Hello chat!", "Sentinel message");

            // Advance past the 30s window from the first "Hello chat!" send -> now allowed again.
            timeProvider.Advance(TimeSpan.FromSeconds(16));
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Hello chat!"), ct);
            await afterWindowSent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            _ = twitch.SentMessages.Select(static m => m.Message).Should().Equal("Hello chat!", "Sentinel message", "Hello chat!");
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    [Fact]
    [Trait("Member", "DuplicateMessageGuard")]
    public async Task IdenticalMessagesAreAllowedForModeratorBotUnlessPolicyIsSuppress()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));
        RecordingChatPlatform twitch = new("twitch")
        {
            IsModOrVip = true,
            SupportsDuplicatesAsNonMod = false,
        };
        DefaultStreamStatusProvider statusProvider = new();

        using OutboundChatDispatcher dispatcher = new(
            [twitch],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance);

        TaskCompletionSource threeSent = twitch.ExpectMessageCount(3);

        await dispatcher.StartAsync(ct);
        try
        {
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Repeat!", DuplicatePolicy: DuplicateMessagePolicy.PlatformDefault), ct);
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Repeat!", DuplicatePolicy: DuplicateMessagePolicy.PlatformDefault), ct);
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Repeat!", DuplicatePolicy: DuplicateMessagePolicy.Suppress), ct);
            await dispatcher.EnqueueAsync(new(workspaceId, "twitch", "chan1", "Done!"), ct);

            await threeSent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            _ = twitch.SentMessages.Select(static m => m.Message).Should().Equal("Repeat!", "Repeat!", "Done!");
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    #endregion

    #region SimulcastRouting

    [Fact]
    [Trait("Member", "SimulcastRouting")]
    public async Task TargetScopesRouteToOriginPrimaryAndActiveStreamPlatformsRespectively()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid workspaceId = Guid.NewGuid();
        FakeTimeProvider timeProvider = new(new DateTimeOffset(2026, 11, 21, 15, 0, 0, TimeSpan.Zero));

        RecordingChatPlatform twitch = new("twitch");
        RecordingChatPlatform youtube = new("youtube");
        RecordingChatPlatform kick = new("kick");
        RecordingChatPlatform discord = new("discord");

        DefaultStreamStatusProvider statusProvider = new();
        statusProvider.SetPlatformStatus(workspaceId, "twitch", isLive: true, "Coding");
        statusProvider.SetPlatformStatus(workspaceId, "youtube", isLive: true, "Coding");
        statusProvider.SetPlatformStatus(workspaceId, "kick", isLive: false);
        statusProvider.SetPlatformStatus(workspaceId, "discord", isLive: true);

        StubWorkspaceStore workspaceStore = new(
        [
            new WorkspacePlatform { WorkspaceId = workspaceId, PlatformId = "twitch", Role = PlatformRole.Primary, PlatformChannelId = "tw-chan" },
            new WorkspacePlatform { WorkspaceId = workspaceId, PlatformId = "youtube", Role = PlatformRole.SimulcastPeer, PlatformChannelId = "yt-chan" },
            new WorkspacePlatform { WorkspaceId = workspaceId, PlatformId = "kick", Role = PlatformRole.SimulcastPeer, PlatformChannelId = "kick-chan" },
            new WorkspacePlatform { WorkspaceId = workspaceId, PlatformId = "discord", Role = PlatformRole.Addon, PlatformChannelId = "dc-chan" },
        ]);

        using OutboundChatDispatcher dispatcher = new(
            [twitch, youtube, kick, discord],
            statusProvider,
            timeProvider,
            NullLogger<OutboundChatDispatcher>.Instance,
            workspaceStore);

        await dispatcher.StartAsync(ct);
        try
        {
            // 1. OriginPlatform -> only youtube
            TaskCompletionSource originSent = youtube.ExpectMessageCount(1);
            await dispatcher.EnqueueAsync(
                new(workspaceId, "youtube", "yt-chan", "origin-only", TargetScope: OutboundTargetScope.OriginPlatform),
                ct);
            await originSent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            // 2. PrimaryPlatformOnly from youtube -> only twitch
            TaskCompletionSource primarySent = twitch.ExpectMessageCount(1);
            await dispatcher.EnqueueAsync(
                new(workspaceId, "youtube", "yt-chan", "primary-only", TargetScope: OutboundTargetScope.PrimaryPlatformOnly),
                ct);
            await primarySent.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            // 3. AllActiveStreamPlatforms -> twitch + youtube (kick is offline, discord is Addon)
            TaskCompletionSource twitchAll = twitch.ExpectMessageCount(2);
            TaskCompletionSource youtubeAll = youtube.ExpectMessageCount(2);
            await dispatcher.EnqueueAsync(
                new(workspaceId, "twitch", "tw-chan", "all-live", TargetScope: OutboundTargetScope.AllActiveStreamPlatforms),
                ct);
            await Task.WhenAll(twitchAll.Task, youtubeAll.Task).WaitAsync(TimeSpan.FromSeconds(5), ct);

            // 4. Whisper routing
            TaskCompletionSource whisperDone = twitch.ExpectWhisperCount(1);
            await dispatcher.EnqueueAsync(
                new(workspaceId, "twitch", string.Empty, "secret whisper", IsWhisper: true, TargetPlatformUserId: "user-99"),
                ct);
            await whisperDone.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            _ = twitch.SentMessages.Select(static m => m.Message).Should().Equal("primary-only", "all-live");
            _ = youtube.SentMessages.Select(static m => m.Message).Should().Equal("origin-only", "all-live");
            _ = kick.SentMessages.Should().BeEmpty();
            _ = discord.SentMessages.Should().BeEmpty();
            _ = twitch.SentWhispers.Should().ContainSingle(static w => w.TargetUserId == "user-99" && w.Message == "secret whisper");
        }
        finally
        {
            await dispatcher.StopAsync(ct);
        }
    }

    #endregion

    /// <summary>
    /// Recorded outbound channel message captured by <see cref="RecordingChatPlatform"/>.
    /// </summary>
    /// <param name="WorkspaceId">The workspace identifier.</param>
    /// <param name="ChannelId">The channel identifier.</param>
    /// <param name="Message">The sent message text.</param>
    /// <param name="ReplyToMessageId">The optional reply message identifier.</param>
    private sealed record RecordedMessage(Guid WorkspaceId, string ChannelId, string Message, string? ReplyToMessageId);

    /// <summary>
    /// Recorded outbound whisper captured by <see cref="RecordingChatPlatform"/>.
    /// </summary>
    /// <param name="WorkspaceId">The workspace identifier.</param>
    /// <param name="TargetUserId">The target platform user identifier.</param>
    /// <param name="Message">The sent whisper text.</param>
    private sealed record RecordedWhisper(Guid WorkspaceId, string TargetUserId, string Message);

    /// <summary>
    /// Thread-safe in-memory <see cref="IChatPlatform"/> test double with deterministic send completion signals.
    /// </summary>
    /// <param name="platformId">The platform identifier.</param>
    private sealed class RecordingChatPlatform(string platformId) : IChatPlatform
    {
        /// <summary>
        /// Lock synchronizing recorded message lists and completion waiters.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        /// Pending waiters triggered when <see cref="SentMessages"/> reaches a target count.
        /// </summary>
        private readonly List<(int TargetCount, TaskCompletionSource Signal)> _messageWaiters = [];

        /// <summary>
        /// Pending waiters triggered when <see cref="SentWhispers"/> reaches a target count.
        /// </summary>
        private readonly List<(int TargetCount, TaskCompletionSource Signal)> _whisperWaiters = [];

        /// <summary>
        /// Backing store of sent channel messages.
        /// </summary>
        private readonly ConcurrentQueue<RecordedMessage> _sentMessages = new();

        /// <summary>
        /// Backing store of sent whispers.
        /// </summary>
        private readonly ConcurrentQueue<RecordedWhisper> _sentWhispers = new();

        /// <inheritdoc/>
        public string PlatformId { get; } = platformId;

        /// <inheritdoc/>
        public string DisplayName => this.PlatformId;

        /// <inheritdoc/>
        public PlatformCapabilities Capabilities => PlatformCapabilities.Chat;

        /// <inheritdoc/>
        public int MaxMessageLength { get; set; } = 500;

        /// <inheritdoc/>
        public bool SupportsDuplicateMessagesAsNonModerator => this.SupportsDuplicatesAsNonMod;

        /// <summary>
        /// Gets or sets a value indicating whether non-moderators can send duplicate messages.
        /// </summary>
        public bool SupportsDuplicatesAsNonMod { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the bot holds moderator or VIP status.
        /// </summary>
        public bool IsModOrVip { get; set; }

        /// <summary>
        /// Gets all recorded channel messages in send order.
        /// </summary>
        public IReadOnlyList<RecordedMessage> SentMessages => [.. this._sentMessages];

        /// <summary>
        /// Gets all recorded whispers in send order.
        /// </summary>
        public IReadOnlyList<RecordedWhisper> SentWhispers => [.. this._sentWhispers];

        /// <summary>
        /// Creates a <see cref="TaskCompletionSource"/> signaled once <see cref="SentMessages"/> reaches <paramref name="count"/>.
        /// </summary>
        /// <param name="count">The target message count.</param>
        /// <returns>A <see cref="TaskCompletionSource"/> completed when the target count is reached.</returns>
        public TaskCompletionSource ExpectMessageCount(int count)
        {
            TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (this._lock)
            {
                if (this._sentMessages.Count >= count)
                {
                    _ = tcs.TrySetResult();
                }
                else
                {
                    this._messageWaiters.Add((count, tcs));
                }
            }

            return tcs;
        }

        /// <summary>
        /// Creates a <see cref="TaskCompletionSource"/> signaled once <see cref="SentWhispers"/> reaches <paramref name="count"/>.
        /// </summary>
        /// <param name="count">The target whisper count.</param>
        /// <returns>A <see cref="TaskCompletionSource"/> completed when the target count is reached.</returns>
        public TaskCompletionSource ExpectWhisperCount(int count)
        {
            TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (this._lock)
            {
                if (this._sentWhispers.Count >= count)
                {
                    _ = tcs.TrySetResult();
                }
                else
                {
                    this._whisperWaiters.Add((count, tcs));
                }
            }

            return tcs;
        }

        /// <inheritdoc/>
        public ValueTask ConnectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask DisconnectWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask<bool> IsBotModeratorOrVipAsync(Guid workspaceId, string channelId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.IsModOrVip);

        /// <inheritdoc/>
        public ValueTask SendMessageAsync(
            Guid workspaceId,
            string channelId,
            string message,
            string? replyToMessageId = null,
            CancellationToken cancellationToken = default)
        {
            lock (this._lock)
            {
                this._sentMessages.Enqueue(new RecordedMessage(workspaceId, channelId, message, replyToMessageId));
                int total = this._sentMessages.Count;
                for (int i = this._messageWaiters.Count - 1; i >= 0; i--)
                {
                    if (total >= this._messageWaiters[i].TargetCount)
                    {
                        _ = this._messageWaiters[i].Signal.TrySetResult();
                        this._messageWaiters.RemoveAt(i);
                    }
                }
            }

            return ValueTask.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask SendWhisperAsync(
            Guid workspaceId,
            string targetPlatformUserId,
            string message,
            CancellationToken cancellationToken = default)
        {
            lock (this._lock)
            {
                this._sentWhispers.Enqueue(new RecordedWhisper(workspaceId, targetPlatformUserId, message));
                int total = this._sentWhispers.Count;
                for (int i = this._whisperWaiters.Count - 1; i >= 0; i--)
                {
                    if (total >= this._whisperWaiters[i].TargetCount)
                    {
                        _ = this._whisperWaiters[i].Signal.TrySetResult();
                        this._whisperWaiters.RemoveAt(i);
                    }
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Minimal <see cref="IWorkspaceStore"/> stub returning configured <see cref="WorkspacePlatform"/> attachments.
    /// </summary>
    /// <param name="platforms">The configured workspace platforms.</param>
    private sealed class StubWorkspaceStore(IReadOnlyList<WorkspacePlatform> platforms) : IWorkspaceStore
    {
        /// <inheritdoc/>
        public ValueTask<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Workspace?>(null);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<Workspace>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([]);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<Workspace>>([]);

        /// <inheritdoc/>
        public ValueTask<Workspace> CreateWorkspaceAsync(Workspace workspace, WorkspacePlatform? primaryPlatform = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);

        /// <inheritdoc/>
        public ValueTask<Workspace> UpdateWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(workspace);

        /// <inheritdoc/>
        public ValueTask<bool> DeleteWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        /// <inheritdoc/>
        public ValueTask<IReadOnlyList<WorkspacePlatform>> GetPlatformsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<WorkspacePlatform>>([.. platforms.Where(p => p.WorkspaceId == workspaceId)]);

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform?> GetPlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(platforms.FirstOrDefault(p => p.WorkspaceId == workspaceId && string.Equals(p.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)));

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform> UpsertPlatformAsync(WorkspacePlatform platform, string? plaintextCustomClientSecret = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(platform);

        /// <inheritdoc/>
        public ValueTask<WorkspacePlatform> SetCustomOAuthAppAsync(Guid workspaceId, string platformId, string? clientId, string? clientSecret, Uri? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<PlatformOAuthAppCredentials?> GetCustomOAuthAppAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PlatformOAuthAppCredentials?>(null);

        /// <inheritdoc/>
        public ValueTask<bool> RemovePlatformAsync(Guid workspaceId, string platformId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    /// <summary>
    /// Delegating <see cref="TimeProvider"/> that signals waiters whenever a timer (such as <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/>) is registered.
    /// </summary>
    /// <param name="inner">The underlying <see cref="FakeTimeProvider"/>.</param>
    private sealed class TrackingTimeProvider(FakeTimeProvider inner) : TimeProvider
    {
        /// <summary>
        /// Lock synchronizing pending timer registration waiters.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        /// Pending waiters to signal on the next <see cref="CreateTimer"/> invocation.
        /// </summary>
        private readonly List<TaskCompletionSource> _waiters = [];

        /// <inheritdoc/>
        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        /// <inheritdoc/>
        public override long TimestampFrequency => inner.TimestampFrequency;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        /// <inheritdoc/>
        public override long GetTimestamp() => inner.GetTimestamp();

        /// <summary>
        /// Returns a <see cref="TaskCompletionSource"/> completed when the next timer is created on this provider.
        /// </summary>
        /// <returns>A <see cref="TaskCompletionSource"/> signaled on the next timer registration.</returns>
        public TaskCompletionSource ExpectNextTimer()
        {
            TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (this._lock)
            {
                this._waiters.Add(tcs);
            }

            return tcs;
        }

        /// <inheritdoc/>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ITimer timer = inner.CreateTimer(callback, state, dueTime, period);

            List<TaskCompletionSource>? toSignal = null;
            lock (this._lock)
            {
                if (this._waiters.Count > 0)
                {
                    toSignal = [.. this._waiters];
                    this._waiters.Clear();
                }
            }

            if (toSignal is not null)
            {
                foreach (TaskCompletionSource waiter in toSignal)
                {
                    _ = waiter.TrySetResult();
                }
            }

            return timer;
        }
    }
}
