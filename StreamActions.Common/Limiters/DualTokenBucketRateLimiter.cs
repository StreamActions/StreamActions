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
        /// The local <see cref="TokenBucketRateLimiter"/>.
        /// </summary>
        public TokenBucketRateLimiter LocalBucket { get; init; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Waits until a token is available in each bucket, then acquires them. If either token is not acquired, the other one is returned to its bucket.
        /// </summary>
        /// <param name="timeout">The interval to wait for the lock, or -1 milliseconds to wait indefinitely.</param>
        /// <returns><c>true</c> if both tokens were acquired; <c>false</c> otherwise.</returns>
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
            }

            bool gotLt = false;
            try
            {
                await lt.ConfigureAwait(false);
                gotLt = true;
            }
            catch (TimeoutException)
            {
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
                    }
                }
            }

            return gotGt && gotLt;
        }

        #endregion Public Methods
    }
}
