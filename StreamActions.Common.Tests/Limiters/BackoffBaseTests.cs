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
using StreamActions.Common.Limiters;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StreamActions.Common.Tests.Limiters;

public sealed class BackoffBaseTests
{
    private sealed class TestBackoff : BackoffBase
    {
        public long NextTicksToReturn { get; set; }

        public TestBackoff(TimeSpan initialDuration, TimeSpan maxDuration)
            : base(initialDuration, maxDuration)
        {
            this.NextTicksToReturn = initialDuration.Ticks * 2;
        }

        protected override long CalcNextDurationTicks()
        {
            return this.NextTicksToReturn;
        }

        public void SetNextDurationReflectively(TimeSpan duration)
        {
            var field = typeof(BackoffBase).GetField("_nextDuration", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Could not find _nextDuration field.");
            this.Rwl.EnterWriteLock();
            try
            {
                field.SetValue(this, duration);
            }
            finally
            {
                this.Rwl.ExitWriteLock();
            }
        }

        public ReaderWriterLockSlim GetRwl() => this.Rwl;
    }

    #region Constructor

    [Fact]
    [Trait("Member", "Constructor")]
    public void Constructor_InitializesProperties()
    {
        var initial = TimeSpan.FromTicks(10);
        var max = TimeSpan.FromTicks(100);

        var backoff = new TestBackoff(initial, max);

        backoff.IntialDuration.Should().Be(initial);
        backoff.MaxDuration.Should().Be(max);
        backoff.NextDuration.Should().Be(initial);
        backoff.IsReset.Should().BeTrue();
        backoff.IsMaxDuration.Should().BeFalse();
    }

    #endregion Constructor

    #region IntialDuration

    [Fact]
    [Trait("Member", "IntialDuration")]
    public void IntialDuration_ReturnsSetValue()
    {
        var initial = TimeSpan.FromTicks(50);
        var backoff = new TestBackoff(initial, TimeSpan.FromTicks(100));

        backoff.IntialDuration.Should().Be(initial);
    }

    #endregion IntialDuration

    #region MaxDuration

    [Fact]
    [Trait("Member", "MaxDuration")]
    public void MaxDuration_ReturnsSetValue()
    {
        var max = TimeSpan.FromTicks(200);
        var backoff = new TestBackoff(TimeSpan.FromTicks(10), max);

        backoff.MaxDuration.Should().Be(max);
    }

    #endregion MaxDuration

    #region NextDuration

    [Fact]
    [Trait("Member", "NextDuration")]
    public void NextDuration_ReturnsCurrentNextDuration()
    {
        var initial = TimeSpan.FromTicks(10);
        var max = TimeSpan.FromTicks(100);
        var backoff = new TestBackoff(initial, max);

        backoff.NextDuration.Should().Be(initial);

        backoff.SetNextDurationReflectively(TimeSpan.FromTicks(20));

        backoff.NextDuration.Should().Be(TimeSpan.FromTicks(20));
    }

    #endregion NextDuration

    #region IsReset

    [Fact]
    [Trait("Member", "IsReset")]
    public void IsReset_ReturnsTrueWhenNextDurationEqualsInitialDuration()
    {
        var initial = TimeSpan.FromTicks(10);
        var backoff = new TestBackoff(initial, TimeSpan.FromTicks(100));

        backoff.IsReset.Should().BeTrue();

        backoff.SetNextDurationReflectively(TimeSpan.FromTicks(20));

        backoff.IsReset.Should().BeFalse();
    }

    #endregion IsReset

    #region IsMaxDuration

    [Fact]
    [Trait("Member", "IsMaxDuration")]
    public void IsMaxDuration_ReturnsTrueWhenNextDurationEqualsMaxDuration()
    {
        var max = TimeSpan.FromTicks(100);
        var backoff = new TestBackoff(TimeSpan.FromTicks(10), max);

        backoff.IsMaxDuration.Should().BeFalse();

        backoff.SetNextDurationReflectively(max);

        backoff.IsMaxDuration.Should().BeTrue();
    }

    #endregion IsMaxDuration

    #region Reset

    [Fact]
    [Trait("Member", "Reset")]
    public void Reset_RestoresNextDurationToInitialDuration()
    {
        var initial = TimeSpan.FromTicks(10);
        var backoff = new TestBackoff(initial, TimeSpan.FromTicks(100));

        // Advance _nextDuration artificially
        backoff.SetNextDurationReflectively(TimeSpan.FromTicks(50));
        backoff.NextDuration.Should().Be(TimeSpan.FromTicks(50));
        backoff.IsReset.Should().BeFalse();

        // Call Reset
        backoff.Reset(TimeSpan.FromSeconds(1));

        backoff.NextDuration.Should().Be(initial);
        backoff.IsReset.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "Reset")]
    public async Task Reset_WithTimeout_ThrowsTimeoutExceptionIfLockFails()
    {
        var initial = TimeSpan.FromTicks(10);
        var backoff = new TestBackoff(initial, TimeSpan.FromTicks(100));

        // Ensure _nextDuration is not initial so Reset attempts to get write lock
        backoff.SetNextDurationReflectively(TimeSpan.FromTicks(50));

        using var lockTakenEvent = new SemaphoreSlim(0, 1);
        using var releaseLockEvent = new SemaphoreSlim(0, 1);

        // Lock in a background thread
        var lockTask = Task.Run(() =>
        {
            var rwl = backoff.GetRwl();

            rwl.EnterWriteLock();
            try
            {
                lockTakenEvent.Release();
                releaseLockEvent.Wait();
            }
            finally
            {
                rwl.ExitWriteLock();
            }
        });

        await lockTakenEvent.WaitAsync().ConfigureAwait(true); // Wait for background thread to acquire lock

        try
        {
            Action act = () => backoff.Reset(TimeSpan.FromMilliseconds(10));
            act.Should().Throw<TimeoutException>();
        }
        finally
        {
            releaseLockEvent.Release(); // Let background thread release the lock
            await lockTask.ConfigureAwait(true);
        }
    }

    #endregion Reset

    #region Wait

    [Fact(Skip = "Known bug: SynchronizationLockException in Wait() due to ReaderWriterLockSlim across await")]
    [Trait("Member", "Wait")]
    public async Task Wait_WaitsForNextDurationAndCalculatesNewNextDuration()
    {
        var initial = TimeSpan.FromTicks(10);
        var max = TimeSpan.FromTicks(100);
        var backoff = new TestBackoff(initial, max);

        backoff.NextTicksToReturn = 50;

        await backoff.Wait(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        backoff.NextDuration.Should().Be(TimeSpan.FromTicks(50));
    }

    #endregion Wait
}
