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
    /// Handles rate limits using dual token buckets, where a token must be acquired from both a local bucket and a shared global bucket to succeed.
    /// </summary>
    public class DualTokenBucketRateLimiter
    {
        #region Public Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="localBucket">The local <see cref="TokenBucketRateLimiter"/>.</param>
        /// <param name="globalBucket">The shared global <see cref="TokenBucketRateLimiter"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="localBucket"/> or <paramref name="globalBucket"/> is null.</exception>
        public DualTokenBucketRateLimiter(TokenBucketRateLimiter localBucket, TokenBucketRateLimiter globalBucket)
        {
            this.LocalBucket = localBucket ?? throw new ArgumentNullException(nameof(localBucket));
            this.GlobalBucket = globalBucket ?? throw new ArgumentNullException(nameof(globalBucket));
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// The shared global <see cref="TokenBucketRateLimiter"/>.
        /// </summary>
        public TokenBucketRateLimiter GlobalBucket { get; init; }

        /// <summary>
        /// Indicates if both <see cref="GlobalBucket"/> and <see cref="LocalBucket"/> are full.
        /// </summary>
        public bool IsBothFull => this.GlobalBucket.IsFull && this.LocalBucket.IsFull;

        /// <summary>
        /// The local <see cref="TokenBucketRateLimiter"/>.
        /// </summary>
        public TokenBucketRateLimiter LocalBucket { get; init; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Waits until a token is available in both buckets, then acquires them. If either token is not acquired, the other one is returned to its bucket.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
        /// <returns><see langword="true"/> if both tokens were acquired; <see langword="false"/> otherwise.</returns>
        public async Task<bool> WaitForRateLimit(TimeSpan timeout)
        {
            Task gt = this.GlobalBucket.WaitForRateLimit(timeout);
            Task lt = this.LocalBucket.WaitForRateLimit(timeout);

            bool gotGt = false;
            try
            {
                await gt.ConfigureAwait(false);
                gotGt = true;
            }
            catch (TimeoutException)
            {
                // Don't need to process exception
            }

            bool gotLt = false;
            try
            {
                await lt.ConfigureAwait(false);
                gotLt = true;
            }
            catch (TimeoutException)
            {
                // Don't need to process exception
            }

            if (gotGt != gotLt)
            {
                if (gotGt)
                {
                    try
                    {
                        this.GlobalBucket.ReturnToken(timeout);
                    }
                    catch (TimeoutException)
                    {
                        // Don't need to process exception
                    }
                }

                if (gotLt)
                {
                    try
                    {
                        this.LocalBucket.ReturnToken(timeout);
                    }
                    catch (TimeoutException)
                    {
                        // Don't need to process exception
                    }
                }
            }

            return gotGt && gotLt;
        }

        #endregion Public Methods
    }
}
