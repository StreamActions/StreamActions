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

public sealed class DualTokenBucketRateLimiterTests
{
    #region Constructor

    [Fact]
    [Trait("Member", "Constructor")]
    public void Constructor_ThrowsArgumentNullException_WhenLocalBucketIsNull()
    {
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(1));

        Action act = () => _ = new DualTokenBucketRateLimiter(null!, globalBucket);

        act.Should().Throw<ArgumentNullException>().WithParameterName("localBucket");
    }

    [Fact]
    [Trait("Member", "Constructor")]
    public void Constructor_ThrowsArgumentNullException_WhenGlobalBucketIsNull()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(1));

        Action act = () => _ = new DualTokenBucketRateLimiter(localBucket, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("globalBucket");
    }

    [Fact]
    [Trait("Member", "Constructor")]
    public void Constructor_InitializesProperties()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(1));
        using TokenBucketRateLimiter globalBucket = new(2, TimeSpan.FromSeconds(2));

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        limiter.LocalBucket.Should().BeSameAs(localBucket);
        limiter.GlobalBucket.Should().BeSameAs(globalBucket);
    }

    #endregion Constructor

    #region IsBothFull

    [Fact]
    [Trait("Member", "IsBothFull")]
    public void IsBothFull_ReturnsTrue_WhenBothAreFull()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        limiter.IsBothFull.Should().BeTrue();
    }

    [Fact]
    [Trait("Member", "IsBothFull")]
    public void IsBothFull_ReturnsFalse_WhenLocalIsNotFull()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        localBucket.UpdateRemaining(0);

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        limiter.IsBothFull.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "IsBothFull")]
    public void IsBothFull_ReturnsFalse_WhenGlobalIsNotFull()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        globalBucket.UpdateRemaining(0);

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        limiter.IsBothFull.Should().BeFalse();
    }

    [Fact]
    [Trait("Member", "IsBothFull")]
    public void IsBothFull_ReturnsFalse_WhenNeitherAreFull()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        localBucket.UpdateRemaining(0);
        globalBucket.UpdateRemaining(0);

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        limiter.IsBothFull.Should().BeFalse();
    }

    #endregion IsBothFull

    #region WaitForRateLimit

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ReturnsTrue_WhenBothTokensAcquired()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        bool result = await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        result.Should().BeTrue();
        localBucket.Remaining.Should().Be(0);
        globalBucket.Remaining.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ReturnsFalseAndReturnsToken_WhenLocalTimesOut()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        localBucket.UpdateRemaining(0);
        localBucket.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        bool result = await limiter.WaitForRateLimit(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);

        result.Should().BeFalse();
        globalBucket.Remaining.Should().Be(1); // Token should be returned
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ReturnsFalseAndReturnsToken_WhenGlobalTimesOut()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        globalBucket.UpdateRemaining(0);
        globalBucket.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        bool result = await limiter.WaitForRateLimit(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);

        result.Should().BeFalse();
        localBucket.Remaining.Should().Be(1); // Token should be returned
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ReturnsFalse_WhenBothTimeOut()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        localBucket.UpdateRemaining(0);
        localBucket.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        globalBucket.UpdateRemaining(0);
        globalBucket.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        bool result = await limiter.WaitForRateLimit(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);

        result.Should().BeFalse();
        localBucket.Remaining.Should().Be(0);
        globalBucket.Remaining.Should().Be(0);
    }

    [Fact]
    [Trait("Member", "WaitForRateLimit")]
    public async Task WaitForRateLimit_ReturnsFalseAndReturnsToken_WhenCanceled()
    {
        using TokenBucketRateLimiter localBucket = new(1, TimeSpan.FromSeconds(10));
        using TokenBucketRateLimiter globalBucket = new(1, TimeSpan.FromSeconds(10));

        // local will be acquired immediately
        // global will wait, but we will cancel it
        globalBucket.UpdateRemaining(0);
        globalBucket.UpdateNextReset(DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(5).Ticks);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync().ConfigureAwait(true);

        DualTokenBucketRateLimiter limiter = new(localBucket, globalBucket);

        // Even though it's canceled, the method catches OperationCanceledException and returns false
        bool result = await limiter.WaitForRateLimit(TimeSpan.FromSeconds(1), cts.Token).ConfigureAwait(true);

        result.Should().BeFalse();
        localBucket.Remaining.Should().Be(1); // Token should be returned
    }

    #endregion WaitForRateLimit
}
