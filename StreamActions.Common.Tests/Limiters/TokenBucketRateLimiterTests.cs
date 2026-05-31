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

using System;
using System.Threading;
using System.Threading.Tasks;
using StreamActions.Common.Limiters;
using Xunit;
using FluentAssertions;

namespace StreamActions.Common.Tests.Limiters;

public sealed class TokenBucketRateLimiterTests
{
    #region WaitForRateLimit

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_AcquiresTokenImmediatelyWhenAvailable()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1));

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

        Func<Task> act = async () => await limiter.WaitForRateLimit(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("Timed out waiting for the rate limit.");
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ThrowsOperationCanceledException_WhenCanceled()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromSeconds(10));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = async () => await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(false);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_WaitsForRefillAndAcquiresToken()
    {
        using TokenBucketRateLimiter limiter = new(1, TimeSpan.FromMilliseconds(100));

        limiter.UpdateRemaining(0);
        limiter.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromMilliseconds(50).Ticks);

        await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1));

        limiter.Remaining.Should().Be(0);
    }

    #endregion WaitForRateLimit
}
