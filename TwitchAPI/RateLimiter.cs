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

namespace TwitchAPI
{
    /// <summary>
    /// Handles Rate Limits.
    /// </summary>
    internal class RateLimiter
    {
        #region Internal Methods

        /// <summary>
        /// Parses response headers for Rate Limit information.
        /// </summary>
        /// <param name="headers">The response headers.</param>
        internal void ParseHeaders(WebHeaderCollection headers)
        {
            lock (this._lock)
            {
                string? limitS = headers.Get("Ratelimit-Limit");

                if (limitS is not null)
                {
                    _ = int.TryParse(limitS, out this._limit);
                }

                string? remainingS = headers.Get("Ratelimit-Remaining");

                if (remainingS is not null)
                {
                    _ = int.TryParse(remainingS, out this._remaining);
                }

                string? resetS = headers.Get("Ratelimit-Reset");

                if (resetS is not null)
                {
                    if (long.TryParse(resetS, out long reset))
                    {
                        this._reset = DateTime.UnixEpoch.AddSeconds(reset);
                    }
                }
            }
        }

        /// <summary>
        /// Waits until a token is available in the bucket, then aquires it.
        /// </summary>
        /// <returns>A <see cref="Task"/> that can be awaited.</returns>
        internal async Task WaitForRateLimit()
        {
            lock (this._lock)
            {
                this.RefillBucket();
                if (this._remaining > 0)
                {
                    this._remaining--;
                    return;
                }
            }

            await Task.Delay(this.GetTimeUntilNextToken()).ConfigureAwait(false);
            await this.WaitForRateLimit().ConfigureAwait(false);
        }

        #endregion Internal Methods

        #region Private Fields

        /// <summary>
        /// The period over which the bucket normally refills from empty to full, in seconds.
        /// </summary>
        private static readonly int _period = 60;

        /// <summary>
        /// Lock object for aquring a token from the bucket.
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// The last reported limit, when the bucket is full.
        /// </summary>
        private int _limit = 1;

        /// <summary>
        /// The remaining limit in the bucket.
        /// </summary>
        private int _remaining = 1;

        /// <summary>
        /// When the entire bucket will be full again if no more requests are made.
        /// </summary>
        private DateTime _reset = DateTime.UtcNow;

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Calculates when a new token should be available in the bucket.
        /// </summary>
        /// <returns>The time remaining until a new token is available in the bucket.</returns>
        private TimeSpan GetTimeUntilNextToken() => this._reset.Subtract(this._reset.Subtract(TimeSpan.FromSeconds(_period))).Divide(this._limit);

        /// <summary>
        /// Refills the bucket.
        /// </summary>
        private void RefillBucket()
        {
            lock (this._lock)
            {
                int replenishedTokens = (int)Math.Floor(DateTime.UtcNow.Subtract(this._reset.Subtract(TimeSpan.FromSeconds(_period))).TotalSeconds * (this._limit / _period));
                this._remaining += Math.Min(replenishedTokens, this._limit);
            }
        }

        #endregion Private Methods
    }
}
