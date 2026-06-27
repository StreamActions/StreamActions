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

using FluentAssertions;
using StreamActions.Common.Limiters;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StreamActions.Common.Tests.Limiters;

public sealed class TokenBucketRateLimiterTests
{
    #region WaitForRateLimit

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_AcquiresTokenImmediatelyWhenAvailable()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        // Wait should complete successfully, consuming the token
        limiter.Remaining.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ThrowsTimeoutException_WhenNoTokenAndTimeoutExceeded()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        Func<Task> act = async () => await limiter.WaitForRateLimit(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("Timed out waiting for the rate limit.").ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ThrowsOperationCanceledException_WhenCanceled()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync().ConfigureAwait(true);

        Func<Task> act = async () => await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(true);

        await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_WaitsForRefillAndAcquiresToken()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        limiter.Remaining.Should().Be(0);
    }

    #endregion WaitForRateLimit

    #region UpdatePeriod

    [Fact]
    [Trait("Member", "UpdatePeriod")]
    public async Task UpdatePeriod_UpdatesPeriodTicks_Successfully()
    {
        // 100ms
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        // Update period to 50ms
        limiter.UpdatePeriod(TimeSpan.FromMilliseconds(50).Ticks);

        limiter.Period.Should().Be(TimeSpan.FromMilliseconds(50).Ticks);

        // Consume the token, the next one should be available in 50ms instead of 100ms
        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        limiter.Remaining.Should().Be(0);

        long expectedNextReset = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks;
        // Check if next reset is around 50ms from now (allow a small margin for execution time)
        Math.Abs(limiter.NextResetDateTime.Ticks - expectedNextReset).Should().BeLessThan(TimeSpan.FromMilliseconds(500).Ticks);
    }

    [Fact]
    [Trait("Member", "UpdatePeriod")]
    public async Task UpdatePeriod_UpdatesPeriodTimeSpan_Successfully()
    {
        // 100ms
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        // Update period to 50ms
        limiter.UpdatePeriod(TimeSpan.FromMilliseconds(50));

        limiter.PeriodTimeSpan.Should().Be(TimeSpan.FromMilliseconds(50));

        // Consume the token, the next one should be available in 50ms instead of 100ms
        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        limiter.Remaining.Should().Be(0);

        long expectedNextReset = DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks;
        // Check if next reset is around 50ms from now (allow a small margin for execution time)
        Math.Abs(limiter.NextResetDateTime.Ticks - expectedNextReset).Should().BeLessThan(TimeSpan.FromMilliseconds(500).Ticks);
    }

    [Theory]
    [Trait("Member", "UpdatePeriod")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void UpdatePeriod_FailsSilentlyIfPeriodIsLessThanOne(long invalidPeriodTicks)
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdatePeriod(invalidPeriodTicks);

        limiter.Period.Should().Be(TimeSpan.FromMilliseconds(100).Ticks);
    }

    #endregion UpdatePeriod
}
