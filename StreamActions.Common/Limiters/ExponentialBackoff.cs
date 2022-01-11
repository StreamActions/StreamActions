/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace StreamActions.Common.Limiters
{
    /// <summary>
    /// Handles backoff timing using an exponentially increasing duration strategy.
    /// </summary>
    public class ExponentialBackoff
    {
        #region Public Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initialDuration">The initial duration of the backoff, after a reset.</param>
        /// <param name="maxDuration">The maximum allowed backoff duration.</param>
        public ExponentialBackoff(TimeSpan initialDuration, TimeSpan maxDuration)
        {
            this._maxDuration = maxDuration;
            this._initialDuration = initialDuration;

            this._rwl.EnterWriteLock();
            try
            {
                this._nextDuration = TimeSpan.FromTicks(initialDuration.Ticks);
            }
            finally
            {
                this._rwl.ExitWriteLock();
            }
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// The initial duration of the backoff, after a reset.
        /// </summary>
        public TimeSpan IntialDuration => TimeSpan.FromTicks(this._initialDuration.Ticks);

        /// <summary>
        /// The maximum allowed backoff duration.
        /// </summary>
        public TimeSpan MaxDuration => TimeSpan.FromTicks(this._maxDuration.Ticks);

        /// <summary>
        /// The duration of the next backoff.
        /// </summary>
        public TimeSpan NextDuration => TimeSpan.FromTicks(this._nextDuration.Ticks);

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Resets <see cref="NextDuration"/> to <see cref="InitialDuration"/>.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
        /// <exception cref="TimeoutException">The lock timed out.</exception>
        public void Reset(TimeSpan timeout)
        {
            if (this._rwl.TryEnterUpgradeableReadLock(timeout))
            {
                try
                {
                    if (this._nextDuration.CompareTo(this._initialDuration) != 0)
                    {
                        if (this._rwl.TryEnterWriteLock(timeout))
                        {
                            try
                            {
                                this._nextDuration = TimeSpan.FromTicks(this._initialDuration.Ticks);
                            }
                            finally
                            {
                                this._rwl.ExitWriteLock();
                            }
                        }
                        else
                        {
                            throw new TimeoutException();
                        }
                    }
                }
                finally
                {
                    this._rwl.ExitUpgradeableReadLock();
                }
            }
            else
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Waits for <see cref="NextDuration"/>, then updates it for the next wait call.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
        /// <returns>A <see cref="Task"/> that can be awaited.</returns>
        /// <exception cref="TimeoutException">The lock timed out.</exception>
        public async Task Wait(TimeSpan timeout)
        {
            if (this._rwl.TryEnterUpgradeableReadLock(timeout))
            {
                try
                {
                    await Task.Delay(this._nextDuration).ConfigureAwait(false);
                    if (this._nextDuration.CompareTo(this._maxDuration) < 0)
                    {
                        long ticks = this._nextDuration.Ticks * this._nextDuration.Ticks;

                        if (ticks > this._maxDuration.Ticks)
                        {
                            ticks = this._maxDuration.Ticks;
                        }

                        if (this._rwl.TryEnterWriteLock(timeout))
                        {
                            try
                            {
                                this._nextDuration = TimeSpan.FromTicks(ticks);
                            }
                            finally
                            {
                                this._rwl.ExitWriteLock();
                            }
                        }
                        else
                        {
                            throw new TimeoutException();
                        }
                    }
                }
                finally
                {
                    this._rwl.ExitUpgradeableReadLock();
                }
            }
            else
            {
                throw new TimeoutException();
            }
        }

        #endregion Public Methods

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
        /// Lock for controlling Read/Write access to the variables.
        /// </summary>
        private readonly ReaderWriterLockSlim _rwl = new();

        /// <summary>
        /// The duration of the next backoff.
        /// </summary>
        private TimeSpan _nextDuration;

        #endregion Private Fields
    }
}
