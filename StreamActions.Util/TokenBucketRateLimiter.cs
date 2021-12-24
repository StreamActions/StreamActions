/*
 * Copyright © 2019-2021 StreamActions Team
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

using System.Net;

namespace StreamActions.Util
{
    /// <summary>
    /// Handles Rate Limits using a Token Bucket which trickles back tokens over a set period.
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
            _ = Interlocked.Exchange(ref this._limit, Math.Max(limit, 1));
            _ = Interlocked.Exchange(ref this._remaining, this._limit);
            _ = Interlocked.Exchange(ref this._period, Math.Max(period, 1));
            _ = Interlocked.Exchange(ref this._nextReset, DateTime.UtcNow.Ticks + this._period);
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
        public int Limit => this._limit;

        /// <summary>
        /// The timestamp when entire bucket will be reset to full again, in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        public long NextReset => this._nextReset;

        /// <summary>
        /// The period over which the bucket normally refills from empty to full, in <see cref="TimeSpan.Ticks"/>.
        /// </summary>
        public long Period => this._period;

        /// <summary>
        /// The remaining limit in the bucket.
        /// </summary>
        public int Remaining => this._remaining;

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Parses response headers from a <see cref="HttpWebResponse"/> for Rate Limit information.
        /// </summary>
        /// <param name="headers">The response headers.</param>
        /// <param name="headerLimit">The name of the header denoting the current maximum number of tokens the bucket can hold.</param>
        /// <param name="headerRemaining">The name of the header denoting the current number of tokens remaining in the bucket.</param>
        /// <param name="headerReset">The name of the header denoting the timestamp when the bucket will fully refill to the limit.</param>
        /// <param name="headerResetType">Indicates the type of timestamp provided by the <paramref name="headerReset"/> header.</param>
        /// <exception cref="ArgumentOutOfRangeException">The value of <paramref name="headerResetType"/> is not one of the valid values of <see cref="HeaderResetType"/>.</exception>
        public void ParseHeaders(WebHeaderCollection headers, string headerLimit = "Ratelimit-Limit", string headerRemaining = "Ratelimit-Remaining",
            string headerReset = "Ratelimit-Reset", HeaderResetType headerResetType = HeaderResetType.Seconds)
        {
            string? limitS = headers.Get(headerLimit);
            string? remainingS = headers.Get(headerRemaining);
            string? resetS = headers.Get(headerReset);

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
            if (limit >= 1 && this._limit != limit)
            {
                _ = Interlocked.Exchange(ref this._limit, limit);

                if (limit < this._remaining)
                {
                    _ = Interlocked.Exchange(ref this._remaining, limit);
                }
            }
        }

        /// <summary>
        /// Changes the next time that the bucket will fully refill to the limit.
        /// </summary>
        /// <param name="nextReset">The next reset time, in <see cref="TimeSpan.Ticks"/></param>
        /// <remarks>Fails silently if <paramref name="nextReset"/> is earlier than <see cref="DateTime.UtcNow"/>.</remarks>
        public void UpdateNextReset(long nextReset)
        {
            if (nextReset >= DateTime.UtcNow.Ticks && this._nextReset != nextReset)
            {
                _ = Interlocked.Exchange(ref this._nextReset, nextReset);
            }
        }

        /// <summary>
        /// Changes the period over which the bucket normally refills from empty to full.
        /// </summary>
        /// <param name="period">The period, in <see cref="TimeSpan.Ticks"/>.</param>
        /// <remarks>Fails silently if <paramref name="period"/> is less than 1.</remarks>
        public void UpdatePeriod(long period)
        {
            if (period >= 1 && this._period != period)
            {
                _ = Interlocked.Exchange(ref this._period, period);
            }
        }

        /// <summary>
        /// Changes the number of tokens currently held by this bucket.
        /// </summary>
        /// <param name="remaining">The new token amount.</param>
        /// <remarks>Fails silently if <paramref name="remaining"/> is less than 0. Will set to <see cref="Limit"/> if <paramref name="remaining"/> exceeds it.</remarks>
        public void UpdateRemaining(int remaining)
        {
            remaining = Math.Min(remaining, this._limit);
            if (remaining >= 0 && this._remaining != remaining)
            {
                _ = Interlocked.Exchange(ref this._remaining, remaining);
            }
        }

        /// <summary>
        /// Waits until a token is available in the bucket, then aquires it.
        /// </summary>
        /// <returns>A <see cref="Task"/> that can be awaited.</returns>
        public async Task WaitForRateLimit()
        {
            this.RefillBucket();
            if (this._remaining > 0)
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

            await Task.Delay(this.GetTimeUntilNextToken()).ConfigureAwait(false);
            await this.WaitForRateLimit().ConfigureAwait(false);
        }

        #endregion Public Methods

        #region Private Fields

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
        /// Calculates when a new token should be available in the bucket.
        /// </summary>
        /// <returns>The time remaining until a new token is available in the bucket.</returns>
        private TimeSpan GetTimeUntilNextToken() => TimeSpan.FromTicks((long)Math.Ceiling((this._nextReset - this._period) / (float)this._limit));

        /// <summary>
        /// Refills the bucket.
        /// </summary>
        private void RefillBucket()
        {
            if (DateTime.UtcNow.Ticks >= this._nextReset)
            {
                _ = Interlocked.Exchange(ref this._remaining, this._limit);
                _ = Interlocked.Exchange(ref this._nextReset, DateTime.UtcNow.Ticks + this._period);
            }
            else
            {
                long elapsed = DateTime.UtcNow.Subtract(TimeSpan.FromTicks(this._nextReset).Subtract(TimeSpan.FromTicks(this._period))).Ticks;
                float percent = elapsed / (float)this._period;
                int addedTokens = (int)Math.Floor(percent * this._limit);
                if (Interlocked.Add(ref this._remaining, addedTokens) > this._limit)
                {
                    _ = Interlocked.Exchange(ref this._remaining, this._limit);
                }
            }
        }

        #endregion Private Methods
    }
}
