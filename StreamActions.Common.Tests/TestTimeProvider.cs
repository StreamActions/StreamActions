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

namespace StreamActions.Common.Tests;

/// <summary>
/// A lightweight deterministic <see cref="TimeProvider"/> for unit tests.
/// </summary>
/// <param name="startTime">The initial UTC time, or <see langword="null"/> to default to 2026-01-01T00:00:00Z.</param>
internal sealed class TestTimeProvider(DateTimeOffset? startTime = null) : TimeProvider
{
    private readonly Lock _lock = new();
    private readonly List<TestTimer> _timers = [];
    private DateTimeOffset _utcNow = startTime ?? new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>
    /// Advances the current UTC time by <paramref name="delta"/> and fires any due timers.
    /// </summary>
    /// <param name="delta">The amount of time to advance.</param>
    public void Advance(TimeSpan delta)
    {
        List<TestTimer> dueTimers = [];
        lock (this._lock)
        {
            this._utcNow = this._utcNow.Add(delta);
            long nowTicks = this._utcNow.UtcTicks;
            foreach (TestTimer timer in this._timers)
            {
                if (!timer.Disposed && timer.DueTicks <= nowTicks)
                {
                    dueTimers.Add(timer);
                }
            }

            foreach (TestTimer timer in dueTimers)
            {
                timer.DueTicks = timer.Period == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : nowTicks + timer.Period.Ticks;
            }
        }

        foreach (TestTimer timer in dueTimers)
        {
            timer.Fire();
        }
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        TestTimer timer = new(this, callback, state, dueTime, period);
        lock (this._lock)
        {
            this._timers.Add(timer);
        }

        if (dueTime == TimeSpan.Zero)
        {
            timer.Fire();
        }

        return timer;
    }

    /// <inheritdoc/>
    public override long GetTimestamp()
    {
        lock (this._lock)
        {
            return this._utcNow.UtcTicks;
        }
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (this._lock)
        {
            return this._utcNow;
        }
    }

    private sealed class TestTimer(TestTimeProvider provider, TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
    {
        private readonly TimerCallback _callback = callback;
        private readonly TestTimeProvider _provider = provider;
        private readonly object? _state = state;

        public bool Disposed { get; private set; }

        public long DueTicks { get; set; } = dueTime == Timeout.InfiniteTimeSpan
                ? long.MaxValue
                : provider.GetUtcNow().UtcTicks + dueTime.Ticks;

        public TimeSpan Period { get; private set; } = period;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (this._provider._lock)
            {
                if (this.Disposed)
                {
                    return false;
                }

                this.Period = period;
                this.DueTicks = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : this._provider.GetUtcNow().UtcTicks + dueTime.Ticks;
                return true;
            }
        }

        public void Dispose()
        {
            lock (this._provider._lock)
            {
                this.Disposed = true;
                _ = this._provider._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            this.Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire() => this._callback(this._state);
    }
}
