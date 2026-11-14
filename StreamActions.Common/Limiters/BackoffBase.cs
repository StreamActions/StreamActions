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

namespace StreamActions.Common.Limiters;

/// <summary>
/// Base class for backoff types.
/// </summary>
public abstract class BackoffBase
{
    #region Public Properties

    /// <summary>
    /// The initial duration of the backoff, after a reset.
    /// </summary>
    public TimeSpan IntialDuration => TimeSpan.FromTicks(this._initialDuration.Ticks);

    /// <summary>
    /// Indicates if the current value of <see cref="NextDuration"/> is equal to <see cref="MaxDuration"/>.
    /// </summary>
    public bool IsMaxDuration => this.NextDuration.Ticks == this._maxDuration.Ticks;

    /// <summary>
    /// Indicates if the current value of <see cref="NextDuration"/> is equal to <see cref="IntialDuration"/>.
    /// </summary>
    public bool IsReset => this.NextDuration.Ticks == this._initialDuration.Ticks;

    /// <summary>
    /// The maximum allowed backoff duration.
    /// </summary>
    public TimeSpan MaxDuration => TimeSpan.FromTicks(this._maxDuration.Ticks);

    /// <summary>
    /// The duration of the next backoff.
    /// </summary>
    public TimeSpan NextDuration => this.GetNextDuration();

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// Resets <see cref="NextDuration"/> to <see cref="IntialDuration"/>.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
    /// <exception cref="TimeoutException">The lock timed out.</exception>
    public void Reset(TimeSpan timeout)
    {
        if (this.Lock.TryEnter(timeout))
        {
            try
            {
                if (this._nextDuration.CompareTo(this._initialDuration) != 0)
                {
                    this._nextDuration = TimeSpan.FromTicks(this._initialDuration.Ticks);
                }
            }
            finally
            {
                this.Lock.Exit();
            }
        }
        else
        {
            throw new TimeoutException("Timed out attempting to acquire lock.");
        }
    }

    /// <summary>
    /// Waits for <see cref="NextDuration"/>, then updates it for the next wait call.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the wait.</param>
    /// <returns>A <see cref="Task"/> that can be awaited.</returns>
    /// <exception cref="TimeoutException">The lock timed out.</exception>
    /// <exception cref="OperationCanceledException">A cancellation was requested via <paramref name="cancellationToken"/> before a rate limit token could be acquired.</exception>
    public async Task Wait(TimeSpan timeout, CancellationToken? cancellationToken = null)
    {
        TimeSpan delayDuration;

        if (this.Lock.TryEnter(timeout))
        {
            try
            {
                delayDuration = this._nextDuration;

                if (this._nextDuration.CompareTo(this._maxDuration) < 0)
                {
                    long ticks = this.CalcNextDurationTicks();

                    if (ticks > this._maxDuration.Ticks)
                    {
                        ticks = this._maxDuration.Ticks;
                    }

                    this._nextDuration = TimeSpan.FromTicks(ticks);
                }
            }
            finally
            {
                this.Lock.Exit();
            }
        }
        else
        {
            throw new TimeoutException("Timed out attempting to acquire lock.");
        }

        await Task.Delay(delayDuration, this._timeProvider, cancellationToken.GetValueOrDefault()).ConfigureAwait(false);
    }

    #endregion Public Methods

    #region Protected Constructors

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="initialDuration">The initial duration of the backoff, after a reset.</param>
    /// <param name="maxDuration">The maximum allowed backoff duration.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> used for delay operations, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
    protected BackoffBase(TimeSpan initialDuration, TimeSpan maxDuration, TimeProvider? timeProvider = null)
    {
        this._maxDuration = maxDuration;
        this._initialDuration = initialDuration;
        this._timeProvider = timeProvider ?? TimeProvider.System;

        lock (this.Lock)
        {
            this._nextDuration = TimeSpan.FromTicks(initialDuration.Ticks);
        }
    }

    #endregion Protected Constructors

    #region Protected Properties

    /// <summary>
    /// Lock for controlling Read/Write access to the variables.
    /// </summary>
    protected Lock Lock { get; } = new();

    #endregion Protected Properties

    #region Protected Methods

    /// <summary>
    /// Calculates the next value for <see cref="NextDuration"/>, in ticks.
    /// </summary>
    /// <returns>The next value that will be set for <see cref="NextDuration"/>, in ticks.</returns>
    protected abstract long CalcNextDurationTicks();

    #endregion Protected Methods

    #region Private Fields

    /// <summary>
    /// The initial duration of the backoff, after a reset.
    /// </summary>
    private readonly TimeSpan _initialDuration;

    /// <summary>
    /// The maximum allowed backoff duration.
    /// </summary>
    private readonly TimeSpan _maxDuration;

    /// <summary>
    /// The <see cref="TimeProvider"/> used for delay operations.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The duration of the next backoff.
    /// </summary>
    private TimeSpan _nextDuration;

    #endregion Private Fields

    #region Private Methods

    /// <summary>
    /// Thread-safe getter for <see cref="NextDuration"/>.
    /// </summary>
    /// <returns>The next duration.</returns>
    private TimeSpan GetNextDuration()
    {
        lock (this.Lock)
        {
            return TimeSpan.FromTicks(this._nextDuration.Ticks);
        }
    }

    #endregion Private Methods
}
