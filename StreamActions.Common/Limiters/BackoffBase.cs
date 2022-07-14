/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

namespace StreamActions.Common.Limiters
{
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
        /// Indicates if the current value of <see cref="NextDuration"/> is equal to <see cref="InitialDuration"/>.
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
        /// Resets <see cref="NextDuration"/> to <see cref="InitialDuration"/>.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
        /// <exception cref="TimeoutException">The lock timed out.</exception>
        public void Reset(TimeSpan timeout)
        {
            if (this.Rwl.TryEnterUpgradeableReadLock(timeout))
            {
                try
                {
                    if (this._nextDuration.CompareTo(this._initialDuration) != 0)
                    {
                        if (this.Rwl.TryEnterWriteLock(timeout))
                        {
                            try
                            {
                                this._nextDuration = TimeSpan.FromTicks(this._initialDuration.Ticks);
                            }
                            finally
                            {
                                this.Rwl.ExitWriteLock();
                            }
                        }
                        else
                        {
                            throw new TimeoutException("Timed out attempting to acquire write lock.");
                        }
                    }
                }
                finally
                {
                    this.Rwl.ExitUpgradeableReadLock();
                }
            }
            else
            {
                throw new TimeoutException("Timed out attempting to acquire upgradeable read lock.");
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
            if (this.Rwl.TryEnterUpgradeableReadLock(timeout))
            {
                try
                {
                    await Task.Delay(this._nextDuration, cancellationToken.GetValueOrDefault()).ConfigureAwait(false);
                    if (this._nextDuration.CompareTo(this._maxDuration) < 0)
                    {
                        long ticks = this.CalcNextDurationTicks();

                        if (ticks > this._maxDuration.Ticks)
                        {
                            ticks = this._maxDuration.Ticks;
                        }

                        if (this.Rwl.TryEnterWriteLock(timeout))
                        {
                            try
                            {
                                this._nextDuration = TimeSpan.FromTicks(ticks);
                            }
                            finally
                            {
                                this.Rwl.ExitWriteLock();
                            }
                        }
                        else
                        {
                            throw new TimeoutException("Timed out attempting to acquire write lock.");
                        }
                    }
                }
                finally
                {
                    this.Rwl.ExitUpgradeableReadLock();
                }
            }
            else
            {
                throw new TimeoutException("Timed out attempting to acquire upgradeable read lock.");
            }
        }

        #endregion Public Methods

        #region Protected Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initialDuration">The initial duration of the backoff, after a reset.</param>
        /// <param name="maxDuration">The maximum allowed backoff duration.</param>
        protected BackoffBase(TimeSpan initialDuration, TimeSpan maxDuration)
        {
            this._maxDuration = maxDuration;
            this._initialDuration = initialDuration;

            this.Rwl.EnterWriteLock();
            try
            {
                this._nextDuration = TimeSpan.FromTicks(initialDuration.Ticks);
            }
            finally
            {
                this.Rwl.ExitWriteLock();
            }
        }

        #endregion Protected Constructors

        #region Protected Properties

        /// <summary>
        /// Lock for controlling Read/Write access to the variables.
        /// </summary>
        protected ReaderWriterLockSlim Rwl { get; } = new();

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
            this.Rwl.EnterReadLock();
            try
            {
                return TimeSpan.FromTicks(this._nextDuration.Ticks);
            }
            finally
            {
                this.Rwl.ExitReadLock();
            }
        }

        #endregion Private Methods
    }
}
