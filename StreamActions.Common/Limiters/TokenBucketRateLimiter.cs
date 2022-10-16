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

using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;

namespace StreamActions.Common.Limiters;

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
    public TokenBucketRateLimiter(int limit, TimeSpan period)
    {
        this._rwl.EnterWriteLock();
        try
        {
            _ = Interlocked.Exchange(ref this._limit, Math.Max(limit, 1));
            _ = Interlocked.Exchange(ref this._remaining, this._limit);
            _ = Interlocked.Exchange(ref this._period, Math.Max(period.Ticks, 1));
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
        /// Number of seconds since the Unix epoch.
        /// </summary>
        Seconds,

        /// <summary>
        /// Number of milliseconds since the Unix epoch.
        /// </summary>
        Milliseconds,

        /// <summary>
        /// Number of .Net <see cref="TimeSpan.Ticks"/> since the Unix epoch.
        /// </summary>
        Ticks
    }

    #endregion Public Enums

    #region Public Properties

    /// <summary>
    /// Indicates if the bucket is full.
    /// </summary>
    public bool IsFull => this.GetRemaining() == this.GetLimit();

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
        string? limitS = headers?.GetValues(headerLimit).FirstOrDefault();
        string? remainingS = headers?.GetValues(headerRemaining).FirstOrDefault();
        string? resetS = headers?.GetValues(headerReset).FirstOrDefault();

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
                DateTime? dt = DateTime.Parse(resetS, CultureInfo.InvariantCulture);
                if (dt.HasValue)
                {
                    nextReset = dt.Value.Ticks;
                }
            }
            else if (long.TryParse(resetS, out long reset))
            {
                nextReset = headerResetType switch
                {
                    HeaderResetType.Seconds => TimeSpan.FromSeconds(reset).Ticks,
                    HeaderResetType.Milliseconds => TimeSpan.FromMilliseconds(reset).Ticks,
                    HeaderResetType.Ticks => reset,
                    HeaderResetType.ISO8601 => throw new InvalidOperationException(),
                    _ => throw new ArgumentOutOfRangeException(nameof(headerResetType)),
                };
            }
            else
            {
                // Allow initial value to pass, which will be a no-op
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
    /// <param name="period">The period.</param>
    /// <remarks>Fails silently if <paramref name="period"/> is less than 1.</remarks>
    public void UpdatePeriod(TimeSpan period) => this.UpdatePeriod(period.Ticks);

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
    /// Waits until a token is available in the bucket, then acquires it.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock and rate limit, or -1 milliseconds to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the wait.</param>
    /// <returns>A <see cref="Task"/> that can be awaited.</returns>
    /// <exception cref="TimeoutException">The lock timed out; unable to acquire a token within <paramref name="timeout"/>.</exception>
    /// <exception cref="OperationCanceledException">A cancellation was requested via <paramref name="cancellationToken"/> before a rate limit token could be acquired.</exception>
    public async Task WaitForRateLimit(TimeSpan timeout, CancellationToken? cancellationToken = null)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (true)
        {
            if (timer.Elapsed > timeout)
            {
                throw new TimeoutException("Timed out waiting for the rate limit.");
            }

            cancellationToken?.ThrowIfCancellationRequested();

            this.RefillBucket(timeout);
            if (this._rwl.TryEnterUpgradeableReadLock(timeout))
            {
                try
                {
                    if (this._remaining > 0)
                    {
                        if (this._rwl.TryEnterWriteLock(timeout))
                        {
                            try
                            {
                                if (Interlocked.Decrement(ref this._remaining) >= 0)
                                {
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
                        else
                        {
                            throw new TimeoutException("Timed out attempting to acquire write lock.");
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
                throw new TimeoutException("Timed out attempting to acquire upgradeable read lock.");
            }

            await Task.Delay(this.GetTimeUntilNextToken(timeout), cancellationToken.GetValueOrDefault()).ConfigureAwait(false);
        }
    }

    #endregion Public Methods

    #region Internal Methods

    /// <summary>
    /// Returns a token to the bucket.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
    /// <exception cref="TimeoutException">The lock timed out.</exception>
    internal void ReturnToken(TimeSpan timeout)
    {
        if (this._rwl.TryEnterUpgradeableReadLock(timeout))
        {
            try
            {
                if (this._rwl.TryEnterWriteLock(timeout))
                {
                    try
                    {
                        _ = Interlocked.Increment(ref this._remaining);
                    }
                    finally
                    {
                        this._rwl.ExitWriteLock();
                    }
                }
                else
                {
                    throw new TimeoutException("Timed out attempting to acquire write lock.");
                }
            }
            finally
            {
                this._rwl.ExitUpgradeableReadLock();
            }
        }
        else
        {
            throw new TimeoutException("Timed out attempting to acquire upgradeable read lock.");
        }
    }

    #endregion Internal Methods

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
    /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
    /// <returns>The time remaining until a new token is available in the bucket.</returns>
    /// <exception cref="TimeoutException">The lock timed out.</exception>
    private TimeSpan GetTimeUntilNextToken(TimeSpan timeout)
    {
        if (this._rwl.TryEnterReadLock(timeout))
        {
            try
            {
                return TimeSpan.FromTicks((long)Math.Ceiling((this._nextReset - this._period) / (float)this._limit));
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }
        else
        {
            throw new TimeoutException("Timed out attempting to acquire read lock.");
        }
    }

    /// <summary>
    /// Refills the bucket.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
    /// <exception cref="TimeoutException">The lock timed out.</exception>
    private void RefillBucket(TimeSpan timeout)
    {
        if (this._rwl.TryEnterReadLock(timeout))
        {
            try
            {
                if (this._remaining == this._limit)
                {
                    return;
                }
            }
            finally
            {
                this._rwl.ExitReadLock();
            }
        }
        else
        {
            throw new TimeoutException("Timed out attempting to acquire read lock.");
        }
        if (this._rwl.TryEnterUpgradeableReadLock(timeout))
        {
            try
            {
                if (DateTime.UtcNow.Ticks >= this._nextReset)
                {
                    if (this._rwl.TryEnterWriteLock(timeout))
                    {
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
                        throw new TimeoutException("Timed out attempting to acquire write lock.");
                    }
                }
                else
                {
                    long elapsed = DateTime.UtcNow.Subtract(TimeSpan.FromTicks(this._nextReset).Subtract(TimeSpan.FromTicks(this._period))).Ticks;
                    float percent = elapsed / (float)this._period;
                    int addedTokens = (int)Math.Floor(percent * this._limit);
                    if (this._rwl.TryEnterWriteLock(timeout))
                    {
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
                    else
                    {
                        throw new TimeoutException("Timed out attempting to acquire write lock.");
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
            throw new TimeoutException("Timed out attempting to acquire upgradeable read lock.");
        }
    }

    #endregion Private Methods
}
