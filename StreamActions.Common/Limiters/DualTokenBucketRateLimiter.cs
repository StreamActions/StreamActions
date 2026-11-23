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

using System.Runtime.ExceptionServices;

namespace StreamActions.Common.Limiters;

/// <summary>
/// Handles rate limits using dual token buckets, where a token must be acquired from both a local bucket and a shared global bucket to succeed.
/// </summary>
/// <remarks>
/// Constructor.
/// </remarks>
/// <param name="localBucket">The local <see cref="TokenBucketRateLimiter"/>.</param>
/// <param name="globalBucket">The shared global <see cref="TokenBucketRateLimiter"/>.</param>
/// <exception cref="ArgumentNullException"><paramref name="localBucket"/> or <paramref name="globalBucket"/> is <see langword="null"/>.</exception>
public sealed class DualTokenBucketRateLimiter(TokenBucketRateLimiter localBucket, TokenBucketRateLimiter globalBucket)
{
    #region Public Properties

    /// <summary>
    /// The shared global <see cref="TokenBucketRateLimiter"/>.
    /// </summary>
    public TokenBucketRateLimiter GlobalBucket { get; init; } = globalBucket ?? throw new ArgumentNullException(nameof(globalBucket));

    /// <summary>
    /// Indicates if both <see cref="GlobalBucket"/> and <see cref="LocalBucket"/> are full.
    /// </summary>
    public bool IsBothFull => this.GlobalBucket.IsFull && this.LocalBucket.IsFull;

    /// <summary>
    /// The local <see cref="TokenBucketRateLimiter"/>.
    /// </summary>
    public TokenBucketRateLimiter LocalBucket { get; init; } = localBucket ?? throw new ArgumentNullException(nameof(localBucket));

    #endregion Public Properties

    #region Public Methods

    /// <summary>
    /// Waits until a token is available in both buckets, then acquires them. If either token is not acquired, the other one is returned to its bucket.
    /// </summary>
    /// <param name="timeout">The interval to wait for the lock and rate limit, or -1 milliseconds to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the wait.</param>
    /// <returns><see langword="true"/> if both tokens were acquired; <see langword="false"/> otherwise.</returns>
    /// <exception cref="OperationCanceledException">A cancellation was requested via <paramref name="cancellationToken"/> before both rate limit tokens could be acquired.</exception>
    public async Task<bool> WaitForRateLimit(TimeSpan timeout, CancellationToken? cancellationToken = null)
    {
        Task gt = this.GlobalBucket.WaitForRateLimit(timeout, cancellationToken);
        Task lt = this.LocalBucket.WaitForRateLimit(timeout, cancellationToken);

        bool gotGt = false;
        OperationCanceledException? canceledException = null;
        try
        {
            await gt.ConfigureAwait(false);
            gotGt = true;
        }
        catch (TimeoutException)
        {
            // Don't need to process exception
        }
        catch (OperationCanceledException ex)
        {
            canceledException = ex;
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
        catch (OperationCanceledException ex)
        {
            canceledException = ex;
        }

        if (gotGt != gotLt)
        {
            TimeSpan returnTimeout = timeout == Timeout.InfiniteTimeSpan || timeout > TimeSpan.FromSeconds(5)
                ? timeout
                : TimeSpan.FromSeconds(5);

            if (gotGt)
            {
                try
                {
                    this.GlobalBucket.ReturnToken(returnTimeout);
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
                    this.LocalBucket.ReturnToken(returnTimeout);
                }
                catch (TimeoutException)
                {
                    // Don't need to process exception
                }
            }
        }

        if (canceledException is not null)
        {
            ExceptionDispatchInfo.Throw(canceledException);
        }

        return gotGt && gotLt;
    }

    #endregion Public Methods
}
