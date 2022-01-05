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

using System.Net.Http.Headers;

namespace StreamActions.Common.Limiters
{
    /// <summary>
    /// Handles rate limits using a token bucket which trickles back tokens over a set period.
    /// </summary>
    public class TokenBucketRateLimiter
    {
        #region Public Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="limit">The maximum number of tokens the bucket can hold.</param>
        /// <param name="period">The period over which tokens normally refill from 0 to <paramref name="limit"/>.</param>
        public TokenBucketRateLimiter(int limit, long period)
        {
            this._rwl.EnterWriteLock();
            try
            {
                _ = Interlocked.Exchange(ref this._limit, Math.Max(limit, 1));
                _ = Interlocked.Exchange(ref this._remaining, this._limit);
                _ = Interlocked.Exchange(ref this._period, Math.Max(period, 1));
                _ = Interlocked.Exchange(ref this._nextReset, DateTime.UtcNow.Ticks + this._period);
            }
            finally
            {
                this._rwl.ExitWriteLock();
            }
        }

        #endregion Public Constructors

        #region Public Enums

        /// <summary>
        /// Represents the possible DateTime types of <i>headerReset</i> in <see cref="ParseHeaders(WebHeaderCollection, string, string, string)"/>.
        /// </summary>
        public enum HeaderResetType
        {
            /// <summary>
            /// An ISO8601-formatted timestamp. Supports optional dashes (<c>-</c>) between date fields, optional colons (<c>:</c>) between time fields, and fractional seconds to 3 decimal places.
            /// </summary>
            ISO8601,

            /// <summary>
            /// Number of seconds since the unix epoch.
            /// </summary>
            Seconds,

            /// <summary>
            /// Number of milliseconds since the unix epoch.
            /// </summary>
            Milliseconds,

            /// <summary>
            /// Number of .Net <see cref="TimeSpan.Ticks"/> since the unix epoch.
            /// </summary>
            Ticks
        }

        #endregion Public Enums

        #region Public Properties

        /// <summary>
        /// The maximum number of tokens that can be in the bucket.
        /// </summary>
        public int Limit => this.GetLimit();

        /// <summary>
        /// The timestamp when entire bucket will be reset to full again, in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        public long NextReset => this.GetNextReset();

        /// <summary>
        /// The timestamp when entire bucket will be reset to full again, as a <see cref="DateTime"/>.
        /// </summary>
        public DateTime NextResetDateTime => new(this.GetNextReset());

        /// <summary>
        /// The period over which the bucket normally refills from empty to full, in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        public long Period => this.GetPeriod();

        /// <summary>
        /// The period over which the bucket normally refills from empty to full, as a <see cref="TimeSpan"/>.
        /// </summary>
        public TimeSpan PeriodTimeSpan => TimeSpan.FromTicks(this.GetPeriod());

        /// <summary>
        /// The remaining limit in the bucket.
        /// </summary>
        public int Remaining => this.GetRemaining();

        /// <summary>
        /// The number of tasks waiting for a token.
        /// </summary>
        public int Waiters => this.GetWaiters();

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Parses response headers from a <see cref="HttpResponseMessage"/> for Rate Limit information.
        /// </summary>
        /// <param name="headers">The response headers.</param>
        /// <param name="headerLimit">The name of the header denoting the current maximum number of tokens the bucket can hold.</param>
        /// <param name="headerRemaining">The name of the header denoting the current number of tokens remaining in the bucket.</param>
        /// <param name="headerReset">The name of the header denoting the timestamp when the bucket will fully refill to the limit.</param>
        /// <param name="headerResetType">Indicates the type of timestamp provided by the <paramref name="headerReset"/> header.</param>
        /// <exception cref="ArgumentOutOfRangeException">The value of <paramref name="headerResetType"/> is not one of the valid values of <see cref="HeaderResetType"/>.</exception>
        public void ParseHeaders(HttpResponseHeaders headers, string headerLimit = "Ratelimit-Limit", string headerRemaining = "Ratelimit-Remaining",
        string headerReset = "Ratelimit-Reset", HeaderResetType headerResetType = HeaderResetType.Seconds)
        {
            string? limitS = headers.GetValues(headerLimit).FirstOrDefault();
            string? remainingS = headers.GetValues(headerRemaining).FirstOrDefault();
            string? resetS = headers.GetValues(headerReset).FirstOrDefault();

            if (limitS is not null)
            {
                if (int.TryParse(limitS, out int limit))
                {
                    this.UpdateLimit(limit);
                }
            }

            if (remainingS is not null)
            {
                if (int.TryParse(remainingS, out int remaining))
                {
                    this.UpdateRemaining(remaining);
                }
            }

            if (resetS is not null)
            {
                long nextReset = -1;
                if (headerResetType == HeaderResetType.ISO8601)
                {
                    if (DateTime.TryParseExact(resetS, "yyyyMMddTHH:mm:ss.FFFZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime resetDt1))
                    {
                        nextReset = resetDt1.Ticks;
                    }
                    else if (DateTime.TryParseExact(resetS, "yyyyMMddTHHmmss.FFFZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime resetDt2))
                    {
                        nextReset = resetDt2.Ticks;
                    }
                    else if (DateTime.TryParseExact(resetS, "yyyy-MM-ddTHH:mm:ss.FFFZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime resetDt3))
                    {
                        nextReset = resetDt3.Ticks;
                    }
                    else if (DateTime.TryParseExact(resetS, "yyyy-MM-ddTHHmmss.FFFZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime resetDt4))
                    {
                        nextReset = resetDt4.Ticks;
                    }
                }
                else if (long.TryParse(resetS, out long reset))
                {
                    nextReset = headerResetType switch
                    {
                        HeaderResetType.Seconds => TimeSpan.FromSeconds(reset).Ticks,
                        HeaderResetType.Milliseconds => TimeSpan.FromMilliseconds(reset).Ticks,
                        HeaderResetType.Ticks => reset,
                        _ => throw new ArgumentOutOfRangeException(nameof(headerResetType)),
                    };
                }
                this.UpdateNextReset(nextReset);
            }
        }

        /// <summary>
        /// Changes the maximum number of tokens that can be held by this bucket.
        /// </summary>
        /// <param name="limit">The new token limit.</param>
        /// <remarks>Fails silently if <paramref name="limit"/> is less than 1. Updates <see cref="Remaining"/> if it is higher than the new limit.</remarks>
        public void UpdateLimit(int limit)
        {
            this._rwl.EnterUpgradeableReadLock();
            try
            {
                if (limit >= 1 && this._limit != limit)
                {
                    this._rwl.EnterWriteLock();
                    try
                    {
                        _ = Interlocked.Exchange(ref this._limit, limit);

                        if (limit < this._remaining)
                        {
                            _ = Interlocked.Exchange(ref this._remaining, limit);
                        }
                    }
                    finally
                    {
                        this._rwl.ExitWriteLock();
                    }
                }
            }
            finally
            {
                this._rwl.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Changes the next time that the bucket will fully refill to the limit.
        /// </summary>
        /// <param name="nextReset">The next reset time, in <see cref="TimeSpan.Ticks"/></param>
        /// <remarks>Fails silently if <paramref name="nextReset"/> is earlier than <see cref="DateTime.UtcNow"/>.</remarks>
        public void UpdateNextReset(long nextReset)
        {
            this._rwl.EnterUpgradeableReadLock();
            try
            {
                if (nextReset >= DateTime.UtcNow.Ticks && this._nextReset != nextReset)
                {
                    this._rwl.EnterWriteLock();
                    try
                    {
                        _ = Interlocked.Exchange(ref this._nextReset, nextReset);
                    }
                    finally
                    {
                        this._rwl.ExitWriteLock();
                    }
                }
            }
            finally
            {
                this._rwl.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Changes the period over which the bucket normally refills from empty to full.
        /// </summary>
        /// <param name="period">The period, in <see cref="TimeSpan.Ticks"/>.</param>
        /// <remarks>Fails silently if <paramref name="period"/> is less than 1.</remarks>
        public void UpdatePeriod(long period)
        {
            this._rwl.EnterUpgradeableReadLock();
            try
            {
                if (period >= 1 && this._period != period)
                {
                    this._rwl.EnterWriteLock();
                    try
                    {
                        _ = Interlocked.Exchange(ref this._period, period);
                    }
                    finally
                    {
                        this._rwl.ExitWriteLock();
                    }
                }
            }
            finally
            {
                this._rwl.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Changes the number of tokens currently held by this bucket.
        /// </summary>
        /// <param name="remaining">The new token amount.</param>
        /// <remarks>Fails silently if <paramref name="remaining"/> is less than 0. Will set to <see cref="Limit"/> if <paramref name="remaining"/> exceeds it.</remarks>
        public void UpdateRemaining(int remaining)
        {
            this._rwl.EnterUpgradeableReadLock();
            try
            {
                remaining = Math.Min(remaining, this._limit);
                if (remaining >= 0 && this._remaining != remaining)
                {
                    this._rwl.EnterWriteLock();
                    try
                    {
                        _ = Interlocked.Exchange(ref this._remaining, remaining);
                    }
                    finally
                    {
                        this._rwl.ExitWriteLock();
                    }
                }
            }
            finally
            {
                this._rwl.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Waits until a token is available in the bucket, then aquires it.
        /// </summary>
        /// <returns>A <see cref="Task"/> that can be awaited.</returns>
        public async Task WaitForRateLimit()
        {
            this._rwl.EnterWriteLock();
            try
            {
                _ = Interlocked.Increment(ref this._waiters);
            }
            finally
            {
                this._rwl.ExitWriteLock();
            }

            while (true)
            {
                this.RefillBucket();
                this._rwl.EnterUpgradeableReadLock();
                try
                {
                    if (this._remaining > 0)
                    {
                        this._rwl.EnterWriteLock();
                        try
                        {
                            if (Interlocked.Decrement(ref this._remaining) >= 0)
                            {
                                _ = Interlocked.Decrement(ref this._waiters);
                                return;
                            }
                            else
                            {
                                _ = Interlocked.Exchange(ref this._remaining, 0);
                            }
                        }
                        finally
                        {
                            this._rwl.ExitWriteLock();
                        }
                    }
                }
                finally
                {
                    this._rwl.ExitUpgradeableReadLock();
                }

                await Task.Delay(this.GetTimeUntilNextToken()).ConfigureAwait(false);
            }
        }

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Lock for controlling Read/Write access to the variables.
        /// </summary>
        private readonly ReaderWriterLockSlim _rwl = new();

        /// <summary>
        /// The maximum number of tokens that can be in the bucket.
        /// </summary>
        private int _limit;

        /// <summary>
        /// The timestamp when entire bucket will be reset to full again, in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        private long _nextReset;

        /// <summary>
        /// The period over which the bucket normally refills from empty to full, in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        private long _period;

        /// <summary>
        /// The remaining limit in the bucket.
        /// </summary>
        private int _remaining = 1;

        /// <summary>
        /// The number of tasks waiting for a token.
        /// </summary>
        private int _waiters = 0;

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Thread-safe getter for <see cref="Limit"/>.
        /// </summary>
        /// <returns>The current limit.</returns>
        private int GetLimit()
        {
            this._rwl.EnterReadLock();
            try
            {
                return this._limit;
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }

        /// <summary>
        /// Thread-safe getter for <see cref="NextReset"/> and <see cref="NextResetDateTime"/>.
        /// </summary>
        /// <returns>The next reset.</returns>
        private long GetNextReset()
        {
            this._rwl.EnterReadLock();
            try
            {
                return this._nextReset;
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }

        /// <summary>
        /// Thread-safe getter for <see cref="Period"/> and <see cref="PeriodTimeSpan"/>.
        /// </summary>
        /// <returns>The current period.</returns>
        private long GetPeriod()
        {
            this._rwl.EnterReadLock();
            try
            {
                return this._period;
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }

        /// <summary>
        /// Thread-safe getter for <see cref="Remaining"/>.
        /// </summary>
        /// <returns>The current remaining tokens.</returns>
        private int GetRemaining()
        {
            this._rwl.EnterReadLock();
            try
            {
                return this._remaining;
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }

        /// <summary>
        /// Calculates when a new token should be available in the bucket.
        /// </summary>
        /// <returns>The time remaining until a new token is available in the bucket.</returns>
        private TimeSpan GetTimeUntilNextToken()
        {
            this._rwl.EnterReadLock();
            try
            {
                return TimeSpan.FromTicks((long)Math.Ceiling((this._nextReset - this._period) / (float)this._limit));
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }

        /// <summary>
        /// Thread-safe getter for <see cref="Waiters"/>.
        /// </summary>
        /// <returns>The current number of waiters.</returns>
        private int GetWaiters()
        {
            this._rwl.EnterReadLock();
            try
            {
                return this._waiters;
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }

        /// <summary>
        /// Refills the bucket.
        /// </summary>
        private void RefillBucket()
        {
            if (this._remaining == this._limit)
            {
                return;
            }
            this._rwl.EnterUpgradeableReadLock();
            try
            {
                if (DateTime.UtcNow.Ticks >= this._nextReset)
                {
                    this._rwl.EnterWriteLock();
                    try
                    {
                        _ = Interlocked.Exchange(ref this._remaining, this._limit);
                        _ = Interlocked.Exchange(ref this._nextReset, DateTime.UtcNow.Ticks + this._period);
                    }
                    finally
                    {
                        this._rwl.ExitWriteLock();
                    }
                }
                else
                {
                    long elapsed = DateTime.UtcNow.Subtract(TimeSpan.FromTicks(this._nextReset).Subtract(TimeSpan.FromTicks(this._period))).Ticks;
                    float percent = elapsed / (float)this._period;
                    int addedTokens = (int)Math.Floor(percent * this._limit);
                    this._rwl.EnterWriteLock();
                    try
                    {
                        if (Interlocked.Add(ref this._remaining, addedTokens) > this._limit)
                        {
                            _ = Interlocked.Exchange(ref this._remaining, this._limit);
                        }
                    }
                    finally
                    {
                        this._rwl.ExitWriteLock();
                    }
                }
            }
            finally
            {
                this._rwl.ExitUpgradeableReadLock();
            }
        }

        #endregion Private Methods
    }
}
